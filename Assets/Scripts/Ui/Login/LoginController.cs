using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace MiningSafetyAR.UI.Login
{
    public class LoginController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;
        
        [Header("Buttons & Feedback")]
        [SerializeField] private Button loginButton;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Scene Routing")]
        [SerializeField] private string nextSceneName = "MainMenu"; // Replace with your actual main menu scene name

        private void Start()
        {
            // Hide the error text when the screen first loads
            if (errorText != null) 
            {
                errorText.gameObject.SetActive(false);
                errorText.text = "";
            }

            // Hook up the login button
            if (loginButton != null)
            {
                loginButton.onClick.AddListener(AttemptLogin);
            }
        }

        public void AttemptLogin()
        {
            if (errorText != null) errorText.gameObject.SetActive(false);

            string username = usernameInput != null ? usernameInput.text : "";
            string password = passwordInput != null ? passwordInput.text : "";

            // 1. Basic Validation
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both username and password.");
                return;
            }

            // 2. Mock Authentication (Later, you would connect to a database or API here)
            // For now, as long as they typed something, we let them in!
            
            // 3. Save User Data
            // We save the username so we can display a "Welcome back, [Name]!" message later
            PlayerPrefs.SetString("Username", username);
            PlayerPrefs.Save();

            Debug.Log($"User {username} authenticated successfully. Loading {nextSceneName}...");

            // 4. Transition to next scene
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.LogWarning("LoginController: Next Scene Name is not set!");
            }
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = Color.red; // Make it clearly look like an error
                errorText.gameObject.SetActive(true);
            }
            Debug.LogWarning("Login Error: " + message);
        }
    }
}
