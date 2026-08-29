# 04 — Reusable Components

## Component Library

### 1. BottomNav (BottomNav.uxml)

**Purpose:** 4-tab bottom navigation bar
**Visibility:** Hidden on Splash, Login, Register, LearningContent, ARSimulation, Assessment

**UXML Structure:**
```xml
<VisualElement class="bottom-nav" name="bottom-nav">
    <Button class="bottom-nav__tab bottom-nav__tab--active" name="tab-home">
        <Image class="bottom-nav__tab-icon" name="icon-home" />
        <Label class="bottom-nav__tab-label" text="Home" />
    </Button>
    <Button class="bottom-nav__tab" name="tab-training">
        <Image class="bottom-nav__tab-icon" name="icon-training" />
        <Label class="bottom-nav__tab-label" text="Training" />
    </Button>
    <Button class="bottom-nav__tab" name="tab-progress">
        <Image class="bottom-nav__tab-icon" name="icon-progress" />
        <Label class="bottom-nav__tab-label" text="Progress" />
    </Button>
    <Button class="bottom-nav__tab" name="tab-settings">
        <Image class="bottom-nav__tab-icon" name="icon-settings" />
        <Label class="bottom-nav__tab-label" text="Settings" />
    </Button>
</VisualElement>
```

**C# Controller:**
```csharp
public class BottomNavController : MonoBehaviour
{
    [SerializeField] UIDocument document;

    private VisualElement root;
    private Button[] tabs;
    private int activeIndex = 0;

    private static readonly string[] pageRoutes = {
        "dashboard", "training_catalogue", "progress", "settings"
    };

    void OnEnable()
    {
        root = document.rootVisualElement;
        tabs = new Button[4];
        tabs[0] = root.Q<Button>("tab-home");
        tabs[1] = root.Q<Button>("tab-training");
        tabs[2] = root.Q<Button>("tab-progress");
        tabs[3] = root.Q<Button>("tab-settings");

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].RegisterCallback<ClickEvent>(evt => OnTabClicked(index));
        }
    }

    private void OnTabClicked(int index)
    {
        NavigationManager.Instance.NavigateToRoot(pageRoutes[index]);
    }

    public void SetActiveTab(int index)
    {
        if (index < 0 || index >= tabs.Length) return;
        tabs[activeIndex].RemoveFromClassList("bottom-nav__tab--active");
        activeIndex = index;
        tabs[activeIndex].AddToClassList("bottom-nav__tab--active");
    }

    public void SetVisibility(bool visible)
    {
        root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
```

---

### 2. Header (Header.uxml)

**Purpose:** Sticky page header with optional back button and title

**UXML Structure:**
```xml
<VisualElement class="header" name="header">
    <Button class="header__back-btn" name="back-button">
        <Image class="header__back-icon" name="back-icon" />
    </Button>
    <Label class="header__title" name="title" text="" />
    <VisualElement class="header__right-action" name="right-action" />
</VisualElement>
```

**C# Controller:**
```csharp
public class HeaderController
{
    private readonly VisualElement root;
    private readonly Button backButton;
    private readonly Label titleLabel;
    private readonly VisualElement rightActionSlot;

    public HeaderController(VisualElement headerRoot)
    {
        root = headerRoot;
        backButton = headerRoot.Q<Button>("back-button");
        titleLabel = headerRoot.Q<Label>("title");
        rightActionSlot = headerRoot.Q("right-action");

        backButton?.RegisterCallback<ClickEvent>(evt =>
            NavigationManager.Instance.GoBack());
    }

    public void Configure(string title, bool showBack,
                          VisualElement rightAction = null)
    {
        titleLabel.text = title;
        backButton.style.display =
            showBack ? DisplayStyle.Flex : DisplayStyle.None;
        rightActionSlot.Clear();
        if (rightAction != null)
            rightActionSlot.Add(rightAction);
    }
}
```

---

### 3. ModuleCard (ModuleCard.uxml)

**Purpose:** Training module card with icon, title, meta, status, progress

**UXML Structure:**
```xml
<VisualElement class="card-interactive module-card" name="module-card">
    <VisualElement class="module-card__icon-box" name="icon-box">
        <Label class="module-card__icon-emoji" name="icon-emoji" text="" />
    </VisualElement>
    <VisualElement class="module-card__info">
        <Label class="module-card__title" name="module-title" text="" />
        <Label class="module-card__meta" name="module-meta" text="" />
        <VisualElement class="module-card__progress-track" name="progress-track">
            <VisualElement class="progress-fill" name="progress-fill" />
        </VisualElement>
        <Label class="module-card__best-score" name="best-score" text="" />
    </VisualElement>
    <Label class="module-card__status-badge" name="status-badge" text="" />
</VisualElement>
```

