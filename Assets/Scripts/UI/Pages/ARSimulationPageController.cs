using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Pages
{
    public class ARSimulationPageController : PageController
    {
        string moduleId;
        int currentPhase = 0;
        int score = 50;
        float timeLeft = 120f;
        bool timerRunning = false;

        Label scoreValue, timerValue, instructionText;
        Label phaseEmoji, phaseInstruction;
        VisualElement phaseDots, scanLine, phaseVisual, arScene, timerPill;

        PhaseData[] phases;

        [Serializable]
        class PhaseData
        {
            public string emoji;
            public string instruction;
            public int scoreBonus;
            public bool showScanLine;
            public bool startTimer;
        }

        protected override void BindUI()
        {
            scoreValue = root.Q<Label>("score-value");
            timerValue = root.Q<Label>("timer-value");
            instructionText = root.Q<Label>("instruction-text");
            phaseEmoji = root.Q<Label>("phase-emoji");
            phaseInstruction = root.Q<Label>("phase-instruction");
            phaseDots = root.Q("phase-dots");
            scanLine = root.Q("scan-line");
            phaseVisual = root.Q("phase-visual");
            arScene = root.Q("ar-scene");
            timerPill = root.Q("timer-pill");
        }

        public override void SetNavigationParameter(object param) => moduleId = param as string;

        public override void OnPageEnter()
        {
            currentPhase = 0;
            score = 50;
            timeLeft = 120f;
            timerRunning = false;
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            LoadPhases();
            CreatePhaseDots();
            RefreshPhase();
            // Additive AR scene behind UI (existing AR Plane Detection Placement)
            StartCoroutine(LoadARSceneAdditive());
        }

        public override void OnPageExit()
        {
            UnloadARScene();
            StopAllCoroutines();
            timerRunning = false;
        }

        IEnumerator LoadARSceneAdditive()
        {
            // Load existing AR scene additively if not already loaded
            string arSceneName = "AR Plane Detection Placement";
            if (!SceneManager.GetSceneByName(arSceneName).isLoaded)
            {
                var op = SceneManager.LoadSceneAsync(arSceneName, LoadSceneMode.Additive);
                while (op != null && !op.isDone) yield return null;
                Debug.Log($"[ARSim] Additive AR scene loaded: {arSceneName}");
            }
        }

        void UnloadARScene()
        {
            string arSceneName = "AR Plane Detection Placement";
            var arSceneObj = SceneManager.GetSceneByName(arSceneName);
            if (arSceneObj.isLoaded)
            {
                SceneManager.UnloadSceneAsync(arSceneName);
                Debug.Log($"[ARSim] Unloaded AR scene: {arSceneName}");
            }
        }

        void LoadPhases()
        {
            phases = new PhaseData[]
            {
                new PhaseData { emoji="📱", instruction="Scanning environment...", showScanLine=true },
                new PhaseData { emoji="👆", instruction="Move phone slowly across a flat surface...", showScanLine=true },
                new PhaseData { emoji="✅", instruction="Surface detected! Tap to place scenario" },
                new PhaseData { emoji="🔥", instruction="Fire detected! Locate the extinguisher!", scoreBonus=0 },
                new PhaseData { emoji="🧯", instruction="Extinguisher located! Move to it and tap to pick up", scoreBonus=10, startTimer=true },
                new PhaseData { emoji="🔓", instruction="Tap to OPEN CAP", scoreBonus=10 },
                new PhaseData { emoji="💨", instruction="HOLD to SPRAY (6 seconds)...", scoreBonus=10 },
                new PhaseData { emoji="✅", instruction="Fire extinguished! Area Clear!", scoreBonus=10 }
            };
        }

        void CreatePhaseDots()
        {
            if (phaseDots == null) return;
            phaseDots.Clear();
            for (int i = 0; i < phases.Length; i++)
            {
                var dot = new VisualElement();
                dot.name = $"phase-dot-{i}";
                dot.AddToClassList("phase-dot");
                dot.style.width = 8; dot.style.height = 8;
                dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4; dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
                dot.style.marginLeft = 4; dot.style.marginRight = 4;
                dot.style.backgroundColor = new StyleColor(new Color(0.2f,0.2f,0.2f));
                phaseDots.Add(dot);
            }
        }

        void RefreshPhase()
        {
            if (currentPhase >= phases.Length) { OnSimulationComplete(); return; }
            var phase = phases[currentPhase];
            if (phaseEmoji != null) phaseEmoji.text = phase.emoji;
            if (phaseInstruction != null) phaseInstruction.text = phase.instruction;
            if (instructionText != null) instructionText.text = phase.instruction;
            score += phase.scoreBonus;
            if (scoreValue != null) scoreValue.text = score.ToString();
            if (scanLine != null) scanLine.style.display = phase.showScanLine ? DisplayStyle.Flex : DisplayStyle.None;
            if (phase.startTimer && !timerRunning)
            {
                timerRunning = true;
                StartCoroutine(TimerCoroutine());
            }
            // Dots
            for (int i = 0; i < phases.Length; i++)
            {
                var dot = phaseDots.Q($"phase-dot-{i}");
                if (dot == null) continue;
                if (i < currentPhase) dot.style.backgroundColor = new StyleColor(new Color(0.30f,0.68f,0.31f)); // green
                else if (i == currentPhase) dot.style.backgroundColor = new StyleColor(new Color(1f,0.42f,0f)); // orange
                else dot.style.backgroundColor = new StyleColor(new Color(0.2f,0.2f,0.2f));
            }
            StartCoroutine(AutoAdvance(phase.showScanLine ? 1.5f : 2f));
        }

        IEnumerator AutoAdvance(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentPhase < phases.Length - 1) { currentPhase++; RefreshPhase(); }
            else OnSimulationComplete();
        }

        IEnumerator TimerCoroutine()
        {
            while (timeLeft > 0 && timerRunning)
            {
                timeLeft -= Time.deltaTime;
                if (timerValue != null) timerValue.text = $"{Mathf.CeilToInt(timeLeft)}s";
                if (timerValue != null)
                {
                    timerValue.RemoveFromClassList("timer-green");
                    timerValue.RemoveFromClassList("timer-yellow");
                    timerValue.RemoveFromClassList("timer-red");
                    if (timeLeft > 60) timerValue.AddToClassList("timer-green");
                    else if (timeLeft > 30) timerValue.AddToClassList("timer-yellow");
                    else timerValue.AddToClassList("timer-red");
                }
                yield return null;
            }
        }

        void OnSimulationComplete()
        {
            StopAllCoroutines();
            timerRunning = false;
            Debug.Log($"[ARSim] Complete score={score} module={moduleId} -> Assessment");
            // Navigate to Assessment with moduleId and sim score
            NavigationManager.Instance.NavigateTo("UI_Assessment", new System.Collections.Generic.Dictionary<string, object>{{"moduleId", moduleId}, {"simulationScore", score}});
            // Also support direct param as string for fallback
        }
    }
}
