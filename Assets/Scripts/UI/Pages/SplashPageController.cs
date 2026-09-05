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

            void GoTo(bool loggedIn)
            {
                try
                {
                    var nav = NavigationManager.Instance;
                    if (nav != null)
                    {
                        if (loggedIn) nav.NavigateToRoot("UI_Dashboard");
                        else nav.NavigateTo("UI_Login");
                    }
                    else
                    {
                        Debug.LogWarning("[Splash] NavigationManager missing — using SceneManager fallback");
                        UnityEngine.SceneManagement.SceneManager.LoadScene(loggedIn ? "UI_Dashboard" : "UI_Login");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[Splash] Navigate failed: {ex}");
                    UnityEngine.SceneManagement.SceneManager.LoadScene("UI_Login");
                }
            }

            var auth = Firebase.FirebaseAuthManager.Instance;
            if (auth != null && auth.IsLoggedIn)
            {
                // Already logged in this process (e.g. scene reload without an app restart).
                GoTo(true);
            }
            else if (auth != null)
            {
                // Not logged in yet this launch — try restoring a persisted session (silent refresh
                // if online, or an offline fallback using the last cached worker if not) before
                // falling back to the login screen. See FirebaseAuthManager.TryRestoreSession.
                if (statusLabel != null) statusLabel.text = "Restoring session...";
                auth.TryRestoreSession(restored => GoTo(restored));
            }
            else
            {
                Debug.LogWarning("[Splash] FirebaseAuthManager missing — going to Login.");
                GoTo(false);
            }
        }
    }
}
