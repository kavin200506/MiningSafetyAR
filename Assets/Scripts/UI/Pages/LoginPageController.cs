using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class LoginPageController : PageController
    {
        // Remembers only (workerId, firebaseUid) for the last worker who completed a real manual
        // login on this device — never a credential. Written on every successful OnLogin(); NOT
        // cleared on logout (a shared-device "who scanned in last" convenience marker, not a session).
        // See OnBiometricLoginClicked()'s comment for why a face match alone still can't skip PIN entry.
        const string LastWorkerUidKey = "MSAR_LastWorkerUid";
        const string LastWorkerIdKey = "MSAR_LastWorkerIdText";

        TextField workerIdInput, pinInput;
        Button loginBtn, demoBtn, registerBtn, pinToggle, biometricLoginBtn;
        Label errorMsg;
        bool showPin = false;
        string lastAttemptedWorkerId;

        // Circular face-scanner overlay (FaceScannerOverlay.uxml) for the "Face Scan Login" shortcut.
        // webCamPreview/verificationBridge are MonoBehaviours added once to this same GameObject and
        // reused across opens; scannerUI is a plain class rebuilt against each fresh clone of the
        // overlay template.
        VisualTreeAsset scannerOverlayTemplate;
        VisualElement scannerOverlayRoot;
        WebCamPreviewController webCamPreview;
        FaceScannerUIController scannerUI;
        FaceVerificationBridge verificationBridge;

        protected override void BindUI()
        {
            workerIdInput = root.Q<TextField>("worker-id");
            pinInput = root.Q<TextField>("pin");
            loginBtn = root.Q<Button>("login-btn");
            demoBtn = root.Q<Button>("demo-btn");
            registerBtn = root.Q<Button>("register-btn");
            pinToggle = root.Q<Button>("pin-toggle");
            biometricLoginBtn = root.Q<Button>("biometric-login-btn");
            errorMsg = root.Q<Label>("error-msg");

            // Ensure fields are focusable and enabled for typing
            if (workerIdInput != null)
            {
                workerIdInput.SetEnabled(true);
                workerIdInput.focusable = true;
                workerIdInput.isPasswordField = false;
                ForceTextFieldColors(workerIdInput);
            }
            if (pinInput != null)
            {
                pinInput.SetEnabled(true);
                pinInput.focusable = true;
                pinInput.isPasswordField = true;
                ForceTextFieldColors(pinInput);
                
                pinInput.RegisterValueChangedCallback(e => {
                    if (e.newValue != null && e.newValue.Length == 4) {
                        pinInput.Blur();
                    }
                });
            }

            // Prevent ARPlacementManager from stealing pointer events when typing
            if (workerIdInput != null) workerIdInput.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            if (pinInput != null) pinInput.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());

            if (loginBtn != null) loginBtn.RegisterCallback<ClickEvent>(evt => OnLogin());
            if (demoBtn != null) demoBtn.RegisterCallback<ClickEvent>(evt => OnDemoLogin());
            if (registerBtn != null) registerBtn.RegisterCallback<ClickEvent>(evt => {
                var nav = NavigationManager.Instance;
                if (nav != null) nav.NavigateTo("UI_Register");
                else UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Register");
            });
            if (pinToggle != null) pinToggle.RegisterCallback<ClickEvent>(evt => TogglePin());
            if (biometricLoginBtn != null) biometricLoginBtn.RegisterCallback<ClickEvent>(evt => OnBiometricLoginClicked());

            // Auto-focus first field after a frame
            if (workerIdInput != null) workerIdInput.schedule.Execute(() => workerIdInput.Focus()).StartingIn(100);

            // Firebase events
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess += OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed += OnFirebaseLoginFailed;
            }
        }

        void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess -= OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed -= OnFirebaseLoginFailed;
            }
            // Reliable release even though NavigationManager does not currently invoke OnPageExit()
            // (see that override's comment) — OnDisable() always runs when this scene's GameObjects
            // are torn down, so this is the safety net that actually fires on ordinary navigation.
            CloseScannerOverlay();
        }

        void OnLogin()
        {
            string workerId = workerIdInput != null ? workerIdInput.value.Trim() : "";
            string pin = pinInput != null ? pinInput.value.Trim() : "";

            if (string.IsNullOrEmpty(workerId) || string.IsNullOrEmpty(pin))
            {
                ShowError("Please enter Worker ID and PIN");
                return;
            }

            if (loginBtn != null) { loginBtn.text = "Logging in..."; loginBtn.SetEnabled(false); }
            if (demoBtn != null) demoBtn.SetEnabled(false);
            HideError();

            lastAttemptedWorkerId = workerId;
            Firebase.FirebaseAuthManager.Instance.Login(workerId, pin);
        }

        // ================================================================
        // BIOMETRIC ("FACE SCAN") LOGIN SHORTCUT
        // ================================================================
        //
        // ARCHITECTURE NOTE: FaceVerificationService.VerifyFace(uid, ...) is a 1:1 check — it compares
        // a live face against ONE ALREADY-KNOWN worker's stored embedding; there is no 1:N "whose face
        // is this" lookup anywhere in this codebase, and FaceVerificationService's own class doc
        // explicitly says it is "NOT a general biometric login replacement." So a face scan alone can
        // neither identify an unknown worker nor produce a real Firebase ID token (no token is ever
        // requested here) — meaning "automatically log the worker in" from a bare face scan is not a
        // safe or correct thing to build. What IS safely buildable, and is what this does: remember
        // (workerId, uid) for whoever last completed a real manual login on this device, run a 1:1
        // verification against that REMEMBERED uid, and on a match pre-fill the Worker ID field and
        // focus PIN entry — a genuine quick-login assist for low-literacy users (typing/finding a
        // Worker ID is more error-prone than a 4-digit PIN) that never bypasses real authentication.

        void OnBiometricLoginClicked()
        {
            string rememberedUid = PlayerPrefs.GetString(LastWorkerUidKey, "");
            string rememberedWorkerId = PlayerPrefs.GetString(LastWorkerIdKey, "");

            if (string.IsNullOrEmpty(rememberedUid))
            {
                ShowError("No face profile remembered on this device yet. Log in with your Worker ID and PIN once first.");
                return;
            }

            HideError();
            ShowScannerOverlay();

            verificationBridge.StartVerification(rememberedUid, (ok, reason) =>
            {
                if (ok)
                {
                    Debug.Log($"[Login] Biometric match confirmed for remembered worker uid {rememberedUid}.");
                    CloseScannerOverlay();
                    if (workerIdInput != null) workerIdInput.value = rememberedWorkerId;
                    if (pinInput != null) { pinInput.value = ""; pinInput.Focus(); }
                }
                else
                {
                    Debug.LogWarning($"[Login] Biometric verification failed: {reason}");
                    // Overlay's own Failure state + "Retry Scan" (wired inside FaceVerificationBridge)
                    // handle a retry; Cancel (OnScannerCancelled) returns to the normal login form.
                }
            });
        }

        void ShowScannerOverlay()
        {
            if (scannerOverlayRoot != null) return; // already showing

            if (scannerOverlayTemplate == null)
                scannerOverlayTemplate = Resources.Load<VisualTreeAsset>("UI/Templates/Pages/FaceScannerOverlay");
            if (scannerOverlayTemplate == null)
            {
                Debug.LogError("[ERROR] LoginPageController FaceScannerOverlay template not found at Resources/UI/Templates/Pages/FaceScannerOverlay — cannot show scanner UI.");
                return;
            }

            scannerOverlayRoot = scannerOverlayTemplate.CloneTree();
            scannerOverlayRoot.style.position = Position.Absolute;
            scannerOverlayRoot.style.left = 0; scannerOverlayRoot.style.right = 0;
            scannerOverlayRoot.style.top = 0; scannerOverlayRoot.style.bottom = 0;
            root.Add(scannerOverlayRoot);

            if (webCamPreview == null) webCamPreview = gameObject.AddComponent<WebCamPreviewController>();
            if (verificationBridge == null) verificationBridge = gameObject.AddComponent<FaceVerificationBridge>();

            scannerUI = new FaceScannerUIController(scannerOverlayRoot);
            scannerUI.OnCancelClicked += OnScannerCancelled;
            verificationBridge.Configure(webCamPreview, scannerUI);

            webCamPreview.PlayCamera();
            webCamPreview.AttachToElement(scannerOverlayRoot.Q("scanner-viewport"));
        }

        void CloseScannerOverlay()
        {
            webCamPreview?.StopCamera();
            scannerUI?.Dispose();
            scannerUI = null;
            if (scannerOverlayRoot != null)
            {
                root?.Remove(scannerOverlayRoot);
                scannerOverlayRoot = null;
            }
        }

        void OnScannerCancelled()
        {
            Debug.Log("[INFO] LoginPageController Worker cancelled biometric login scan.");
            CloseScannerOverlay();
        }

        public override void OnPageExit()
        {
            // Explicit release on navigation away, on top of the automatic cleanup WebCamPreviewController
            // already does in its own OnDisable()/OnDestroy() when this scene's GameObjects are torn down,
            // and on top of this page's own OnDisable() above.
            CloseScannerOverlay();
        }

        void OnDemoLogin()
        {
            if (demoBtn != null) { demoBtn.text = "Loading..."; demoBtn.SetEnabled(false); }
            if (loginBtn != null) loginBtn.SetEnabled(false);
            HideError();
            Firebase.FirebaseAuthManager.Instance.DemoLogin();
        }

        void OnFirebaseLoginSuccess(global::Firebase.Auth.FirebaseUser user)
        {
            string uid = user != null ? user.UserId : Firebase.FirebaseAuthManager.Instance.CurrentUserId;
            Debug.Log($"[Login] Success {uid}");

            // Remember (workerId, uid) for the "Face Scan Login" shortcut — see that section's
            // architecture note. Skipped for Demo Mode (lastAttemptedWorkerId is only set in OnLogin()).
            if (!string.IsNullOrEmpty(lastAttemptedWorkerId) && !string.IsNullOrEmpty(uid))
            {
                PlayerPrefs.SetString(LastWorkerUidKey, uid);
                PlayerPrefs.SetString(LastWorkerIdKey, lastAttemptedWorkerId);
                PlayerPrefs.Save();
            }

            // Small delay to allow AppDataService to load worker
            Invoke(nameof(GoDashboard), 0.3f);
        }

        void GoDashboard()
        {
            NavigationManager.Instance.NavigateToRoot("UI_Dashboard");
        }

        void OnFirebaseLoginFailed(string error)
        {
            if (loginBtn != null) { loginBtn.text = "LOGIN"; loginBtn.SetEnabled(true); }
            if (demoBtn != null) { demoBtn.text = "Demo Mode (Skip Login)"; demoBtn.SetEnabled(true); }
            ShowError(error);
        }

        void TogglePin()
        {
            showPin = !showPin;
            if (pinInput != null) pinInput.isPasswordField = !showPin;
        }

        void ShowError(string message)
        {
            if (errorMsg != null)
            {
                errorMsg.text = message;
                errorMsg.style.display = DisplayStyle.Flex;
            }
            Debug.LogWarning($"[Login] Error: {message}");
        }

        void HideError()
        {
            if (errorMsg != null) errorMsg.style.display = DisplayStyle.None;
        }

        void ForceTextFieldColors(TextField tf)
        {
            if (tf == null) return;
            tf.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
            tf.style.backgroundColor = new StyleColor(Color.white);
            foreach (var te in tf.Query<TextElement>().ToList())
            {
                te.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
                te.style.opacity = 1;
                te.style.display = DisplayStyle.Flex;
            }
            var inner = tf.Q(className: "unity-text-field__input");
            if (inner != null)
            {
                inner.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
                inner.style.backgroundColor = new StyleColor(new Color(1,1,1,0));
                inner.style.opacity = 1;
                inner.style.display = DisplayStyle.Flex;
                foreach (var te in inner.Query<TextElement>().ToList())
                {
                    te.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
                    te.style.opacity = 1;
                    te.style.display = DisplayStyle.Flex;
                }
            }
            tf.schedule.Execute(() => {
                tf.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
                foreach (var te in tf.Query<TextElement>().ToList())
                    te.style.color = new StyleColor(new Color(0.10f, 0.10f, 0.10f, 1f));
            }).StartingIn(50);
        }
    }
}
