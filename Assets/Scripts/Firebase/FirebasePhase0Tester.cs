using UnityEngine;
using MiningSafetyAR.Firebase;
using Firebase;
using Firebase.Auth;

namespace MiningSafetyAR.Firebase.Test
{
    /// <summary>
    /// Phase 0 manual tester — attach to any GameObject in a test scene (or use context menu).
    /// Provides Edit-mode + Play-mode checks without needing UI scenes.
    /// </summary>
    public class FirebasePhase0Tester : MonoBehaviour
    {
        [Header("Manual Test Credentials")]
        public string testWorkerId = "JH99999";
        public string testPin = "0000";
        public string testName = "Phase0 Test Worker";

        void OnEnable()
        {
            // Subscribe if manager already exists, else wait
            TrySubscribe();
        }

        void TrySubscribe()
        {
            if (FirebaseAuthManager.Instance == null) return;
            FirebaseAuthManager.Instance.OnInitSuccess += OnInitSuccess;
            FirebaseAuthManager.Instance.OnInitFailed += OnInitFailed;
            FirebaseAuthManager.Instance.OnLoginSuccess += OnLoginSuccess;
            FirebaseAuthManager.Instance.OnLoginFailed += OnLoginFailed;
        }

        void OnDisable()
        {
            if (FirebaseAuthManager.Instance == null) return;
            FirebaseAuthManager.Instance.OnInitSuccess -= OnInitSuccess;
            FirebaseAuthManager.Instance.OnInitFailed -= OnInitFailed;
            FirebaseAuthManager.Instance.OnLoginSuccess -= OnLoginSuccess;
            FirebaseAuthManager.Instance.OnLoginFailed -= OnLoginFailed;
        }

        void OnInitSuccess()
        {
            Debug.Log("[Phase0Tester] Firebase Init SUCCESS — auth ready. You can now call Test Register/Login.");
        }

        void OnInitFailed(DependencyStatus s)
        {
            Debug.LogError($"[Phase0Tester] Firebase Init FAILED: {s} — check google-services.json, package name, import.");
        }

        void OnLoginSuccess(FirebaseUser u)
        {
            string uid = u != null ? u.UserId : FirebaseAuthManager.Instance.CurrentUserId;
            string email = u != null ? u.Email : FirebaseAuthManager.Instance.CurrentUserEmail;
            Debug.Log($"[Phase0Tester] Login SUCCESS uid={uid} email={email}");
            // Firestore write test: save a dummy worker doc
            string json = $"{{\"id\":\"{testWorkerId}\",\"name\":\"{testName}\",\"phase\":\"phase0\",\"ts\":\"{System.DateTime.UtcNow:o}\"}}";
            FirestoreService.Instance.SaveRaw($"phase0_tests/{uid}", json, (ok, resp) =>
            {
                Debug.Log($"[Phase0Tester] Firestore WRITE {(ok ? "OK" : "FAIL")} resp={resp?.Substring(0, System.Math.Min(300, resp.Length))}");
                if (ok)
                {
                    FirestoreService.Instance.GetTestDocument("phase0_tests", uid, (ok2, resp2) =>
                    {
                        Debug.Log($"[Phase0Tester] Firestore READ {(ok2 ? "OK" : "FAIL")} {resp2?.Substring(0, System.Math.Min(400, resp2.Length))}");
                    });
                }
            });
        }

        void OnLoginFailed(string err)
        {
            Debug.LogError($"[Phase0Tester] Login FAILED: {err}");
        }

        // Context menu buttons for manual click in Inspector
        [ContextMenu("Test: Register Phase0 User")]
        public void TestRegister()
        {
            if (FirebaseAuthManager.Instance == null) { Debug.LogError("No FirebaseAuthManager in scene. Add AppManagers prefab or ensure one exists."); return; }
            Debug.Log($"[Phase0Tester] Register {testWorkerId}/{testPin}...");
            FirebaseAuthManager.Instance.Register(testWorkerId, testPin, testName);
        }

        [ContextMenu("Test: Login Phase0 User")]
        public void TestLogin()
        {
            if (FirebaseAuthManager.Instance == null) { Debug.LogError("No FirebaseAuthManager in scene."); return; }
            Debug.Log($"[Phase0Tester] Login {testWorkerId}/{testPin}...");
            FirebaseAuthManager.Instance.Login(testWorkerId, testPin);
        }

        [ContextMenu("Test: Demo Login")]
        public void TestDemo() => FirebaseAuthManager.Instance?.DemoLogin();

        [ContextMenu("Test: Logout")]
        public void TestLogout() => FirebaseAuthManager.Instance?.Logout();

        [ContextMenu("Test: Firestore Raw Write (no auth)")]
        public void TestFirestoreRaw()
        {
            string json = $"{{\"hello\":\"world\",\"v\":123,\"ts\":\"{System.DateTime.UtcNow:o}\"}}";
            FirestoreService.Instance.SaveRaw($"phase0_tests/manual_{System.Guid.NewGuid().ToString().Substring(0, 6)}", json, (ok, r) =>
            {
                Debug.Log($"[Phase0Tester] Raw write {(ok ? "OK" : "FAIL")} {r}");
            });
        }
    }
}
