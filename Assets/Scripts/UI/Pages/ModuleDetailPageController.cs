using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;
using MiningSafetyAR.Localization;

namespace MiningSafetyAR.UI.Pages
{
    public class ModuleDetailPageController : PageController
    {
        [SerializeField] VisualTreeAsset scoreBarTemplate;

        string moduleId;
        ModuleData currentModule;

        Label moduleTitle, heroBadge;
        VisualElement heroEmoji;
        Label description, duration, difficulty, domain;
        VisualElement bestScoreCard, competencySection;
        VisualElement objectivesList, scoreBars;
        VisualElement tableBody;
        Button actionBtn, certBtn, backBtn;
        VisualElement feedbackBanner;
        Label feedbackIcon, feedbackText;

        protected override void BindUI()
        {
            moduleTitle = root.Q<Label>("module-title");
            heroEmoji = root.Q<VisualElement>("hero-emoji");
            heroBadge = root.Q<Label>("hero-badge");
            description = root.Q<Label>("description");
            duration = root.Q<Label>("duration");
            difficulty = root.Q<Label>("difficulty");
            domain = root.Q<Label>("domain");
            bestScoreCard = root.Q("best-score-card");
            competencySection = root.Q("competency-section");
            objectivesList = root.Q("objectives-list");
            scoreBars = root.Q("score-bars");
            tableBody = root.Q("table-body");
            actionBtn = root.Q<Button>("action-btn");
            certBtn = root.Q<Button>("cert-btn");
            backBtn = root.Q<Button>("back-btn");
            feedbackBanner = root.Q("feedback-banner");
            feedbackIcon = root.Q<Label>("feedback-icon");
            feedbackText = root.Q<Label>("feedback-text");

            if (scoreBarTemplate == null)
            {
                scoreBarTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Components/ScoreBar");
#if UNITY_EDITOR
                if (scoreBarTemplate == null)
                    scoreBarTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Templates/Components/ScoreBar.uxml");
#endif
            }

            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
            if (actionBtn != null) actionBtn.RegisterCallback<ClickEvent>(e => OnActionClicked());
            if (certBtn != null) certBtn.RegisterCallback<ClickEvent>(e => OnViewCertificate());

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void SetNavigationParameter(object param) => moduleId = param as string;

        public override void OnPageEnter()
        {
            HideFeedback();
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            var app = AppDataService.Instance;
            if (app != null)
            {
                // Use dynamic per-worker progress overlay
                var allWithProgress = app.GetAllModulesWithProgress();
                currentModule = allWithProgress.Find(m => m.id == moduleId);
                if (currentModule == null) currentModule = app.GetModule(moduleId);
            }
            if (currentModule != null) Refresh();
            else Debug.LogWarning($"[ModuleDetail] Module not found: {moduleId}");
        }

        void Refresh()
        {
            var app = AppDataService.Instance;
            var mod = currentModule;
            if (moduleTitle != null) moduleTitle.text = mod.title;
            if (heroEmoji != null)
            {
                IconLoader.ApplyModuleIcon(heroEmoji, mod.id);
            }
            if (heroBadge != null)
            {
                heroBadge.text = mod.status.ToString();
                heroBadge.RemoveFromClassList("badge--completed");
                heroBadge.RemoveFromClassList("badge--in_progress");
                heroBadge.RemoveFromClassList("badge--not_started");
                heroBadge.RemoveFromClassList("badge--locked");
                heroBadge.AddToClassList($"badge--{mod.status.ToString().ToLower()}");
            }
            if (description != null) description.text = mod.description;
            if (duration != null) duration.text = mod.duration;
            if (difficulty != null) difficulty.text = mod.difficulty;
            if (domain != null) domain.text = mod.domain;

            if (bestScoreCard != null)
            {
                bestScoreCard.style.display = DisplayStyle.None;
            }

            if (objectivesList != null)
            {
                objectivesList.Clear();
                if (mod.objectives != null)
                {
                    for (int i = 0; i < mod.objectives.Length; i++)
                    {
                        var row = new Label($"{i + 1}. {mod.objectives[i]}");
                        row.style.fontSize = 12;
                        row.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                        row.style.marginBottom = 6;
                        objectivesList.Add(row);
                    }
                }
            }

            if (competencySection != null && scoreBars != null)
            {
                if (mod.bestScore > 0 && mod.competencyScores != null)
                {
                    competencySection.style.display = DisplayStyle.Flex;
                    scoreBars.Clear();
                    AddScoreBar("Hazard Recognition", mod.competencyScores.hazardRecognition);
                    AddScoreBar("Extinguisher Use", mod.competencyScores.extinguisherUse);
                    AddScoreBar("Time Taken", mod.competencyScores.timeManagement);
                    AddScoreBar("Evacuation", mod.competencyScores.evacuation);
                    AddScoreBar("Quiz Score", mod.competencyScores.quizScore);
                }
                else if (competencySection != null) competencySection.style.display = DisplayStyle.None;
            }

            if (tableBody != null)
            {
                tableBody.Clear();
                var attempts = app != null ? app.GetAttemptsForModule(mod.id) : new List<TrainingResult>();
                if (attempts != null && attempts.Count > 0)
                {
                    int index = 1;
                    foreach (var att in attempts)
                    {
                        var row = new VisualElement();
                        row.AddToClassList("table-row");
                        row.Add(CreateCell(index.ToString()));
                        row.Add(CreateCell(att.timestamp));
                        bool isBest = att.score == mod.bestScore && att.score > 0;
                        string scoreClass = isBest ? "table-cell--accent" : null;
                        string scoreText = isBest ? $"{att.score}% ★ Best" : $"{att.score}%";
                        row.Add(CreateCell(scoreText, scoreClass));
                        row.Add(CreateCell(att.passed ? "Pass" : "Fail", att.passed ? "table-cell--success" : "table-cell--danger"));
                        tableBody.Add(row);
                        index++;
                    }
                }
                else if (mod.bestScore > 0 && !string.IsNullOrEmpty(mod.lastAttempt))
                {
                    var row = new VisualElement();
                    row.AddToClassList("table-row");
                    row.Add(CreateCell("1"));
                    row.Add(CreateCell(mod.lastAttempt));
                    row.Add(CreateCell($"{mod.bestScore}% ★ Best", "table-cell--accent"));
                    row.Add(CreateCell(mod.bestScore >= 60 ? "Pass" : "Fail", mod.bestScore >= 60 ? "table-cell--success" : "table-cell--danger"));
                    tableBody.Add(row);
                }
                else
                {
                    var empty = new Label("No attempts yet");
                    empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                    empty.style.fontSize = 11;
                    tableBody.Add(empty);
                }
            }

            ConfigureActionButton();
        }

        void ConfigureActionButton()
        {
            if (actionBtn == null) return;
            var lockedMsg = root.Q("locked-message");
            switch (currentModule.status)
            {
                case ModuleStatus.Locked:
                    actionBtn.style.display = DisplayStyle.None;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.Flex;
                    break;
                case ModuleStatus.Completed:
                    actionBtn.text = "RETAKE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.Flex;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
                case ModuleStatus.InProgress:
                    actionBtn.text = "CONTINUE TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
                default:
                    actionBtn.text = "START TRAINING";
                    actionBtn.style.display = DisplayStyle.Flex;
                    if (certBtn != null) certBtn.style.display = DisplayStyle.None;
                    if (lockedMsg != null) lockedMsg.style.display = DisplayStyle.None;
                    break;
            }
        }

        void OnActionClicked()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            HideFeedback();

            var worker = AppDataService.Instance != null ? AppDataService.Instance.CurrentWorker : null;
            string uid = worker != null ? worker.firebaseUid : null;

            if (string.IsNullOrEmpty(uid) || FaceVerificationService.Instance == null)
            {
                // No session/service to verify against — this mirrors the app's general
                // offline/degraded-tolerant posture rather than hard-blocking training entirely.
                Debug.LogWarning("[WARN] ModuleDetailPageController Face verification unavailable (no worker/service) — proceeding without it.");
                ProceedToTraining();
                return;
            }

            StartCoroutine(VerifyThenProceed(uid));
        }

