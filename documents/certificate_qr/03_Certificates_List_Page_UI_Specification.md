# Certificates Portfolio List Page UI Specification (`UI_CertificatesList`)

## 1. Page Overview

* **Page Name**: `UI_CertificatesList`
* **Controller**: [CertificatesListPageController.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/CertificatesListPageController.cs)
* **Template**: `Assets/UI/Templates/Pages/CertificatesListPage.uxml`
* **Stylesheets**:
  * `Assets/UI/Styles/Theme.uss`
  * `Assets/UI/Styles/Reset.uss`
  * `Assets/UI/Styles/Components.uss`
  * `Assets/UI/Styles/Header.uss`
  * `Assets/UI/Styles/Forms.uss`
  * `Assets/UI/Styles/BottomNav.uss`
  * `Assets/UI/Styles/Icons.uss`

The Certificates Portfolio List Page provides mining workers with a centralized, searchable catalogue of all safety certificates they have earned across different industrial domains (e.g. *Fire & Explosion Safety*, *Gas Leak & Confined Space*).

---

## 2. Visual Layout Map

```
┌────────────────────────────────────────────────────────┐
│ [←]               My Safety Certificates               │  <-- Header
├────────────────────────────────────────────────────────┤
│ ┌────────────────────────────────────────────────────┐ │
│ │ [ 🔍 Search by module or Cert ID...              ] │ │  <-- #cert-search-input
│ │                                                    │ │
│ │ [ All ]  [ Fire Safety ]  [ Gas Leak ]  [ Valid ]  │ │  <-- Filter Pills
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ 2 Certificates Earned        Tap card to view full cert│  <-- Counter Header
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ [🛡️]  Fire & Explosion Safety        88%  PASSED   │ │  <-- Certificate Card 1
│ │      ID: JH-FIRE-849201 • Issued: 2026-09-03       │ │      (Clickable)
│ └────────────────────────────────────────────────────┘ │
│ ┌────────────────────────────────────────────────────┐ │
│ │ [🛡️]  Gas Leak & Confined Space      92%  PASSED   │ │  <-- Certificate Card 2
│ │      ID: JH-GAS-002156 • Issued: 2026-09-03        │ │      (Clickable)
│ └────────────────────────────────────────────────────┘ │
├────────────────────────────────────────────────────────┤
│ [Home]         [Training]     [Progress]    [Settings] │  <-- Bottom Navigation Bar
└────────────────────────────────────────────────────────┘
```

---

## 3. Exhaustive UI Component Inventory

| # | Element Tag | Element `name` | CSS Class (`class`) | Description / Binding Source | Callback / Action |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | `ui:VisualElement` | *None* | `page-container` | Root page container. | *N/A* |
| **2** | `ui:VisualElement` | *None* | `header` | Sticky top navigation bar. | *N/A* |
| **3** | `ui:Button` | `back-btn` | `header__back-btn` | Back navigation button. | `NavigationManager.Instance.GoBack()` |
| **4** | `ui:VisualElement` | *None* | `icon-back` | Back arrow vector icon. | *N/A* |
| **5** | `ui:Label` | *None* | `header__title` | Page title (`"My Safety Certificates"`). | Static text |
| **6** | `ui:VisualElement` | *None* | `header__right-action` | Header spacing element. | *N/A* |
| **7** | `ui:ScrollView` | *None* | `scroll-area` | Scrollable body container. | Scroll gesture |
| **8** | `ui:VisualElement` | *None* | `card` | Search & Filter card container. | Visual Container |
| **9** | `ui:TextField` | `cert-search-input` | `input-field` | Live search input text box. | Triggers instant filtering on value change |
| **10** | `ui:Button` | `filter-all` | `quick-fill-btn` | Filter Pill: All certificates. | Sets filter: `"ALL"` |
| **11** | `ui:Button` | `filter-fire` | `quick-fill-btn` | Filter Pill: Fire Safety module. | Sets filter: `"fire_safety"` |
| **12** | `ui:Button` | `filter-gas` | `quick-fill-btn` | Filter Pill: Gas Leak module. | Sets filter: `"gas_leak"` |
| **13** | `ui:Button` | `filter-valid` | `quick-fill-btn` | Filter Pill: Valid certificates only. | Sets filter: `"VALID"` |
| **14** | `ui:Label` | `cert-count-label` | *Inline Style* | Counter label (e.g. `"2 Certificates Earned"`). | Bound to filtered certificates count |
| **15** | `ui:VisualElement` | `cert-list-container` | *Inline Style* | Container dynamically populated with certificate card items. | Dynamic List Container |
| **16** | `ui:VisualElement` | `empty-state` | *Inline Style* | Container shown when no search results match (`display: none;`). | Visual Container |
| **17** | `ui:VisualElement` | `bottom-nav` | `bottom-nav` | Sticky bottom navigation bar. | Visual Bar |
| **18** | `ui:Button` | `tab-home` | `bottom-nav__tab` | Home tab button. | `NavigationManager.Instance.NavigateToTab("UI_Dashboard")` |
| **19** | `ui:Button` | `tab-training` | `bottom-nav__tab` | Training tab button. | `NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue")` |
| **20** | `ui:Button` | `tab-progress` | `bottom-nav__tab` | Progress tab button. | `NavigationManager.Instance.NavigateToTab("UI_Progress")` |
| **21** | `ui:Button` | `tab-settings` | `bottom-nav__tab` | Settings tab button. | `NavigationManager.Instance.NavigateToTab("UI_Settings")` |

---

## 4. Card Click Navigation Logic

When a worker taps any certificate card item in the list:

```csharp
card.RegisterCallback<ClickEvent>(e =>
{
    NavigationManager.Instance.NavigateTo("UI_Certificate", cert.moduleId);
});
```

This seamlessly transitions the worker to the single detailed view for that specific certificate card!
