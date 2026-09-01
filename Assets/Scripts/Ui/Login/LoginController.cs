using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.UI.Login
{
    public class LoginController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput; // Worker ID
        [SerializeField] private TMP_InputField passwordInput; // PIN
        
        [Header("Buttons & Feedback")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button forgotPasswordButton;
        [SerializeField] private Button createAccountButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Scene Routing")]
        [SerializeField] private string nextSceneName = "MainMenu";

        private bool isFirebaseInitialized = false;

        private void Start()
        {
            HideError();
            DisableButtons(); // Prevent clicks until Firebase is ready

            // Hook up initialization via FirebaseAuthManager instead of native FirebaseApp
            if (FirebaseAuthManager.Instance != null)
            {
                if (FirebaseAuthManager.Instance.IsInitialized) Initialize();
                else FirebaseAuthManager.Instance.OnInitSuccess += Initialize;
            }
            else Invoke(nameof(RetryInit), 0.5f);
        }

        private void RetryInit()
        {
            if (FirebaseAuthManager.Instance != null)
            {
                if (FirebaseAuthManager.Instance.IsInitialized) Initialize();
                else FirebaseAuthManager.Instance.OnInitSuccess += Initialize;
            }
            else Invoke(nameof(RetryInit), 0.5f);
        }

        private void Initialize()
        {
            if (FirebaseAuthManager.Instance != null)
                FirebaseAuthManager.Instance.OnInitSuccess -= Initialize;

            isFirebaseInitialized = true;

            // Hook up the buttons now that we're ready
            if (loginButton != null) loginButton.onClick.AddListener(AttemptLogin);
            if (forgotPasswordButton != null) forgotPasswordButton.onClick.AddListener(ForgotPassword);
            if (createAccountButton != null) createAccountButton.onClick.AddListener(CreateAccount);
            if (guestButton != null) guestButton.onClick.AddListener(ContinueAsGuest);
            
            EnableButtons();

            if (PlayerPrefs.HasKey("Username") && usernameInput != null)
            {
                usernameInput.text = PlayerPrefs.GetString("Username");
            }
        }

        private void DisableButtons()
        {
            if (loginButton) loginButton.interactable = false;
            if (forgotPasswordButton) forgotPasswordButton.interactable = false;
            if (createAccountButton) createAccountButton.interactable = false;
            if (guestButton) guestButton.interactable = false;
        }

        private void EnableButtons()
        {
            if (loginButton) loginButton.interactable = true;
            if (forgotPasswordButton) forgotPasswordButton.interactable = true;
            if (createAccountButton) createAccountButton.interactable = true;
            if (guestButton) guestButton.interactable = true;
        }

        public void AttemptLogin()
        {
            if (!isFirebaseInitialized) return;
            HideError();

            string workerId = usernameInput != null ? usernameInput.text : "";
            string pin = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(pin))
            {
                ShowError("Please enter both Worker ID and PIN.");
                return;
            }

            DisableButtons();
            
#if !UNITY_WEBGL
            FirebaseAuthManager.Instance.OnLoginSuccess += OnLoginSuccessNative;
#endif
            FirebaseAuthManager.Instance.OnLoginFailed += OnLoginFailed;
            FirebaseAuthManager.Instance.Login(workerId, pin);
        }

#if !UNITY_WEBGL
        void OnLoginSuccessNative(global::Firebase.Auth.FirebaseUser user)
        {
            FirebaseAuthManager.Instance.OnLoginSuccess -= OnLoginSuccessNative;
            FirebaseAuthManager.Instance.OnLoginFailed -= OnLoginFailed;
            OnLoginSuccessCore();
        }
#endif

        void OnLoginSuccessCore()
        {
            if (usernameInput != null)
            {
                PlayerPrefs.SetString("Username", usernameInput.text);
                PlayerPrefs.Save();
            }
            LoadNextScene();
        }

        void OnLoginFailed(string err)
        {
#if !UNITY_WEBGL
            FirebaseAuthManager.Instance.OnLoginSuccess -= OnLoginSuccessNative;
#endif
            FirebaseAuthManager.Instance.OnLoginFailed -= OnLoginFailed;
            EnableButtons();
            ShowError(err);
        }

        public void CreateAccount()
        {
            // Optional: If they click "Create Account" from UI_Login, route them to UI_Register
            SceneManager.LoadScene("UI_Register");
        }

        public void ForgotPassword()
        {
            ShowError("Password reset is handled via HR. Please contact your manager.");
        }

        public void ContinueAsGuest()
        {
            if (!isFirebaseInitialized) return;
            HideError();
            DisableButtons();

#if !UNITY_WEBGL
            FirebaseAuthManager.Instance.OnLoginSuccess += OnLoginSuccessNative;
#endif
            FirebaseAuthManager.Instance.OnLoginFailed += OnLoginFailed;
            FirebaseAuthManager.Instance.DemoLogin();
        }

        private void LoadNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.LogWarning("LoginController: Next Scene Name is not set!");
                EnableButtons();
            }
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = Color.red;
                errorText.gameObject.SetActive(true);
            }
            Debug.LogWarning("Login Error: " + message);
        }

        private void ShowMessage(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.color = Color.green;
                errorText.gameObject.SetActive(true);
            }
            Debug.Log("Login Message: " + message);
        }
        
        private void HideError()
        {
            if (errorText != null)
            {
                errorText.gameObject.SetActive(false);
            }
        }
    }
}
