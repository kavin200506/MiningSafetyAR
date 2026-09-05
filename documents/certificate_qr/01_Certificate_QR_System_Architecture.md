# Certificate & QR System Architecture

## 1. Executive Summary & Purpose

The **Certification & QR Verification System** in **MiningSafetyAR** provides DGMS-compliant (Directorate General of Mines Safety) digital certificates for underground and surface mine workers. 

When a worker completes an interactive AR simulation module (e.g. *Fire & Explosion Safety*) and passes the subsequent multi-lingual assessment with a score $\ge 70\%$, the system automatically issues a cryptographically signed, verifiable digital certificate.

### Key Capabilities
1. **Cryptographic Security (HMAC-SHA256)**: Protects certificate data from forgery, grade inflation, or manual file tampering.
2. **Self-Contained JSON QR Payload**: The QR code encodes all certificate parameters inside its visual pixels, enabling **100% offline verification** deep inside underground mine shafts where internet access is unavailable.
3. **Dual Online/Offline Verification**: Online verification checks Cloud Firestore records; offline verification decodes and evaluates the cryptographic signature locally on the device.
4. **Expiration Enforcement**: Certificates automatically expire after **1 year (365 days)** to enforce mandatory annual re-certification under mining safety regulations.

---

## 2. End-to-End Execution & Data Flow

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Training & Evaluation                                                         │
└────────────────────────────────────────────────────────────────────────────────────────┘
 [Worker completes AR Simulation] ──► [AssessmentEngine evaluates Quiz]
                                                   │
                                         Is Score >= 70%?
                                          ├── NO  ──► [Display Retake Prompt]
                                          └── YES ──► Proceed to Phase 2

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Cryptographic Certificate Generation                                          │
└────────────────────────────────────────────────────────────────────────────────────────┘
 1. Generate unique Cert ID: "JH-FIRE-" + 6 random digits (e.g., JH-FIRE-849201)
 2. Capture UTC Timestamp: ISO 8601 string (e.g., "2026-09-03T00:15:00Z")
 3. Compute Expiry Timestamp: ISO 8601 string (+1 Year)
 4. Construct Raw Signature String: "JH-FIRE-849201|W-10492|fire_safety|88|2026-09-03T00:15:00Z"
 5. Generate HMAC-SHA256 Hash using Secret Key "DGMS_MINING_SAFETY_SECRET_KEY_2026"
 6. Package CertificatePayload Object & Serialize to JSON

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 3: QR Code Matrix Generation & Rendering                                         │
└────────────────────────────────────────────────────────────────────────────────────────┘
 1. Convert JSON Payload / Web Verification URL into Bit Matrix
 2. Generate 256x256 Texture2D pixel matrix (Black #000000 / White #FFFFFF)
 3. Apply Texture to UI_Certificate Document Element (#cert-qr-image)

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Storage & Synchronization                                                     │
└────────────────────────────────────────────────────────────────────────────────────────┘
 1. Save Certificate to Local PlayerPrefs Cache ("Certificates_W-10492")
 2. Update ModuleProgress record status to Completed
 3. Dispatch REST POST request to Firestore: workers/{uid}/certificates/{certId}
 4. If Offline: Add to offline_queue.json for background retry upon reconnect

┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Phase 5: Verification (Inspector Flow)                                                 │
└────────────────────────────────────────────────────────────────────────────────────────┘
 Option A: Camera Scan ──► Decode QR JSON ──► Extract fields & signature ──► Check HMAC
 Option B: Manual Input ──► Query Firestore/Local DB ──► Extract fields ──► Check HMAC
                                       │
                      ┌────────────────┴────────────────┐
                      ▼                                 ▼
             Is Signature Valid?               Is Signature Valid?
                   │ NO                              │ YES
                   ▼                                 ▼
          [ INVALID CERTIFICATE ❌ ]        Is Expiry Date > Today?
                                                  ├── NO  ──► [ CERTIFICATE EXPIRED ⚠️ ]
                                                  └── YES ──► [ CERTIFICATE VALID ✅ ]
```

---

## 3. Cryptographic HMAC Security Architecture

### Raw String Format
To guarantee data integrity, the system constructs a raw string joining five canonical fields with pipe (`|`) delimiters:

$$\text{RawPayload} = \text{CertificateID} \parallel \text{"|"} \parallel \text{WorkerID} \parallel \text{"|"} \parallel \text{ModuleName} \parallel \text{"|"} \parallel \text{Score} \parallel \text{"|"} \parallel \text{IssuedAt}$$

#### Concrete Example:
```
JH-FIRE-849201|W-10492|fire_safety|88|2026-09-03T00:15:00Z
```

### Signature Generation Algorithm
The raw string is hashed using **HMAC-SHA256** with the system secret key:

$$\text{SignatureHash} = \text{HMAC-SHA256}(\text{RawPayload}, \text{"DGMS\_MINING\_SAFETY\_SECRET\_KEY\_2026"})$$

```csharp
private string GenerateHMACSHA256(string rawData, string secretKey)
{
    byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
    byte[] textBytes = Encoding.UTF8.GetBytes(rawData);

    using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
    {
        byte[] hashBytes = hmac.ComputeHash(textBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
```

---

## 4. Self-Contained JSON QR Payload Specification

To support **100% offline verification**, the QR Code texture directly encodes a compact JSON payload.

### JSON Schema Specification
```json
{
  "v": 1,
  "id": "JH-FIRE-849201",
  "wId": "W-10492",
  "wName": "Ramesh Kumar",
  "mId": "fire_safety",
  "mTitle": "Fire & Explosion Safety",
  "score": 88,
  "iss": "2026-09-03T00:15:00Z",
  "exp": "2027-09-03T00:15:00Z",
  "org": "Bharat Coking Coal Ltd",
  "sig": "k9XpQ2m1A7zR4v8L0wN5jF3sP6tU1y9B"
}
```

### Field Definitions

| Field Name | Type | Key | Description |
| :--- | :--- | :--- | :--- |
| **Version** | Integer | `v` | Schema version identifier (Default: `1`). |
| **Certificate ID** | String | `id` | Unique certificate identifier (e.g. `JH-FIRE-849201`). |
| **Worker ID** | String | `wId` | Unique ID of the trained worker. |
| **Worker Name** | String | `wName` | Full display name of the worker. |
| **Module ID** | String | `mId` | Code identifier of the module (e.g. `fire_safety`). |
| **Module Title** | String | `mTitle` | Human-readable title of the module. |
| **Score** | Integer | `score` | Passing score percentage ($0 - 100$). |
| **Issued Timestamp** | String | `iss` | ISO 8601 UTC creation date/time. |
| **Expiration Timestamp** | String | `exp` | ISO 8601 UTC expiry date/time (+1 year). |
| **Organization** | String | `org` | Mining organization / enterprise. |
| **Signature Hash** | String | `sig` | Base64 HMAC-SHA256 signature hash. |

---

## 5. Storage Architecture & Schemas

### A. Local Storage (`PlayerPrefs` Cache)
* **Key Format**: `Certificates_{workerId}`
* **Format**: JSON serialized `CertificateListWrapper` containing an array of `CertificateData` objects.

```json
{
  "certificates": [
    {
      "id": "JH-FIRE-849201",
      "workerName": "Ramesh Kumar",
      "workerId": "W-10492",
      "moduleId": "fire_safety",
      "moduleTitle": "Fire & Explosion Safety",
      "score": 88,
      "issuedDate": "2026-09-03",
      "expiryDate": "2027-09-03",
      "organization": "Bharat Coking Coal Ltd",
      "status": "VALID",
      "signatureHash": "k9XpQ2m1A7zR4v8L0wN5jF3sP6tU1y9B"
    }
  ]
}
```

### B. Unity ScriptableObject Database ([CertificateDatabase.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/CertificateDatabase.cs))
* **Asset Location**: `Assets/Resources/Data/CertificateDatabase.asset`
* **Purpose**: In-memory database of pre-populated demo certificates for offline testing and offline verification fallback.

### C. Cloud Storage (Firebase Firestore Collection)
* **Collection Path**: `workers/{uid}/certificates/{certId}`
* **Firestore Schema**:
```json
{
  "fields": {
    "certificateId": { "stringValue": "JH-FIRE-849201" },
    "workerId": { "stringValue": "W-10492" },
    "workerName": { "stringValue": "Ramesh Kumar" },
    "moduleId": { "stringValue": "fire_safety" },
    "moduleTitle": { "stringValue": "Fire & Explosion Safety" },
    "score": { "integerValue": "88" },
    "issuedAt": { "stringValue": "2026-09-03T00:15:00Z" },
    "expiryDate": { "stringValue": "2027-09-03T00:15:00Z" },
    "organization": { "stringValue": "Bharat Coking Coal Ltd" },
    "signatureHash": { "stringValue": "k9XpQ2m1A7zR4v8L0wN5jF3sP6tU1y9B" }
  }
}
```

---

## 6. Verification Rules & Decision Matrix

When an inspector scans a QR code or inputs a Certificate ID, the system evaluates the state using the following deterministic matrix:

| Condition 1: DB / QR Record Exists? | Condition 2: HMAC Hash Valid? | Condition 3: Expiry Date > Today? | Resulting Verification Status | Status Badge Style |
| :---: | :---: | :---: | :---: | :---: |
| **Yes** | **Yes** | **Yes** | **`CERTIFICATE VALID`** | 🟢 `badge--pass` (Green) |
| **Yes** | **Yes** | **No** | **`CERTIFICATE EXPIRED`** | 🟡 `badge--warning` (Amber) |
| **Yes** | **No** | *Any* | **`INVALID CERTIFICATE`** | 🔴 `badge--fail` (Red) |
| **No** | *N/A* | *N/A* | **`INVALID CERTIFICATE`** | 🔴 `badge--fail` (Red) |
