using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Pages
{
    public class LoginPageController : PageController
    {
        TextField workerIdInput, pinInput;
        Button loginBtn, demoBtn, registerBtn, pinToggle;
        Label errorMsg;
        bool showPin = false;

        protected override void BindUI()
        {
            workerIdInput = root.Q<TextField>("worker-id");
            pinInput = root.Q<TextField>("pin");
            loginBtn = root.Q<Button>("login-btn");
            demoBtn = root.Q<Button>("demo-btn");
            registerBtn = root.Q<Button>("register-btn");
            pinToggle = root.Q<Button>("pin-toggle");
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

            Firebase.FirebaseAuthManager.Instance.Login(workerId, pin);
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
