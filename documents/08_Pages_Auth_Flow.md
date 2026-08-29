# 08 — Page Implementation: Auth Flow

> Each page is its own scene. Auth uses Firebase Auth.

---

## 1. UI_Splash Scene

**File:** `Assets/Scenes/UI_Splash.unity`
**Controller:** `Assets/Scripts/UI/Pages/SplashPageController.cs`
**Template:** `Assets/UI/Templates/Pages/SplashPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="splash">
        <VisualElement class="splash__logo-box">
            <Label class="splash__logo-icon" text="🛡️" />
        </VisualElement>
        <Label class="splash__title" text="ARC" />
        <Label class="splash__subtitle" text="MineSafetyAr" />
        <Label class="splash__desc"
               text="AR-Based Industrial Safety Training" />
        <VisualElement class="splash__progress-track">
            <VisualElement class="progress-fill"
                           name="progress-fill" />
        </VisualElement>
        <Label class="splash__status" name="status"
               text="Loading..." />
        <VisualElement style="flex: 1;" />
        <Label class="splash__footer"
               text="Powered by Government of Jharkhand" />
        <Label class="splash__footer-sub"
               text="Department of Higher &amp; Technical Education" />
        <Label class="splash__footer-id" text="PS: SIH26041" />
    </VisualElement>
</VisualElement>
```

### USS (Light Theme)
```uss
.splash {
    flex: 1;
    align-items: center;
    justify-content: center;
    padding: 40px 24px;
    background-color: #FFFFFF;
}

.splash__logo-box {
    width: 80px;
    height: 80px;
    border-radius: 20px;
    background-color: var(--accent);
    align-items: center;
    justify-content: center;
    margin-bottom: 16px;
}

.splash__logo-icon { font-size: 48px; }

.splash__title {
    font-size: 36px;
    font-weight: 800;
    color: var(--text-primary);
    letter-spacing: 6px;
    margin-bottom: 4px;
}

.splash__subtitle {
    font-size: 14px;
    font-weight: 600;
    color: var(--accent);
    letter-spacing: 2px;
    margin-bottom: 8px;
}

.splash__desc {
    font-size: 13px;
    color: var(--text-muted);
    margin-bottom: 40px;
}

.splash__progress-track {
    width: 200px;
    height: 3px;
    background-color: var(--border);
    border-radius: 2px;
    overflow: hidden;
}

.splash__status {
    font-size: 11px;
    color: var(--text-disabled);
    margin-top: 8px;
}

.splash__footer {
    font-size: 11px;
    color: var(--text-muted);
    text-align: center;
}

.splash__footer-sub {
    font-size: 10px;
    color: var(--text-disabled);
    text-align: center;
}

.splash__footer-id {
    font-size: 10px;
    color: var(--accent);
    font-weight: 600;
    text-align: center;
}
```

