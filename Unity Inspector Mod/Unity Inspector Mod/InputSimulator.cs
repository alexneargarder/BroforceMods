using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using RocketLib.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity_Inspector_Mod
{
    public static class InputSimulator
    {
        public const int DefaultDurationMs = 50;
        private const int DefaultIntervalMs = 200;
        private const int MaxPlanMs = 60000;
        private const int FirstGamepadController = 4;
        private const int ControllerCount = 8;

        public const int Up = 0, Down = 1, Left = 2, Right = 3, Fire = 4, Jump = 5, Special = 6, HighFive = 7, Gesture = 8, Sprint = 9, Start = 10, Escape = 11;
        private static readonly string[] KeyNames = { "up", "down", "left", "right", "fire", "jump", "special", "highfive", "gesture", "sprint", "start", "escape" };

        private static readonly string[] PatchedReads =
        {
            "GetMenuInputCombined", "GetInput", "GetMenuInput", "GetControllerPressingStart", "GetControllerPressingCancel",
            "GetControllerPressingFire", "GetControllerPressingHighFive", "GetControllerPressingJump", "GetControllerPressingSpecial",
            "GetControllerPressingGesture", "IsControllerPressingFire", "IsControllerPressingJump", "IsControllerPressingSpecial",
            "GetControllerHoldingFire", "GetControllerHoldingJump", "GetControllerHoldingSpecial", "GetControllerHoldingMelee"
        };

        private sealed class Step
        {
            public int Key = -1;
            public int HoldMs;
            public int GapMs;
        }

        private sealed class Press
        {
            public int FramesRead;
            public int LastReadFrame = -1;
            public bool FromFallback;
            public string Warning;
            public bool ClockStopped;
            public string Unread;
            public bool Completed;
            public double HeldMs;
        }

        private sealed class KeyState
        {
            public int Player;
            public int Key;
            public int DownFrom = -1;
            public int UpFrom = -1;
            public Plan Owner;
            public Press Press;
            public object Binding;
        }

        private sealed class BindingTarget
        {
            public string Label;
            public object Owner;
        }

        private sealed class Plan
        {
            public int Id;
            public object Owner;
            public int Player;
            public string Form;
            public Step[] Steps;
            public HashSet<int> Keys;
            public JObject Echo;
            public double EstimatedMs;
            public System.Diagnostics.Stopwatch Clock;

            public bool Cancelled;
            public bool Done;
            public JObject Result;
            public int FinishedSeq;

            public int Index;
            public int Phase;
            public double PhaseStart;
            public KeyState Current;
            public KeyState Last;
            public Press[] Presses;
        }

        private const int PhaseBegin = 0, PhaseDown = 1, PhaseGap = 2;

        private static readonly object inputLock = new object();
        private static readonly Dictionary<int, Plan> plans = new Dictionary<int, Plan>();
        private static readonly List<Plan> incoming = new List<Plan>();
        private static int nextPlanId;
        private static int finishedSeq;
        private const int MaxRetainedResults = 32;

        private static readonly List<Plan> running = new List<Plan>();
        private static readonly KeyState[,] keys = new KeyState[Nums.PLAYER_MAX, KeyNames.Length];
        private static readonly Dictionary<int, KeyState>[] bindingKeys = new Dictionary<int, KeyState>[Nums.PLAYER_MAX];
        private static readonly List<BindingTarget> bindingTargets = new List<BindingTarget>();
        private static int heldUntilFrame = -1;
        private static int loadRequestFrame = -1;
        private static volatile float frameMs = 1000f / 30f;
        private static bool initialized;

        public static string PatchProblem { get; private set; }
        public static string BindingPatchProblem { get; private set; } = "The patch on RocketLib's KeyBinding.IsDown was never installed, so no mod would read a simulated binding";

        private static FieldInfo bindingRegistry;
        private static MethodInfo bindingForPlayer;
        private static MethodInfo bindingHasKey;

        private sealed class InputDriver : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }
        }

        public static void Initialize()
        {
            if ( initialized )
            {
                return;
            }
            initialized = true;

            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                for ( int k = 0; k < KeyNames.Length; k++ )
                {
                    keys[p, k] = new KeyState { Player = p, Key = k };
                }
                bindingKeys[p] = new Dictionary<int, KeyState>();
            }

            var go = new GameObject( "UnityInspectorInputDriver" );
            // PauseController.PauseGameObjects deactivates every active object not tagged Unpausable.
            go.tag = "Unpausable";
            go.AddComponent<InputDriver>();
            UnityEngine.Object.DontDestroyOnLoad( go );

            SceneManager.sceneLoaded += ( scene, mode ) =>
            {
                if ( mode == LoadSceneMode.Single )
                {
                    StopAll( $"scene {scene.name} loaded", Time.frameCount, loadRequestFrame );
                }
            };
        }

        public static void CheckPatches( Harmony harmony, Exception patchError )
        {
            var found = new HashSet<string>();
            try
            {
                foreach ( var method in harmony.GetPatchedMethods() )
                {
                    if ( method.DeclaringType == typeof( InputReader ) )
                    {
                        found.Add( method.Name );
                    }
                }
            }
            catch ( Exception ex )
            {
                patchError = patchError ?? ex;
            }

            var missing = PatchedReads.Where( name => !found.Contains( name ) ).ToArray();
            PatchProblem = missing.Length == 0
                ? null
                : $"The input patches on InputReader.{string.Join( ", ", missing )} are not installed, so the game would never read simulated input" +
                  ( patchError != null ? $" (Harmony failed: {patchError.Message})" : "" );
        }

        public static void InstallBindingPatch( Harmony harmony )
        {
            try
            {
                Type keyBinding = AccessTools.TypeByName( "RocketLib.KeyBinding" );
                Type forPlayers = AccessTools.TypeByName( "RocketLib.KeyBindingForPlayers" );
                Type registry = AccessTools.TypeByName( "RocketLib.AllModKeyBindings" );
                if ( keyBinding == null || forPlayers == null || registry == null )
                {
                    BindingPatchProblem = "RocketLib is not loaded, so there are no mod key bindings to press";
                    return;
                }

                MethodInfo isDown = AccessTools.Method( keyBinding, "IsDown", Type.EmptyTypes );
                bindingRegistry = AccessTools.Field( registry, "AllKeyBindings" );
                bindingForPlayer = AccessTools.Method( forPlayers, "get_Item", new[] { typeof( int ) } );
                bindingHasKey = AccessTools.Method( keyBinding, "HasKeyAssigned", Type.EmptyTypes );
                if ( isDown == null || bindingRegistry == null || bindingForPlayer == null )
                {
                    BindingPatchProblem = "This RocketLib has no KeyBinding.IsDown(), AllModKeyBindings.AllKeyBindings or KeyBindingForPlayers indexer, so its key bindings cannot be pressed";
                    return;
                }

                harmony.Patch( isDown, postfix: new HarmonyMethod( typeof( InputSimulator ), nameof( KeyBindingIsDownPostfix ) ) );
                BindingPatchProblem = null;
            }
            catch ( Exception ex )
            {
                BindingPatchProblem = $"The patch on RocketLib's KeyBinding.IsDown could not be installed ({ex.Message}), so no mod would read a simulated binding";
                Main.Log( BindingPatchProblem );
            }
        }

        private static void KeyBindingIsDownPostfix( object __instance, ref bool __result )
        {
            if ( __result || !Active )
            {
                return;
            }
            __result = BindingHeld( __instance );
        }

        public static object StartPlan( JObject parameters, object owner )
        {
            if ( PatchProblem != null )
            {
                return Fail( PatchProblem );
            }

            JToken actionToken = parameters?["action"];
            if ( actionToken != null && actionToken.Type != JTokenType.Null && actionToken.Type != JTokenType.String )
            {
                return Fail( "action must be a string" );
            }
            string action = actionToken?.Type == JTokenType.String ? actionToken.Value<string>() : null;

            JToken bindingToken = parameters?["binding"];
            if ( bindingToken != null && bindingToken.Type != JTokenType.Null && bindingToken.Type != JTokenType.String )
            {
                return Fail( "binding must be a string" );
            }
            string binding = bindingToken?.Type == JTokenType.String ? bindingToken.Value<string>() : null;

            JToken rawSequence = parameters?["sequence"];
            JArray sequence = rawSequence as JArray;
            if ( rawSequence != null && rawSequence.Type != JTokenType.Null && sequence == null )
            {
                return Fail( "sequence must be an array of steps" );
            }

            bool hasAction = !string.IsNullOrEmpty( action );
            bool hasBinding = !string.IsNullOrEmpty( binding );
            bool hasSequence = sequence != null && sequence.Count > 0;
            int forms = ( hasAction ? 1 : 0 ) + ( hasBinding ? 1 : 0 ) + ( hasSequence ? 1 : 0 );
            if ( forms > 1 )
            {
                return Fail( "Provide only one of action, binding and sequence" );
            }
            if ( forms == 0 )
            {
                throw new CallerErrorException( "Exactly one of action, binding or sequence is required" );
            }

            string error;
            int? duration = MessageHandler.ReadInt( parameters, "duration" );
            int? count = MessageHandler.ReadInt( parameters, "count" );
            int? interval = MessageHandler.ReadInt( parameters, "interval" );
            int? playerNum = MessageHandler.ReadInt( parameters, "player" );

            int player = playerNum ?? 0;
            if ( player < Nums.PLAYER_MIN || player >= Nums.PLAYER_MAX )
            {
                return Fail( $"player must be {Nums.PLAYER_MIN} to {Nums.PLAYER_MAX - 1}, got {player}" );
            }

            Plan plan = hasSequence
                ? SequencePlan( sequence, duration, count, interval, player, out error )
                : ActionPlan( hasAction ? action : binding, hasBinding, duration, count, interval, player, out error );
            if ( plan == null )
            {
                return Fail( error );
            }

            plan.EstimatedMs = Estimate( plan.Steps );
            if ( plan.EstimatedMs > MaxPlanMs )
            {
                return Fail( $"At the game's current {System.Math.Round( 1000f / frameMs )} fps this plan would take about {System.Math.Round( plan.EstimatedMs )}ms, over the {MaxPlanMs}ms limit: every press is held for at least one frame, and a key pressed again is released for at least one frame first" );
            }

            plan.Owner = owner;
            plan.Player = player;
            plan.Keys = new HashSet<int>( plan.Steps.Where( s => s.Key >= 0 ).Select( s => s.Key ) );
            plan.Presses = new Press[plan.Steps.Length];
            plan.Clock = System.Diagnostics.Stopwatch.StartNew();

            lock ( inputLock )
            {
                plan.Id = ++nextPlanId;
                plans[plan.Id] = plan;
                incoming.Add( plan );
            }

            return new JObject { ["pending"] = true, ["planId"] = plan.Id, ["remainingMs"] = System.Math.Round( plan.EstimatedMs ) };
        }

        public static object PlanStatus( JObject parameters )
        {
            int? id = MessageHandler.ReadInt( parameters, "planId" );
            if ( !id.HasValue )
            {
                throw new CallerErrorException( "planId is required" );
            }

            lock ( inputLock )
            {
                Plan plan;
                if ( !plans.TryGetValue( id.Value, out plan ) )
                {
                    return Fail( $"No input plan {id.Value}: its result was already read, the connection that started it closed, or {MaxRetainedResults} newer results were left unread" );
                }

                if ( !plan.Done )
                {
                    double remaining = System.Math.Max( 0, plan.EstimatedMs - plan.Clock.Elapsed.TotalMilliseconds );
                    return new JObject { ["pending"] = true, ["planId"] = plan.Id, ["remainingMs"] = System.Math.Round( remaining ) };
                }

                plans.Remove( plan.Id );
                return plan.Result;
            }
        }

        public static void ReleaseOwner( object owner )
        {
            lock ( inputLock )
            {
                foreach ( var plan in plans.Values.Where( p => p.Owner == owner ).ToList() )
                {
                    plan.Cancelled = true;
                    plans.Remove( plan.Id );
                }
            }
        }

        public static object CancelPlan( JObject parameters, object owner )
        {
            int? id = MessageHandler.ReadInt( parameters, "planId" );
            if ( !id.HasValue )
            {
                throw new CallerErrorException( "planId is required" );
            }

            lock ( inputLock )
            {
                Plan plan;
                if ( !plans.TryGetValue( id.Value, out plan ) || plan.Owner != owner )
                {
                    return new JObject { ["cancelled"] = false };
                }

                plan.Cancelled = true;
                plans.Remove( plan.Id );
                return new JObject { ["cancelled"] = !plan.Done };
            }
        }

        public static void StopAll( string reason )
        {
            StopAll( reason, Time.frameCount, -1 );
        }

        public static void SceneLoadRequested()
        {
            loadRequestFrame = Time.frameCount;
        }

        private static void StopAll( string reason, int upFrom, int ownLoadFrame )
        {
            foreach ( var plan in running.ToArray() )
            {
                Stop( plan, upFrom, reason, ownLoadFrame );
            }

            List<Plan> queued;
            lock ( inputLock )
            {
                queued = new List<Plan>( incoming );
                incoming.Clear();
            }

            foreach ( var plan in queued )
            {
                if ( !plan.Cancelled )
                {
                    Finish( plan, reason );
                }
            }
        }

        private static JObject Fail( string error )
        {
            return new JObject { ["success"] = false, ["error"] = error };
        }

        private static int KeyIndex( string action )
        {
            return Array.IndexOf( KeyNames, action.ToLowerInvariant() );
        }

        private static Plan ActionPlan( string name, bool isBinding, int? duration, int? count, int? interval, int player, out string error )
        {
            error = null;
            int key = isBinding ? BindingKey( name, out error ) : KeyIndex( name );
            if ( key < 0 )
            {
                error = error ?? $"Unknown input action: {name}";
                return null;
            }

            if ( ( duration.HasValue && duration.Value < 0 ) || ( count.HasValue && count.Value < 0 ) || ( interval.HasValue && interval.Value < 0 ) )
            {
                error = "duration, count and interval must not be negative";
                return null;
            }

            if ( count.HasValue && count.Value < 1 )
            {
                error = "count must be at least 1";
                return null;
            }

            int pressCount = count ?? 1;
            int pressInterval = interval ?? DefaultIntervalMs;
            int holdMs = duration.HasValue && duration.Value > 0 ? duration.Value : DefaultDurationMs;
            long plannedMs = (long)pressCount * holdMs + (long)( pressCount - 1 ) * pressInterval;

            if ( plannedMs > MaxPlanMs )
            {
                error = pressCount > 1
                    ? $"{pressCount} presses would take {plannedMs}ms, over the {MaxPlanMs}ms limit"
                    : $"Holding for {holdMs}ms is over the {MaxPlanMs}ms limit";
                return null;
            }

            var steps = new Step[pressCount];
            for ( int i = 0; i < pressCount; i++ )
            {
                steps[i] = new Step { Key = key, HoldMs = holdMs, GapMs = i < pressCount - 1 ? pressInterval : 0 };
            }

            var echo = isBinding ? new JObject { ["binding"] = KeyLabel( key ) } : new JObject { ["action"] = name };
            if ( pressCount > 1 )
            {
                echo["count"] = pressCount;
                echo["interval"] = pressInterval;
            }
            else if ( duration.HasValue && duration.Value > 0 )
            {
                echo["duration"] = duration.Value;
            }
            echo["player"] = player;

            return new Plan { Form = pressCount > 1 ? "count" : "action", Steps = steps, Echo = echo };
        }

        private static readonly string[] StepKeys = { "action", "binding", "duration", "wait" };

        private static Plan SequencePlan( JArray sequence, int? duration, int? count, int? interval, int player, out string error )
        {
            error = null;
            if ( duration.HasValue || count.HasValue || interval.HasValue )
            {
                error = "duration, count and interval apply to an action, not to a sequence; give each step its own duration and wait";
                return null;
            }

            var steps = new Step[sequence.Count];
            long totalMs = 0;

            for ( int i = 0; i < sequence.Count; i++ )
            {
                var raw = sequence[i] as JObject;
                if ( raw == null )
                {
                    error = $"Step {i} is not an object";
                    return null;
                }

                foreach ( var property in raw.Properties() )
                {
                    if ( Array.IndexOf( StepKeys, property.Name ) < 0 )
                    {
                        error = $"Step {i} has no key \"{property.Name}\"; a step takes action, binding, duration and wait";
                        return null;
                    }
                }

                JToken actionToken = raw["action"];
                if ( actionToken != null && actionToken.Type != JTokenType.Null && actionToken.Type != JTokenType.String )
                {
                    error = $"Step {i}: action must be a string";
                    return null;
                }
                string stepAction = actionToken?.Type == JTokenType.String ? actionToken.Value<string>() : null;

                JToken bindingToken = raw["binding"];
                if ( bindingToken != null && bindingToken.Type != JTokenType.Null && bindingToken.Type != JTokenType.String )
                {
                    error = $"Step {i}: binding must be a string";
                    return null;
                }
                string stepBinding = bindingToken?.Type == JTokenType.String ? bindingToken.Value<string>() : null;

                if ( !string.IsNullOrEmpty( stepAction ) && !string.IsNullOrEmpty( stepBinding ) )
                {
                    error = $"Step {i} has both an action and a binding; a step presses one";
                    return null;
                }

                int? stepDuration = MessageHandler.ReadInt( raw, "duration", $"sequence[{i}].duration" );
                int? stepWait = MessageHandler.ReadInt( raw, "wait", $"sequence[{i}].wait" );

                if ( ( stepDuration ?? 0 ) < 0 || ( stepWait ?? 0 ) < 0 )
                {
                    error = $"Step {i} has a negative duration or wait";
                    return null;
                }

                var step = new Step { GapMs = stepWait ?? 0 };

                if ( string.IsNullOrEmpty( stepAction ) && string.IsNullOrEmpty( stepBinding ) )
                {
                    if ( stepDuration.HasValue )
                    {
                        error = $"Step {i} has a duration but no action or binding; a pause is a step with only a wait";
                        return null;
                    }
                }
                else
                {
                    string bindingError = null;
                    step.Key = string.IsNullOrEmpty( stepBinding ) ? KeyIndex( stepAction ) : BindingKey( stepBinding, out bindingError );
                    if ( step.Key < 0 )
                    {
                        error = bindingError != null ? $"Step {i}: {bindingError}" : $"Unknown input action in step {i}: {stepAction}";
                        return null;
                    }

                    step.HoldMs = stepDuration.HasValue && stepDuration.Value > 0 ? stepDuration.Value : DefaultDurationMs;
                }

                if ( step.HoldMs > MaxPlanMs || step.GapMs > MaxPlanMs )
                {
                    error = $"Step {i} alone exceeds the {MaxPlanMs}ms limit";
                    return null;
                }

                totalMs += step.HoldMs + step.GapMs;
                steps[i] = step;
            }

            if ( totalMs > MaxPlanMs )
            {
                error = $"Input sequence would take {totalMs}ms, over the {MaxPlanMs}ms limit";
                return null;
            }

            var echo = new JObject { ["steps"] = steps.Length, ["totalMs"] = totalMs, ["player"] = player };
            return new Plan { Form = "sequence", Steps = steps, Echo = echo };
        }

        private static double Estimate( Step[] steps )
        {
            double frame = frameMs;
            double total = 0;
            int lastKey = -1;
            for ( int i = 0; i < steps.Length; i++ )
            {
                Step step = steps[i];
                if ( step.Key >= 0 )
                {
                    if ( step.Key == lastKey && ( i == 0 || steps[i - 1].GapMs < frame ) )
                    {
                        total += frame;
                    }
                    total += System.Math.Max( step.HoldMs, frame );
                    lastKey = step.Key;
                }
                total += step.GapMs;
            }
            return total;
        }

        private static void Tick()
        {
            int frame = Time.frameCount;
            double now = Time.realtimeSinceStartup * 1000.0;
            float delta = Time.unscaledDeltaTime * 1000f;
            if ( delta > 0f && delta < 1000f )
            {
                frameMs = frameMs * 0.9f + delta * 0.1f;
            }

            List<Plan> arriving = null;
            List<Plan> cancelled = null;
            lock ( inputLock )
            {
                if ( incoming.Count > 0 )
                {
                    arriving = new List<Plan>( incoming );
                    incoming.Clear();
                }

                foreach ( var plan in running )
                {
                    if ( plan.Cancelled )
                    {
                        ( cancelled = cancelled ?? new List<Plan>() ).Add( plan );
                    }
                }
            }

            if ( cancelled != null )
            {
                foreach ( var plan in cancelled )
                {
                    Stop( plan, frame + 1, null );
                }
            }

            if ( arriving != null )
            {
                foreach ( var plan in arriving )
                {
                    if ( plan.Cancelled )
                    {
                        continue;
                    }

                    foreach ( var other in running.ToArray() )
                    {
                        if ( other.Player != plan.Player || !other.Keys.Overlaps( plan.Keys ) )
                        {
                            continue;
                        }
                        string taken = string.Join( ", ", other.Keys.Where( plan.Keys.Contains ).Select( KeyLabel ).ToArray() );
                        Stop( other, frame + 1, $"a later simulate_input for player {plan.Player} took over {taken}" );
                    }

                    plan.PhaseStart = now;
                    running.Add( plan );
                }
            }

            foreach ( var plan in running.ToArray() )
            {
                try
                {
                    Advance( plan, frame, now );
                }
                catch ( Exception ex )
                {
                    Main.Log( $"simulate_input plan {plan.Id} failed: {ex}" );
                    Stop( plan, frame + 1, $"the plan failed in the game: {ex.Message}" );
                }
            }
        }

        private static void Advance( Plan plan, int frame, double now )
        {
            while ( true )
            {
                if ( plan.Index >= plan.Steps.Length )
                {
                    if ( plan.Last != null && frame < plan.Last.UpFrom )
                    {
                        return;
                    }
                    Finish( plan, null );
                    return;
                }

                Step step = plan.Steps[plan.Index];
                switch ( plan.Phase )
                {
                    case PhaseBegin:
                        if ( step.Key < 0 )
                        {
                            plan.Phase = PhaseGap;
                            plan.PhaseStart = now;
                            continue;
                        }

                        KeyState state = StateOf( plan.Player, step.Key );
                        if ( state.UpFrom > frame )
                        {
                            return;
                        }

                        if ( IsBindingKey( step.Key ) )
                        {
                            string problem;
                            state.Binding = BindingFor( step.Key, plan.Player, out problem );
                            if ( state.Binding == null )
                            {
                                Stop( plan, frame + 1, problem );
                                return;
                            }
                        }

                        if ( ( step.Key == Start || step.Key == Escape ) && MenuControllerFor( plan.Player ) < 0 )
                        {
                            Stop( plan, frame + 1, $"every gamepad controller ({FirstGamepadController}-{ControllerCount - 1}) belongs to a local player, so there is none to send player {plan.Player}'s {KeyNames[step.Key]} from" );
                            return;
                        }

                        var press = new Press();
                        plan.Presses[plan.Index] = press;
                        state.Owner = plan;
                        state.Press = press;
                        state.DownFrom = frame + 1;
                        state.UpFrom = int.MaxValue;
                        heldUntilFrame = int.MaxValue;
                        plan.Current = state;
                        plan.Phase = PhaseDown;
                        plan.PhaseStart = now;
                        return;

                    case PhaseDown:
                        if ( now - plan.PhaseStart < step.HoldMs || frame < plan.Current.DownFrom )
                        {
                            return;
                        }
                        plan.Current.Press.Completed = true;
                        Release( plan, plan.Current, frame + 1 );
                        plan.Last = plan.Current;
                        plan.Current = null;
                        plan.Phase = PhaseGap;
                        plan.PhaseStart = now;
                        continue;

                    case PhaseGap:
                        if ( now - plan.PhaseStart < step.GapMs )
                        {
                            return;
                        }
                        plan.Index++;
                        plan.Phase = PhaseBegin;
                        continue;
                }
            }
        }

        private static void Release( Plan plan, KeyState state, int upFrom )
        {
            state.UpFrom = upFrom;
            state.Owner = null;
            RecomputeHeldUntil();

            Press press = state.Press;
            if ( press != null && press.FramesRead == 0 )
            {
                press.Unread = UnreadReasons( plan.Player, state.Key );
            }
        }

        private static void RecomputeHeldUntil()
        {
            int until = -1;
            foreach ( KeyState state in keys )
            {
                if ( state.UpFrom > until )
                {
                    until = state.UpFrom;
                }
            }
            foreach ( var player in bindingKeys )
            {
                foreach ( KeyState state in player.Values )
                {
                    if ( state.UpFrom > until )
                    {
                        until = state.UpFrom;
                    }
                }
            }
            heldUntilFrame = until;
        }

        private static void Stop( Plan plan, int upFrom, string reason, int ownLoadFrame = -1 )
        {
            if ( plan.Current != null && plan.Current.Owner == plan )
            {
                Press press = plan.Current.Press;
                double held = Time.realtimeSinceStartup * 1000.0 - plan.PhaseStart;
                bool ownEffect = ownLoadFrame >= 0 && plan.Current.DownFrom == ownLoadFrame && press.FramesRead > 0;
                press.Completed = ownEffect || ( held >= plan.Steps[plan.Index].HoldMs && Time.frameCount >= plan.Current.DownFrom );
                press.HeldMs = held;
                Release( plan, plan.Current, upFrom );
                plan.Last = plan.Current;
                plan.Current = null;
            }

            running.Remove( plan );

            if ( reason == null )
            {
                lock ( inputLock )
                {
                    plan.Done = true;
                    plans.Remove( plan.Id );
                }
                return;
            }

            Finish( plan, reason );
        }

        private static void Finish( Plan plan, string stopReason )
        {
            running.Remove( plan );

            var result = new JObject();
            var frames = new JArray();
            var unread = new List<string>();
            var warnings = new List<string>();
            var notes = new List<string>();
            const string clockNote = "Time.timeScale is 0: the bro took the press, but nothing moves until the clock runs";
            int keyed = 0, started = 0, firstNotStarted = -1, cutShort = -1;

            for ( int i = 0; i < plan.Steps.Length; i++ )
            {
                Step step = plan.Steps[i];
                Press press = plan.Presses[i];
                if ( step.Key < 0 )
                {
                    frames.Add( JValue.CreateNull() );
                    continue;
                }

                keyed++;
                if ( press == null )
                {
                    if ( firstNotStarted < 0 )
                    {
                        firstNotStarted = i;
                    }
                    frames.Add( JValue.CreateNull() );
                    continue;
                }

                started++;
                if ( !press.Completed && cutShort < 0 )
                {
                    cutShort = i;
                }
                frames.Add( press.FramesRead );

                if ( press.FramesRead == 0 )
                {
                    unread.Add( StepLabel( plan, i ) + ": " + ( press.Unread ?? UnreadReasons( plan.Player, step.Key ) ) );
                }
                else if ( press.Warning != null && !warnings.Contains( press.Warning ) )
                {
                    warnings.Add( press.Warning );
                }

                if ( press.ClockStopped && !notes.Contains( clockNote ) )
                {
                    notes.Add( clockNote );
                }

                if ( press.FromFallback )
                {
                    string note = $"nobody is playing, so the press came from controller {FirstGamepadController}, which joins the next free player slot rather than player {plan.Player} as such";
                    if ( !notes.Contains( note ) )
                    {
                        notes.Add( note );
                    }
                }
            }

            bool complete = started == keyed && cutShort < 0;
            var errors = new List<string>();
            if ( cutShort >= 0 )
            {
                Press cut = plan.Presses[cutShort];
                errors.Add( $"{StepLabel( plan, cutShort )} was released after {System.Math.Round( System.Math.Max( 0, cut.HeldMs ) )}ms of its {plan.Steps[cutShort].HoldMs}ms: {stopReason}" );
            }
            else if ( started < keyed )
            {
                errors.Add( plan.Form == "sequence" ? $"Stopped at step {firstNotStarted} of {plan.Steps.Length}: {stopReason}"
                    : plan.Form == "count" ? $"Stopped before press {firstNotStarted + 1} of {plan.Steps.Length}: {stopReason}"
                    : $"Stopped before the press began: {stopReason}" );
            }
            if ( unread.Count > 0 )
            {
                errors.Add( "No frame read " + string.Join( "; ", unread.ToArray() ) );
            }

            result["success"] = errors.Count == 0;
            if ( errors.Count > 0 )
            {
                result["error"] = string.Join( ". ", errors.ToArray() );
            }
            foreach ( var property in plan.Echo.Properties() )
            {
                result[property.Name] = property.Value;
            }
            result["framesRead"] = plan.Form == "action" ? frames[0].DeepClone() : frames;
            if ( complete && stopReason != null )
            {
                result["endedEarly"] = stopReason;
            }
            if ( warnings.Count > 0 )
            {
                result["warning"] = "The game read the input, but no bro will act on it: " + string.Join( "; ", warnings.ToArray() );
            }
            if ( notes.Count > 0 )
            {
                result["note"] = string.Join( "; ", notes.ToArray() );
            }

            lock ( inputLock )
            {
                plan.Result = result;
                plan.Done = true;
                plan.FinishedSeq = ++finishedSeq;
                if ( plan.Cancelled )
                {
                    plans.Remove( plan.Id );
                }

                var retained = plans.Values.Where( p => p.Done ).OrderBy( p => p.FinishedSeq ).ToList();
                for ( int i = 0; i < retained.Count - MaxRetainedResults; i++ )
                {
                    plans.Remove( retained[i].Id );
                }
            }
        }

        private static string StepLabel( Plan plan, int index )
        {
            string key = KeyLabel( plan.Steps[index].Key );
            if ( plan.Form == "sequence" )
            {
                return $"step {index} ({key})";
            }
            if ( plan.Form == "count" )
            {
                return $"press {index + 1} ({key})";
            }
            return key;
        }

        private static string BroReasons( int player )
        {
            var reasons = new List<string>();
            try
            {
                string notPlaying = StateModifier.NotPlayingReason();
                if ( notPlaying != null )
                {
                    reasons.Add( notPlaying );
                }
                else
                {
                    if ( PauseController.pauseStatus != PauseStatus.UnPaused )
                    {
                        reasons.Add( $"the game is paused ({PauseController.pauseStatus})" );
                    }

                    string slot = StateModifier.ReadSlot( player ).State;
                    if ( slot != "alive" )
                    {
                        reasons.Add( $"player {player} has no live bro ({slot})" );
                    }
                }
            }
            catch ( Exception ex )
            {
                reasons.Add( $"the game state could not be read ({ex.Message})" );
            }

            return reasons.Count == 0 ? null : string.Join( "; ", reasons.ToArray() );
        }

        private static string UnreadReasons( int player, int key )
        {
            if ( IsBindingKey( key ) )
            {
                return BindingUnreadReasons( player, key );
            }

            var reasons = new List<string>();
            try
            {
                if ( InputReader.IsBlocked )
                {
                    reasons.Add( "the game is blocking input (a fade, a scene change or an overlay message)" );
                }
                if ( CutsceneController.InCutscene )
                {
                    reasons.Add( "a cutscene is running" );
                }
                if ( !CutsceneController.PlayersCanMove() )
                {
                    reasons.Add( "the game is holding players still (a camera pan or a cutscene)" );
                }
                if ( PauseController.DelayInput )
                {
                    reasons.Add( "the game ignores input just after an unpause" );
                }
                if ( key != Start && key != Escape && ControllerOf( player ) < 0 && AnyLocalPlayerPlaying() )
                {
                    reasons.Add( $"player {player} is not playing on this machine, so no controller carries its input" );
                }
            }
            catch ( Exception ex )
            {
                reasons.Add( $"the input gates could not be read ({ex.Message})" );
            }

            if ( key != Start && key != Escape )
            {
                string bro = BroReasons( player );
                if ( bro != null )
                {
                    reasons.Add( bro );
                }
            }

            return reasons.Count == 0 ? "nothing in the game polled it while it was held" : string.Join( "; ", reasons.ToArray() );
        }

        public static bool Active => running.Count > 0 || heldUntilFrame > Time.frameCount;

        private static bool IsDown( KeyState state, int frame )
        {
            return state.DownFrom >= 0 && frame >= state.DownFrom && frame < state.UpFrom;
        }

        private static void MarkRead( KeyState state, int frame )
        {
            Press press = state.Press;
            if ( press != null && press.LastReadFrame != frame )
            {
                // A menu's accept can close the pause later in this frame.
                if ( press.FramesRead == 0 && !IsBindingKey( state.Key ) && state.Key != Start && state.Key != Escape )
                {
                    press.Warning = BroReasons( state.Player );
                    press.ClockStopped = press.Warning == null && Time.timeScale == 0f;
                }

                press.LastReadFrame = frame;
                press.FramesRead++;
            }
        }

        public static bool HeldOn( int key, int controller )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( IsDown( state, frame ) && controller >= 0 && ControllerOf( p ) == controller )
                {
                    MarkRead( state, frame );
                    return true;
                }
            }
            return false;
        }

        public static bool HeldAny( int key )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( IsDown( state, frame ) )
                {
                    MarkRead( state, frame );
                    return true;
                }
            }
            return false;
        }

        public static bool HeldOnMenu( int key, int controller )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( IsDown( state, frame ) && MenuControllerFor( p ) == controller )
                {
                    MarkRead( state, frame );
                    return true;
                }
            }
            return false;
        }

        public static int HeldController( int key )
        {
            return Reported( key, false, false );
        }

        public static int PressedController( int key )
        {
            return Reported( key, true, false );
        }

        public static int PressedMenuController( int key )
        {
            return Reported( key, true, true );
        }

        public static bool PressedOn( int key, int controller )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( !IsDown( state, frame ) || frame != state.DownFrom )
                {
                    continue;
                }

                bool fallback;
                if ( controller >= 0 && ControllerFor( p, out fallback ) == controller )
                {
                    if ( fallback && state.Press != null )
                    {
                        state.Press.FromFallback = true;
                    }
                    MarkRead( state, frame );
                    return true;
                }
            }
            return false;
        }

        public static bool PressedOnMenu( int key, int controller )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( IsDown( state, frame ) && frame == state.DownFrom && MenuControllerFor( p ) == controller )
                {
                    MarkRead( state, frame );
                    return true;
                }
            }
            return false;
        }

        private static int Reported( int key, bool edge, bool menu )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                KeyState state = keys[p, key];
                if ( !IsDown( state, frame ) || ( edge && frame != state.DownFrom ) )
                {
                    continue;
                }

                bool fallback = false;
                int controller = menu ? MenuControllerFor( p ) : ControllerFor( p, out fallback );
                if ( controller < 0 )
                {
                    continue;
                }

                if ( fallback && state.Press != null )
                {
                    state.Press.FromFallback = true;
                }
                MarkRead( state, frame );
                return controller;
            }
            return -1;
        }

        public static bool BindingHeld( object binding )
        {
            int frame = Time.frameCount;
            for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
            {
                foreach ( KeyState state in bindingKeys[p].Values )
                {
                    if ( IsDown( state, frame ) && ReferenceEquals( state.Binding, binding ) )
                    {
                        MarkRead( state, frame );
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsBindingKey( int key )
        {
            return key >= KeyNames.Length;
        }

        private static string KeyLabel( int key )
        {
            if ( !IsBindingKey( key ) )
            {
                return KeyNames[key];
            }
            lock ( inputLock )
            {
                return bindingTargets[key - KeyNames.Length].Label;
            }
        }

        private static KeyState StateOf( int player, int key )
        {
            if ( !IsBindingKey( key ) )
            {
                return keys[player, key];
            }

            KeyState state;
            if ( !bindingKeys[player].TryGetValue( key, out state ) )
            {
                state = new KeyState { Player = player, Key = key };
                bindingKeys[player][key] = state;
            }
            return state;
        }

        private static int BindingKey( string name, out string error )
        {
            BindingTarget target = ResolveBinding( name, out error );
            if ( target == null )
            {
                return -1;
            }

            lock ( inputLock )
            {
                for ( int i = 0; i < bindingTargets.Count; i++ )
                {
                    if ( bindingTargets[i].Label == target.Label )
                    {
                        bindingTargets[i].Owner = target.Owner;
                        return KeyNames.Length + i;
                    }
                }
                bindingTargets.Add( target );
                return KeyNames.Length + bindingTargets.Count - 1;
            }
        }

        private static string Match( IEnumerable<string> names, Func<string, bool> exact, Func<string, bool> loose )
        {
            var ordered = names.OrderByDescending( n => n.Length ).ToList();
            return ordered.FirstOrDefault( exact ) ?? ordered.FirstOrDefault( loose );
        }

        private static BindingTarget ResolveBinding( string name, out string error )
        {
            error = null;
            if ( BindingPatchProblem != null )
            {
                error = BindingPatchProblem;
                return null;
            }

            try
            {
                var registry = bindingRegistry.GetValue( null ) as IDictionary;
                if ( registry == null )
                {
                    error = "RocketLib's key binding registry is not loaded, so there are no mod key bindings to press";
                    return null;
                }

                var mods = registry.Keys.Cast<string>().ToList();
                string mod = Match( mods, m => name.StartsWith( m + "/", StringComparison.Ordinal ),
                    m => name.StartsWith( m + "/", StringComparison.OrdinalIgnoreCase ) );
                if ( mod == null )
                {
                    error = mods.Count == 0
                        ? $"Unknown binding \"{name}\": no mod has registered a RocketLib key binding"
                        : $"Unknown binding \"{name}\": a binding is \"<mod id>/<binding name>\", and the mods with key bindings are {string.Join( ", ", mods.OrderBy( m => m ).ToArray() )}";
                    return null;
                }

                string wanted = name.Substring( mod.Length + 1 );
                var modBindings = registry[mod] as IDictionary;
                var names = modBindings == null ? new List<string>() : modBindings.Keys.Cast<string>().ToList();
                string match = Match( names, n => n == wanted, n => string.Equals( n, wanted, StringComparison.OrdinalIgnoreCase ) );
                object owner = match == null ? null : modBindings[match];
                if ( owner == null )
                {
                    error = $"Unknown binding \"{name}\": {mod} registers {( names.Count == 0 ? "no key bindings" : string.Join( ", ", names.OrderBy( n => n ).ToArray() ) )}";
                    return null;
                }

                return new BindingTarget { Label = mod + "/" + match, Owner = owner };
            }
            catch ( Exception ex )
            {
                error = $"RocketLib's key binding registry could not be read ({ex.Message})";
                return null;
            }
        }

        private static object BindingFor( int key, int player, out string problem )
        {
            BindingTarget target;
            lock ( inputLock )
            {
                target = bindingTargets[key - KeyNames.Length];
            }

            problem = null;
            try
            {
                object binding = bindingForPlayer.Invoke( target.Owner, new object[] { player } );
                if ( binding == null )
                {
                    problem = $"{target.Label} has no key binding for player {player}";
                }
                return binding;
            }
            catch ( Exception ex )
            {
                problem = $"{target.Label} could not be read for player {player} ({( ex.InnerException ?? ex ).Message})";
                return null;
            }
        }

        private static string BindingUnreadReasons( int player, int key )
        {
            KeyState state = StateOf( player, key );
            string reason = $"no code called its IsDown for player {player} while it was held";
            try
            {
                if ( state.Binding != null && bindingHasKey != null && !(bool)bindingHasKey.Invoke( state.Binding, null ) )
                {
                    reason += $"; it has no key assigned for player {player}, and a mod may poll only the bindings that have one";
                }
            }
            catch ( Exception ex )
            {
                reason += $"; whether it has a key assigned could not be read ({( ex.InnerException ?? ex ).Message})";
            }
            return reason;
        }

        public static int ControllerOf( int player )
        {
            try
            {
                if ( player < 0 || player >= HeroController.playerControllerIDs.Length )
                {
                    return -1;
                }
                if ( !HeroController.IsPlayerPlaying( player ) || !HeroController.PIDS[player].IsMine )
                {
                    return -1;
                }
                return HeroController.playerControllerIDs[player];
            }
            catch
            {
                return -1;
            }
        }

        // With no local player playing, the join screen joins controller 4 as the first free slot.
        private static int ControllerFor( int player, out bool fallback )
        {
            fallback = false;
            int controller = ControllerOf( player );
            if ( controller >= 0 || AnyLocalPlayerPlaying() )
            {
                return controller;
            }
            fallback = true;
            return FirstGamepadController;
        }

        // A pause by keyboard controller 0 ignores every controller's cancel and start until the real Escape key.
        private static int MenuControllerFor( int player )
        {
            int own = ControllerOf( player );
            if ( own >= FirstGamepadController )
            {
                return own;
            }

            for ( int controller = FirstGamepadController; controller < ControllerCount; controller++ )
            {
                bool owned = false;
                for ( int p = 0; p < Nums.PLAYER_MAX; p++ )
                {
                    if ( ControllerOf( p ) == controller )
                    {
                        owned = true;
                    }
                }
                if ( !owned )
                {
                    return controller;
                }
            }
            return -1;
        }

        private static bool AnyLocalPlayerPlaying()
        {
            for ( int i = 0; i < Nums.PLAYER_MAX; i++ )
            {
                if ( ControllerOf( i ) >= 0 )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
