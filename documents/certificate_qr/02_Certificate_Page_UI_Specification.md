# Single Certificate Detail Page UI Specification (`UI_Certificate`)

## 1. Page Overview

* **Page Name**: `UI_Certificate`
* **Controller**: [CertificatePageController.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/CertificatePageController.cs)
* **Template**: `Assets/UI/Templates/Pages/CertificatePage.uxml`
* **Stylesheets**:
  * `Assets/UI/Styles/Theme.uss`
  * `Assets/UI/Styles/Reset.uss`
  * `Assets/UI/Styles/Components.uss`
  * `Assets/UI/Styles/Header.uss`
  * `Assets/UI/Styles/BottomNav.uss`
  * `Assets/UI/Styles/Icons.uss`

The Single Certificate Detail Page presents the official Government of Jharkhand safety certificate card for **one specific completed training module**. It features a large 2D QR Code image, worker score percentage, issue/expiry dates, organization metadata, security signature status, and action buttons for downloading PNG images, sharing, and navigating to the full certificates portfolio.

---

## 2. Visual Layout Map

```
┌────────────────────────────────────────────────────────┐
│ [←]               My Safety Certificates               │  <-- Header
├────────────────────────────────────────────────────────┤
│ ┌────────────────────────────────────────────────────┐ │
│ │              DGMS SAFETY CERTIFICATE               │ │  <-- Orange Header Banner
│ │   Department of Mines Safety • Govt of Jharkhand   │ │      #FF6D00
│ ├────────────────────────────────────────────────────┤ │
│ │               This is to certify that              │ │
│ │                    RAMESH KUMAR                    │ │  <-- #worker-name
│ │       has successfully completed training for      │ │
│ │              FIRE & EXPLOSION SAFETY               │ │  <-- #module-title
│ │                                                    │ │
│ │                 [ 88% ] [ PASSED ]                 │ │  <-- Score & Pass Badge
│ │                                                    │ │
│ │               ┌───────────────────┐                │ │
│ │               │  [ QR Code Image ]│                │ │  <-- 2D QR Code Matrix
│ │               │   JH-FIRE-849201  │                │ │      (#qr-image)
│ │               └───────────────────┘                │ │
│ │            Official Verification QR Code           │ │
│ │                                                    │ │
│ │ Certificate ID                     JH-FIRE-849201  │ │  <-- Metadata Table
│ │ Issue Date                         2026-09-03      │ │
│ │ Valid Until                        2027-09-03      │ │
│ │ Organization            Bharat Coking Coal Ltd     │ │
│ │ Security Hash                 HMAC-SHA256 Signed 🟢│ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ [           DOWNLOAD CERTIFICATE (PNG)         ]       │  <-- #download-btn
│ [               SHARE CERTIFICATE              ]       │  <-- #share-btn
│ [           VIEW ALL MY CERTIFICATES           ]       │  <-- #view-all-certs-btn
├────────────────────────────────────────────────────────┤
│ [Home]         [Training]     [Progress]    [Settings] │  <-- Bottom Navigation Bar
└────────────────────────────────────────────────────────┘
```

---

## 3. Exhaustive UI Component Inventory

| # | Element Tag | Element `name` | CSS Class (`class`) | Description / Binding Source | Callback / Action |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | `ui:VisualElement` | *None* | `page-container` | Root page container. | *N/A* |
| **2** | `ui:VisualElement` | *None* | `header` | Sticky top navigation header. | *N/A* |
| **3** | `ui:Button` | `back-btn` | `header__back-btn` | Back navigation button. | `NavigationManager.Instance.GoBack()` |
| **4** | `ui:VisualElement` | *None* | `icon-back` | Back arrow vector icon. | *N/A* |
| **5** | `ui:Label` | *None* | `header__title` | Header title (`"My Safety Certificates"`). | Static text |
| **6** | `ui:VisualElement` | `cert-card` | `card` | Primary certificate card container (`padding: 0; overflow: hidden;`). | Visual Container |
| **7** | `ui:VisualElement` | *None* | *Inline Style* | Top branding banner container (`bg: #FF6D00; padding: 16px; align-items: center;`). | Visual Container |
| **8** | `ui:VisualElement` | *None* | `logo-shield` | Government shield logo icon. | Visual Icon |
| **9** | `ui:Label` | *None* | *Inline Style* | Certificate banner header (`"DGMS SAFETY CERTIFICATE"`). | Static text |
| **10** | `ui:Label` | *None* | *Inline Style* | Authority text (`"Department of Mines Safety • Govt of Jharkhand"`). | Static text |
| **11** | `ui:Label` | `worker-name` | *Inline Style* | Worker full name label (`font: 20px 700 bold #1A1A1A`). | Bound to `worker.name` |
| **12** | `ui:Label` | `module-title` | *Inline Style* | Module title label (`font: 15px 700 bold #FF6D00`). | Bound to `mod.title` |
| **13** | `ui:Label` | `score` | *Inline Style* | Assessment score percentage (`font: 16px 700 bold`). | Bound to `${scoreVal}%` |
| **14** | `ui:Label` | `passed-badge` | `badge badge--pass` | Status badge (`"PASSED"` or `"IN PROGRESS"`). | Class toggled: `badge--pass` / `badge--fail` |
| **15** | `ui:VisualElement` | `qr-image` | *Inline Style* | Visual element displaying 2D QR Code texture render. | Rendered `Texture2D` matrix |
| **16** | `ui:Label` | `cert-id` | *Inline Style* | Certificate ID text under QR box. | Bound to `certIdStr` |
| **17** | `ui:Label` | `cert-id-meta` | *Inline Style* | Certificate ID value label in metadata table. | Bound to `certIdStr` |
| **18** | `ui:Label` | `issued-date` | *Inline Style* | Issue date value label (`yyyy-MM-dd`). | Bound to issue timestamp |
| **19** | `ui:Label` | `expiry-date` | *Inline Style* | Expiry date value label (`yyyy-MM-dd` + 1 year). | Bound to expiry timestamp |
| **20** | `ui:Label` | `organization` | *Inline Style* | Mining enterprise organization label. | Bound to `worker.organization` |
| **21** | `ui:Label` | `security-status` | *Inline Style* | Cryptographic security indicator (`"HMAC-SHA256 Signed"`). | Green bold status text |
| **22** | `ui:Button` | `download-btn` | `btn-primary` | Action button: Download Certificate PNG. | Saves certificate PNG image to phone storage |
| **23** | `ui:Button` | `share-btn` | `btn-secondary` | Action button: Share Certificate. | Opens native sharing dialog |
| **24** | `ui:Button` | `view-all-certs-btn` | `btn-secondary` | Action button: View All My Certificates. | `NavigationManager.Instance.NavigateTo("UI_CertificatesList")` |
| **25** | `ui:VisualElement` | `bottom-nav` | `bottom-nav` | Sticky bottom navigation bar. | Visual Bar |
