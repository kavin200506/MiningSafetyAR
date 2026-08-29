using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiningSafetyAR.UI.MainMenu
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Header UI")]
        [SerializeField] private TextMeshProUGUI welcomeText;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button settingsButton;
        
        [Header("Main Menu Buttons")]
        [SerializeField] private Button startTrainingButton;
        [SerializeField] private Button modulesButton;
        [SerializeField] private Button progressButton;
        [SerializeField] private Button assessmentButton;
        [SerializeField] private Button certificateButton;

        [Header("Scene Routing")]
        [SerializeField] private string startTrainingSceneName = "SampleScene"; // Replace with your AR scene name
        [SerializeField] private string modulesSceneName = "Modules";
        [SerializeField] private string loginSceneName = "Login";

        private void Start()
        {
            // 1. Personalize the menu using the data we saved during Login!
            string username = PlayerPrefs.GetString("Username", "Miner");
            
            if (welcomeText != null)
            {
                welcomeText.text = $"Welcome Back, {username}!";
            }

            // 2. Hook up the buttons
            if (startTrainingButton != null) 
            {
                startTrainingButton.onClick.AddListener(() => LoadScene(startTrainingSceneName));
            }
            
            // For now, we will just make the Profile button act as the Logout button for testing
            if (profileButton != null) 
            {
                profileButton.onClick.AddListener(Logout);
            }

            // Hook up placeholder listeners for the rest of the buttons
            if (settingsButton != null) settingsButton.onClick.AddListener(() => Debug.Log("Settings clicked!"));
            if (modulesButton != null) modulesButton.onClick.AddListener(() => LoadScene(modulesSceneName));
            if (progressButton != null) progressButton.onClick.AddListener(() => Debug.Log("Progress clicked!"));
            if (assessmentButton != null) assessmentButton.onClick.AddListener(() => Debug.Log("Assessment clicked!"));
            if (certificateButton != null) certificateButton.onClick.AddListener(() => Debug.Log("Certificate clicked!"));
        }

        private void LoadScene(string sceneName)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                Debug.Log($"Loading Scene: {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning("Scene name is empty! Cannot load.");
            }
        }

        private void Logout()
        {
            Debug.Log("Logging out...");
            // Clear the saved user session
            PlayerPrefs.DeleteKey("Username");
            PlayerPrefs.Save();

            // Send them back to the login screen
            LoadScene(loginSceneName);
        }
    }
}