**C# Factory:**
```csharp
public static class ModuleCardFactory
{
    public static VisualElement Create(VisualTreeAsset template,
                                        ModuleData mod,
                                        Action<ModuleData> onClick)
    {
        var card = template.Instantiate();

        // Populate data
        card.Q<Label>("icon-emoji").text = mod.iconEmoji;
        card.Q<Label>("module-title").text = mod.title;
        card.Q<Label>("module-meta").text =
            $"{mod.duration} · {mod.difficulty}";

        // Status badge
        var badge = card.Q<Label>("status-badge");
        badge.text = GetStatusText(mod.status);
        badge.AddToClassList($"badge--{mod.status.ToString().ToLower()}");

        // Progress bar
        var fill = card.Q("progress-fill");
        fill.style.width = Length.Percent(mod.progress);

        // Best score
        var bestScore = card.Q("best-score");
        if (mod.bestScore > 0)
        {
            bestScore.style.display = DisplayStyle.Flex;
            card.Q<Label>("best-score").text = $"Best: {mod.bestScore}%";
        }
        else
        {
            bestScore.style.display = DisplayStyle.None;
        }

        // Icon box background (20% opacity of module color)
        var iconBox = card.Q("icon-box");
        iconBox.style.backgroundColor = HexToColor(mod.color + "33");

        // Locked state
        if (mod.status == ModuleStatus.Locked)
        {
            card.AddToClassList("module-card--locked");
            card.style.opacity = 0.5f;
            card.pickingMode = PickingMode.Ignore;
        }
        else
        {
            card.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(mod));
        }

        return card;
    }

    private static string GetStatusText(ModuleStatus status) => status switch
    {
        ModuleStatus.Completed => "Completed",
        ModuleStatus.InProgress => "In Progress",
        ModuleStatus.NotStarted => "Not Started",
        ModuleStatus.Locked => "Locked",
        _ => ""
    };

    private static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;
            return new Color(r, g, b, 1f);
        }
        if (hex.Length == 8)
        {
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;
            float a = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) / 255f;
            return new Color(r, g, b, a);
        }
        return Color.white;
    }
}
```

---

### 4. ProgressBar (ProgressBar.uxml)

**Purpose:** Animated horizontal progress bar

**UXML Structure:**
```xml
<VisualElement class="progress-track" name="progress-track">
    <VisualElement class="progress-fill" name="progress-fill" />
</VisualElement>
```

**C# Helper:**
```csharp
public static class ProgressBarHelper
{
    public static void SetProgress(VisualElement track, float value,
                                    string color = null)
    {
        var fill = track.Q("progress-fill");
        float clamped = Mathf.Clamp(value, 0f, 100f);

        // Animate via USS transition
        fill.style.width = Length.Percent(clamped);

        // Custom color
        if (color != null)
        {
            fill.style.backgroundColor = HexToColor(color);
        }

        // Green at 100%
        if (clamped >= 100f)
        {
            fill.AddToClassList("progress-fill--complete");
        }
        else
        {
            fill.RemoveFromClassList("progress-fill--complete");
        }
    }

    public static void SetProgressImmediate(VisualElement track, float value)
    {
        var fill = track.Q("progress-fill");
        float clamped = Mathf.Clamp(value, 0f, 100f);

        // Remove transition temporarily for instant set
        fill.style.transitionProperty = new List<StylePropertyName>();
        fill.style.width = Length.Percent(clamped);
        fill.style.transitionProperty = null; // Restore transitions
    }
}
```

---

### 5. ScoreBar (ScoreBar.uxml)

**Purpose:** Labeled horizontal score bar with percentage

**UXML Structure:**
```xml
<VisualElement class="score-bar" name="score-bar">
    <VisualElement class="score-bar__header">
        <Label class="score-bar__label" name="score-label" text="" />
        <Label class="score-bar__value" name="score-value" text="" />
    </VisualElement>
    <VisualElement class="progress-track" name="track">
        <VisualElement class="progress-fill" name="progress-fill" />
    </VisualElement>
</VisualElement>
```

