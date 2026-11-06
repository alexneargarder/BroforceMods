using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using RocketLib.Collections;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public static class InputSimulator
    {
        public const int DefaultDurationMs = 50;

        public sealed class InputStep
        {
            public string Action;
            public int Duration = DefaultDurationMs;
            public int Wait;
        }

        private const int MaxSequenceMs = 60000;
        private const int StartRepeatMs = 100;

        // simulatedInputs and activeHolds are touched from the TCP handler thread, from thread-pool
        // timer callbacks and from the Unity main thread (the Harmony input patches), so every read
        // and write of either goes through this lock. Never held across a Thread.Sleep.
        private static readonly object inputLock = new object();

        private sealed class PendingRelease
        {
            public Timer Timer;
            public int Generation;
        }

        private static Dictionary<string, bool> simulatedInputs = new Dictionary<string, bool>();
        private static Dictionary<string, int> keyPlayers = new Dictionary<string, int>();
        private static Dictionary<string, PendingRelease> activeHolds = new Dictionary<string, PendingRelease>();
        private static Dictionary<string, int> releaseGenerations = new Dictionary<string, int>();
        private static DateTime lastStartTrigger = DateTime.MinValue;

        public static void Initialize()
        {
            lock ( inputLock )
            {
                simulatedInputs["up"] = false;
                simulatedInputs["down"] = false;
                simulatedInputs["left"] = false;
                simulatedInputs["right"] = false;
                simulatedInputs["fire"] = false;
                simulatedInputs["jump"] = false;
                simulatedInputs["special"] = false;
                simulatedInputs["highfive"] = false;
                simulatedInputs["gesture"] = false;
                simulatedInputs["sprint"] = false;
                simulatedInputs["start"] = false;
                simulatedInputs["escape"] = false;
            }
        }

        public static object SimulateInput( string action, int? duration = null, int? playerNum = null, int? count = null, int? interval = null, List<InputStep> sequence = null )
        {
            try
            {
                int targetPlayer = playerNum ?? 0;

                if ( targetPlayer < Nums.PLAYER_MIN || targetPlayer >= Nums.PLAYER_MAX )
                {
                    return new { success = false, error = $"player must be {Nums.PLAYER_MIN} to {Nums.PLAYER_MAX - 1}, got {targetPlayer}" };
                }

                if ( sequence != null )
                {
                    if ( !string.IsNullOrEmpty( action ) )
                    {
                        return new { success = false, error = "Provide either action or sequence, not both" };
                    }

                    return RunSequence( sequence, targetPlayer );
                }

                string inputKey = action.ToLower();

                if ( !IsKnownAction( inputKey ) )
                {
                    return new { success = false, error = $"Unknown input action: {action}" };
                }

                // Thread.Sleep(-1) never returns, so a negative interval would hold this connection forever.
                if ( ( duration.HasValue && duration.Value < 0 ) || ( count.HasValue && count.Value < 0 ) || ( interval.HasValue && interval.Value < 0 ) )
                {
                    return new { success = false, error = "duration, count and interval must not be negative" };
                }

                string warning = InputWarning( new[] { inputKey }, targetPlayer );

                int pressCount = count ?? 1;
                int pressInterval = interval ?? 200;
                int holdMs = duration.HasValue && duration.Value > 0 ? duration.Value : DefaultDurationMs;

                long plannedMs = (long)pressCount * holdMs + (long)( pressCount - 1 ) * pressInterval;

                if ( plannedMs > MaxSequenceMs )
                {
                    if ( pressCount > 1 )
                    {
                        return new { success = false, error = $"{pressCount} presses would take {plannedMs}ms, over the {MaxSequenceMs}ms limit" };
                    }

                    return new { success = false, error = $"Holding for {holdMs}ms is over the {MaxSequenceMs}ms limit" };
                }

                CancelPendingReleases( new List<string> { inputKey } );

                if ( pressCount > 1 )
                {
                    PressInputMultiple( inputKey, pressCount, pressInterval, holdMs, targetPlayer );
                    return WithWarning( new { success = true, action = action, count = pressCount, interval = pressInterval, player = targetPlayer }, warning );
                }
                else if ( duration.HasValue && duration.Value > 0 )
                {
                    HoldInput( inputKey, duration.Value, targetPlayer );

                    return WithWarning( new { success = true, action = action, duration = duration.Value, player = targetPlayer }, warning );
                }
                else
                {
                    PressInput( inputKey, targetPlayer );
                    return WithWarning( new { success = true, action = action, player = targetPlayer }, warning );
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"Error simulating input: {ex.Message}" );
                return new { success = false, error = ex.Message };
            }
        }

        private static bool IsKnownAction( string inputKey )
        {
            lock ( inputLock )
            {
                return simulatedInputs.ContainsKey( inputKey );
            }
        }

        private static void HoldInput( string inputKey, int durationMs, int playerNum )
        {
            lock ( inputLock )
            {
                MarkPressed( inputKey, playerNum );
                ScheduleRelease( inputKey, durationMs );
            }
        }

        private static void PressInput( string inputKey, int playerNum )
        {
            lock ( inputLock )
            {
                MarkPressed( inputKey, playerNum );
                ScheduleRelease( inputKey, DefaultDurationMs );
            }
        }

        private static void MarkPressed( string inputKey, int playerNum )
        {
            simulatedInputs[inputKey] = true;
            keyPlayers[inputKey] = playerNum;
        }

        // Every pending release lives in activeHolds and carries the generation its key stood at when
        // it was scheduled, so a later command on the same key can both dispose it and neuter a
        // callback already in flight. The generation is per key: cancelling one key's release must
        // leave every other key's live release alone.
        private static void ScheduleRelease( string inputKey, int delayMs )
        {
            lock ( inputLock )
            {
                int generation;
                releaseGenerations.TryGetValue( inputKey, out generation );

                PendingRelease holder = new PendingRelease { Generation = generation };

                holder.Timer = new Timer( _ =>
                {
                    lock ( inputLock )
                    {
                        int current;
                        releaseGenerations.TryGetValue( inputKey, out current );

                        if ( current != holder.Generation )
                        {
                            return;
                        }

                        PendingRelease pending;
                        if ( !activeHolds.TryGetValue( inputKey, out pending ) || !ReferenceEquals( pending, holder ) )
                        {
                            return;
                        }

                        simulatedInputs[inputKey] = false;
                        holder.Timer.Dispose();
                        activeHolds.Remove( inputKey );
                    }
                }, null, Timeout.Infinite, Timeout.Infinite );

                PendingRelease previous;
                if ( activeHolds.TryGetValue( inputKey, out previous ) && previous.Timer != null )
                {
                    previous.Timer.Dispose();
                }

                activeHolds[inputKey] = holder;
                holder.Timer.Change( delayMs, Timeout.Infinite );
            }
        }

        // Cancellation is scoped to the keys the new command is about to press: a cancelled release
        // leaves its key released, since the press it belonged to is over, while a key the command
        // does not touch keeps its pending release.
        private static void CancelPendingReleases( ICollection<string> inputKeys )
        {
            lock ( inputLock )
            {
                foreach ( string inputKey in inputKeys )
                {
                    int generation;
                    releaseGenerations.TryGetValue( inputKey, out generation );
                    releaseGenerations[inputKey] = generation + 1;

                    PendingRelease pending;
                    if ( activeHolds.TryGetValue( inputKey, out pending ) )
                    {
                        if ( pending.Timer != null )
                        {
                            pending.Timer.Dispose();
                        }

                        activeHolds.Remove( inputKey );
                    }

                    if ( simulatedInputs.ContainsKey( inputKey ) )
                    {
                        simulatedInputs[inputKey] = false;
                    }
                }
            }
        }

        private static void PressInputMultiple( string inputKey, int count, int intervalMs, int holdMs, int playerNum )
        {
            for ( int i = 0; i < count; i++ )
            {
                lock ( inputLock )
                {
                    MarkPressed( inputKey, playerNum );
                }

                Thread.Sleep( holdMs );

                lock ( inputLock )
                {
                    simulatedInputs[inputKey] = false;
                }

                if ( i < count - 1 )
                {
                    Thread.Sleep( intervalMs );
                }
            }
        }

        private static object RunSequence( List<InputStep> sequence, int targetPlayer )
        {
            if ( sequence.Count == 0 )
            {
                return new { success = false, error = "Input sequence is empty" };
            }

            long totalMs = 0;
            List<string> stepKeys = new List<string>();

            for ( int i = 0; i < sequence.Count; i++ )
            {
                var step = sequence[i];

                if ( step.Duration < 0 || step.Wait < 0 )
                {
                    return new { success = false, error = $"Step {i} has a negative duration or wait" };
                }

                if ( step.Duration > MaxSequenceMs || step.Wait > MaxSequenceMs )
                {
                    return new { success = false, error = $"Step {i} alone exceeds the {MaxSequenceMs}ms limit" };
                }

                if ( step.Duration == 0 )
                {
                    step.Duration = DefaultDurationMs;
                }

                if ( !string.IsNullOrEmpty( step.Action ) )
                {
                    string stepKey = step.Action.ToLower();

                    if ( !IsKnownAction( stepKey ) )
                    {
                        return new { success = false, error = $"Unknown input action in step {i}: {step.Action}" };
                    }

                    if ( !stepKeys.Contains( stepKey ) )
                    {
                        stepKeys.Add( stepKey );
                    }

                    totalMs += step.Duration;
                }

                totalMs += step.Wait;
            }

            if ( totalMs > MaxSequenceMs )
            {
                return new { success = false, error = $"Input sequence would take {totalMs}ms, over the {MaxSequenceMs}ms limit" };
            }

            string warning = InputWarning( stepKeys, targetPlayer );

            CancelPendingReleases( stepKeys );

            foreach ( var step in sequence )
            {
                if ( !string.IsNullOrEmpty( step.Action ) )
                {
                    string inputKey = step.Action.ToLower();

                    lock ( inputLock )
                    {
                        MarkPressed( inputKey, targetPlayer );
                    }

                    Thread.Sleep( step.Duration );

                    lock ( inputLock )
                    {
                        simulatedInputs[inputKey] = false;
                    }
                }

                if ( step.Wait > 0 )
                {
                    Thread.Sleep( step.Wait );
                }
            }

            return WithWarning( new { success = true, steps = sequence.Count, totalMs = totalMs, player = targetPlayer }, warning );
        }

        private static object WithWarning( object reply, string warning )
        {
            if ( warning == null )
            {
                return reply;
            }

            JObject withWarning = JObject.FromObject( reply );
            withWarning["warning"] = warning;
            return withWarning;
        }

        private static readonly HashSet<string> MenuOnlyKeys = new HashSet<string> { "start", "escape" };

        private static string InputWarning( ICollection<string> inputKeys, int targetPlayer )
        {
            bool reachesBro = false;
            foreach ( string key in inputKeys )
            {
                if ( !MenuOnlyKeys.Contains( key ) )
                {
                    reachesBro = true;
                }
            }

            if ( !reachesBro )
            {
                return null;
            }

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

                    if ( Time.timeScale == 0f )
                    {
                        reasons.Add( "Time.timeScale is 0, so no bro moves" );
                    }

                    if ( StateModifier.ReadSlot( targetPlayer ).State != "alive" )
                    {
                        reasons.Add( $"player {targetPlayer} has no live bro" );
                    }
                }
            }
            catch ( Exception ex )
            {
                Main.Log( $"simulate_input state check failed: {ex.Message}" );
            }

            return reasons.Count == 0 ? null : "This input will not be consumed by a bro: " + string.Join( "; ", reasons.ToArray() );
        }

        public static bool IsInputSimulated( string inputKey )
        {
            lock ( inputLock )
            {
                bool pressed;
                return simulatedInputs.TryGetValue( inputKey, out pressed ) && pressed;
            }
        }

        private static int PressedFor( string inputKey )
        {
            lock ( inputLock )
            {
                bool pressed;
                int player;
                if ( !simulatedInputs.TryGetValue( inputKey, out pressed ) || !pressed )
                {
                    return -1;
                }
                return keyPlayers.TryGetValue( inputKey, out player ) ? player : -1;
            }
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

        public static bool IsSimulatedOn( string inputKey, int controller )
        {
            int player = PressedFor( inputKey );
            return player >= 0 && controller >= 0 && ControllerOf( player ) == controller;
        }

        public static int ControllerPressing( string inputKey )
        {
            int player = PressedFor( inputKey );
            if ( player < 0 )
            {
                return -1;
            }

            int controller = ControllerOf( player );
            // With no local player playing, the join screen joins controller 4 as player 0.
            if ( controller < 0 && !AnyLocalPlayerPlaying() )
            {
                return FirstGamepadController;
            }

            return controller;
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

        private const int FirstGamepadController = 4;

        private static int MenuController( int player )
        {
            int controller = ControllerOf( player );
            // A pause by keyboard controller 0 ignores every controller's cancel and start until the real Escape key.
            return controller >= FirstGamepadController ? controller : FirstGamepadController;
        }

        public static int MenuControllerPressing( string inputKey )
        {
            int player = PressedFor( inputKey );
            return player < 0 ? -1 : MenuController( player );
        }

        public static bool IsSimulatedOnMenu( string inputKey, int controller )
        {
            int player = PressedFor( inputKey );
            return player >= 0 && MenuController( player ) == controller;
        }

        public static bool HasAnySimulatedInput()
        {
            lock ( inputLock )
            {
                foreach ( bool pressed in simulatedInputs.Values )
                {
                    if ( pressed )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool ShouldTriggerStart()
        {
            lock ( inputLock )
            {
                bool pressed;
                if ( !simulatedInputs.TryGetValue( "start", out pressed ) || !pressed )
                {
                    return false;
                }

                DateTime now = DateTime.UtcNow;
                if ( ( now - lastStartTrigger ).TotalMilliseconds < StartRepeatMs )
                {
                    return false;
                }

                lastStartTrigger = now;
                return true;
            }
        }

        public static void ClearAllInputs()
        {
            lock ( inputLock )
            {
                CancelPendingReleases( new List<string>( activeHolds.Keys ) );

                foreach ( string key in new List<string>( simulatedInputs.Keys ) )
                {
                    simulatedInputs[key] = false;
                }
            }
        }

        public static void Cleanup()
        {
            ClearAllInputs();
        }
    }
}