### C# Controller
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Pages
{
    public class SplashPageController : PageController
    {
        private VisualElement progressFill;
        private Label statusLabel;

        protected override void BindUI()
        {
            progressFill = root.Q("progress-fill");
            statusLabel = root.Q<Label>("status");
        }

        public override void OnPageEnter()
        {
            StartCoroutine(AnimateSplash());
        }

        private IEnumerator AnimateSplash()
        {
            float progress = 0f;

            while (progress < 100f)
            {
                progress += 4f;
                progressFill.style.width =
                    Length.Percent(Mathf.Min(progress, 100f));
                yield return new WaitForSeconds(0.05f);
            }

            statusLabel.text = "Ready";
            yield return new WaitForSeconds(0.3f);

            NavigationManager.Instance.NavigateTo("UI_Login");
        }
    }
}
```

---

## 2. UI_Login Scene

**File:** `Assets/Scenes/UI_Login.unity`
**Controller:** `Assets/Scripts/UI/Pages/LoginPageController.cs`
**Template:** `Assets/UI/Templates/Pages/LoginPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <ScrollView>
        <VisualElement class="login">
            <!-- Logo -->
            <VisualElement class="login__logo-box">
                <Label class="login__logo-icon" text="🛡️" />
            </VisualElement>
            <Label class="login__title" text="ARC" />
            <Label class="login__subtitle" text="MineSafetyAr" />

            <!-- Worker ID -->
            <VisualElement class="form-group">
                <Label class="form-label" text="Worker ID" />
                <TextField class="input-field" name="worker-id"
                           placeholder="e.g. JH10293" />
            </VisualElement>

            <!-- PIN -->
            <VisualElement class="form-group">
                <Label class="form-label" text="PIN" />
                <VisualElement class="input-password">
                    <TextField class="input-field" name="pin"
                               placeholder="Enter 4-digit PIN"
                               maxLength="4" />
                    <Button class="pin-toggle" name="pin-toggle">
                        <Label text="👁️" />
                    </Button>
                </VisualElement>
            </VisualElement>

            <!-- Error Message -->
            <Label class="login__error" name="error-msg"
                   text="" style="display: none;" />

            <!-- Login Button -->
            <Button class="btn-primary" name="login-btn"
                    text="LOGIN" />

            <!-- Divider -->
            <VisualElement class="divider" />
            <Label class="login__or" text="OR" />

            <!-- Demo Mode -->
            <Button class="btn-secondary" name="demo-btn"
                    text="Demo Mode (Skip Login)" />

            <!-- Create Account -->
            <Button class="btn-text" name="register-btn"
                    text="Create New Account"
                    style="margin-top: 16px; align-self: center;" />

            <!-- Footer -->
            <Label class="login__lang"
                   text="English" />
        </VisualElement>
    </ScrollView>
</VisualElement>
```

### USS (Light Theme)
```uss
.login {
    align-items: center;
    padding: 40px 24px;
}

.login__logo-box {
    width: 80px;
    height: 80px;
    border-radius: 20px;
    background-color: var(--accent);
    align-items: center;
    justify-content: center;
    margin-bottom: 16px;
}

.login__logo-icon { font-size: 48px; }

.login__title {
    font-size: 36px;
    font-weight: 800;
    color: var(--text-primary);
    letter-spacing: 6px;
}

.login__subtitle {
    font-size: 14px;
    color: var(--accent);
    letter-spacing: 2px;
    margin-bottom: 32px;
}

.login__error {
    font-size: 13px;
    color: var(--danger);
    text-align: center;
    margin-bottom: 12px;
}

.login__or {
    font-size: 12px;
    color: var(--text-muted);
    text-align: center;
    margin: 12px 0;
}

.login__lang {
    font-size: 12px;
    color: var(--text-muted);
    text-align: center;
    margin-top: 24px;
}

.input-password {
    flex-direction: row;
    align-items: center;
}

.input-password .input-field { flex: 1; }

.pin-toggle {
    width: 40px;
    height: 40px;
    justify-content: center;
    align-items: center;
    background-color: transparent;
    border-width: 0;
}
```

### C# Controller (Firebase Auth)
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Pages
{
    public class LoginPageController : PageController
    {
        private TextField workerIdInput, pinInput;
        private Button loginBtn, demoBtn, registerBtn, pinToggle;
        private Label errorMsg;
        private bool showPin = false;

        protected override void BindUI()
        {
            workerIdInput = root.Q<TextField>("worker-id");
            pinInput = root.Q<TextField>("pin");
            loginBtn = root.Q<Button>("login-btn");
            demoBtn = root.Q<Button>("demo-btn");
            registerBtn = root.Q<Button>("register-btn");
            pinToggle = root.Q<Button>("pin-toggle");
            errorMsg = root.Q<Label>("error-msg");

            loginBtn.RegisterCallback<ClickEvent>(evt => OnLogin());
            demoBtn.RegisterCallback<ClickEvent>(evt => OnDemoLogin());
            registerBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.NavigateTo("UI_Register"));
            pinToggle.RegisterCallback<ClickEvent>(evt => TogglePin());

            // Subscribe to Firebase events
            Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                += OnFirebaseLoginSuccess;
            Firebase.FirebaseAuthManager.Instance.OnLoginFailed
                += OnFirebaseLoginFailed;
        }

        private void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                    -= OnFirebaseLoginSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed
                    -= OnFirebaseLoginFailed;
            }
        }

        private void OnLogin()
        {
            string workerId = workerIdInput.value;
            string pin = pinInput.value;

            if (string.IsNullOrEmpty(workerId) ||
                string.IsNullOrEmpty(pin))
            {
                ShowError("Please enter Worker ID and PIN");
                return;
            }

            loginBtn.text = "Logging in...";
            loginBtn.SetEnabled(false);

            // Firebase Auth login
            Firebase.FirebaseAuthManager.Instance
                .Login(workerId, pin);
        }

        private void OnDemoLogin()
        {
            demoBtn.text = "Loading...";
            demoBtn.SetEnabled(false);

            Firebase.FirebaseAuthManager.Instance.DemoLogin();
        }

        private void OnFirebaseLoginSuccess(
            Firebase.FirebaseUser user)
        {
            // Navigate to dashboard
            NavigationManager.Instance
                .NavigateToRoot("UI_Dashboard");
        }

        private void OnFirebaseLoginFailed(string error)
        {
            loginBtn.text = "LOGIN";
            loginBtn.SetEnabled(true);
            demoBtn.text = "Demo Mode (Skip Login)";
            demoBtn.SetEnabled(true);
            ShowError(error);
        }

        private void TogglePin()
        {
            showPin = !showPin;
            pinInput.isPasswordField = !showPin;
        }

        private void ShowError(string message)
        {
            errorMsg.text = message;
            errorMsg.style.display = DisplayStyle.Flex;
        }
    }
}
```

