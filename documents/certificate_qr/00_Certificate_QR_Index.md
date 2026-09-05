# Certificate & QR System Documentation - Master Index

## Overview

This directory contains the complete technical documentation, architectural specifications, data schemas, UI component breakdowns, and code references for the **Certification & QR Verification System** in **MiningSafetyAR**.

The subsystem is architected into two distinct operational flows:
1. **Worker Mobile Experience**: Workers complete AR training modules, earn cryptographically signed digital certificates, view their searchable certificate portfolio (`UI_CertificatesList`), inspect individual official certificate cards (`UI_Certificate`), and download/share certificate images.
2. **Admin & Inspector Verification Portal**: Inspectors and supervisors verify certificate authenticity, check HMAC-SHA256 cryptographic signatures, validate expiration dates, and audit compliance via Web/App portals (`https://miningsafetyar.web.app/verify`).

---

## Complete Documentation Inventory

```
documents/certificate_qr/
├── 00_Certificate_QR_Index.md                               <-- You are here
├── 01_Certificate_QR_System_Architecture.md                 <-- High-Level Architecture & Cryptography
├── 02_Certificate_Page_UI_Specification.md                   <-- UI Breakdown: Single Certificate View (UI_Certificate)
├── 03_Certificates_List_Page_UI_Specification.md            <-- UI Breakdown: Certificates Portfolio List (UI_CertificatesList)
├── 04_Implementation_Code_Reference.md                    <-- C# Code Schemas & Technical References
└── 05_Admin_Inspector_Verification_Specification.md         <-- Web Admin & Inspector Verification Specification
```

---

## File Summaries

### 1. [01_Certificate_QR_System_Architecture.md](file:///u:/Projects/MiningSafetyAR/documents/certificate_qr/01_Certificate_QR_System_Architecture.md)
* Architectural design, cryptographic HMAC-SHA256 signature generation (`certId|workerId|moduleName|score|issuedAt`), self-contained JSON QR payload format, storage layers (`PlayerPrefs`, ScriptableObject, Cloud Firestore), and verification decision tree.

### 2. [02_Certificate_Page_UI_Specification.md](file:///u:/Projects/MiningSafetyAR/documents/certificate_qr/02_Certificate_Page_UI_Specification.md)
* Exhaustive UXML element inventory for **Single Certificate View (`UI_Certificate`)**.
* Official Government of Jharkhand certificate card, large 2D QR Code texture, score percentage, issue/expiry dates, organization, `HMAC-SHA256 Signed` security badge, download PNG button, share button, and "View All My Certificates" link.

### 3. [03_Certificates_List_Page_UI_Specification.md](file:///u:/Projects/MiningSafetyAR/documents/certificate_qr/03_Certificates_List_Page_UI_Specification.md)
* Exhaustive UXML element inventory for **Worker Portfolio List (`UI_CertificatesList`)**.
* Search input bar (`cert-search-input`), category filter pills (`All`, `Fire Safety`, `Gas Leak`, `Valid`), dynamic certificate card list, empty search state, and bottom navigation.

### 4. [04_Implementation_Code_Reference.md](file:///u:/Projects/MiningSafetyAR/documents/certificate_qr/04_Implementation_Code_Reference.md)
* C# code references for [CertificateGenerator.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Certification/CertificateGenerator.cs), [CertificateData.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/CertificateData.cs), [CertificatePageController.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/CertificatePageController.cs), and [CertificatesListPageController.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/UI/Pages/CertificatesListPageController.cs).

### 5. [05_Admin_Inspector_Verification_Specification.md](file:///u:/Projects/MiningSafetyAR/documents/certificate_qr/05_Admin_Inspector_Verification_Specification.md)
* Dedicated specification for the **Web Admin Compliance & Inspector Verification Portal** (`miningsafetyar.web.app/verify`).
* Camera QR scanner, manual ID search, Firestore database lookup, HMAC hash validation, expiration check, verification status badges (Valid 🟢, Expired 🟡, Invalid 🔴), and PDF/CSV compliance audit export.
