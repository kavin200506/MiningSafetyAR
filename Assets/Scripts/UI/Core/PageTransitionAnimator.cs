using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Core
{
    public static class PageTransitionAnimator
    {
        public static IEnumerator SlideInFromRight(VisualElement incoming, float duration = 0.3f)
        {
            if (incoming == null) yield break;
            incoming.style.translate = new Translate(Length.Percent(100), 0);
            incoming.style.opacity = 1;
            incoming.style.display = DisplayStyle.Flex;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                incoming.style.translate = new Translate(Length.Percent(100f * (1f - t)), 0);
                yield return null;
            }
            incoming.style.translate = new Translate(0, 0);
        }

        public static IEnumerator SlideOutToLeft(VisualElement outgoing, float duration = 0.3f)
        {
            if (outgoing == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                outgoing.style.translate = new Translate(Length.Percent(-30f * t), 0);
                outgoing.style.opacity = 1f - 0.5f * t;
                yield return null;
            }
            outgoing.style.opacity = 0;
            outgoing.style.display = DisplayStyle.None;
        }

        public static IEnumerator FadeIn(VisualElement element, float duration = 0.3f)
        {
            if (element == null) yield break;
            element.style.opacity = 0;
            element.style.display = DisplayStyle.Flex;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.opacity = t;
                yield return null;
            }
            element.style.opacity = 1;
        }

        public static IEnumerator FadeOut(VisualElement element, float duration = 0.3f)
        {
            if (element == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.opacity = 1f - t;
                yield return null;
            }
            element.style.opacity = 0;
            element.style.display = DisplayStyle.None;
        }

        public static IEnumerator ScaleIn(VisualElement element, float duration = 0.2f)
        {
            if (element == null) yield break;
            element.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));
            element.style.opacity = 0;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.scale = new Scale(new Vector3(0.8f + 0.2f * t, 0.8f + 0.2f * t, 1f));
                element.style.opacity = t;
                yield return null;
            }
            element.style.scale = new Scale(new Vector3(1f, 1f, 1f));
            element.style.opacity = 1;
        }

        public static IEnumerator SlideUp(VisualElement element, float distance = 20f, float duration = 0.3f)
        {
            if (element == null) yield break;
            element.style.translate = new Translate(0, distance);
            element.style.opacity = 0;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.translate = new Translate(0, distance * (1f - t));
                element.style.opacity = t;
                yield return null;
            }
            element.style.translate = new Translate(0, 0);
            element.style.opacity = 1;
        }
    }
}
