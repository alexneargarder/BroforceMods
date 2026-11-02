using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher instance;
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        private static volatile int mainThreadId;

        // A TimeoutException means the action did not run and will not run. The wrapper and the
        // waiter race for the same state through one interlocked exchange: the wrapper claims Pending
        // for Running or returns without touching the action, and the waiter claims Pending for
        // Abandoned or - having lost, so the action is already running - waits it out for one more
        // budget and reports its real outcome. An action still running past that second budget has
        // the main thread hung; the waiter then throws MainThreadActionRunningException, a different
        // type, so nothing reads that as an action that never ran.
        private const int StatePending = 0;
        private const int StateRunning = 1;
        private const int StateAbandoned = 2;

        private sealed class PendingAction
        {
            public int State;
            public volatile bool Completed;
            public Exception Error;
        }
        
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    Initialize();
                }
                return instance;
            }
        }
        
        public static void Initialize()
        {
            if (instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }
        
        void Awake()
        {
            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        public void Update()
        {
            if (mainThreadId == 0)
            {
                mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }

            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    var action = executionQueue.Dequeue();
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Main.Log($"[MainThreadDispatcher] Error executing action: {ex.Message}");
                    }
                }
            }
        }
        
        public static void Enqueue(Action action)
        {
            if (action == null)
            {
                return;
            }
            
            lock (executionQueue)
            {
                executionQueue.Enqueue(action);
            }
        }
        
        public static void EnqueueAndWait(Action action, int timeoutMs = 5000)
        {
            if (action == null)
            {
                return;
            }
            
            // Already on the main thread: the queue cannot drain while this call waits, so waiting
            // on it would only spend the whole budget and time out.
            if (System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                action();
                return;
            }

            var pending = new PendingAction();

            // Update holds the queue lock for the whole drain, so the enqueue itself can block for as
            // long as the action running now takes; the budget covers that wait, and running out of it
            // while still outside the lock is a timeout with nothing queued at all.
            var startTime = DateTime.Now;

            Action wrapper = () =>
            {
                if (System.Threading.Interlocked.CompareExchange(
                        ref pending.State, StateRunning, StatePending) != StatePending)
                {
                    return;
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    pending.Error = ex;
                }
                finally
                {
                    pending.Completed = true;
                }
            };

            if (!System.Threading.Monitor.TryEnter(executionQueue, timeoutMs))
            {
                throw new TimeoutException($"Main thread action timed out after {timeoutMs}ms");
            }
            try
            {
                executionQueue.Enqueue(wrapper);
            }
            finally
            {
                System.Threading.Monitor.Exit(executionQueue);
            }

            bool started = false;
            while (!pending.Completed)
            {
                double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (!started && elapsed > timeoutMs)
                {
                    if (System.Threading.Interlocked.CompareExchange(
                            ref pending.State, StateAbandoned, StatePending) == StatePending)
                    {
                        throw new TimeoutException($"Main thread action timed out after {timeoutMs}ms");
                    }

                    // The action beat the deadline to the state and is running now, so it will finish
                    // and set Completed; the caller gets its return or its exception, never a timeout.
                    started = true;
                }
                else if (started && elapsed > timeoutMs * 2)
                {
                    throw new MainThreadActionRunningException(
                        $"Main thread action started but has not finished after {timeoutMs * 2}ms - the main thread is not advancing");
                }
                System.Threading.Thread.Sleep(10);
            }

            if (pending.Error != null)
            {
                throw pending.Error;
            }
        }
        
        public sealed class MainThreadActionRunningException : Exception
        {
            public MainThreadActionRunningException(string message) : base(message) { }
        }

        void OnDestroy()
        {
            instance = null;
        }
    }
}