using System;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.Localization;

namespace MiningSafetyAR.UI.Pages
{
    /// <summary>
    /// Standalone consent screen shown before a worker's first face enrollment. Explains, in the
    /// worker's selected language, exactly what is captured (a mathematical face embedding, never a
    /// photo), why (preventing proxy/impersonated training completion), and how to withdraw consent
    /// later from Settings. Only after AcceptConsent() records consent does anything call
    /// FaceVerificationService.EnrollFace().
    /// </summary>
    public class FaceConsentPageController : PageController
    {
        Label titleLabel, bodyLabel, withdrawNoteLabel, captureStatusLabel, feedbackIcon, feedbackText;
        VisualElement feedbackBanner;
        Button acceptBtn, retryBtn, declineBtn;

        // Where to navigate once consent + enrollment both succeed, and what navigation parameter (if
        // any) that destination scene needs (e.g. ModuleDetail needs its moduleId back). Callers pass
        // either a plain scene-name string (no parameter needed on arrival) or a (scene, param) tuple.
        string onAcceptScene = "UI_Dashboard";
        object onAcceptParam;

        protected override void BindUI()
        {
            titleLabel = root.Q<Label>("consent-title");
            bodyLabel = root.Q<Label>("consent-body");
            withdrawNoteLabel = root.Q<Label>("consent-withdraw-note");
            captureStatusLabel = root.Q<Label>("capture-status");
            feedbackBanner = root.Q("feedback-banner");
            feedbackIcon = root.Q<Label>("feedback-icon");
            feedbackText = root.Q<Label>("feedback-text");
            acceptBtn = root.Q<Button>("consent-accept-btn");
            retryBtn = root.Q<Button>("consent-retry-btn");
            declineBtn = root.Q<Button>("consent-decline-btn");

            if (acceptBtn != null) acceptBtn.RegisterCallback<ClickEvent>(e => OnAccept());
            if (retryBtn != null) retryBtn.RegisterCallback<ClickEvent>(e => OnRetryEnrollment());
            if (declineBtn != null) declineBtn.RegisterCallback<ClickEvent>(e => OnDecline());
        }

        public override void SetNavigationParameter(object param)
        {
            if (param is ValueTuple<string, object> sceneAndParam && !string.IsNullOrEmpty(sceneAndParam.Item1))
            {
                onAcceptScene = sceneAndParam.Item1;
                onAcceptParam = sceneAndParam.Item2;
            }
            else if (param is string scene && !string.IsNullOrEmpty(scene))
            {
                onAcceptScene = scene;
                onAcceptParam = null;
            }
        }

