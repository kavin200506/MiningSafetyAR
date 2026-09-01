using System;
using System.Collections;
using System.Linq;

#if !UNITY_WEBGL
using Firebase;
using Firebase.Auth;
#endif

using UnityEngine;
using UnityEngine.Networking;
using MiningSafetyAR.Helpers;

namespace MiningSafetyAR.Firebase
{
    /// <summary>
    /// Lightweight container for user state in REST fallback mode.
    /// </summary>
    [Serializable]
    public class RestUserProxy
    {
        public string localId;
        public string email;
        public string idToken;
        public string displayName;

        public string UserId => localId;
        public string Email => email;
        public string DisplayName => displayName;
    }

    /// <summary>
    /// Robust Firebase Auth manager — supports Native C++ Firebase SDK with automatic cross-platform REST API fallback.
    /// Eliminates DllNotFoundException on macOS Unity Editor while providing full authentication functionality.
    /// workerId@miningsafety.app + PIN => ARC{PIN}2026!
    /// </summary>
    public class FirebaseAuthManager : MonoBehaviour
    {
        public static FirebaseAuthManager Instance { get; private set; }

        private const string API_KEY = "AIzaSyBxhWZvIqgIMP1niRSc_H2iRMkdDLNdybI";

#if !UNITY_WEBGL
        private FirebaseAuth _auth;
        private FirebaseUser _currentUser;
#endif
        private RestUserProxy _restUser;
        private bool _initialized;
        private bool _useRestFallback;

        public bool IsLoggedIn => 
#if !UNITY_WEBGL
            _currentUser != null || 
#endif
            _restUser != null;

        public string CurrentUserId => 
#if !UNITY_WEBGL
            _currentUser?.UserId ?? 
#endif
            _restUser?.UserId;

        public string CurrentUserEmail => 
#if !UNITY_WEBGL
            _currentUser?.Email ?? 
#endif
            _restUser?.Email;

#if !UNITY_WEBGL
        public FirebaseUser CurrentUser => _currentUser;
#endif
        public bool IsInitialized => _initialized;

#pragma warning disable CS0067
#if !UNITY_WEBGL
        public event Action<FirebaseUser> OnLoginSuccess;
        public event Action<DependencyStatus> OnInitFailed;
#else
        public event Action<object> OnLoginSuccess;
        public event Action<object> OnInitFailed;
#endif
#pragma warning restore CS0067
        public event Action<string> OnLoginFailed;
        public event Action OnLogout;
        public event Action OnInitSuccess;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }

        void InitializeFirebase()
        {
            Debug.Log("[FirebaseAuth] Initializing Firebase Auth...");

            try
            {
                // FORCE REST MODE GLOBALLY ON ALL PLATFORMS (Android/iOS/Editor)
                // This completely bypasses the native Firebase SDK and prevents CheckAndFixDependenciesAsync from hanging forever.
                EnableRestFallback();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FirebaseAuth] Exception enabling REST mode: {ex.Message}.");
            }
        }

        private void EnableRestFallback()
        {
            _useRestFallback = true;
            _initialized = true;
            Debug.Log("[FirebaseAuth] Cross-Platform REST API Auth Mode ENABLED (100% immune to DllNotFoundException).");
            MainThreadDispatcher.Enqueue(() => OnInitSuccess?.Invoke());
        }

        static string BuildEmail(string workerId) => $"{workerId.Trim()}@miningsafety.app";
        static string BuildPassword(string pin) => $"ARC{pin.Trim()}2026!";

        public void Register(string workerId, string pin, string displayName, string onSuccessScene = null)
        {
            if (!_initialized)
            {
                OnLoginFailed?.Invoke("Firebase not ready. Try again.");
                return;
            }
            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(pin))
            {
                OnLoginFailed?.Invoke("Worker ID and PIN required.");
                return;
            }

            string email = BuildEmail(workerId);
            string password = BuildPassword(pin);

            if (_useRestFallback
#if !UNITY_WEBGL
                || _auth == null
#endif
               )
            {
                StartCoroutine(RegisterViaRestCoroutine(email, password, displayName));
            }
            else
            {
#if !UNITY_WEBGL
                Debug.Log($"[FirebaseAuth] Registering {email} via Native SDK...");
                _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        string err = ExtractError(task);
                        Debug.LogError($"[FirebaseAuth] Register failed: {err}");
                        MainThreadDispatcher.Enqueue(() => OnLoginFailed?.Invoke(err));
                        return;
                    }

                    _currentUser = task.Result.User;
                    Debug.Log($"[FirebaseAuth] Registered OK: {_currentUser.UserId}");

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        var profile = new UserProfile { DisplayName = displayName.Trim() };
                        _currentUser.UpdateUserProfileAsync(profile);
                    }

                    MainThreadDispatcher.Enqueue(() => OnLoginSuccess?.Invoke(_currentUser));
                });
