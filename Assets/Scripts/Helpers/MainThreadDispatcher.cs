using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Helpers
{
    /// <summary>
    /// Queues actions from background threads (Firebase Task callbacks) to main thread Update.
    /// Persistent singleton via DontDestroyOnLoad.
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public static void Enqueue(Action action)
        {
            if (action == null) return;
            lock (_lock)
            {
                _queue.Enqueue(action);
            }
            // Ensure instance exists so Update will drain queue
            if (_instance == null) _ = Instance;
        }

        // Alias matching docs naming
        public static void EnqueueOnMainThread(Action action) => Enqueue(action);

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            while (true)
            {
                Action action = null;
                lock (_lock)
                {
                    if (_queue.Count == 0) break;
                    action = _queue.Dequeue();
                }
                try { action?.Invoke(); }
                catch (Exception ex) { Debug.LogError($"[MainThreadDispatcher] {ex}"); }
            }
        }
    }
}
