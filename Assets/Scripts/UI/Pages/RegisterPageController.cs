using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class RegisterPageController : PageController
    {
        TextField nameInput, workerIdInput, orgInput, phoneInput, pinInput, confirmPinInput;
        DropdownField sectorDropdown;
        Button registerBtn, signinBtn;
        Button langEn, langHi, langSat;
        Label errorMsg;
        string selectedLanguage = "English";
        string profileImageUri = "";

        void PickProfileImage()
        {
            profileImageUri = "profile_photo_selected";
            SetUploadPhotoVisualState(true);
            Debug.Log("[Register] Profile photo selected");
        }

        void SetUploadPhotoVisualState(bool hasImage)
        {
            var uploadBtn = root.Q<Button>("upload-photo-btn");
            var icon = root.Q<VisualElement>("upload-photo-icon");
            if (uploadBtn != null && icon != null)
            {
                if (hasImage)
                {
                    uploadBtn.style.backgroundColor = new StyleColor(new Color(255f/255f, 224f/255f, 178f/255f));
                    icon.style.backgroundColor = new StyleColor(new Color(255f/255f, 109f/255f, 0f));
                }
            }
        }

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
            signinBtn = root.Q<Button>("signin-btn");
            langEn = root.Q<Button>("lang-en");
            langHi = root.Q<Button>("lang-hi");
            langSat = root.Q<Button>("lang-sat");
            errorMsg = root.Q<Label>("error-msg");

            Debug.Log($"[Register] BindUI name={nameInput != null} workerId={workerIdInput != null} pin={pinInput != null} confirm={confirmPinInput != null}");

            // Ensure all TextFields focusable, enabled, and FORCE dark text (fix invisible typing)
            foreach (var tf in new[] { nameInput, workerIdInput, orgInput, phoneInput, pinInput, confirmPinInput })
            {
                if (tf != null)
                {
                    tf.SetEnabled(true);
                    tf.focusable = true;
                    ForceTextFieldColors(tf, tf.name);
                    tf.RegisterCallback<FocusInEvent>(e => { Debug.Log($"[Register] {tf.name} FocusIn"); ForceTextFieldColors(tf, tf.name); });
                    tf.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                    tf.RegisterCallback<ChangeEvent<string>>(e => ForceTextFieldColors(tf, tf.name));
                }
            }
            if (pinInput != null) 
            {
                pinInput.isPasswordField = true;
                pinInput.RegisterValueChangedCallback(e => {
                    if (e.newValue != null && e.newValue.Length == 4) pinInput.Blur();
                });
            }
            if (confirmPinInput != null) 
            {
                confirmPinInput.isPasswordField = true;
                confirmPinInput.RegisterValueChangedCallback(e => {
                    if (e.newValue != null && e.newValue.Length == 4) confirmPinInput.Blur();
                });
            }
            if (nameInput != null) nameInput.schedule.Execute(() => { nameInput.Focus(); ForceTextFieldColors(nameInput, "name-input"); }).StartingIn(150);

            if (registerBtn != null) registerBtn.RegisterCallback<ClickEvent>(evt => OnRegister());
            if (signinBtn != null) signinBtn.RegisterCallback<ClickEvent>(evt => NavigationManager.Instance.NavigateToRoot("UI_Login"));
            if (langEn != null) langEn.RegisterCallback<ClickEvent>(evt => SetLanguage("English", langEn));
            if (langHi != null) langHi.RegisterCallback<ClickEvent>(evt => SetLanguage("Hindi", langHi));
            if (langSat != null) langSat.RegisterCallback<ClickEvent>(evt => SetLanguage("Santali", langSat));

            var uploadBtn = root.Q<Button>("upload-photo-btn");
            if (uploadBtn != null) 
            {
                uploadBtn.RegisterCallback<ClickEvent>(evt => PickProfileImage());
            }

            SetLanguage("English", langEn);

            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess += OnFirebaseRegisterSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed += OnFirebaseRegisterFailed;
            }
        }

        void OnDisable()
        {
            if (Firebase.FirebaseAuthManager.Instance != null)
            {
                Firebase.FirebaseAuthManager.Instance.OnLoginSuccess -= OnFirebaseRegisterSuccess;
                Firebase.FirebaseAuthManager.Instance.OnLoginFailed -= OnFirebaseRegisterFailed;
            }
        }

        void OnRegister()
        {
            string pin = pinInput != null ? pinInput.value.Trim() : "";
            string confirm = confirmPinInput != null ? confirmPinInput.value.Trim() : "";

            if (pin != confirm)
            {
                ShowError("PINs do not match");
                return;
            }
            if (string.IsNullOrEmpty(nameInput?.value) || string.IsNullOrEmpty(workerIdInput?.value))
            {
                ShowError("Please fill all required fields");
                return;
            }
            if (pin.Length != 4)
            {
                ShowError("PIN must be 4 digits");
                return;
            }

            if (registerBtn != null) { registerBtn.text = "Registering..."; registerBtn.SetEnabled(false); }
            HideError();

            Firebase.FirebaseAuthManager.Instance.Register(workerIdInput.value.Trim(), pin, nameInput.value.Trim());
        }

        void OnFirebaseRegisterSuccess(global::Firebase.Auth.FirebaseUser user)
        {
            string uid = user != null ? user.UserId : Firebase.FirebaseAuthManager.Instance.CurrentUserId;
            var worker = new WorkerData
            {
                firebaseUid = uid,
                id = workerIdInput != null ? workerIdInput.value.Trim() : "NEW",
                name = nameInput != null ? nameInput.value.Trim() : "New Worker",
                organization = orgInput != null ? orgInput.value.Trim() : "",
                sector = sectorDropdown != null ? sectorDropdown.value : "Mining",
                phone = phoneInput != null ? phoneInput.value.Trim() : "",
                language = selectedLanguage,
                profilePicUrl = profileImageUri,
                joinDate = System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
                overallProgress = 0,
                certificatesEarned = 0,
                totalAttempts = 0,
                competencyScores = new CompetencyScores()
            };

            // Cache worker locally FIRST so AppDataService picks it up immediately
            string workerJson = JsonUtility.ToJson(worker);
            PlayerPrefs.SetString("CachedWorker", workerJson);
            PlayerPrefs.Save();

            // Save to Firestore, wait for completion, THEN navigate
            Firebase.FirestoreService.Instance.SaveWorker(worker.firebaseUid, workerJson, (ok, resp) =>
            {
                Debug.Log($"[Register] Firestore save {(ok ? "OK" : "FAIL")} for {worker.id}");
                // Small delay to ensure Firestore consistency before AppDataService loads
                StartCoroutine(NavigateAfterSave(worker));
            });
        }

        System.Collections.IEnumerator NavigateAfterSave(WorkerData worker)
        {
            yield return new WaitForSeconds(0.5f);
            NavigationManager.Instance.NavigateToRoot("UI_Dashboard");
        }

        void OnFirebaseRegisterFailed(string error)
        {
            if (registerBtn != null) { registerBtn.text = "REGISTER"; registerBtn.SetEnabled(true); }
            ShowError(error);
        }

        void SetLanguage(string lang, Button activeBtn)
        {
            selectedLanguage = lang;
            if (langEn != null) { langEn.RemoveFromClassList("lang-btn--active"); langEn.style.backgroundColor = new Color(0.96f, 0.96f, 0.96f); langEn.style.color = new Color(0.1f, 0.1f, 0.1f); }
            if (langHi != null) { langHi.RemoveFromClassList("lang-btn--active"); langHi.style.backgroundColor = new Color(0.96f, 0.96f, 0.96f); langHi.style.color = new Color(0.1f, 0.1f, 0.1f); }
            if (langSat != null) { langSat.RemoveFromClassList("lang-btn--active"); langSat.style.backgroundColor = new Color(0.96f, 0.96f, 0.96f); langSat.style.color = new Color(0.1f, 0.1f, 0.1f); }
            if (activeBtn != null) { activeBtn.AddToClassList("lang-btn--active"); activeBtn.style.backgroundColor = new Color(1f, 0.42f, 0f); activeBtn.style.color = Color.white; }
        }

        void ShowError(string message)
        {
            if (errorMsg != null) { errorMsg.text = message; errorMsg.style.display = DisplayStyle.Flex; }
        }

        void HideError()
        {
            if (errorMsg != null) errorMsg.style.display = DisplayStyle.None;
        }

        void ForceTextFieldColors(TextField tf, string name)
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
