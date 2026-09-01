using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class SettingsPageController : PageController
    {
        Label profileName, profileId, profileOrg;
        ToggleSwitchController soundToggle, voiceToggle, locationToggle;
        Button logoutBtn;
        Button langEn, langHi, langSat;

        protected override void BindUI()
        {
            profileName = root.Q<Label>("profile-name");
            profileId = root.Q<Label>("profile-id");
            profileOrg = root.Q<Label>("profile-org");
            logoutBtn = root.Q<Button>("logout-btn");
            langEn = root.Q<Button>("lang-en");
            langHi = root.Q<Button>("lang-hi");
            langSat = root.Q<Button>("lang-sat");

            var soundEl = root.Q("toggle-sound");
            var voiceEl = root.Q("toggle-voice");
            var locationEl = root.Q("toggle-location") ?? root.Q("toggle-geo");
            if (soundEl != null) soundToggle = new ToggleSwitchController(soundEl);
            if (voiceEl != null) voiceToggle = new ToggleSwitchController(voiceEl);
            if (locationEl != null) locationToggle = new ToggleSwitchController(locationEl);

            if (logoutBtn != null) logoutBtn.RegisterCallback<ClickEvent>(e => OnLogout());
            if (langEn != null) langEn.RegisterCallback<ClickEvent>(e => SetLanguage("English", langEn));
            if (langHi != null) langHi.RegisterCallback<ClickEvent>(e => SetLanguage("Hindi", langHi));
            if (langSat != null) langSat.RegisterCallback<ClickEvent>(e => SetLanguage("Santali", langSat));

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void OnPageEnter()
        {
            var worker = AppDataService.Instance != null ? AppDataService.Instance.CurrentWorker : null;
            if (worker != null)
            {
                if (profileName != null) profileName.text = worker.name;
                if (profileId != null) profileId.text = worker.id;
                if (profileOrg != null) profileOrg.text = worker.organization;
            }
            string currentLang = PlayerPrefs.GetString("SelectedLanguage", "English");
            SetLanguageUI(currentLang);
            if (soundToggle != null) soundToggle.SetValue(PlayerPrefs.GetInt("SoundEnabled", 1) == 1);
            if (voiceToggle != null) voiceToggle.SetValue(PlayerPrefs.GetInt("VoiceEnabled", 1) == 1);
            if (locationToggle != null) locationToggle.SetValue(TrainingLocationCapture.HasUserConsented);
            if (soundToggle != null) soundToggle.OnToggled += on => PlayerPrefs.SetInt("SoundEnabled", on ? 1 : 0);
            if (voiceToggle != null) voiceToggle.OnToggled += on => PlayerPrefs.SetInt("VoiceEnabled", on ? 1 : 0);
            if (locationToggle != null) locationToggle.OnToggled += on => TrainingLocationCapture.HasUserConsented = on;
        }

        void OnLogout()
        {
            if (AppDataService.Instance != null) AppDataService.Instance.Logout();
            else if (Firebase.FirebaseAuthManager.Instance != null) Firebase.FirebaseAuthManager.Instance.Logout();
            PlayerPrefs.DeleteKey("SelectedLanguage");
            NavigationManager.Instance.NavigateToRoot("UI_Login");
        }

        void SetLanguage(string lang, Button activeBtn)
        {
            PlayerPrefs.SetString("SelectedLanguage", lang);
            PlayerPrefs.Save();
            SetLanguageUI(lang);
        }

        void SetLanguageUI(string lang)
        {
            if (langEn != null) { langEn.RemoveFromClassList("lang-btn--active"); langEn.style.backgroundColor = new StyleColor(new Color(0.96f,0.96f,0.96f)); langEn.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f)); }
            if (langHi != null) { langHi.RemoveFromClassList("lang-btn--active"); langHi.style.backgroundColor = new StyleColor(new Color(0.96f,0.96f,0.96f)); langHi.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f)); }
            if (langSat != null) { langSat.RemoveFromClassList("lang-btn--active"); langSat.style.backgroundColor = new StyleColor(new Color(0.96f,0.96f,0.96f)); langSat.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f)); }
            Button target = lang == "Hindi" ? langHi : lang == "Santali" ? langSat : langEn;
            if (target != null) { target.AddToClassList("lang-btn--active"); target.style.backgroundColor = new StyleColor(new Color(1f,0.42f,0f)); target.style.color = new StyleColor(Color.white); }
        }
    }
}
