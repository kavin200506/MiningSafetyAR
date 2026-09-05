# QR Verification Page UI Specification (`UI_QRVerify`)

## 1. Page Overview

* **Page Name**: `UI_QRVerify`
* **Controller**: [QRVerifyPageController.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/QRVerifyPageController.cs)
* **Template**: `Assets/UI/Templates/Pages/QRVerifyPage.uxml`
* **Stylesheets**:
  * `Assets/UI/Styles/Theme.uss`
  * `Assets/UI/Styles/Reset.uss`
  * `Assets/UI/Styles/Components.uss`
  * `Assets/UI/Styles/Header.uss`
  * `Assets/UI/Styles/Forms.uss`
  * `Assets/UI/Styles/BottomNav.uss`
  * `Assets/UI/Styles/Icons.uss`

The QR Verification Page allows mine safety inspectors, supervisors, and compliance officers to verify the authenticity of a DGMS certificate by scanning a physical QR code with the device camera or manually typing a Certificate ID.

---

## 2. Visual Layout Map

```
┌────────────────────────────────────────────────────────┐
│ [←]                 Verify Certificate                 │  <-- Header
├────────────────────────────────────────────────────────┤
│ ┌────────────────────────────────────────────────────┐ │
│ │                  [ Search Icon ]                   │ │
│ │              Certificate Verification              │ │  <-- Search Card (#search-card)
│ │          Enter your certificate ID to verify       │ │
│ │                                                    │ │
│ │ [ Input Field: JH-FIRE-849201                    ] │ │  <-- #cert-input
│ │ [               VERIFY CERTIFICATE               ] │ │  <-- #verify-btn
│ │ [            📷 SCAN WITH CAMERA                 ] │ │  <-- #scan-cam-btn
│ │                                                    │ │
│ │ Quick Test Codes:                                  │ │
│ │ [JH-FIRE-001928]   [JH-GAS-002156]   [INVALID-000] │ │  <-- #qf-1, #qf-2, #qf-3
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │                ( Spinner Animation )               │ │  <-- Loading Card (#loading-card)
│ │                Checking certificate...             │ │      (Hidden by default)
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │              [  CERTIFICATE VALID ✅  ]            │ │  <-- Result Card (#result-card)
│ │                                                    │ │      Badge (#verify-badge)
│ │ Worker                             Ramesh Kumar    │ │
│ │ Worker ID                              W-10492     │ │  <-- Dynamic Details List
│ │ Module                 Fire & Explosion Safety     │ │      (#verify-details)
│ │ Score                                     88%      │ │
│ │ Issued                             2026-09-03      │ │
│ │ Expires                            2027-09-03      │ │
│ │ Status                                  VALID      │ │
│ │ Integrity Check              HMAC SHA-256 MATCH    │ │
│ │                                                    │ │
│ │ [                 VIEW CERTIFICATE               ] │ │  <-- #view-cert-btn
│ └────────────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────────┤
│ [Home]         [Training]     [Progress]    [Settings] │  <-- Bottom Navigation Bar
└────────────────────────────────────────────────────────┘
```

---

## 3. Exhaustive UI Component Inventory

Below is the complete element-by-element tree mapping every single UXML tag, element name (`name`), CSS class (`class`), inline style, binding source, and click handler:

