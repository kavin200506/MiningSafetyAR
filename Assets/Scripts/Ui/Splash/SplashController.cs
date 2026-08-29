using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace MiningSafetyAR.UI.Splash
{
    public class SplashController : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Slider loadingBar;
        [SerializeField] private TextMeshProUGUI loadingText;
        
        [Header("Settings")]
        [SerializeField] private float fakeLoadingTime = 3f;
        [SerializeField] private string nextSceneName = "Language";

        private void Start()
        {
            // Initialize slider to zero
            if (loadingBar != null)
            {
                loadingBar.value = 0f;
            }

            // Start the fake loading sequence immediately
            StartCoroutine(LoadingSequence());
        }

        private IEnumerator LoadingSequence()
        {
            float elapsedTime = 0f;

            while (elapsedTime < fakeLoadingTime)
            {
                // Increment time and calculate percentage (0 to 1)
                elapsedTime += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedTime / fakeLoadingTime);

                // Update Slider Visuals
                if (loadingBar != null)
                {
                    loadingBar.value = progress;
                }

                // Update Text Visuals (Animates the dots: Loading... Loading....)
                if (loadingText != null)
                {
                    int dots = Mathf.FloorToInt((elapsedTime % 1f) * 4f);
                    loadingText.text = "Loading Experience" + new string('.', dots);
                }

                yield return null; // Wait for the next frame
            }

            // Ensure it's visually full before jumping to the next scene
            if (loadingBar != null) loadingBar.value = 1f;
            yield return new WaitForSeconds(0.25f);

            // Transition to the Language Scene
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.LogWarning("SplashController: Next Scene Name is not set!");
            }
        }
    }
}
