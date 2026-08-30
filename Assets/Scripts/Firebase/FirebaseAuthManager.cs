using System;
using System.Linq;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using MiningSafetyAR.Helpers;

namespace MiningSafetyAR.Firebase
{
    /// <summary>
    /// Firebase Auth manager — workerId@miningsafety.app + PIN => ARC{PIN}2026!
    /// Persistent singleton. Events marshalled to main thread.
    /// </summary>
    public class FirebaseAuthManager : MonoBehaviour
    {
        public static FirebaseAuthManager Instance { get; private set; }

        private FirebaseAuth _auth;
        private FirebaseUser _currentUser;
        private bool _initialized;

        public bool IsLoggedIn => _currentUser != null;
        public string CurrentUserId => _currentUser?.UserId;
        public string CurrentUserEmail => _currentUser?.Email;
        public FirebaseUser CurrentUser => _currentUser;
        public bool IsInitialized => _initialized;

        public event Action<FirebaseUser> OnLoginSuccess;
        public event Action<string> OnLoginFailed;
        public event Action OnLogout;
        public event Action<DependencyStatus> OnInitFailed;
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
            Debug.Log("[FirebaseAuth] Checking dependencies...");
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                var status = task.Result;
                if (status == DependencyStatus.Available)
                {
                    _auth = FirebaseAuth.DefaultInstance;
                    _initialized = true;
                    Debug.Log($"[FirebaseAuth] Initialized. SDK={FirebaseApp.DefaultInstance.Options.AppId}");
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        OnInitSuccess?.Invoke();
                        if (_auth.CurrentUser != null)
                        {
                            _currentUser = _auth.CurrentUser;
                            Debug.Log($"[FirebaseAuth] Already logged in: {_currentUser.UserId} ({_currentUser.Email})");
                            OnLoginSuccess?.Invoke(_currentUser);
                        }
                    });
                }
                else
                {
                    Debug.LogError($"[FirebaseAuth] Dependency failed: {status}");
                    MainThreadDispatcher.Enqueue(() => OnInitFailed?.Invoke(status));
                }
            });
        }

        static string BuildEmail(string workerId) => $"{workerId.Trim()}@miningsafety.app";
        static string BuildPassword(string pin) => $"ARC{pin.Trim()}2026!";

        /// <summary>
        /// Register new worker. Creates Firebase user then fires OnLoginSuccess.
        /// </summary>
        public void Register(string workerId, string pin, string displayName, string onSuccessScene = null)
        {
            if (!_initialized || _auth == null)
            {
                Debug.LogError("[FirebaseAuth] Not initialized yet.");
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
            Debug.Log($"[FirebaseAuth] Registering {email}...");

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

                // Update display name (fire-and-forget)
                if (!string.IsNullOrEmpty(displayName))
                {
                    var profile = new UserProfile { DisplayName = displayName.Trim() };
                    _currentUser.UpdateUserProfileAsync(profile).ContinueWith(p =>
                    {
                        if (p.IsFaulted) Debug.LogWarning($"[FirebaseAuth] DisplayName update failed: {ExtractError(p)}");
                        else Debug.Log($"[FirebaseAuth] DisplayName set: {displayName}");
                    });
                }

                MainThreadDispatcher.Enqueue(() => OnLoginSuccess?.Invoke(_currentUser));
            });
        }

        public void Login(string workerId, string pin)
        {
            if (!_initialized || _auth == null)
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
            Debug.Log($"[FirebaseAuth] Login {email}...");

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
        }

        /// <summary>
        /// Demo login — tries demo@miningsafety.app / ARCDemo2026!, creates if missing.
        /// </summary>
        public void DemoLogin()
        {
            if (!_initialized || _auth == null)
            {
                OnLoginFailed?.Invoke("Firebase not ready.");
                return;
            }

            const string email = "demo@miningsafety.app";
            const string password = "ARCDemo2026!";
            Debug.Log("[FirebaseAuth] DemoLogin...");

            _auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.Log($"[FirebaseAuth] Demo sign-in failed, creating account: {ExtractError(task)}");
                    _auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(createTask =>
                    {
                        if (createTask.IsFaulted || createTask.IsCanceled)
                        {
                            string err = ExtractError(createTask);
                            Debug.LogError($"[FirebaseAuth] Demo create failed: {err}");
                            MainThreadDispatcher.Enqueue(() => OnLoginFailed?.Invoke("Demo login failed: " + err));
                            return;
                        }
                        _currentUser = createTask.Result.User;
                        var profile = new UserProfile { DisplayName = "Demo Worker" };
                        _currentUser.UpdateUserProfileAsync(profile);
                        Debug.Log($"[FirebaseAuth] Demo created: {_currentUser.UserId}");
                        MainThreadDispatcher.Enqueue(() => OnLoginSuccess?.Invoke(_currentUser));
                    });
                    return;
                }

                _currentUser = task.Result.User;
                Debug.Log($"[FirebaseAuth] Demo login OK: {_currentUser.UserId}");
                MainThreadDispatcher.Enqueue(() => OnLoginSuccess?.Invoke(_currentUser));
            });
        }

        public void Logout()
        {
            if (_auth != null) _auth.SignOut();
            _currentUser = null;
            Debug.Log("[FirebaseAuth] Logged out");
            OnLogout?.Invoke();
        }

        /// <summary>
        /// Get fresh ID token for REST calls (optional).
        /// </summary>
        public void GetIdToken(Action<string> callback)
        {
            if (_currentUser == null) { callback?.Invoke(null); return; }
            _currentUser.TokenAsync(false).ContinueWith(t =>
            {
                if (t.IsFaulted) MainThreadDispatcher.Enqueue(() => callback?.Invoke(null));
                else MainThreadDispatcher.Enqueue(() => callback?.Invoke(t.Result));
            });
        }

        static string ExtractError(System.Threading.Tasks.Task task)
        {
            if (task.Exception == null) return "Unknown error";
            // Log full aggregate for debugging
            Debug.LogError($"[FirebaseAuth] Full exception: {task.Exception}");
            foreach (var e in task.Exception.InnerExceptions)
                Debug.LogError($"[FirebaseAuth] Inner: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            var inner = task.Exception.InnerExceptions.FirstOrDefault();
            if (inner is FirebaseException fe) return $"{fe.Message} (code {(int)fe.ErrorCode} / {fe.ErrorCode}) Full: {task.Exception}";
            return $"{inner?.Message ?? task.Exception.Message} | Full: {task.Exception}";
        }
    }
}
