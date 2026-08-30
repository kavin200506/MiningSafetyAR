using UnityEngine;

namespace MiningSafetyAR.UI.Core
{
    public static class UIBootstrap
    {
        static bool spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (spawned) return;
            spawned = true;

            // NavigationManager persistent
            var navGo = new GameObject("NavigationManager");
            Object.DontDestroyOnLoad(navGo);
            navGo.AddComponent<Navigation.NavigationManager>();

            // AppDataService persistent
            var appGo = new GameObject("AppDataService");
            Object.DontDestroyOnLoad(appGo);
            var svc = appGo.AddComponent<Data.AppDataService>();
#if UNITY_EDITOR
            // Try to auto-assign databases in Editor
            var modDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.ModuleDatabase>("Assets/Data/ModuleDatabase.asset");
            var qDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.QuestionDatabase>("Assets/Data/QuestionDatabase.asset");
            var certDb = UnityEditor.AssetDatabase.LoadAssetAtPath<Data.CertificateDatabase>("Assets/Data/CertificateDatabase.asset");
            var t = typeof(Data.AppDataService);
            var f1 = t.GetField("moduleDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f1 != null && modDb != null) f1.SetValue(svc, modDb);
            var f2 = t.GetField("questionDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f2 != null && qDb != null) f2.SetValue(svc, qDb);
            var f3 = t.GetField("certificateDatabase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f3 != null && certDb != null) f3.SetValue(svc, certDb);
#endif
            Debug.Log("[UIBootstrap] Spawned NavigationManager + AppDataService");
        }
    }
}
