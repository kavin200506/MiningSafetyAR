# 14 — Unity UI Toolkit Reference

## Quick Reference

### Creating a UIDocument at Runtime

```csharp
// In a MonoBehaviour
[SerializeField] private UIDocument uiDocument;
[SerializeField] private VisualTreeAsset pageTemplate;

void OnEnable()
{
    var root = uiDocument.rootVisualElement;
    root.Clear();
    pageTemplate.CloneTree(root);

    // Query elements
    var label = root.Q<Label>("my-label");
    var button = root.Q<Button>("my-button");
    var container = root.Q("my-container");
}
```

### Element Queries

```csharp
// By name
var el = root.Q("element-name");

// By type + name
var btn = root.Q<Button>("submit-btn");
var label = root.Q<Label>("title");

// By class
var cards = root.Query(className: "module-card").ToList();

// By type + class
var buttons = root.Query<Button>(className: "btn-primary").ToList();

// Children only (not deep)
var child = root.Q("name", 0); // depth 0
```

### USS Class Manipulation

```csharp
// Add class
element.AddToClassList("active");

// Remove class
element.RemoveFromClassList("active");

// Toggle class
if (element.ClassListContains("active"))
    element.RemoveFromClassList("active");
else
    element.AddToClassList("active");

// Check if has class
bool has = element.ClassListContains("active");
```

### Style Manipulation

```csharp
// Set display
element.style.display = DisplayStyle.Flex;
element.style.display = DisplayStyle.None;

// Set opacity
element.style.opacity = 0.5f;

// Set background color
element.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);

// Set text color (on Label)
label.style.color = new StyleColor(Color.white);

// Set width/height
element.style.width = 200;
element.style.height = 48;

// Set percentage width
element.style.width = Length.Percent(50);

// Set border radius
element.style.borderTopLeftRadius = 8;
element.style.borderTopRightRadius = 8;

// Set padding
element.style.paddingTop = 12;
element.style.paddingLeft = 16;

// Set margin
element.style.marginBottom = 16;

// Set translate (for animations)
element.style.translate = new Translate(Length.Percent(100), 0);

// Set scale
element.style.scale = new Scale(0.97f);

// Set rotate
element.style.rotate = new Rotate(45f);
```

### Event Callbacks

```csharp
// Click event
button.RegisterCallback<ClickEvent>(evt =>
{
    Debug.Log("Button clicked!");
});

// Value change (TextField, Slider, etc.)
textField.RegisterCallback<ChangeEvent<string>>(evt =>
{
    Debug.Log($"New value: {evt.newValue}");
});

// Mouse enter/leave
element.RegisterCallback<MouseEnterEvent>(evt =>
{
    element.AddToClassList("hover");
});

element.RegisterCallback<MouseLeaveEvent>(evt =>
{
    element.RemoveFromClassList("hover");
});

// Pointer down (for press feedback)
element.RegisterCallback<PointerDownEvent>(evt =>
{
    element.AddToClassList("pressed");
});

element.RegisterCallback<PointerUpEvent>(evt =>
{
    element.RemoveFromClassList("pressed");
});

// Unregister (important for cleanup)
button.UnregisterCallback<ClickEvent>(handler);
```

### Instantiating Templates

```csharp
// Clone a UXML template
var instance = template.CloneTree();

// Or instantiate (returns root)
var instance = template.Instantiate();

// Add to parent
parentElement.Add(instance);

// Clear parent
parentElement.Clear();
```

### Working with ScrollView

```csharp
// Query the scroll view
var scrollView = root.Q<ScrollView>();

// Scroll to top
scrollView.scrollOffset = Vector2.zero;

// Scroll to bottom
scrollView.scrollOffset = new Vector2(0, scrollView.contentContainer/worldBound.height);
```

### Creating Elements in C# (No UXML)

```csharp
// Create a Label
var label = new Label("Hello World");
label.style.fontSize = 14;
label.style.color = Color.white;

// Create a Button
var button = new Button(() => Debug.Log("Click!"));
button.text = "Press Me";
button.AddToClassList("btn-primary");

// Create a VisualElement (container)
var container = new VisualElement();
container.style.flexDirection = FlexDirection.Row;
container.Add(label);
container.Add(button);

// Add to root
root.Add(container);
```

## Common Patterns

### Pattern: Page with Header + Content + Bottom Nav

```csharp
public class MyPageController : PageController
{
    private HeaderController header;

    protected override void BindUI()
    {
        header = new HeaderController(root.Q("header"));
    }

    public override void OnPageEnter()
    {
        header.Configure("My Page", showBack: true);
        // Load data, populate content...
    }
}
```