        IEnumerator VerifyThenProceed(string uid)
        {
            bool enrollmentChecked = false;
            bool hasEnrollment = false;
            FaceVerificationService.Instance.HasActiveEnrollment(uid, ok => { hasEnrollment = ok; enrollmentChecked = true; });
            yield return new WaitUntil(() => enrollmentChecked);

            if (!hasEnrollment)
            {
                Debug.Log($"[INFO] ModuleDetailPageController No active face enrollment for worker {uid} — routing to consent screen before training.");
                (string, object) dest = ("UI_ModuleDetail", moduleId);
                NavigationManager.Instance.NavigateTo("UI_FaceConsent", dest);
                yield break;
            }

            if (actionBtn != null) { actionBtn.SetEnabled(false); actionBtn.text = "VERIFYING FACE..."; }

            bool verifyDone = false;
            FaceVerificationService.FaceVerificationResult result = null;
            FaceVerificationService.Instance.VerifyFace(uid, r => { result = r; verifyDone = true; });
            yield return new WaitUntil(() => verifyDone);

            if (actionBtn != null) actionBtn.SetEnabled(true);
            ConfigureActionButton(); // restore correct label (START/CONTINUE/RETAKE)

            if (result != null && result.failureReason == "model_version_mismatch_reenrollment_required")
            {
                Debug.Log($"[INFO] ModuleDetailPageController Embedding model version mismatch for worker {uid} — routing to re-enrollment.");
                (string, object) dest = ("UI_ModuleDetail", moduleId);
                NavigationManager.Instance.NavigateTo("UI_FaceConsent", dest);
                yield break;
            }

            if (result != null && result.passed)
            {
                ProceedToTraining();
            }
            else
            {
                Debug.LogWarning($"[WARN] ModuleDetailPageController Face verification failed for worker {uid}: reason={result?.failureReason}, similarity={result?.similarityScore:F3}");
                ShowFailure(result?.failureReason);
            }
        }