---

## 3. UI_Register Scene

**File:** `Assets/Scenes/UI_Register.unity`
**Controller:** `Assets/Scripts/UI/Pages/RegisterPageController.cs`
**Template:** `Assets/UI/Templates/Pages/RegisterPage.uxml`

### UXML Structure
```xml
<VisualElement class="page-container">
    <VisualElement class="header">
        <Button class="header__back-btn" name="back-btn">
            <Label text="←" style="font-size: 20px;" />
        </Button>
        <Label class="header__title" text="Register" />
        <VisualElement class="header__right-action" />
    </VisualElement>

    <ScrollView>
        <VisualElement class="register-form">
            <VisualElement class="form-group">
                <Label class="form-label" text="Full Name" />
                <TextField class="input-field" name="name-input"
                           placeholder="Enter your full name" />
            </VisualElement>

            <VisualElement class="form-group">
                <Label class="form-label" text="Worker ID" />
                <TextField class="input-field" name="worker-id"
                           placeholder="e.g. JH10293" />
            </VisualElement>

            <VisualElement class="form-group">
                <Label class="form-label"
                       text="Organization / Mine Name" />
                <TextField class="input-field" name="org-input"
                           placeholder="e.g. Jharkhand Steel Works" />
            </VisualElement>

            <VisualElement class="form-group">
                <Label class="form-label" text="Sector" />
                <DropdownField class="input-field"
                               name="sector-dropdown"
                               choices="Mining,Steel Manufacturing,Mica Processing,Construction,Other" />
            </VisualElement>

            <VisualElement class="form-group">
                <Label class="form-label" text="Phone Number" />
                <TextField class="input-field" name="phone-input"
                           placeholder="10-digit number" />
            </VisualElement>

            <VisualElement style="flex-direction: row; gap: 12px;">
                <VisualElement class="form-group"
                               style="flex: 1;">
                    <Label class="form-label" text="Create PIN" />
                    <TextField class="input-field" name="pin"
                               maxLength="4" />
                </VisualElement>
                <VisualElement class="form-group"
                               style="flex: 1;">
                    <Label class="form-label"
                           text="Confirm PIN" />
                    <TextField class="input-field"
                               name="confirm-pin"
                               maxLength="4" />
                </VisualElement>
            </VisualElement>

            <VisualElement class="form-group">
                <Label class="form-label"
                       text="Preferred Language" />
                <VisualElement class="language-selector">
                    <Button class="lang-btn lang-btn--active"
                            name="lang-en" text="English" />
                    <Button class="lang-btn" name="lang-hi"
                            text="Hindi" />
                    <Button class="lang-btn" name="lang-sat"
                            text="Santali" />
                </VisualElement>
            </VisualElement>

            <Label class="register__error" name="error-msg"
                   text="" style="display: none;" />

            <Button class="btn-primary" name="register-btn"
                    text="REGISTER" />

            <Label class="register__footer"
                   text="Your data is stored securely in Firebase." />
        </VisualElement>
    </ScrollView>
</VisualElement>
```

