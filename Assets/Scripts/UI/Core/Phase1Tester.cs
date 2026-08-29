using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Test
{
    public class Phase1Tester : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[Phase1Tester] --- Phase 1 Verification Start ---");

#if UNITY_EDITOR
            // 1. USS Theme check
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>("Assets/UI/Styles/Theme.uss");
            Debug.Log(theme != null ? "[Phase1] Theme.uss OK" : "[Phase1] Theme.uss MISSING");

            var reset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>("Assets/UI/Styles/Reset.uss");
            Debug.Log(reset != null ? "[Phase1] Reset.uss OK" : "[Phase1] Reset.uss MISSING");

            // 2. Databases
            var modDb = UnityEditor.AssetDatabase.LoadAssetAtPath<ModuleDatabase>("Assets/Data/ModuleDatabase.asset");
            Debug.Log(modDb != null ? $"[Phase1] ModuleDatabase OK: {modDb.modules.Count} modules" : "[Phase1] ModuleDatabase MISSING — run Mining Safety AR -> Phase 1 Seeder");

            if (modDb != null)
                foreach (var m in modDb.modules)
                    Debug.Log($"[Phase1] Module: {m.id} title={m.title} status={m.status} progress={m.progress}");

            var qDb = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestionDatabase>("Assets/Data/QuestionDatabase.asset");
            Debug.Log(qDb != null ? $"[Phase1] QuestionDatabase OK: {qDb.questions.Count} Q" : "[Phase1] QuestionDatabase MISSING");

            var cDb = UnityEditor.AssetDatabase.LoadAssetAtPath<CertificateDatabase>("Assets/Data/CertificateDatabase.asset");
            Debug.Log(cDb != null ? $"[Phase1] CertificateDatabase OK: {cDb.certificates.Count} certs" : "[Phase1] CertificateDatabase MISSING");

            var ps = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI/PanelSettings/DefaultPanelSettings.asset");
            Debug.Log(ps != null ? $"[Phase1] PanelSettings OK: {ps.name} scale={ps.scaleMode} ref={ps.referenceResolution}" : "[Phase1] PanelSettings MISSING");
#else
            Debug.Log("[Phase1] Editor checks skipped (not in Editor)");
#endif

            // 3. NavigationManager existence (via bootstrap)
            var nav = FindFirstObjectByType<NavigationManager>();
            Debug.Log(nav != null ? $"[Phase1] NavigationManager OK: current={nav.CurrentScene}" : "[Phase1] NavigationManager MISSING — should be auto-spawned by UIBootstrap");

            // 4. AppDataService
            var app = FindFirstObjectByType<AppDataService>();
            if (app == null) Debug.Log("[Phase1] AppDataService MISSING — UIBootstrap may not have run (check Play mode)");
            else
            {
                var all = app.GetAllModules();
                Debug.Log($"[Phase1] AppDataService OK: GetAllModules()={all.Count}");
                var mod = app.GetModule("fire_safety");
                Debug.Log(mod != null ? $"[Phase1] GetModule fire_safety OK: {mod.title}" : "[Phase1] GetModule fire_safety FAIL");
                var qs = app.GetQuestions("fire_safety");
                Debug.Log($"[Phase1] GetQuestions fire_safety: {qs.Count} (expect 5)");
            }

            Debug.Log("[Phase1Tester] --- Verification Complete --- Check for MISSING above. All OK = Phase 1 PASS");
        }

        [ContextMenu("Test Navigation To Splash")]
        public void TestNav()
        {
            var nav = FindFirstObjectByType<NavigationManager>();
            if (nav == null) { Debug.LogError("[Phase1Tester] No NavigationManager"); return; }
            Debug.Log($"[Phase1Tester] Current scene: {nav.CurrentScene}, BuildSettings scenes: {UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings}");
        }
    }
}
