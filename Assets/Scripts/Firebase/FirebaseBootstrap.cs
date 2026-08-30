using UnityEngine;
using MiningSafetyAR.Helpers;

namespace MiningSafetyAR.Firebase
{
    /// <summary>
    /// Auto-creates persistent Firebase managers on app start — no need to place prefab in scene.
    /// Add this script anywhere or let RuntimeInitializeOnLoadMethod create it.
    /// </summary>
    public static class FirebaseBootstrap
    {
        private static bool _spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (_spawned) return;
            _spawned = true;

            var go = new GameObject("AppManagers (Firebase)");
            Object.DontDestroyOnLoad(go);
            // Ensure dispatcher first
            go.AddComponent<MainThreadDispatcher>();
            go.AddComponent<FirebaseAuthManager>();
            go.AddComponent<FirestoreService>();
            Debug.Log("[FirebaseBootstrap] Spawned AppManagers (FirebaseAuthManager + FirestoreService + MainThreadDispatcher)");
        }

        // Manual spawn for scenes where auto-init didn't run (editor Play from specific scene)
        public static void EnsureExists()
        {
            if (FirebaseAuthManager.Instance != null && FirestoreService.Instance != null) return;
            if (_spawned) return;
            AutoInit();
        }
    }
}
