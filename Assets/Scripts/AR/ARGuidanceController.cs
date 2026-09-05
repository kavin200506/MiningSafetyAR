using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// AR Guidance Controller — handles the onboarding tutorial flow
    /// (3-tip vignette + mission briefing) and on-demand contextual hints
    /// via the lightbulb (💡) button.
    /// </summary>
    public class ARGuidanceController : MonoBehaviour
    {
        public static ARGuidanceController Instance { get; private set; }

        [Header("Tutorial UI Elements")]
        [SerializeField] private VisualElement vignetteOverlay;
        [SerializeField] private VisualElement tutorialCallout;
        [SerializeField] private VisualElement missionModal;

        [Header("Hint Configuration")]

        private string[] tutorialTips = new string[]
        {
            "Need help to progress? Just tap this light bulb icon then follow the blue arrow.",
            "Point your camera at the floor to detect surfaces for placing the fire hazard.",
            "Walk around the room to find the fire extinguisher after the fire starts."
        };

        // Re-indexed to match FireSafetyModuleManager's 4 real steps (Sound Alarm / Select
        // Extinguisher are no longer separately tracked steps — see documents/
        // technical_scoring_explained.md §3.2 and §6.2).
        private string[] contextualHints = new string[]
        {
            "Pull the safety pin on the extinguisher handle to unlock mechanism.",
            "Aim the nozzle at the base of the fire and test spray.",
            "Squeeze handle firmly and sweep nozzle side-to-side across base of flames.",
            "Move to the safe distance shown by the arrow to complete evacuation."
        };

        private int currentTipIndex = 0;
        private bool tutorialCompleted = false;
        private bool missionShown = false;

        private Button btnCloseTip;
        private Button btnSkipTutorial;
        private Button btnStartMission;
        private Label tipLabel;
        private Label tipText;
        private Label missionText;

        public bool IsTutorialCompleted => tutorialCompleted;
        public bool IsMissionShown => missionShown;

        public event Action OnTutorialCompleted;
        public event Action OnMissionStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Bind UI elements from the AR overlay root. Call from ARSimulationPageController.BindUI().
        /// </summary>
        public void BindToUIDocument(VisualElement root)
        {
            if (root == null) return;

            vignetteOverlay = root.Q<VisualElement>("tutorial-vignette");
            tutorialCallout = root.Q<VisualElement>("tutorial-callout");
            missionModal = root.Q<VisualElement>("mission-modal");

            btnCloseTip = root.Q<Button>("btn-close-tip");
            btnSkipTutorial = root.Q<Button>("btn-skip-tutorial");
            btnStartMission = root.Q<Button>("btn-start-mission");
            tipLabel = root.Q<Label>("tip-label");
            tipText = root.Q<Label>("tip-text");
            missionText = root.Q<Label>("mission-text");

            if (btnCloseTip != null) btnCloseTip.clicked += ShowNextTip;
            if (btnSkipTutorial != null) btnSkipTutorial.clicked += SkipTutorial;
            if (btnStartMission != null) btnStartMission.clicked += StartDrill;

            HideAll();
        }

        /// <summary>
        /// Start the 3-tip tutorial flow with vignette overlay.
        /// </summary>
        public void StartTutorial()
        {
            if (tutorialCompleted)
            {
                Debug.Log("[ARGuidanceController] Tutorial already completed. Skipping.");
                OnTutorialCompleted?.Invoke();
                return;
            }

            currentTipIndex = 0;
            ShowVignette(true);
            ShowTip(currentTipIndex);
            Debug.Log("[ARGuidanceController] Tutorial started — TIP 1/3");
        }

        /// <summary>
        /// Get contextual hint text for the given step index (0-4).
        /// </summary>
        public string GetContextualHint(int stepIndex)
        {
            if (stepIndex >= 0 && stepIndex < contextualHints.Length)
            {
                return contextualHints[stepIndex];
            }
            return "Follow emergency evacuation arrows to safe assembly zone.";
        }

        private int cachedStepIndex = 0;

        /// <summary>
        /// Set the current drill step index from external source (ARSimulationPageController).
        /// </summary>
        public void SetCurrentStepIndex(int stepIndex)
        {
            cachedStepIndex = stepIndex;
        }

        /// <summary>
        /// Show or hide the hint modal (called by 💡 button).
        /// </summary>
        public void ShowHintModal()
        {
            if (tutorialCallout == null) return;

            if (tutorialCallout.style.display == DisplayStyle.Flex)
            {
                tutorialCallout.style.display = DisplayStyle.None;
            }
            else
            {
                ShowHintForStep(cachedStepIndex);
            }
        }

        private void ShowVignette(bool show)
        {
            if (vignetteOverlay != null)
            {
                vignetteOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ShowTip(int index)
        {
            if (index >= tutorialTips.Length)
            {
                CompleteTutorial();
                return;
            }

            if (tutorialCallout != null)
            {
                tutorialCallout.style.display = DisplayStyle.Flex;
            }
            if (tipLabel != null)
            {
                tipLabel.text = $"TIP {index + 1}/{tutorialTips.Length}";
            }
            if (tipText != null)
            {
                tipText.text = tutorialTips[index];
            }
        }

        private void ShowNextTip()
        {
            currentTipIndex++;
            if (currentTipIndex >= tutorialTips.Length)
            {
                CompleteTutorial();
            }
            else
            {
                ShowTip(currentTipIndex);
                Debug.Log($"[ARGuidanceController] Tutorial TIP {currentTipIndex + 1}/{tutorialTips.Length}");
            }
        }

        private void SkipTutorial()
        {
            Debug.Log("[ARGuidanceController] Tutorial SKIPPED by user.");
            CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            tutorialCompleted = true;
            ShowVignette(false);
            if (tutorialCallout != null) tutorialCallout.style.display = DisplayStyle.None;

            ShowMissionBriefing();
            OnTutorialCompleted?.Invoke();
        }

        private void ShowMissionBriefing()
        {
            if (missionModal != null)
            {
                missionModal.style.display = DisplayStyle.Flex;
            }
            if (missionText != null)
            {
                missionText.text = "A fire just started in a trash can! What should you do?";
            }
            missionShown = true;
            Debug.Log("[ARGuidanceController] Mission briefing displayed.");
        }

        private void StartDrill()
        {
            if (missionModal != null) missionModal.style.display = DisplayStyle.None;
            Debug.Log("[ARGuidanceController] Mission STARTED — drill begins!");
            OnMissionStarted?.Invoke();
        }

        private void ShowHintForStep(int stepIndex)
        {
            string hint = GetContextualHint(stepIndex);

            if (tipLabel != null) tipLabel.text = "HINT";
            if (tipText != null) tipText.text = hint;
            if (tutorialCallout != null) tutorialCallout.style.display = DisplayStyle.Flex;
        }

        private void HideAll()
        {
            if (vignetteOverlay != null) vignetteOverlay.style.display = DisplayStyle.None;
            if (tutorialCallout != null) tutorialCallout.style.display = DisplayStyle.None;
            if (missionModal != null) missionModal.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            if (btnCloseTip != null) btnCloseTip.clicked -= ShowNextTip;
            if (btnSkipTutorial != null) btnSkipTutorial.clicked -= SkipTutorial;
            if (btnStartMission != null) btnStartMission.clicked -= StartDrill;
        }

        [ContextMenu("Test Start Tutorial Flow")]
        public void TestStartTutorialFlow()
        {
            tutorialCompleted = false;
            StartTutorial();
        }

        [ContextMenu("Test Show Contextual Hint")]
        public void TestShowContextualHint()
        {
            ShowHintForStep(cachedStepIndex);
        }

        [ContextMenu("Test Show Mission Briefing")]
        public void TestShowMissionBriefing()
        {
            ShowMissionBriefing();
        }
    }
}
