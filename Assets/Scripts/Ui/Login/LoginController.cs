using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using System;

namespace MiningSafetyAR.UI.Login
{
    public class LoginController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField usernameInput; // Used for email in Firebase
        [SerializeField] private TMP_InputField passwordInput;
        
        [Header("Buttons & Feedback")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button forgotPasswordButton;
        [SerializeField] private Button createAccountButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private TextMeshProUGUI errorText;

        [Header("Scene Routing")]
        [SerializeField] private string nextSceneName = "MainMenu";

        private FirebaseAuth auth;
        private bool isFirebaseInitialized = false;

        private void Start()
        {
            HideError();
            DisableButtons(); // Prevent clicks until Firebase is ready

            // Check and fix dependencies before initializing Firebase
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => 
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == DependencyStatus.Available)
                {
                    InitializeFirebase();
                }
                else
                {
                    Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
                    ShowError("System Error: Failed to initialize backend.");
                }
            });
        }

        private void InitializeFirebase()
        {
            auth = FirebaseAuth.DefaultInstance;
            isFirebaseInitialized = true;

            // Hook up the buttons now that we're ready
            if (loginButton != null) loginButton.onClick.AddListener(AttemptLogin);
            if (forgotPasswordButton != null) forgotPasswordButton.onClick.AddListener(ForgotPassword);
            if (createAccountButton != null) createAccountButton.onClick.AddListener(CreateAccount);
            if (guestButton != null) guestButton.onClick.AddListener(ContinueAsGuest);
            
            EnableButtons();
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

            string email = usernameInput != null ? usernameInput.text : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter both email and password.");
                return;
            }

            DisableButtons();
            
            auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => 
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    HandleAuthError(task.Exception);
                    EnableButtons();
                    return;
                }

                AuthResult result = task.Result;
                Debug.LogFormat("User signed in successfully: {0} ({1})", result.User.Email, result.User.UserId);
                
                PlayerPrefs.SetString("Username", result.User.Email);
                PlayerPrefs.Save();
                
                LoadNextScene();
            });
        }

        public void CreateAccount()
        {
            if (!isFirebaseInitialized) return;
            HideError();

            string email = usernameInput != null ? usernameInput.text : "";
            string password = passwordInput != null ? passwordInput.text : "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter an email and password to register.");
                return;
            }

            DisableButtons();

            auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => 
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    HandleAuthError(task.Exception);
                    EnableButtons();
                    return;
                }

                AuthResult result = task.Result;
                Debug.LogFormat("Firebase user created successfully: {0} ({1})", result.User.Email, result.User.UserId);
                
                PlayerPrefs.SetString("Username", result.User.Email);
                PlayerPrefs.Save();
                
                LoadNextScene();
            });
        }

        public void ForgotPassword()
        {
            if (!isFirebaseInitialized) return;
            HideError();

            string email = usernameInput != null ? usernameInput.text : "";
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowError("Please enter your email address to reset password.");
                return;
            }

            DisableButtons();

            auth.SendPasswordResetEmailAsync(email).ContinueWithOnMainThread(task =>
            {
                EnableButtons();
                if (task.IsCanceled || task.IsFaulted)
                {
                    HandleAuthError(task.Exception);
                    return;
                }
                
                ShowMessage("Password reset email sent!");
            });
        }

        public void ContinueAsGuest()
        {
            if (!isFirebaseInitialized) return;
            HideError();
            DisableButtons();

            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task => 
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    HandleAuthError(task.Exception);
                    EnableButtons();
                    return;
                }

                AuthResult result = task.Result;
                Debug.LogFormat("Guest signed in successfully: {0}", result.User.UserId);
                
                PlayerPrefs.SetString("Username", "Guest User");
                PlayerPrefs.Save();
                
                LoadNextScene();
            });
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

        private void HandleAuthError(AggregateException aggregateException)
        {
            string errorMessage = "An unknown error occurred.";
            
            if (aggregateException != null)
            {
                FirebaseException firebaseEx = aggregateException.Flatten().InnerExceptions[0] as FirebaseException;
                if (firebaseEx != null)
                {
                    AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                    errorMessage = GetErrorMessage(errorCode);
                }
                else
                {
                    errorMessage = aggregateException.Flatten().InnerExceptions[0].Message;
                }
            }

            ShowError(errorMessage);
        }

        private string GetErrorMessage(AuthError errorCode)
        {
            switch (errorCode)
            {
                // case AuthError.AccountExistsWithDifferentCredential: return "Account already exists with different credentials.";
                case AuthError.MissingPassword: return "Please enter a password.";
                case AuthError.WeakPassword: return "Password is too weak. Please use at least 6 characters.";
                case AuthError.InvalidEmail: return "Invalid email address format.";
                case AuthError.UserNotFound: return "Account not found. Please register first.";
                case AuthError.WrongPassword: return "Incorrect password. Please try again.";
                case AuthError.EmailAlreadyInUse: return "An account with this email already exists.";
                case AuthError.NetworkRequestFailed: return "Network error. Please check your internet connection.";
                default: return "Authentication failed. Please try again.";
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