        public override void OnPageEnter()
        {
            ResetToInitialState();
            var lang = LanguageManager.Instance;

            if (titleLabel != null)
            {
                titleLabel.text = lang != null
                    ? lang.GetLocalizedText(
                        "Face Verification Consent",
                        "चेहरा सत्यापन सहमति",
                        "ᱚᱛᱚᱨ ᱥᱟᱠᱷᱭᱦᱮᱛᱮᱨ ᱥᱚᱝᱜᱚᱛ",
                        "முக சரிபார்ப்பு ஒப்புதல்")
                    : "Face Verification Consent";
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = lang != null
                    ? lang.GetLocalizedText(
                        "This app can verify it is really you before you complete training, so no one else can finish it on your behalf. It does this by capturing a mathematical face embedding — a set of numbers describing your face shape, not a photo. No image ever leaves your device. You can withdraw this anytime from Settings.",
                        "यह ऐप प्रशिक्षण पूरा करने से पहले यह सत्यापित कर सकता है कि यह वास्तव में आप ही हैं, ताकि कोई और आपकी जगह इसे पूरा न कर सके। यह आपके चेहरे का एक गणितीय एम्बेडिंग कैप्चर करके किया जाता है — आपकी तस्वीर नहीं। कोई भी तस्वीर आपके डिवाइस से बाहर नहीं जाती। आप इसे सेटिंग्स से कभी भी वापस ले सकते हैं।",
                        "ᱱᱚᱶᱟ ᱮᱯ ᱟᱢ ᱛᱟᱨᱤᱚᱛ ᱛᱟᱦᱮᱸᱱ ᱠᱟᱱᱟᱢ ᱠᱚ ᱥᱟᱠᱷᱭᱦᱮᱛ ᱫᱟᱲᱮᱭᱟᱜᱼᱟ, ᱟᱨ ᱮᱴᱟᱜ ᱦᱚᱲ ᱟᱢᱟᱜ ᱴᱷᱟᱶ ᱮᱢ ᱵᱟᱭ ᱥᱟᱹᱫᱷᱚᱨᱮᱭᱟᱜᱼᱟ ᱟᱢᱟᱜ ᱟᱠᱚᱲ ᱨᱮᱭᱟᱜ ᱦᱤᱥᱟᱹᱵᱽ ᱠᱚ ᱮᱢ ᱠᱟᱛᱮ, ᱪᱤᱛᱟᱹᱨ ᱵᱟᱝ। ᱡᱟᱦᱟᱸ ᱡᱟᱦᱟᱸ ᱥᱮᱴᱤᱝᱥ ᱠᱷᱚᱱ ᱩᱴᱟᱹᱲ ᱫᱟᱲᱮᱭᱟᱜᱼᱟᱢ।",
                        "பயிற்சியை நிறைவு செய்வதற்கு முன் இது நீங்கள்தான் என்பதை இந்த ஆப் உறுதி செய்யும், இதனால் வேறு யாரும் உங்களுக்குப் பதிலாக அதை முடிக்க முடியாது. இது உங்கள் முகத்தின் கணித எம்பெடிங்கை (எண்களின் தொகுப்பு) மட்டுமே பதிவு செய்யும் — புகைப்படம் அல்ல. எந்த படமும் உங்கள் சாதனத்தை விட்டு வெளியேறாது. அமைப்புகளில் இருந்து இதை எப்போது வேண்டுமானாலும் திரும்பப் பெறலாம்.")
                    : "This app captures a mathematical face embedding (not a photo) to confirm you are the one completing training. No image ever leaves your device.";
            }

            if (withdrawNoteLabel != null)
            {
                withdrawNoteLabel.text = lang != null
                    ? lang.GetLocalizedText(
                        "You can withdraw this consent anytime from Settings > Withdraw Face Data Consent.",
                        "आप सेटिंग्स > चेहरा डेटा सहमति वापस लें से इसे कभी भी वापस ले सकते हैं।",
                        "ᱟᱢ ᱥᱮᱴᱤᱝᱥ > ᱚᱛᱚᱨ ᱰᱟᱴᱟ ᱥᱚᱝᱜᱚᱛ ᱩᱴᱟᱹᱲ ᱠᱷᱚᱱ ᱡᱟᱦᱟᱸ ᱡᱟᱦᱟᱸ ᱫᱟᱲᱮᱭᱟᱜᱼᱟᱢ।",
                        "அமைப்புகள் > முக தரவு ஒப்புதலை திரும்பப் பெறு என்பதன் மூலம் எப்போது வேண்டுமானாலும் இதை திரும்பப் பெறலாம்.")
                    : "You can withdraw this consent anytime from Settings.";
            }
        }

        void ResetToInitialState()
        {
            if (acceptBtn != null) { acceptBtn.style.display = DisplayStyle.Flex; acceptBtn.SetEnabled(true); }
            if (retryBtn != null) retryBtn.style.display = DisplayStyle.None;
            if (captureStatusLabel != null) captureStatusLabel.style.display = DisplayStyle.None;
            if (feedbackBanner != null) feedbackBanner.style.display = DisplayStyle.None;
            if (declineBtn != null) declineBtn.SetEnabled(true);
        }

        void OnAccept()
        {
            string uid = CurrentUid();
            if (string.IsNullOrEmpty(uid) || FaceVerificationService.Instance == null)
            {
                Debug.LogWarning("[WARN] FaceConsentPageController Accept pressed but no worker/session is available.");
                NavigationManager.Instance.GoBack();
                return;
            }

            string languageName = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage.ToString() : "English";
            if (acceptBtn != null) { acceptBtn.style.display = DisplayStyle.None; }
            if (feedbackBanner != null) feedbackBanner.style.display = DisplayStyle.None;
            ShowCaptureStatus(Localized(
                "Recording your consent...", "आपकी सहमति दर्ज हो रही है...",
                "ᱟᱢᱟᱜ ᱥᱚᱝᱜᱚᱛ ᱚᱞ ᱦᱩᱭᱩᱜ ᱠᱟᱱᱟ...", "உங்கள் ஒப்புதல் பதிவு செய்யப்படுகிறது..."));

            FaceVerificationService.Instance.RecordConsent(uid, languageName, (ok, resp) =>
            {
                if (!ok)
                {
                    Debug.LogWarning($"[WARN] FaceConsentPageController Failed to record consent: {resp}");
                    ShowFailure("consent_failed");
                    return;
                }
                StartEnrollment(uid);
            });
        }

        void StartEnrollment(string uid)
        {
            ShowCaptureStatus(Localized(
                "Look directly at the camera to register your face...",
                "अपना चेहरा दर्ज करने के लिए सीधे कैमरे की ओर देखें...",
                "ᱟᱢᱟᱜ ᱚᱛᱚᱨ ᱨᱤᱡᱤᱥᱴᱚᱨ ᱞᱟᱹᱜᱤᱛ ᱠᱮᱢᱨᱟ ᱥᱮᱛᱮᱨ ᱫᱟᱲᱮᱠᱟᱛᱮ ᱧᱮᱞᱢᱮ...",
                "உங்கள் முகத்தை பதிவு செய்ய நேரடியாக கேமராவைப் பாருங்கள்..."));

            FaceVerificationService.Instance.EnrollFace(uid, (ok, resp) =>
            {
                if (ok)
                {
                    Debug.Log("[INFO] FaceConsentPageController Enrollment succeeded, proceeding.");
                    NavigationManager.Instance.NavigateTo(onAcceptScene, onAcceptParam);
                }
                else
                {
                    Debug.LogWarning($"[WARN] FaceConsentPageController Enrollment failed: {resp}");
                    ShowFailure(resp);
                }
            });
        }

