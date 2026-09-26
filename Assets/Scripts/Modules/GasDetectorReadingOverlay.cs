using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Full-screen "camera focus" overlay shown while the gas detector is held. Each grab advances
    /// to the next reading image (reading_1, reading_2, ...) and wraps back to the first once the
    /// list is exhausted, so repeated grabs cycle through the same sequence instead of repeating
    /// or randomizing — the worker sees a fresh reading each time they check the detector.
    ///
    /// The reveal is deliberately delayed and faded rather than popped in instantly: BeltItemGrabController
    /// passes its own grab-animation duration as the delay, so the reading only appears once the detector
    /// has visually finished lifting into view — like the worker is actually waiting to focus on the readout,
    /// not having a flat picture slapped over their screen the instant they tap.
    /// </summary>
    public class GasDetectorReadingOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private CanvasGroup overlayCanvasGroup;
        [SerializeField] private RawImage readingImage;
        [SerializeField] private Texture2D[] readings;

        [Header("Focus Reveal (Live Tunable)")]
        [Tooltip("How long the fade + focus-in takes once the reveal starts.")]
        [SerializeField] private float fadeInDuration = 0.25f;
        [Tooltip("How long the fade-out takes when the detector is put back.")]
        [SerializeField] private float fadeOutDuration = 0.15f;
        [Tooltip("Starting scale of the reading image during the focus-in pulse (1 = no pulse).")]
        [SerializeField] private float focusStartScale = 0.92f;

        private int nextReadingIndex;
        private Coroutine activeCoroutine;

        /// <summary>Starts the next reading in the cycle, revealed after <paramref name="delay"/> seconds
        /// (pass the grab animation's own duration so the two stay in sync).</summary>
        public void ShowNextReading(float delay)
        {
            if (readings == null || readings.Length == 0 || readingImage == null || overlayRoot == null) return;

            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(RevealAfterDelay(Mathf.Max(0f, delay)));
        }

        public void Hide()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            if (overlayRoot == null) return;

            activeCoroutine = overlayRoot.activeSelf ? StartCoroutine(FadeOutAndDisable()) : null;
            if (activeCoroutine == null) overlayRoot.SetActive(false);
        }

        private IEnumerator RevealAfterDelay(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            readingImage.texture = readings[nextReadingIndex];
            nextReadingIndex = (nextReadingIndex + 1) % readings.Length;

            overlayRoot.SetActive(true);

            Transform imageTransform = readingImage.rectTransform;
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
                if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = t;
                imageTransform.localScale = Vector3.one * Mathf.Lerp(focusStartScale, 1f, t);
                yield return null;
            }

            if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 1f;
            imageTransform.localScale = Vector3.one;
            activeCoroutine = null;
        }

        private IEnumerator FadeOutAndDisable()
        {
            float startAlpha = overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 1f;
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = fadeOutDuration > 0f ? Mathf.Clamp01(elapsed / fadeOutDuration) : 1f;
                if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            overlayRoot.SetActive(false);
            if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 0f;
            activeCoroutine = null;
        }
    }
}
