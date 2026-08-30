# 07 — Animation System

## USS Transitions Available in Unity 6

| Property | Supported | Use Case |
|---|---|---|
| `width` | Yes | Progress bars, score bars |
| `height` | Yes | Expand/collapse |
| `translate` | Yes | Page slides, element movement |
| `rotate` | Yes | Loading spinners |
| `scale` | Yes | Press feedback, badges |
| `opacity` | Yes | Fade in/out, overlays |
| `background-color` | Yes | Button hover, active states |
| `border-color` | Yes | Focus states |
| `color` | Yes | Text color changes (timer) |

## USS Transitions NOT Available

| Feature | Workaround |
|---|---|
| CSS `@keyframes` | C# coroutine manipulating styles |
| `animation` shorthand | C# coroutine |
| `transition: all` | Must list each property individually |
| `box-shadow` | Simulate with border or extra element |
| `linear-gradient()` | Use solid colors or pre-baked textures |

## C# Page Transition Animator

```csharp
namespace MiningSafetyAR.UI
{
    public static class PageTransitionAnimator
    {
        public static IEnumerator SlideInFromRight(
            VisualElement incoming, float duration = 0.3f)
        {
            incoming.style.translate = new Translate(Length.Percent(100), 0);
            incoming.style.opacity = 1;
            incoming.style.display = DisplayStyle.Flex;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);

                incoming.style.translate =
                    new Translate(Length.Percent(100f * (1f - t)), 0);

                yield return null;
            }

            incoming.style.translate = new Translate(0, 0);
        }

        public static IEnumerator SlideOutToLeft(
            VisualElement outgoing, float duration = 0.3f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);

                outgoing.style.translate =
                    new Translate(Length.Percent(-30f * t), 0);
                outgoing.style.opacity = 1f - 0.5f * t;

                yield return null;
            }

            outgoing.style.opacity = 0;
            outgoing.style.display = DisplayStyle.None;
        }

        public static IEnumerator FadeIn(
            VisualElement element, float duration = 0.3f)
        {
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

        public static IEnumerator FadeOut(
            VisualElement element, float duration = 0.3f)
        {
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

        public static IEnumerator ScaleIn(
            VisualElement element, float duration = 0.2f)
        {
            element.style.scale = new Scale(0.8f);
            element.style.opacity = 0;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.scale = new Scale(0.8f + 0.2f * t);
                element.style.opacity = t;
                yield return null;
            }

            element.style.scale = new Scale(1f);
            element.style.opacity = 1;
        }

        public static IEnumerator SlideUp(
            VisualElement element, float distance = 20f,
            float duration = 0.3f)
        {
            element.style.translate = new Translate(0, distance);
            element.style.opacity = 0;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                element.style.translate =
                    new Translate(0, distance * (1f - t));
                element.style.opacity = t;
                yield return null;
            }

            element.style.translate = new Translate(0, 0);
            element.style.opacity = 1;
        }
    }
}
```

## Animation Implementations by Page

### SplashPage
```csharp
// Progress bar filling animation
private IEnumerator AnimateProgressBar(VisualElement fill,
                                         VisualElement statusLabel)
{
    float progress = 0f;
    while (progress < 100f)
    {
        progress += 4f;
        fill.style.width = Length.Percent(Mathf.Min(progress, 100f));
        yield return new WaitForSeconds(0.05f);
    }

    statusLabel.text = "Ready";
    yield return new WaitForSeconds(0.3f);
    NavigationManager.Instance.NavigateTo("login");
}
```

### DashboardPage
```csharp
// Staggered card entrance
private IEnumerator AnimateModuleCards(List<VisualElement> cards)
{
    for (int i = 0; i < cards.Count; i++)
    {
        cards[i].style.opacity = 0;
        cards[i].style.translate = new Translate(0, 20);
    }

    for (int i = 0; i < cards.Count; i++)
    {
        yield return new WaitForSeconds(0.1f);
        StartCoroutine(PageTransitionAnimator.SlideUp(cards[i]));
    }
}
```

### ARSimulationPage
```csharp
// Scan line animation
private IEnumerator AnimateScanLine(VisualElement scanLine)
{
    while (isScanning)
    {
        scanLine.style.translate = new Translate(0, -100);
        float elapsed = 0f;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 1.5f;
            scanLine.style.translate =
                new Translate(0, Mathf.Lerp(-100, 100, t));
            yield return null;
        }
    }
}

// Timer countdown with color changes
private IEnumerator AnimateTimer(Label timerLabel, float timeLeft)
{
    while (timeLeft > 0)
    {
        timeLeft -= Time.deltaTime;
        timerLabel.text = $"{Mathf.CeilToInt(timeLeft)}s";

        // Color transitions via USS classes
        timerLabel.RemoveFromClassList("timer-green");
        timerLabel.RemoveFromClassList("timer-yellow");
        timerLabel.RemoveFromClassList("timer-red");

        if (timeLeft > 60)
            timerLabel.AddToClassList("timer-green");
        else if (timeLeft > 30)
            timerLabel.AddToClassList("timer-yellow");
        else
            timerLabel.AddToClassList("timer-red");

        yield return null;
    }
}
```

### AssessmentPage
```csharp
// Option selection feedback
private void OnOptionSelected(VisualElement option, bool isCorrect)
{
    option.AddToClassList(
        isCorrect ? "option--correct" : "option--incorrect");

    // Show feedback banner
    var banner = root.Q("feedback-banner");
    banner.Q<Label>("feedback-icon").text =
        isCorrect ? "✓" : "✗";
    banner.Q<Label>("feedback-text").text =
        isCorrect ? "Correct!" : "Incorrect";
    banner.AddToClassList(
        isCorrect ? "feedback--correct" : "feedback--incorrect");

    StartCoroutine(PageTransitionAnimator.SlideUp(banner));
}
```

### Toggle Switch
```csharp
// USS handles this via transition:
// .toggle-switch { transition: background-color 0.2s ease; }
// .toggle-switch__knob { transition: translate 0.2s ease; }
// C# just toggles the class:
toggle.AddToClassList("toggle-switch--on");
// or
toggle.RemoveFromClassList("toggle-switch--on");
```

### Progress Bar (All Pages)
```csharp
// USS handles the animation:
// .progress-fill { transition: width 0.6s ease; }
// C# sets the width:
fill.style.width = Length.Percent(68f);
// The USS transition animates it automatically
```

## Safe Area Handling

```csharp
// Set safe area padding on app start
private void ApplySafeArea(VisualElement root)
{
    Rect safeArea = Screen.safeArea;
    float bottomPadding = safeArea.y;

    // Apply to bottom nav
    var bottomNav = root.Q("bottom-nav");
    if (bottomNav != null)
    {
        bottomNav.style.paddingBottom = bottomPadding;
    }

    // Apply to page containers
    var pageContainers = root.Query(className: "page-container").ToList();
    foreach (var container in pageContainers)
    {
        container.style.paddingBottom = bottomPadding;
    }
}
```

## Performance Guidelines

1. **Max 10 simultaneous animations** on mobile
2. **Use `Time.unscaledDeltaTime`** for UI animations (works in pause)
3. **Prefer USS transitions** over C# coroutines where possible
4. **Cache VisualElement references** — avoid `Q<>()` in loops
5. **Use `display: none`** to remove off-screen elements from layout
6. **Avoid animating `translate` on ScrollView children** — causes jitter