| # | Element Tag | Element `name` | CSS Class (`class`) | Description / Binding Source | Callback / Action |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | `ui:VisualElement` | *None* | `page-container` | Main root page container. | *N/A* |
| **2** | `ui:VisualElement` | *None* | `header` | Sticky top navigation bar. | *N/A* |
| **3** | `ui:Button` | `back-btn` | `header__back-btn` | Back navigation button. | `NavigationManager.Instance.GoBack()` |
| **4** | `ui:VisualElement` | *None* | `icon-back` | 20x20 vector back arrow icon. | *N/A* |
| **5** | `ui:Label` | *None* | `header__title` | Page title label (`"Verify Certificate"`). | Static text |
| **6** | `ui:VisualElement` | *None* | `header__right-action` | Spacer element for header balance. | *N/A* |
| **7** | `ui:ScrollView` | *None* | `scroll-area` | Scrollable body area container. | Scroll gesture |
| **8** | `ui:VisualElement` | `search-card` | `card` | Card container holding search input and options (`padding: 16px; align-items: center;`). | Visual Container |
| **9** | `ui:VisualElement` | *None* | `icon-search` | Search icon (28x28). | Visual Icon |
| **10** | `ui:Label` | *None* | *Inline Style* | Title label (`"Certificate Verification"`, font: 16px 700 bold `#1A1A1A`). | Static text |
| **11** | `ui:Label` | *None* | *Inline Style* | Subtitle label (`"Enter your certificate ID to verify"`, font: 13px `#999999`). | Static text |
| **12** | `ui:TextField` | `cert-input` | `input-field` | Text input box for entering Certificate ID manually. | Registers `FocusInEvent` to enforce high contrast text colors (`#1A1A1A`) |
| **13** | `ui:Button` | `verify-btn` | `btn-primary` | Primary action button (`"VERIFY"`). | Triggers `OnVerify()` search logic |
| **14** | `ui:Button` | `scan-cam-btn` | `btn-secondary` | Camera QR scanner toggle button (`"SCAN WITH CAMERA"`). | Opens live camera viewfinder overlay for QR decoding |
| **15** | `ui:VisualElement` | `quick-fill` | *Inline Style* | Quick fill buttons container (`flex-direction: row; margin-top: 12px; width: 100%;`). | Visual Row |
| **16** | `ui:Button` | `qf-1` | `quick-fill-btn` | Quick fill button 1 (`text="JH-FIRE-001928"`). | Populates `certInput.value = "JH-FIRE-001928"` |
| **17** | `ui:Button` | `qf-2` | `quick-fill-btn` | Quick fill button 2 (`text="JH-GAS-002156"`). | Populates `certInput.value = "JH-GAS-002156"` |
| **18** | `ui:Button` | `qf-3` | `quick-fill-btn` | Quick fill button 3 (`text="INVALID-000"`). | Populates `certInput.value = "INVALID-000"` |
| **19** | `ui:VisualElement` | `loading-card` | `card` | Card displayed during async verification delay (`display: none; align-items: center;`). | Visual Container |
| **20** | `ui:VisualElement` | *None* | *Inline Style* | Circular CSS spinner element (24x24px, orange border). | CSS Rotation Animation |
| **21** | `ui:Label` | *None* | *Inline Style* | Loading text label (`"Checking certificate..."`, font: 12px `#999999`). | Static text |
| **22** | `ui:VisualElement` | `result-card` | `card` | Card displaying verification results (`display: none; padding: 16px;`). | Visual Container |
| **23** | `ui:Label` | `verify-badge` | `badge` | Verification status badge (`"CERTIFICATE VALID"`, `"CERTIFICATE EXPIRED"`, or `"INVALID CERTIFICATE"`). | Toggles classes `badge--pass`, `badge--warning`, `badge--fail` |
| **24** | `ui:VisualElement` | `verify-details` | *Inline Style* | Container dynamically populated with key-value parameter rows. | Container |
| **25** | `ui:Button` | `view-cert-btn` | `btn-primary` | Navigation button to view full certificate card (`"VIEW CERTIFICATE"`). | `NavigationManager.Instance.NavigateTo("UI_Certificate", moduleId)` |
| **26** | `ui:VisualElement` | *None* | *Inline Style* | Bottom spacing buffer (`height: 80px;`). | Spacer |
| **27** | `ui:VisualElement` | `bottom-nav` | `bottom-nav` | Sticky bottom nav container. | Visual Bar |
| **28** | `ui:Button` | `tab-home` | `bottom-nav__tab` | Tab Home button. | `NavigationManager.Instance.NavigateToTab("UI_Dashboard")` |
| **29** | `ui:Button` | `tab-training` | `bottom-nav__tab` | Tab Training button. | `NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue")` |
| **30** | `ui:Button` | `tab-progress` | `bottom-nav__tab` | Tab Progress button. | `NavigationManager.Instance.NavigateToTab("UI_Progress")` |
| **31** | `ui:Button` | `tab-settings` | `bottom-nav__tab` | Tab Settings button. | `NavigationManager.Instance.NavigateToTab("UI_Settings")` |

---

## 4. Verification Lifecycle & State Transitions

### State Machine Diagram

```
                     ┌──────────────────┐
                     │  Initial State   │
                     │ (Input Card Only)│
                     └────────┬─────────┘
                              │ User clicks "VERIFY"
                              ▼
                     ┌──────────────────┐
                     │  Loading State   │
                     │  (#loading-card) │
                     └────────┬─────────┘
                              │ Async delay (800ms)
                              ▼
                    Is Certificate Found?
                    ├── YES ──► Check HMAC Hash
                    │           ├── MISMATCH ──► Show Invalid State
                    │           └── MATCH    ──► Check Expiration Date
                    │                            ├── Expired ──► Show Expired State
                    │                            └── Valid   ──► Show Valid State
                    └── NO  ──► Show Invalid State
```

---

## 5. Dynamic Row Rendering Implementation

When a certificate record is verified, rows are injected dynamically into `#verify-details`:

```csharp
void AddDetail(string label, string value)
{
    var row = new VisualElement();
    row.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
    row.style.marginBottom = 8;

    var l = new Label(label);
    l.style.flexGrow = 1;
    l.style.color = new StyleColor(new Color(0.44f, 0.44f, 0.44f));
    l.style.fontSize = 12;

    var v = new Label(value);
    v.style.color = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
    v.style.fontSize = 12;
    v.style.unityFontStyleAndWeight = FontStyle.Bold;

    row.Add(l);
    row.Add(v);
    if (verifyDetails != null) verifyDetails.Add(row);
}
```

### Result Detail Output Schema
* **Worker**: `Ramesh Kumar`
* **Worker ID**: `W-10492`
* **Module**: `Fire & Explosion Safety`
* **Score**: `88%`
* **Issued**: `2026-09-03`
* **Expires**: `2027-09-03`
* **Status**: `VALID`
* **Integrity**: `HMAC-SHA256 MATCHED`
