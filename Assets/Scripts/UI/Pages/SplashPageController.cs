using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;

namespace MiningSafetyAR.UI.Pages
{
    public class SplashPageController : PageController
    {
        VisualElement progressFill;
        Label statusLabel;

        protected override void BindUI()
        {
            progressFill = root.Q("progress-fill");
            statusLabel = root.Q<Label>("status");
            Debug.Log($"[Splash] BindUI root={root != null} progressFill={progressFill != null} status={statusLabel != null} childCount={root?.childCount}");
            if (progressFill == null) Debug.LogWarning("[Splash] progress-fill not found — check SplashPage.uxml name='progress-fill'");
        }

        public override void OnPageEnter()
        {
            Debug.Log($"[Splash] OnPageEnter — starting animation, Nav current={NavigationManager.Instance?.CurrentScene}");
            // Stop any previous run
            StopAllCoroutines();
            StartCoroutine(AnimateSplash());
        }

        IEnumerator AnimateSplash()
        {
            float progress = 0f;
            if (statusLabel != null) statusLabel.text = "Loading...";
            if (progressFill != null) progressFill.style.width = Length.Percent(0);

            while (progress < 100f)
            {
                progress += 4f;
                if (progressFill != null) progressFill.style.width = Length.Percent(Mathf.Min(progress, 100f));
                yield return new WaitForSeconds(0.05f);
            }

            if (statusLabel != null) statusLabel.text = "Ready";
            yield return new WaitForSeconds(0.3f);

            // If already logged in (Firebase auto-login), go to Dashboard
            try
            {
                bool isLoggedIn = Firebase.FirebaseAuthManager.Instance != null && Firebase.FirebaseAuthManager.Instance.IsLoggedIn;
                var nav = NavigationManager.Instance;
                if (nav != null)
                {
                    if (isLoggedIn) nav.NavigateToRoot("UI_Dashboard");
                    else nav.NavigateTo("UI_Login");
                }
                else
                {
                    Debug.LogWarning("[Splash] NavigationManager missing — using SceneManager fallback");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(isLoggedIn ? "UI_Dashboard" : "UI_Login");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Splash] Navigate failed: {ex}");
                UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Login");
            }
        }
    }
}
