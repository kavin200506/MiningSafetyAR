using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiningSafetyAR.UI.Language
{
    public class LanguageController : MonoBehaviour
    {
        public enum SupportedLanguage
        {
            None,
            English,
            Hindi,
            Santali,
            Tamil
        }

        [Header("Language Buttons")]
        [SerializeField] private Button englishButton;
        [SerializeField] private Button hindiButton;
        [SerializeField] private Button santaliButton;
        [SerializeField] private Button tamilButton;
        [SerializeField] private Button continueButton;

        [Header("Selected State Visuals (e.g., Checkmarks)")]
        [SerializeField] private GameObject englishSelectedVisual;
        [SerializeField] private GameObject hindiSelectedVisual;
        [SerializeField] private GameObject santaliSelectedVisual;
        [SerializeField] private GameObject tamilSelectedVisual;

        [Header("Scene Routing")]
        [SerializeField] private string loginSceneName = "Login";

        private SupportedLanguage currentSelectedLanguage = SupportedLanguage.None;

        private void Start()
        {
            // Assign button click listeners
            if (englishButton != null) englishButton.onClick.AddListener(() => SelectLanguage(SupportedLanguage.English));
            if (hindiButton != null) hindiButton.onClick.AddListener(() => SelectLanguage(SupportedLanguage.Hindi));
            if (santaliButton != null) santaliButton.onClick.AddListener(() => SelectLanguage(SupportedLanguage.Santali));
            if (tamilButton != null) tamilButton.onClick.AddListener(() => SelectLanguage(SupportedLanguage.Tamil));
            
            if (continueButton != null) 
            {
                continueButton.onClick.AddListener(OnContinuePressed);
                // Disable continue button until a language is chosen
                continueButton.interactable = false;
            }

            // Reset visuals to default state
            UpdateVisuals();
        }

        public void SelectLanguage(SupportedLanguage lang)
        {
            currentSelectedLanguage = lang;
            
            // Enable the continue button now that a selection has been made
            if (continueButton != null)
            {
                continueButton.interactable = true;
            }

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            // Activate the visual for the selected language, deactivate the others
            if (englishSelectedVisual != null) englishSelectedVisual.SetActive(currentSelectedLanguage == SupportedLanguage.English);
            if (hindiSelectedVisual != null) hindiSelectedVisual.SetActive(currentSelectedLanguage == SupportedLanguage.Hindi);
            if (santaliSelectedVisual != null) santaliSelectedVisual.SetActive(currentSelectedLanguage == SupportedLanguage.Santali);
            if (tamilSelectedVisual != null) tamilSelectedVisual.SetActive(currentSelectedLanguage == SupportedLanguage.Tamil);
        }

        private void OnContinuePressed()
        {
            if (currentSelectedLanguage == SupportedLanguage.None)
            {
                Debug.LogWarning("No language selected!");
                return;
            }

            // Save the choice so we don't have to ask every time
            string langStr = currentSelectedLanguage.ToString();
            PlayerPrefs.SetString("SelectedLanguage", langStr);
            PlayerPrefs.Save();

            if (MiningSafetyAR.Localization.LanguageManager.Instance != null)
            {
                MiningSafetyAR.Localization.LanguageManager.Instance.SetLanguage(langStr);
            }

            Debug.Log($"Language saved: {currentSelectedLanguage}. Loading scene: {loginSceneName}");

            // Load the next scene (Login)
            if (!string.IsNullOrEmpty(loginSceneName))
            {
                SceneManager.LoadScene(loginSceneName);
            }
        }
    }
}
