using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiningSafetyAR.UI.Modules
{
    public class ModulesController : MonoBehaviour
    {
        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        [Header("Available Modules")]
        [SerializeField] private Button fireSafetyButton;
        [SerializeField] private Button hazardIDButton;
        [SerializeField] private Button equipmentCheckButton;
        [SerializeField] private Button emergencyEvacButton;

        [Header("AR Scene Routing (For Later)")]
        [SerializeField] private string fireSafetySceneName = "AR_FireSafety";
        [SerializeField] private string hazardIDSceneName = "AR_HazardID";
        [SerializeField] private string equipmentCheckSceneName = "AR_EquipmentCheck";
        [SerializeField] private string emergencyEvacSceneName = "AR_EmergencyEvac";

        private void Start()
        {
            // Back to Main Menu
            if (backButton != null) 
            {
                backButton.onClick.AddListener(GoBackToMainMenu);
            }

            // Hook up the module selection buttons
            if (fireSafetyButton != null) fireSafetyButton.onClick.AddListener(() => LoadModule(fireSafetySceneName));
            if (hazardIDButton != null) hazardIDButton.onClick.AddListener(() => LoadModule(hazardIDSceneName));
            if (equipmentCheckButton != null) equipmentCheckButton.onClick.AddListener(() => LoadModule(equipmentCheckSceneName));
            if (emergencyEvacButton != null) emergencyEvacButton.onClick.AddListener(() => LoadModule(emergencyEvacSceneName));
        }

        private void LoadModule(string sceneName)
        {
            Debug.Log($"[ModulesController] User selected module to load: {sceneName}");
            
            // Note: We are NOT calling SceneManager.LoadScene() here yet!
            // Since we haven't built these AR scenes yet, clicking the buttons will just 
            // print a message to the Console so you know it works, without crashing the app.
        }

        private void GoBackToMainMenu()
        {
            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
    }
}