        void OnRetryEnrollment()
        {
            string uid = CurrentUid();
            if (string.IsNullOrEmpty(uid) || FaceVerificationService.Instance == null)
            {
                NavigationManager.Instance.GoBack();
                return;
            }
            if (retryBtn != null) retryBtn.style.display = DisplayStyle.None;
            if (feedbackBanner != null) feedbackBanner.style.display = DisplayStyle.None;
            StartEnrollment(uid);
        }

        void ShowCaptureStatus(string text)
        {
            if (captureStatusLabel == null) return;
            captureStatusLabel.text = text;
            captureStatusLabel.style.display = DisplayStyle.Flex;
        }

        void ShowFailure(string reasonCode)
        {
            if (captureStatusLabel != null) captureStatusLabel.style.display = DisplayStyle.None;
            if (acceptBtn != null) acceptBtn.style.display = DisplayStyle.None;
            if (retryBtn != null) retryBtn.style.display = DisplayStyle.Flex;
            if (feedbackBanner == null || feedbackText == null) return;

            string message = reasonCode switch
            {
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
                "models_not_configured" or "anchors_not_configured" => Localized(
                    "Face verification isn't set up on this device yet. Please contact your training administrator.",
                    "इस डिवाइस पर चेहरा सत्यापन अभी सेट नहीं है। कृपया अपने प्रशिक्षण व्यवस्थापक से संपर्क करें।",
                    "ᱱᱚᱶᱟ ᱰᱮᱵᱷᱟᱭᱤᱥ ᱨᱮ ᱚᱛᱚᱨ ᱥᱟᱠᱷᱭᱦᱮᱛ ᱱᱚᱶᱟ ᱛᱟᱦᱮᱸ ᱵᱟᱭ ᱴᱷᱤᱠ ᱦᱩᱭᱮᱱᱟ। ᱟᱢᱟᱜ ᱛᱨᱮᱱᱤᱝ ᱮᱰᱢᱤᱱᱤᱥᱴᱨᱮᱴᱚᱨ ᱥᱟᱶ ᱛᱟᱞᱟᱠᱟᱛᱮ ᱢᱮ।",
                    "இந்த சாதனத்தில் முக சரிபார்ப்பு இன்னும் அமைக்கப்படவில்லை. உங்கள் பயிற்சி நிர்வாகியை தொடர்பு கொள்ளவும்."),
                _ => Localized(
                    "Something went wrong while registering your face. Please try again.",
                    "आपका चेहरा दर्ज करते समय कुछ गड़बड़ हुई। कृपया फिर से प्रयास करें।",
                    "ᱟᱢᱟᱜ ᱚᱛᱚᱨ ᱚᱞ ᱮᱦᱚᱵ ᱡᱚᱛᱚ ᱛᱤᱸᱜᱩ ᱦᱩᱭ ᱮᱱᱟ। ᱫᱩᱦᱲᱟᱹ ᱪᱮᱥᱴᱟ ᱢᱮ।",
                    "உங்கள் முகத்தை பதிவு செய்யும் போது ஏதோ தவறு ஏற்பட்டது. மீண்டும் முயற்சிக்கவும்."),
            };

            feedbackText.text = message;
            if (feedbackIcon != null) feedbackIcon.text = "✗";
            feedbackBanner.style.backgroundColor = new StyleColor(new Color(0.98f, 0.85f, 0.85f));
            if (feedbackIcon != null) feedbackIcon.style.color = new StyleColor(new Color(0.77f, 0.15f, 0.15f));
            feedbackText.style.color = new StyleColor(new Color(0.77f, 0.15f, 0.15f));
            feedbackBanner.style.display = DisplayStyle.Flex;
        }

        string Localized(string en, string hi, string sat, string ta)
        {
            var lang = LanguageManager.Instance;
            return lang != null ? lang.GetLocalizedText(en, hi, sat, ta) : en;
        }

        string CurrentUid()
        {
            var worker = AppDataService.Instance != null ? AppDataService.Instance.CurrentWorker : null;
            return worker != null ? worker.firebaseUid : null;
        }

        void OnDecline()
        {
            Debug.Log("[INFO] FaceConsentPageController Worker declined face verification consent/enrollment.");
            NavigationManager.Instance.GoBack();
        }
    }
}
