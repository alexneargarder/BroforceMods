using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity_Inspector_Mod
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher instance;
        private static readonly Queue<Action> executionQueue = new Queue<Action>();
        
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
        
        public void Update()
        {
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
            
            bool completed = false;
            Exception exception = null;
            
            Enqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    completed = true;
                }
            });
            
            var startTime = DateTime.Now;
            while (!completed)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                {
                    throw new TimeoutException($"Main thread action timed out after {timeoutMs}ms");
                }
                System.Threading.Thread.Sleep(10);
            }
            
            if (exception != null)
            {
                throw exception;
            }
        }
        
        void OnDestroy()
        {
            instance = null;
        }
    }
}