### Pattern: Dynamic List of Cards

```csharp
private void PopulateList(List<ModuleData> modules)
{
    var container = root.Q("card-list");
    container.Clear();

    foreach (var mod in modules)
    {
        var card = ModuleCardFactory.Create(
            cardTemplate, mod, OnCardClicked);
        container.Add(card);
    }
}
```

### Pattern: Async Loading State

```csharp
private async void LoadData()
{
    // Show loading
    loadingIndicator.style.display = DisplayStyle.Flex;
    contentArea.style.display = DisplayStyle.None;

    // Fetch data
    var data = await FetchFromAPI();

    // Hide loading
    loadingIndicator.style.display = DisplayStyle.None;
    contentArea.style.display = DisplayStyle.Flex;

    // Populate
    PopulateContent(data);
}
```

## Gotchas & Workarounds

### 1. No `linear-gradient()` in USS

**Problem:** React uses `linear-gradient(135deg, #FF6D00, #E65100)` for gradients.

**Workaround:** Use solid colors, or create gradient textures in an image editor and use as `background-image`.

```uss
/* Can't do this: */
.logo-box { background: linear-gradient(135deg, #FF6D00, #E65100); }

/* Do this instead: */
.logo-box { background-color: #FF6D00; }
```

### 2. No `box-shadow` in USS

**Problem:** React uses `box-shadow: 0 8px 32px rgba(255, 109, 0, 0.3)`.

**Workaround:** Use border or a slightly larger background element.

```uss
/* Simulate shadow with border */
.card-elevated {
    border-color: #FF6D0033;
    border-width: 1px;
}
```

### 3. No `env(safe-area-inset-bottom)`

**Problem:** Need safe area padding for notched phones.

**Workaround:** Set via C#:

```csharp
void Start()
{
    Rect safeArea = Screen.safeArea;
    float bottomPadding = safeArea.y;
    root.Q("bottom-nav").style.paddingBottom = bottomPadding;
}
```

### 4. Emoji Rendering

**Problem:** Emoji render inconsistently across platforms in UI Toolkit Labels.

**Workaround:** Use PNG sprites for critical icons (BottomNav tabs). Emoji work fine for decorative content (module icons, certificates).

### 5. No `transition: all`

**Problem:** Can't use `transition: all 0.3s ease`.

**Workaround:** List each property:

```uss
.card {
    transition: background-color 0.2s ease,
                border-color 0.2s ease,
                scale 0.15s ease;
}
```

### 6. ScrollView Positioning

**Problem:** ScrollView in UI Toolkit needs explicit height or flex layout.

**Workaround:**

```uss
.page-container {
    flex: 1;  /* Fill parent */
    flex-direction: column;
}

.scroll-area {
    flex: 1;  /* Fill remaining space */
    overflow-y: auto;
}
```

### 7. TextField Password

**Problem:** UI Toolkit TextField doesn't have a built-in password mode.

**Workaround:**

```csharp
// Set via USS or use inputType
textField.isPasswordField = true;
```

### 8. No CSS `::before` / `::after` Pseudo-elements

**Problem:** Can't add decorative content via CSS.

**Workaround:** Create elements in UXML or C#.

### 9. No `text-align: center` on VisualElement

**Problem:** Text alignment only works on Label, not containers.

**Workaround:**

```uss
.center-content {
    align-items: center;
    justify-content: center;
}
```

### 10. Performance with Many Elements

**Problem:** UI Toolkit is slower than UGUI for very large lists.

**Workaround:** This app has max ~20 elements per page — no issue. For larger lists, use virtual scrolling or limit DOM depth.

## UIToolkit vs React Comparison

| React | UI Toolkit |
|---|---|
| `<div className="card">` | `<VisualElement class="card" />` |
| `<button onClick={fn}>` | `<Button name="btn" />` + `RegisterCallback<ClickEvent>` |
| `<input value={v} onChange={fn}>` | `<TextField value="" />` + `RegisterCallback<ChangeEvent<string>>` |
| `useState()` | Private field + `Refresh()` method |
| `useEffect()` | `OnPageEnter()` / `OnPageExit()` |
| `react-router` | `NavigationManager` |
| CSS `transition` | USS `transition` |
| CSS `@keyframes` | C# coroutine |
| `className` | `AddToClassList()` / `RemoveFromClassList()` |
| `style={{ color: 'red' }}` | `element.style.color = Color.red` |
| `props` | Constructor parameters or public properties |
| `componentDidMount` | `OnEnable()` |
