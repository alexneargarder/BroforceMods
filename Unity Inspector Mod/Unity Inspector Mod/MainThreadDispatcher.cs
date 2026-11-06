using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher instance;
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        private static volatile int mainThreadId;

        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static long lastFrameMs = -1;
        private static long actionStartedMs = -1;
        private static volatile string actionLabel;

        private const int StatePending = 0;
        private const int StateRunning = 1;
        private const int StateAbandoned = 2;

        private const int StopIdle = 0;
        private const int StopArmed = 1;
        private const int StopRequested = 2;

        public const int StopGraceMs = 2000;
        private const int CleanupGraceMs = 500;

        // Windows gives the main thread 1MB of stack.
        public const int StackLimitBytes = 512 * 1024;
        private const int UnwindAllowanceBytes = 64 * 1024;

        private static volatile int stopState;
        private static long armedAtMs;
        private static long abortFirstRaisedMs;
        private static long abortRaisedMs;
        private static long abortRaisedStack;
        private static int armSequence;
        private static int stoppableDepth;
        private static long stackTop;
        private static bool recursionStopped;

        private sealed class PendingAction
        {
            public int State;
            public volatile bool Completed;
            public Exception Error;
            public long StartedMs = -1;
        }

        public static MainThreadDispatcher Instance
        {
            get
            {
                if ( instance == null )
                {
                    Initialize();
                }
                return instance;
            }
        }

        public static void Initialize()
        {
            if ( instance == null )
            {
                var go = new GameObject( "MainThreadDispatcher" );
                // PauseController.PauseGameObjects deactivates every active object not tagged Unpausable.
                go.tag = "Unpausable";
                instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad( go );
            }
        }

        private void Awake()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        private static long Now()
        {
            return clock.ElapsedMilliseconds;
        }

        public unsafe void Update()
        {
            byte marker;
            stackTop = (long)&marker;

            if ( mainThreadId == 0 )
            {
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            Main.FlushPendingLog();

            Interlocked.Exchange( ref lastFrameMs, Now() );

            Action[] batch;
            lock ( executionQueue )
            {
                if ( executionQueue.Count == 0 )
                {
                    return;
                }
                batch = executionQueue.ToArray();
                executionQueue.Clear();
            }

            foreach ( var action in batch )
            {
                Interlocked.Exchange( ref actionStartedMs, Now() );
                try
                {
                    action.Invoke();
                }
                catch ( Exception ex )
                {
                    Main.Log( $"[MainThreadDispatcher] Error executing action: {ex.Message}" );
                }
                finally
                {
                    Interlocked.Exchange( ref actionStartedMs, -1 );
                    actionLabel = null;
                }
            }
        }

        public static void Enqueue( Action action )
        {
            if ( action == null )
            {
                return;
            }

            lock ( executionQueue )
            {
                executionQueue.Enqueue( action );
            }
        }

        public static string Stall()
        {
            long now = Now();
            long started = Interlocked.Read( ref actionStartedMs );
            if ( started >= 0 && now - started > 1000 )
            {
                var label = actionLabel;
                return "the main thread has been running " + ( label ?? "one main-thread action" ) + " for " +
                       Seconds( now - started );
            }

            long frame = Interlocked.Read( ref lastFrameMs );
            if ( frame >= 0 && now - frame > 1000 )
            {
                return "the main thread has not finished a frame for " + Seconds( now - frame ) +
                       " (a level load, or code that does not return)";
            }

            return null;
        }

        private static string Seconds( long ms )
        {
            return ( ms / 1000.0 ).ToString( "0.#", System.Globalization.CultureInfo.InvariantCulture ) + "s";
        }

        public static void EnqueueAndWait( Action action, int timeoutMs = 5000 )
        {
            Wait( action, timeoutMs, -1, -1, null );
        }

        public static void EnqueueAndWaitStoppable( Action action, int timeoutMs, int runBudgetMs, int ceilingMs,
            string label )
        {
            Wait( action, timeoutMs, runBudgetMs, ceilingMs, label );
        }

        private static void Wait( Action action, int timeoutMs, int runBudgetMs, int ceilingMs, string label )
        {
            if ( action == null )
            {
                return;
            }

            // Already on the main thread: the queue cannot drain while this call waits, so waiting
            // on it would only spend the whole budget and time out.
            if ( Thread.CurrentThread.ManagedThreadId == mainThreadId )
            {
                action();
                return;
            }

            var pending = new PendingAction();
            long enqueuedMs = Now();

            Action wrapper = () =>
            {
                if ( Interlocked.CompareExchange( ref pending.State, StateRunning, StatePending ) != StatePending )
                {
                    return;
                }

                Interlocked.Exchange( ref pending.StartedMs, Now() );
                if ( label != null )
                {
                    actionLabel = label;
                }
                try
                {
                    action();
                }
                catch ( Exception ex )
                {
                    pending.Error = ex;
                }
                finally
                {
                    pending.Completed = true;
                }
            };

            Enqueue( wrapper );

            bool stoppable = runBudgetMs >= 0;
            bool started = false;
            int requestedSequence = -1;
            long requestedAtMs = 0;

            while ( !pending.Completed )
            {
                long now = Now();
                if ( !started && now - enqueuedMs > timeoutMs )
                {
                    if ( Interlocked.CompareExchange( ref pending.State, StateAbandoned, StatePending ) == StatePending )
                    {
                        var stall = Stall();
                        throw new TimeoutException( $"Main thread action timed out after {timeoutMs}ms" +
                                                    ( stall != null ? ": " + stall : "" ) );
                    }

                    started = true;
                }

                if ( !stoppable )
                {
                    if ( started && now - enqueuedMs > timeoutMs * 2 )
                    {
                        throw new MainThreadActionRunningException(
                            $"Main thread action started but has not finished after {timeoutMs * 2}ms - the main thread is not advancing" );
                    }
                }
                else
                {
                    long startedAt = Interlocked.Read( ref pending.StartedMs );
                    if ( startedAt >= 0 )
                    {
                        started = true;

                        int sequence = Thread.VolatileRead( ref armSequence );
                        if ( stopState == StopArmed && sequence != requestedSequence &&
                             now - Interlocked.Read( ref armedAtMs ) >= runBudgetMs &&
                             Interlocked.CompareExchange( ref stopState, StopRequested, StopArmed ) == StopArmed )
                        {
                            requestedSequence = sequence;
                            requestedAtMs = now;
                        }

                        bool stopMissed = requestedSequence >= 0 && stopState == StopRequested &&
                                          now - requestedAtMs >= StopGraceMs;
                        if ( stopMissed || now - startedAt >= ceilingMs )
                        {
                            throw new MainThreadActionRunningException(
                                "still running after " + Seconds( now - startedAt ) +
                                ( stopMissed
                                    ? ", and the stop requested at " + Seconds( runBudgetMs ) +
                                      " did not reach it: it is inside a game, engine or library call, or " +
                                      "a loop not compiled by the script system"
                                    : "" ) +
                                "; the game's main thread is held until it returns" );
                        }
                    }
                }

                Thread.Sleep( 10 );
            }

            if ( pending.Error != null )
            {
                throw pending.Error;
            }
        }

        public static bool RunStoppable( Action userCode )
        {
            if ( stoppableDepth > 0 || Thread.CurrentThread.ManagedThreadId != mainThreadId )
            {
                userCode();
                return true;
            }

            bool finished = true;
            stoppableDepth++;
            Interlocked.Exchange( ref armedAtMs, Now() );
            Interlocked.Increment( ref armSequence );
            stopState = StopArmed;
            try
            {
                try
                {
                    userCode();
                }
                catch ( ThreadAbortException )
                {
                    if ( stopState != StopRequested )
                    {
                        throw;
                    }
                    Thread.ResetAbort();
                    finished = false;
                }
                catch ( Exception )
                {
                    // User code that wrapped the stop's abort in another exception leaves the abort pending.
                    if ( stopState != StopRequested || !AbortPending() )
                    {
                        throw;
                    }
                    ResetAbortQuietly();
                    finished = false;
                }

                if ( finished && stopState == StopRequested && AbortPending() )
                {
                    ResetAbortQuietly();
                    finished = false;
                }
            }
            finally
            {
                stopState = StopIdle;
                stoppableDepth--;
            }

            return finished;
        }

        public static unsafe void StopPoint()
        {
            if ( stopState != StopRequested || Thread.CurrentThread.ManagedThreadId != mainThreadId )
            {
                return;
            }

            byte marker;
            long used = stackTop - (long)&marker;
            long now = Now();

            if ( AbortPending() )
            {
                // A finally runs on top of the throw's stack, where each abort would nest another dispatch; a catch runs below it and nests nothing.
                if ( now - abortFirstRaisedMs < CleanupGraceMs ||
                     ( used > abortRaisedStack && now - abortRaisedMs < CleanupGraceMs ) )
                {
                    return;
                }

                ResetAbortQuietly();
            }
            else
            {
                abortFirstRaisedMs = now;
            }

            abortRaisedMs = now;
            abortRaisedStack = used;
            Thread.CurrentThread.Abort();
        }

        // A stack overflow on this Mono kills the process rather than throwing.
        public static unsafe void MethodEntry()
        {
            if ( stopState == StopRequested )
            {
                StopPoint();
            }

            byte marker;
            long used = stackTop - (long)&marker;
            if ( used <= StackLimitBytes )
            {
                recursionStopped = false;
                return;
            }

            // Mono runs a finally block on top of the throw's stack, so a throw from each frame's would nest a dispatch per frame until the stack overflows.
            if ( recursionStopped && used <= StackLimitBytes + UnwindAllowanceBytes )
            {
                return;
            }
            if ( Thread.CurrentThread.ManagedThreadId != mainThreadId )
            {
                return;
            }

            recursionStopped = true;
            throw new RunawayRecursionException( "Runaway recursion: this call chain used more than " +
                                                 StackLimitBytes / 1024 + "KB of the main thread's stack, so it was " +
                                                 "stopped before a stack overflow could kill the game" );
        }

        private static bool AbortPending()
        {
            return ( Thread.CurrentThread.ThreadState & System.Threading.ThreadState.AbortRequested ) != 0;
        }

        private static void ResetAbortQuietly()
        {
            try
            {
                Thread.ResetAbort();
            }
            catch ( ThreadStateException )
            {
            }
        }

        public static bool IsStall( Exception ex )
        {
            return ex is TimeoutException || ex is MainThreadActionRunningException;
        }

        public sealed class MainThreadActionRunningException : Exception
        {
            public MainThreadActionRunningException( string message ) : base( message ) { }
        }

        public sealed class RunawayRecursionException : Exception
        {
            public RunawayRecursionException( string message ) : base( message ) { }
        }

        private void OnDestroy()
        {
            instance = null;
        }
    }
}