        void ProceedToTraining()
        {
            if (AppDataService.Instance != null)
            {
                AppDataService.Instance.RecordAttemptStarted(moduleId);
            }

            if (moduleId.StartsWith("fire_safety", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[ModuleDetail] Starting location capture screen for module '{moduleId}'...");
                TrainingLocationCapture.EnsureInstance();

                if (!TrainingLocationCapture.HasConsentBeenPrompted)
                {
                    TrainingLocationCapture.ShowConsentModal(root, (granted) =>
                    {
                        StartLocationCaptureAndNavigate();
                    });
                }
                else
                {
                    StartLocationCaptureAndNavigate();
                }
            }
            else
            {
                Debug.Log($"[ModuleDetail] Skipping AR simulation for '{moduleId}', proceeding directly to Assessment...");
                NavigationManager.Instance.NavigateTo("UI_Assessment", moduleId);
            }
        }

        void HideFeedback()
        {
            if (feedbackBanner != null) feedbackBanner.style.display = DisplayStyle.None;
        }

        void ShowFailure(string reasonCode)
        {
            if (feedbackBanner == null || feedbackText == null) return;

            var lang = LanguageManager.Instance;
            string Localized(string en, string hi, string sat, string ta) => lang != null ? lang.GetLocalizedText(en, hi, sat, ta) : en;

            string message = reasonCode switch
            {
                "similarity_below_threshold" => Localized(
                    "Face did not match your enrolled profile. Please try again.",
                    "चेहरा आपकी दर्ज प्रोफ़ाइल से मेल नहीं खाया। कृपया फिर से प्रयास करें।",
                    "ᱚᱛᱚᱨ ᱟᱢᱟᱜ ᱚᱞ ᱟᱠᱟᱱ ᱯᱨᱚᱯᱷᱟᱭᱤᱞ ᱥᱟᱶ ᱵᱟᱭ ᱢᱤᱞᱟᱣᱮᱱᱟ। ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "முகம் உங்கள் பதிவு செய்யப்பட்ட சுயவிவரத்துடன் பொருந்தவில்லை. மீண்டும் முயற்சிக்கவும்."),
                "no_face_detected" => Localized(
                    "No face was detected. Make sure your face is clearly visible and try again.",
                    "कोई चेहरा नहीं मिला। सुनिश्चित करें कि आपका चेहरा स्पष्ट रूप से दिख रहा है और फिर से प्रयास करें।",
                    "ᱚᱛᱚᱨ ᱵᱟᱭ ᱮᱛᱟᱠᱟᱱᱟ। ᱟᱢᱟᱜ ᱚᱛᱚᱨ ᱯᱟᱨᱚᱢ ᱧᱮᱞᱚᱜ ᱠᱟᱱᱟ ᱠᱚ ᱡᱟᱶᱤ ᱠᱮᱡᱮ ᱟᱨ ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "முகம் கண்டறியப்படவில்லை. உங்கள் முகம் தெளிவாகத் தெரிகிறதா என்பதை உறுதிசெய்து மீண்டும் முயற்சிக்கவும்."),
                "multiple_faces_detected" => Localized(
                    "More than one face was detected. Make sure you are alone in the frame and try again.",
                    "एक से अधिक चेहरे मिले। सुनिश्चित करें कि फ्रेम में केवल आप हैं और फिर से प्रयास करें।",
                    "ᱢᱤᱫ ᱠᱷᱚᱱ ᱲᱮᱨ ᱚᱛᱚᱨ ᱮᱛᱟᱠᱟᱱᱟ। ᱟᱢ ᱡᱚᱛᱚ ᱢᱤᱫᱴᱟᱹᱺ ᱢᱮᱱᱟᱢᱟ ᱡᱟᱶᱤ ᱠᱮᱡᱮ ᱟᱨ ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "ஒன்றுக்கு மேற்பட்ட முகங்கள் கண்டறியப்பட்டன. நீங்கள் மட்டும் தனியாக இருப்பதை உறுதிசெய்து மீண்டும் முயற்சிக்கவும்."),
                "camera_unavailable" => Localized(
                    "Could not access the camera. Check camera permissions and try again.",
                    "कैमरे तक नहीं पहुंच सका। कैमरा अनुमतियां जांचें और फिर से प्रयास करें।",
                    "ᱠᱮᱢᱨᱟ ᱵᱟᱭ ᱧᱟᱢ ᱞᱮᱱᱟ। ᱠᱮᱢᱨᱟ ᱟᱹᱡᱩᱨ ᱠᱚ ᱦᱮᱨᱮᱡ ᱢᱮ ᱟᱨ ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "கேமராவை அணுக முடியவில்லை. கேமரா அனுமதிகளை சரிபார்த்து மீண்டும் முயற்சிக்கவும்."),
                "not_enrolled" or "models_not_configured" or "anchors_not_configured" => Localized(
                    "Face verification isn't ready yet. Please contact your training administrator.",
                    "चेहरा सत्यापन अभी तैयार नहीं है। कृपया अपने प्रशिक्षण व्यवस्थापक से संपर्क करें।",
                    "ᱚᱛᱚᱨ ᱥᱟᱠᱷᱭᱦᱮᱛ ᱱᱚᱶᱟ ᱛᱟᱦᱮᱸ ᱵᱟᱭ ᱛᱮᱭᱟᱨ ᱦᱩᱭᱮᱱᱟ। ᱟᱢᱟᱜ ᱛᱨᱮᱱᱤᱝ ᱮᱰᱢᱤᱱᱤᱥᱴᱨᱮᱴᱚᱨ ᱥᱟᱶ ᱛᱟᱞᱟᱠᱟᱛᱮ ᱢᱮ।",
                    "முக சரிபார்ப்பு இன்னும் தயாராக இல்லை. உங்கள் பயிற்சி நிர்வாகியை தொடர்பு கொள்ளவும்."),
                _ => Localized(
                    "Face verification failed. Please try again.",
                    "चेहरा सत्यापन विफल रहा। कृपया फिर से प्रयास करें।",
                    "ᱚᱛᱚᱨ ᱥᱟᱠᱷᱭᱦᱮᱛ ᱵᱟᱹᱰᱨᱟᱹᱭ ᱦᱩᱭ ᱮᱱᱟ। ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "முக சரிபார்ப்பு தோல்வியடைந்தது. மீண்டும் முயற்சிக்கவும்."),
            };

            feedbackText.text = message;
            if (feedbackIcon != null) { feedbackIcon.text = "✗"; feedbackIcon.style.color = new StyleColor(new Color(0.77f, 0.15f, 0.15f)); }
            feedbackText.style.color = new StyleColor(new Color(0.77f, 0.15f, 0.15f));
            feedbackBanner.style.backgroundColor = new StyleColor(new Color(0.98f, 0.85f, 0.85f));
            feedbackBanner.style.display = DisplayStyle.Flex;
        }

        void StartLocationCaptureAndNavigate()
        {
            NavigationManager.Instance.NavigateTo("UI_LocationCapture", moduleId);
        }

        void OnViewCertificate() => NavigationManager.Instance.NavigateTo("UI_Certificate", !string.IsNullOrEmpty(currentModule.certificateId) ? currentModule.certificateId : currentModule.id);

        Label CreateCell(string text, string cls = null)
        {
            var cell = new Label(text);
            cell.AddToClassList("table-cell");
            if (!string.IsNullOrEmpty(cls)) cell.AddToClassList(cls);
            return cell;
        }

        void AddScoreBar(string label, int value)
        {
            if (scoreBarTemplate == null || scoreBars == null) return;
            var bar = scoreBarTemplate.Instantiate();
            // bar root is the score-bar VisualElement
            var inner = bar.Q("score-bar");
            var target = inner != null ? inner : bar;
            ScoreBarHelper.Configure(target, label, value);
            scoreBars.Add(bar);
        }
    }
}