### C# Controller (Firebase Auth)
```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Pages
{
    public class RegisterPageController : PageController
    {
        private TextField nameInput, workerIdInput, orgInput;
        private TextField phoneInput, pinInput, confirmPinInput;
        private DropdownField sectorDropdown;
        private Button registerBtn, backBtn;
        private Button langEn, langHi, langSat;
        private Label errorMsg;
        private string selectedLanguage = "English";

        protected override void BindUI()
        {
            nameInput = root.Q<TextField>("name-input");
            workerIdInput = root.Q<TextField>("worker-id");
            orgInput = root.Q<TextField>("org-input");
            phoneInput = root.Q<TextField>("phone-input");
            pinInput = root.Q<TextField>("pin");
            confirmPinInput = root.Q<TextField>("confirm-pin");
            sectorDropdown = root.Q<DropdownField>("sector-dropdown");
            registerBtn = root.Q<Button>("register-btn");
            backBtn = root.Q<Button>("back-btn");
            langEn = root.Q<Button>("lang-en");
            langHi = root.Q<Button>("lang-hi");
            langSat = root.Q<Button>("lang-sat");
            errorMsg = root.Q<Label>("error-msg");

            registerBtn.RegisterCallback<ClickEvent>(evt =>
                OnRegister());
            backBtn.RegisterCallback<ClickEvent>(evt =>
                NavigationManager.Instance.GoBack());
            langEn.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("English", langEn));
            langHi.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("Hindi", langHi));
            langSat.RegisterCallback<ClickEvent>(evt =>
                SetLanguage("Santali", langSat));

            Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                += OnFirebaseRegisterSuccess;
            Firebase.FirebaseAuthManager.Instance.OnLoginFailed
                += OnFirebaseRegisterFailed;
        }

        private void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess
                    -= OnFirebaseRegisterSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed
                    -= OnFirebaseRegisterFailed;
            }
        }

        private void OnRegister()
        {
            if (pinInput.value != confirmPinInput.value)
            {
                ShowError("PINs do not match");
                return;
            }

            if (string.IsNullOrEmpty(nameInput.value) ||
                string.IsNullOrEmpty(workerIdInput.value))
            {
                ShowError("Please fill all required fields");
                return;
            }

            registerBtn.text = "Registering...";
            registerBtn.SetEnabled(false);

            // Firebase Auth register
            Firebase.FirebaseAuthManager.Instance.Register(
                workerIdInput.value,
                pinInput.value,
                nameInput.value,
                "UI_Dashboard"
            );
        }

        private void OnFirebaseRegisterSuccess(
            Firebase.FirebaseUser user)
        {
            // Create worker profile in Firestore
            var worker = new WorkerData
            {
                firebaseUid = user.UserId,
                id = workerIdInput.value,
                name = nameInput.value,
                organization = orgInput.value,
                sector = sectorDropdown.value,
                phone = phoneInput.value,
                language = selectedLanguage,
                joinDate = System.DateTime.UtcNow
                    .ToString("yyyy-MM-dd"),
                overallProgress = 0,
                certificatesEarned = 0,
                totalAttempts = 0,
                competencyScores = new CompetencyScores()
            };

            Firebase.FirestoreService.Instance
                .SaveWorker(worker, () =>
                {
                    NavigationManager.Instance
                        .NavigateToRoot("UI_Dashboard");
                });
        }

        private void OnFirebaseRegisterFailed(string error)
        {
            registerBtn.text = "REGISTER";
            registerBtn.SetEnabled(true);
            ShowError(error);
        }

        private void SetLanguage(string lang, Button activeBtn)
        {
            selectedLanguage = lang;
            langEn.RemoveFromClassList("lang-btn--active");
            langHi.RemoveFromClassList("lang-btn--active");
            langSat.RemoveFromClassList("lang-btn--active");
            activeBtn.AddToClassList("lang-btn--active");
        }

        private void ShowError(string message)
        {
            errorMsg.text = message;
            errorMsg.style.display = DisplayStyle.Flex;
        }
    }
}
```
