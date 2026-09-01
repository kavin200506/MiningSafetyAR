# 06 — Certification System

## Overview

The certification system issues tamper-resistant digital certificates after a worker successfully completes a training module (finalScore ≥ 60). Certificates have a unique ID, HMAC-SHA256 signature, one-year expiry, and a QR-verifiable format.

---

## Certificate Generator

**File:** `Assets/Scripts/Certification/CertificateGenerator.cs`

### Certificate ID Format

```
JH-{MODULE_CODE}-{6-digit-number}
```

Examples:
- `JH-FIRE-001928`
- `JH-GAS-002156`

The module code is a 4-character uppercase abbreviation derived from the module name. The 6-digit number is a random value (`Random.Range(100000, 999999)`).

### HMAC-SHA256 Signature

The signature protects certificate integrity:

```csharp
string payload = $"{certificateId}:{workerId}:{moduleName}:{score}:{issuedAt}";
string secretKey = "MiningARCertSecret2026"; // Hardcoded — see 09_known_gaps_and_todos.md
using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
{
    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToBase64String(hash);
}
```

The `signatureHash` is stored in `CertificatePayload.signatureHash`.

### Certificate Payload

```csharp
public class CertificatePayload {
    public string certificateId;
    public string workerId;
    public string moduleName;
    public int score;
    public string issuedAt;        // ISO 8601 UTC timestamp
    public string verificationUrl; // e.g. "https://minesafetyar.web.app/verify/{certId}"
    public string signatureHash;   // HMACSHA256 base64
}
```

---

## Certificate Data Model

**File:** `Assets/Scripts/Data/CertificateData.cs`

```csharp
public class CertificateData {
    public string id;           // e.g. "JH-FIRE-001928"
    public string workerName;
    public string workerId;
    public string moduleId;
    public string moduleTitle;
    public int score;
    public string issuedDate;   // "yyyy-MM-dd"
    public string expiryDate;   // issuedDate + 1 year
    public string organization;
    public string status;       // "valid" | "expired"
}
```

---

## Certificate Database (ScriptableObject)

**Asset:** `Assets/Data/CertificateDatabase.asset`  
**Script:** `Assets/Scripts/Data/CertificateDatabase.cs`

Pre-seeded demo certificates:

| Certificate ID | Worker | Module | Score | Issued | Expiry | Status |
|---|---|---|---|---|---|---|
| `JH-FIRE-001928` | Ramesh Kumar (JH10293) | Fire & Explosion Response | 85% | 2026-08-26 | 2027-08-26 | valid |
| `JH-GAS-002156` | Ramesh Kumar (JH10293) | Gas Leak & Confined Space | 72% | 2026-08-28 | 2027-08-28 | valid |

---

## Certificate Page Controller

**File:** `Assets/Scripts/UI/Pages/CertificatePageController.cs`

Controls `UI_Certificate.unity` scene.

### Data Displayed

| UI Element | Data Source |
|---|---|
| `worker-name` | `AppDataService.CurrentWorker.name` |
| `module-title` | `ModuleData.title` |
| `score` | `ModuleProgress.bestScore` |
| `passed-badge` | "PASSED" / "FAILED" based on `ModuleProgress.status == Completed` |
| `cert-id` | `ModuleProgress.certificateId` OR generated `JH-{code}-{random}` |
| `cert-id-meta` | Same as cert-id |
| `issued-date` | `ModuleData.lastAttempt` or `DateTime.Now` |
| `expiry-date` | `DateTime.Now.AddYears(1)` |
| `organization` | `WorkerData.organization` |

### Buttons

| Button ID | Action |
|---|---|
| `back-btn` | `NavigationManager.GoBack()` |
| `download-btn` | `Debug.Log("Download - not implemented")` — **STUB** |
| `share-btn` | `Debug.Log("Share - not implemented")` — **STUB** |
| `verify-btn` | Navigate to `UI_QRVerify` |

---

## QR Certificate Verification

**File:** `Assets/Scripts/UI/Pages/QRVerifyPageController.cs`

Controls `UI_QRVerify.unity` scene.

### Verification Flow

1. User types a certificate ID in `cert-input` TextField (or taps a quick-fill button).
2. Taps "Verify" → `OnVerify()` is called.
3. `loadingCard` is shown; `resultCard` is hidden.
4. `await Task.Delay(800)` — simulates verification delay.
5. `AppDataService.Instance.GetCertificate(certId)` is called.
6. If certificate found → `CERTIFICATE VALID` badge (green), shows cert details.
7. If not found → `INVALID CERTIFICATE` badge (red).

### Quick-Fill Demo Buttons

| Button | Auto-fills |
|---|---|
| `qf-1` | `"JH-FIRE-001928"` (valid) |
| `qf-2` | `"JH-GAS-002156"` (valid) |
| `qf-3` | `"INVALID-000"` (invalid) |

### Fields Displayed on Valid Certificate

- Worker name
- Worker ID
- Module name
- Score (%)
- Issued date
- Expiry date
- Status ("valid" / "expired")
- "View Certificate" button → navigates to `UI_Certificate`

---

## NOT IMPLEMENTED — Certificate Features

| Feature | Status |
|---|---|
| **PDF/PNG export** of certificate | `download-btn` is a stub; no rendering to image implemented |
| **Native share sheet** | `share-btn` is a stub; no OS share intent |
| **QR code image generation** | No QR code image is rendered; verification is text-based only |
| **Camera-based QR scanning** | The verify page uses manual text input only |
| **Signature verification on server** | HMAC is generated client-side; no server validates the signature |
| **Certificate expiry enforcement** | `status` field is set to "valid" statically; no runtime expiry check |
| **Firebase Storage certificate upload** | Certificates are not stored in Firebase Storage |
