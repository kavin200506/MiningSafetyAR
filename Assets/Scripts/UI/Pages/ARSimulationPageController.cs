using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Helpers;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Pages
{
    /// <summary>
    /// AR Simulation Page Controller — runs the actual working AR simulation scene additively.
    /// Features transparent HUD overlay.
    /// </summary>
    public class ARSimulationPageController : PageController
    {
        private string moduleId;
        private int score = 100;
        private float timeLeft = 180f;
        private bool timerRunning = false;

        private Label scoreValue;
        private Label timerValue;
        private Label instructionText;

        protected override void BindUI()
        {
            scoreValue = root.Q<Label>("score-value");
            timerValue = root.Q<Label>("timer-value");
            instructionText = root.Q<Label>("instruction-text");
        }

        public override void SetNavigationParameter(object param) => moduleId = param as string;

        public override void OnPageEnter()
        {
            score = 100;
            timeLeft = 180f;
            timerRunning = true;
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";

            if (scoreValue != null) scoreValue.text = score.ToString();
            if (instructionText != null)
            {
                instructionText.text = "AR Simulation Active: Point camera at floor to detect planes, tap to place hazard, walk steps to discover extinguisher!";
            }

            StartCoroutine(TimerCoroutine());
            StartCoroutine(LoadARSceneAdditive());
        }

        public override void OnPageExit()
        {
            StopAllCoroutines();
            timerRunning = false;
            UnloadARScene();
        }

        private IEnumerator LoadARSceneAdditive()
        {
            string arSceneName = "AR Plane Detection Placement";
            if (!SceneManager.GetSceneByName(arSceneName).isLoaded)
            {
                Debug.Log($"[ARSim] Loading actual working AR scene additively: {arSceneName}");
                var op = SceneManager.LoadSceneAsync(arSceneName, LoadSceneMode.Additive);
                while (op != null && !op.isDone) yield return null;
                Debug.Log($"[ARSim] Actual AR scene loaded and active: {arSceneName}");
            }
        }

        private void UnloadARScene()
        {
            string arSceneName = "AR Plane Detection Placement";
            var arSceneObj = SceneManager.GetSceneByName(arSceneName);
            if (arSceneObj.isLoaded)
            {
                SceneManager.UnloadSceneAsync(arSceneName);
                Debug.Log($"[ARSim] Unloaded AR scene: {arSceneName}");
            }
        }

        private IEnumerator TimerCoroutine()
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
    }
}