#endif
            }
        }

        public void Login(string workerId, string pin)
        {
            if (!_initialized)
            {
                OnLoginFailed?.Invoke("Firebase not ready. Try again.");
                return;
            }
            if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(pin))
            {
                OnLoginFailed?.Invoke("Please enter Worker ID and PIN.");
                return;
            }

            string email = BuildEmail(workerId);
            string password = BuildPassword(pin);

            if (_useRestFallback
#if !UNITY_WEBGL
                || _auth == null
#endif
               )
            {
                StartCoroutine(LoginViaRestCoroutine(email, password));
            }
            else
            {
#if !UNITY_WEBGL
                Debug.Log($"[FirebaseAuth] Login {email} via Native SDK...");
                _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        string err = ExtractError(task);
                        Debug.LogError($"[FirebaseAuth] Login failed: {err}");
                        MainThreadDispatcher.Enqueue(() => OnLoginFailed?.Invoke(err));
                        return;
                    }

                    _currentUser = task.Result.User;
                    Debug.Log($"[FirebaseAuth] Login OK: {_currentUser.UserId} ({_currentUser.Email})");
                    MainThreadDispatcher.Enqueue(() => OnLoginSuccess?.Invoke(_currentUser));
                });
#endif
            }
        }

        public void DemoLogin()
        {
            Login("demo", "Demo2026");
        }

        // ------------------------------------------------------------------
        // REST API IMPLEMENTATION (Pure C# UnityWebRequest - Zero Native DLLs)
        // ------------------------------------------------------------------

        [Serializable]
        private class AuthRestRequest
        {
            public string email;
            public string password;
            public bool returnSecureToken = true;
        }

        [Serializable]
        private class AuthRestResponse
        {
            public string localId;
            public string email;
            public string idToken;
            public string refreshToken;
            public string expiresIn;
            public AuthErrorContainer error;
        }

        [Serializable]
        private class AuthErrorContainer
        {
            public int code;
            public string message;
        }

        private IEnumerator LoginViaRestCoroutine(string email, string password)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={API_KEY}";
            string bodyJson = JsonUtility.ToJson(new AuthRestRequest { email = email, password = password });

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                AuthRestResponse resp = JsonUtility.FromJson<AuthRestResponse>(req.downloadHandler.text);
                _restUser = new RestUserProxy
                {
                    localId = resp.localId,
                    email = resp.email,
                    idToken = resp.idToken,
                    displayName = email.Split('@')[0]
                };

                Debug.Log($"[FirebaseAuth] REST Login OK: {_restUser.localId} ({_restUser.email})");
#if !UNITY_WEBGL
                OnLoginSuccess?.Invoke(_currentUser);
#else
                OnLoginSuccess?.Invoke(_restUser);
#endif
            }
            else
            {
                // If account not found on sign-in, auto-attempt registration
                if (req.downloadHandler != null && req.downloadHandler.text.Contains("EMAIL_NOT_FOUND"))
                {
                    Debug.Log($"[FirebaseAuth] REST Account not found, auto-creating: {email}...");
                    yield return RegisterViaRestCoroutine(email, password, email.Split('@')[0]);
                }
                else
                {
                    string errText = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                    Debug.LogError($"[FirebaseAuth] REST Login Failed: {errText}");
                    OnLoginFailed?.Invoke($"Login Failed: {req.error}");
                }
            }
        }

        private IEnumerator RegisterViaRestCoroutine(string email, string password, string displayName)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={API_KEY}";
            string bodyJson = JsonUtility.ToJson(new AuthRestRequest { email = email, password = password });

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(bodyJson));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                AuthRestResponse resp = JsonUtility.FromJson<AuthRestResponse>(req.downloadHandler.text);
                _restUser = new RestUserProxy
                {
                    localId = resp.localId,
                    email = resp.email,
                    idToken = resp.idToken,
                    displayName = string.IsNullOrEmpty(displayName) ? email.Split('@')[0] : displayName
                };

                Debug.Log($"[FirebaseAuth] REST Registration OK: {_restUser.localId} ({_restUser.email})");
#if !UNITY_WEBGL
                OnLoginSuccess?.Invoke(_currentUser);
#else
                OnLoginSuccess?.Invoke(_restUser);
#endif
            }
            else
            {
                string errText = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                Debug.LogError($"[FirebaseAuth] REST Registration Failed: {errText}");
                OnLoginFailed?.Invoke($"Registration Failed: {req.error}");
            }
        }

        public void Logout()
        {
#if !UNITY_WEBGL
            if (_auth != null) _auth.SignOut();
            _currentUser = null;
#endif
            _restUser = null;
            Debug.Log("[FirebaseAuth] Logged out");
            OnLogout?.Invoke();
        }

        public void GetIdToken(Action<string> callback)
        {
            if (_restUser != null)
            {
                callback?.Invoke(_restUser.idToken);
                return;
            }
#if !UNITY_WEBGL
            if (_currentUser != null)
            {
                _currentUser.TokenAsync(false).ContinueWith(t =>
                {
                    if (t.IsFaulted) MainThreadDispatcher.Enqueue(() => callback?.Invoke(null));
                    else MainThreadDispatcher.Enqueue(() => callback?.Invoke(t.Result));
                });
                return;
            }
#endif
            callback?.Invoke(null);
        }

        private static string ExtractError(System.Threading.Tasks.Task task)
        {
            if (task.Exception == null) return "Unknown error";
            return task.Exception.InnerExceptions.FirstOrDefault()?.Message ?? task.Exception.Message;
        }
    }
}