**C# Helper:**
```csharp
public static class ScoreBarHelper
{
    public static void Configure(VisualElement root, string label,
                                  int value, int maxValue = 100)
    {
        root.Q<Label>("score-label").text = label;
        root.Q<Label>("score-value").text = $"{value}%";

        float pct = (float)value / maxValue * 100f;
        string color = pct >= 80f ? "#4CAF50"
                     : pct >= 60f ? "#FF6D00"
                     : "#F44336";

        ProgressBarHelper.SetProgress(root.Q("track"), pct, color);
    }
}
```

---

### 6. ToggleSwitch (ToggleSwitch.uxml)

**Purpose:** Animated on/off toggle switch

**UXML Structure:**
```xml
<VisualElement class="toggle-switch" name="toggle">
    <VisualElement class="toggle-switch__knob" name="knob" />
</VisualElement>
```

**C# Controller:**
```csharp
public class ToggleSwitchController
{
    private readonly VisualElement toggle;
    private readonly VisualElement knob;
    private bool isOn;

    public event Action<bool> OnToggled;

    public ToggleSwitchController(VisualElement toggleRoot)
    {
        toggle = toggleRoot;
        knob = toggleRoot.Q("knob");

        toggle.RegisterCallback<ClickEvent>(evt =>
        {
            isOn = !isOn;
            UpdateVisual();
            OnToggled?.Invoke(isOn);
        });
    }

    public void SetValue(bool value)
    {
        isOn = value;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (isOn)
            toggle.AddToClassList("toggle-switch--on");
        else
            toggle.RemoveFromClassList("toggle-switch--on");
    }
}
```

---

### 7. StatusBadge (StatusBadge.uxml)

**Purpose:** Color-coded status pill

**UXML Structure:**
```xml
<Label class="badge" name="status-badge" text="" />
```

**C# Helper:**
```csharp
public static class StatusBadgeHelper
{
    public static void SetStatus(VisualElement badge, ModuleStatus status)
    {
        // Remove all status classes
        badge.RemoveFromClassList("badge--completed");
        badge.RemoveFromClassList("badge--in_progress");
        badge.RemoveFromClassList("badge--not_started");
        badge.RemoveFromClassList("badge--locked");

        // Add current status class
        string className = $"badge--{status.ToString().ToLower()}";
        badge.AddToClassList(className);

        // Set text
        badge.text = status switch
        {
            ModuleStatus.Completed => "Completed",
            ModuleStatus.InProgress => "In Progress",
            ModuleStatus.NotStarted => "Not Started",
            ModuleStatus.Locked => "Locked",
            _ => ""
        };
    }
}
```

---

### 8. MetaPill (MetaPill.uxml)

**Purpose:** Small info pill (duration, difficulty, domain)

**UXML Structure:**
```xml
<VisualElement class="pill meta-pill" name="meta-pill">
    <Label class="meta-pill__text" name="meta-text" text="" />
</VisualElement>
```

---

### 9. OptionButton (OptionButton.uxml)

**Purpose:** Quiz option with A/B/C/D badge

**UXML Structure:**
```xml
<Button class="option-button" name="option-btn">
    <VisualElement class="option-button__badge" name="option-badge">
        <Label class="option-button__letter" name="option-letter" text="A" />
    </VisualElement>
    <Label class="option-button__text" name="option-text" text="" />
</Button>
```

**States:**
- Default: `#262626` bg, gray border
- Selected (unanswered): orange tint
- Correct (answered): green highlight
- Incorrect selected: red highlight

---

### 10. FeedbackBanner (FeedbackBanner.uxml)

**Purpose:** Correct/incorrect answer feedback

**UXML Structure:**
```xml
<VisualElement class="feedback-banner" name="feedback-banner">
    <Label class="feedback-banner__icon" name="feedback-icon" text="" />
    <Label class="feedback-banner__text" name="feedback-text" text="" />
</VisualElement>
```

---

### 11. TableRow (TableRow.uxml)

**Purpose:** Generic table row for attempt history

**UXML Structure:**
```xml
<VisualElement class="table-row" name="table-row">
    <Label class="table-cell table-col-num" name="row-num" text="1" />
    <Label class="table-cell table-col-date" name="row-date" text="" />
    <Label class="table-cell table-col-score" name="row-score" text="" />
    <Label class="table-cell table-col-status" name="row-status" text="" />
</VisualElement>
```

---

### 12. PhaseDot (PhaseDot.uxml)

**Purpose:** Single dot for AR phase indicator

**UXML Structure:**
```xml
<VisualElement class="phase-dot" name="phase-dot" />
```

**States:**
- Inactive: `#333` bg
- Active: `#FF6D00` bg
- Completed: `#4CAF50` bg
