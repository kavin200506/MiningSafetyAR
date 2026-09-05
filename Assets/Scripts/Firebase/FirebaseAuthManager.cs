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
using MiningSafetyAR.Data;

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
        public string refreshToken;
        public string displayName;
        /// <summary>True when this session was restored offline from a cached refresh token with no
        /// live network confirmation — idToken may be stale/absent, so Firestore calls should queue
        /// instead of attempting the network.</summary>
        public bool isOffline;

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
        public bool IsOfflineSession => _restUser != null && _restUser.isOffline;

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
                StartCoroutine(RegisterViaRestCoroutine(email, password, displayName, workerId.Trim()));
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
                StartCoroutine(LoginViaRestCoroutine(email, password, workerId.Trim()));
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

        private IEnumerator LoginViaRestCoroutine(string email, string password, string workerId)
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
                    refreshToken = resp.refreshToken,
                    displayName = email.Split('@')[0]
                };
                OfflineStore.SaveSession(resp.localId, workerId, email, resp.refreshToken);

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
                    yield return RegisterViaRestCoroutine(email, password, email.Split('@')[0], workerId);
                }
                else
                {
                    string errText = req.downloadHandler != null ? req.downloadHandler.text : req.error;
                    Debug.LogError($"[FirebaseAuth] REST Login Failed: {errText}");
                    OnLoginFailed?.Invoke($"Login Failed: {req.error}");
                }
            }
        }

        private IEnumerator RegisterViaRestCoroutine(string email, string password, string displayName, string workerId)
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
                    refreshToken = resp.refreshToken,
                    displayName = string.IsNullOrEmpty(displayName) ? email.Split('@')[0] : displayName
                };
                OfflineStore.SaveSession(resp.localId, workerId, email, resp.refreshToken);

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
            OfflineStore.ClearSession();
            Debug.Log("[FirebaseAuth] Logged out — persisted session cleared, next launch requires login.");
            OnLogout?.Invoke();
        }

        // ------------------------------------------------------------------
        // SESSION RESTORE (stay logged in across restarts, including offline)
        // ------------------------------------------------------------------

        [Serializable]
        private class RefreshTokenResponse
        {
            public string access_token;
            public string expires_in;
            public string token_type;
            public string refresh_token;
            public string id_token;
            public string user_id;
            public string project_id;
        }

        /// <summary>
        /// Call once at app start (before routing to Login/Dashboard). If a session was persisted
        /// from a previous login, this either silently refreshes it online (no PIN re-entry), or —
        /// if there's genuinely no connectivity — falls back to an OFFLINE session that trusts the
        /// last cached worker data (AppDataService.LoadWorkerFromFirestore already falls back to its
        /// own local cache when Firestore is unreachable, so this composes with that). A refresh
        /// token the server explicitly rejects (revoked/expired) is treated as a real logout, not an
        /// offline condition. `onComplete(true)` means "proceed to Dashboard", `false` means "go to
        /// Login screen normally".
        /// </summary>
        public void TryRestoreSession(Action<bool> onComplete)
        {
            // Note: an already-offline session (IsLoggedIn true, IsOfflineSession true) is NOT
            // short-circuited here — this lets CloudSyncManager periodically re-attempt a silent
            // refresh once real connectivity returns mid-session, upgrading it back to a live
            // session without waiting for an app restart.
            if (IsLoggedIn && !IsOfflineSession) { onComplete?.Invoke(true); return; }

            var session = OfflineStore.GetSession();
            if (session == null || string.IsNullOrEmpty(session.refreshToken))
            {
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(RestoreSessionCoroutine(session, onComplete));
        }

        private IEnumerator RestoreSessionCoroutine(OfflineStore.OfflineSession session, Action<bool> onComplete)
        {
            string url = $"https://securetoken.googleapis.com/v1/token?key={API_KEY}";
            string body = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(session.refreshToken)}";

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<RefreshTokenResponse>(req.downloadHandler.text);
                _restUser = new RestUserProxy
                {
                    localId = resp.user_id,
                    email = session.email,
                    idToken = resp.id_token,
                    refreshToken = resp.refresh_token,
                    displayName = !string.IsNullOrEmpty(session.email) ? session.email.Split('@')[0] : session.workerId,
                    isOffline = false
                };
                OfflineStore.SaveSession(resp.user_id, session.workerId, session.email, resp.refresh_token);
                Debug.Log($"[FirebaseAuth] Session restored online for {resp.user_id} — logged in silently.");
#if !UNITY_WEBGL
                OnLoginSuccess?.Invoke(_currentUser);
#else
                OnLoginSuccess?.Invoke(_restUser);
#endif
                onComplete?.Invoke(true);
            }
            else if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                // Server reachable and explicitly rejected the refresh token — a real revoke/expiry,
                // not an offline condition. Require a fresh login.
                Debug.LogWarning($"[FirebaseAuth] Refresh token rejected by server ({req.downloadHandler?.text}) — clearing session.");
                OfflineStore.ClearSession();
                onComplete?.Invoke(false);
            }
            else
            {
                // No connectivity (or a transient failure) — trust the cached session and let the
                // worker in using local data; AppDataService/Firestore writes will queue until the
                // next successful restore.
                Debug.LogWarning($"[FirebaseAuth] Could not reach auth server ({req.error}) — starting OFFLINE session for {session.firebaseUid}.");
                _restUser = new RestUserProxy
                {
                    localId = session.firebaseUid,
                    email = session.email,
                    idToken = null,
                    refreshToken = session.refreshToken,
                    displayName = !string.IsNullOrEmpty(session.email) ? session.email.Split('@')[0] : session.workerId,
                    isOffline = true
                };
#if !UNITY_WEBGL
                OnLoginSuccess?.Invoke(_currentUser);
#else
                OnLoginSuccess?.Invoke(_restUser);
#endif
                onComplete?.Invoke(true);
            }
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
