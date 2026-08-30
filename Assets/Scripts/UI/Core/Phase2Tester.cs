using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Test
{
    public class Phase2Tester : MonoBehaviour
    {
        [SerializeField] UIDocument document;

        void OnEnable()
        {
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null)
            {
                Debug.LogWarning("[Phase2Tester] No UIDocument assigned — add to GameObject with UIDocument or assign one");
                return;
            }
            Invoke(nameof(RunTests), 0.3f);
        }

        void RunTests()
        {
            var doc = document != null ? document : GetComponent<UIDocument>();
            var root = doc.rootVisualElement;
            root.Clear();
            root.style.backgroundColor = Color.white;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 16;

            Debug.Log("[Phase2Tester] --- Phase 2 Verification Start ---");

#if UNITY_EDITOR
            // Check USS files
            CheckUSS("Assets/UI/Styles/Theme.uss", "Theme");
            CheckUSS("Assets/UI/Styles/Components.uss", "Components");
            CheckUSS("Assets/UI/Styles/BottomNav.uss", "BottomNav");
            CheckUSS("Assets/UI/Styles/Header.uss", "Header");

            // Check UXML templates
            var bottomNavT = LoadTemplate("Assets/UI/Templates/Components/BottomNav.uxml", "BottomNav");
            var headerT = LoadTemplate("Assets/UI/Templates/Components/Header.uxml", "Header");
            var moduleCardT = LoadTemplate("Assets/UI/Templates/Components/ModuleCard.uxml", "ModuleCard");
            var progressBarT = LoadTemplate("Assets/UI/Templates/Components/ProgressBar.uxml", "ProgressBar");
            var scoreBarT = LoadTemplate("Assets/UI/Templates/Components/ScoreBar.uxml", "ScoreBar");
            var toggleT = LoadTemplate("Assets/UI/Templates/Components/ToggleSwitch.uxml", "ToggleSwitch");
            var optionT = LoadTemplate("Assets/UI/Templates/Components/OptionButton.uxml", "OptionButton");
            var badgeT = LoadTemplate("Assets/UI/Templates/Components/StatusBadge.uxml", "StatusBadge");
            var pillT = LoadTemplate("Assets/UI/Templates/Components/MetaPill.uxml", "MetaPill");
            var bannerT = LoadTemplate("Assets/UI/Templates/Components/FeedbackBanner.uxml", "FeedbackBanner");
            var dotT = LoadTemplate("Assets/UI/Templates/Components/PhaseDot.uxml", "PhaseDot");
            var rowT = LoadTemplate("Assets/UI/Templates/Components/TableRow.uxml", "TableRow");

            // Build visual test area
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            // Header test
            if (headerT != null)
            {
                var header = headerT.Instantiate();
                header.Q<Label>("title").text = "Phase 2 Test Header";
                scroll.Add(header);
                Debug.Log("[Phase2] Header OK");
            }

            // ModuleCard tests — all 5 statuses
            if (moduleCardT != null)
            {
                var app = FindFirstObjectByType<AppDataService>();
                var mods = app != null ? app.GetAllModules() : null;
                if (mods == null || mods.Count == 0)
                {
                    // Fallback dummy modules
                    mods = new System.Collections.Generic.List<ModuleData>
                    {
                        new ModuleData{id="fire_safety", title="Fire Test", iconEmoji="🔥", duration="45 min", difficulty="Medium", status=ModuleStatus.Completed, progress=100, bestScore=85, color="#FF6D00"},
                        new ModuleData{id="machinery_safety", title="Machinery Test", iconEmoji="⚙️", duration="40 min", difficulty="Medium", status=ModuleStatus.InProgress, progress=45, bestScore=60, color="#1976D2"},
                        new ModuleData{id="heights_safety", title="Heights Locked", iconEmoji="⛰️", duration="40 min", difficulty="Hard", status=ModuleStatus.Locked, progress=0, bestScore=0, color="#388E3C"},
                    };
                }
                foreach (var m in mods)
                {
                    var card = ModuleCardFactory.Create(moduleCardT, m, (mod) => Debug.Log($"[Phase2] Card clicked: {mod.id}"));
                    card.style.marginBottom = 8;
                    scroll.Add(card);
                }
                Debug.Log($"[Phase2] ModuleCard OK: {mods.Count} cards created");
            }

            // ProgressBar + ScoreBar
            if (progressBarT != null)
            {
                var bar = progressBarT.Instantiate();
                bar.style.marginBottom = 12;
                bar.style.marginTop = 12;
                scroll.Add(new Label("ProgressBar 68% test:") { style = { color = Color.black } });
                scroll.Add(bar);
                var track = bar.Q("progress-track") ?? bar;
                ProgressBarHelper.SetProgress(track, 68f, "#FF6D00");
                Debug.Log("[Phase2] ProgressBar OK — set to 68%");
            }

            if (scoreBarT != null)
            {
                var sb = scoreBarT.Instantiate();
                ScoreBarHelper.Configure(sb, "Hazard Recognition", 82);
                scroll.Add(sb);
                var sb2 = scoreBarT.Instantiate();
                ScoreBarHelper.Configure(sb2, "PPE Selection", 65);
                scroll.Add(sb2);
                var sb3 = scoreBarT.Instantiate();
                ScoreBarHelper.Configure(sb3, "Evacuation", 45);
                scroll.Add(sb3);
                Debug.Log("[Phase2] ScoreBar OK — 82/65/45 with green/orange/red");
            }

            // ToggleSwitch
            if (toggleT != null)
            {
                scroll.Add(new Label("ToggleSwitch test (click to toggle):") { style = { color = Color.black, marginTop = 12 } });
                var toggle = toggleT.Instantiate();
                scroll.Add(toggle);
                var ctrl = new ToggleSwitchController(toggle.Q("toggle") ?? toggle);
                ctrl.OnToggled += (on) => Debug.Log($"[Phase2] Toggle switched: {(on ? "ON" : "OFF")}");
                ctrl.SetValue(false);
                // Also test SetValue true after 1 sec
                Invoke(nameof(ToggleOn), 1f);
                Debug.Log("[Phase2] ToggleSwitch OK — click to test");
            }

            // TableRow
            if (rowT != null)
            {
                scroll.Add(new Label("TableRow test:") { style = { color = Color.black, marginTop = 12 } });
                scroll.Add(TableRowFactory.Create(rowT, 1, "2026-08-26", "85%", "Pass", true));
                scroll.Add(TableRowFactory.Create(rowT, 2, "2026-08-27", "60%", "Fail", false));
                Debug.Log("[Phase2] TableRow OK");
            }

            // OptionButton
            if (optionT != null)
            {
                scroll.Add(new Label("OptionButton test:") { style = { color = Color.black, marginTop = 12 } });
                for (int i = 0; i < 4; i++)
                {
                    var opt = optionT.Instantiate();
                    opt.Q<Label>("option-letter").text = ((char)('A' + i)).ToString();
                    opt.Q<Label>("option-text").text = $"Option { (char)('A'+i)} sample text";
                    scroll.Add(opt);
                }
                Debug.Log("[Phase2] OptionButton OK");
            }

            // BottomNav
            if (bottomNavT != null)
            {
                var nav = bottomNavT.Instantiate();
                nav.style.marginTop = 12;
                scroll.Add(nav);
                Debug.Log("[Phase2] BottomNav OK");
            }

            Debug.Log("[Phase2Tester] --- Verification Complete --- Scroll to see components. Check for MISSING above. All OK = Phase 2 PASS");
#else
            Debug.Log("[Phase2] Editor checks skipped (not in Editor)");
#endif
        }

        void ToggleOn()
        {
            Debug.Log("[Phase2] Toggle auto-set to ON for demo");
            var doc = document != null ? document : GetComponent<UIDocument>();
            var toggle = doc.rootVisualElement.Q("toggle");
            if (toggle != null)
            {
                var ctrl = new ToggleSwitchController(toggle);
                ctrl.SetValue(true);
            }
        }

#if UNITY_EDITOR
        void CheckUSS(string path, string name)
        {
            var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            Debug.Log(sheet != null ? $"[Phase2] {name}.uss OK" : $"[Phase2] {name}.uss MISSING at {path}");
        }

        VisualTreeAsset LoadTemplate(string path, string name)
        {
            var t = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Debug.Log(t != null ? $"[Phase2] {name}.uxml OK" : $"[Phase2] {name}.uxml MISSING at {path}");
            return t;
        }
#endif
    }
}
