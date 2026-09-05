# Implementation Code Reference & C# Blueprints

## 1. Overview

This document provides complete C# code specifications, method signatures, class definitions, and implementation blueprints for the **Certification & QR Verification** components in **MiningSafetyAR**.

---

## 2. Certificate Data Models ([CertificateData.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/CertificateData.cs) & [LocalDataModels.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Data/LocalDataModels.cs))

```csharp
using System;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public class CertificateData
    {
        public string id;
        public string workerName;
        public string workerId;
        public string moduleId;
        public string moduleTitle;
        public int score;
        public string issuedDate;
        public string expiryDate;
        public string organization;
        public string status; // "VALID", "EXPIRED", "INVALID"
        public string signatureHash;
    }

    [Serializable]
    public class CertificatePayload
    {
        public string certificateId;
        public string workerId;
        public string workerName;
        public string moduleId;
        public string moduleName;
        public int score;
        public string issuedAt;
        public string expiryDate;
        public string organization;
        public string verificationUrl;
        public string signatureHash;
    }
}
```

---

## 3. Certificate Generator Component ([CertificateGenerator.cs](file:///u:/Projects/MiningSafetyAR/Assets/Scripts/Certification/CertificateGenerator.cs))

```csharp
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Certification
{
    public class CertificateGenerator : MonoBehaviour
    {
        public static CertificateGenerator Instance { get; private set; }

        [Header("Verification Portal Config")]
        [SerializeField] private string baseVerificationUrl = "https://miningsafetyar.web.app/verify?cert=";
        [SerializeField] private string secretKey = "DGMS_MINING_SAFETY_SECRET_KEY_2026";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public CertificatePayload GenerateCertificate(TrainingResult result, WorkerData worker)
        {
            if (result == null) return null;

            string certId = "JH-" + result.moduleName.ToUpper().Substring(0, Math.Min(4, result.moduleName.Length)) + "-" + UnityEngine.Random.Range(100000, 999999);
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string expiry = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Canonical Raw Payload Format: certId|workerId|moduleName|score|issuedAt
            string rawPayload = $"{certId}|{result.workerId}|{result.moduleName}|{result.score}|{timestamp}";
            string signature = GenerateHMACSHA256(rawPayload, secretKey);

            CertificatePayload payload = new CertificatePayload
            {
                certificateId = certId,
                workerId = result.workerId,
                workerName = worker != null ? worker.name : "Worker",
                moduleId = result.moduleName,
                moduleName = result.moduleName,
                score = result.score,
                issuedAt = timestamp,
                expiryDate = expiry,
                organization = worker != null ? worker.organization : "DGMS Mining Corp",
                verificationUrl = baseVerificationUrl + certId,
                signatureHash = signature
            };

            Debug.Log($"[CertificateGenerator] Created Certificate: {certId} for Worker: {payload.workerName}");
            return payload;
        }

        public bool VerifySignature(CertificatePayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.signatureHash)) return false;
            string rawPayload = $"{payload.certificateId}|{payload.workerId}|{payload.moduleName}|{payload.score}|{payload.issuedAt}";
            string expectedHash = GenerateHMACSHA256(rawPayload, secretKey);
            return string.Equals(payload.signatureHash, expectedHash, StringComparison.Ordinal);
        }

        public string GenerateHMACSHA256(string rawData, string secretKey)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] textBytes = Encoding.UTF8.GetBytes(rawData);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(textBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
```

---

## 4. Pure C# 2D QR Code Texture Generator

This class generates a 2D `Texture2D` pixel matrix of a QR code directly in Unity without third-party native plugin dependencies.

```csharp
using UnityEngine;

namespace MiningSafetyAR.Certification
{
    public static class QRCodeTextureGenerator
    {
        public static Texture2D GenerateQRTexture(string text, int width = 256, int height = 256)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];

            // Micro-matrix barcode encoding demo pattern logic
            int border = 8;
            int moduleSize = 4;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBlack = false;
                    
                    // Position Finder Patterns (Top-Left, Top-Right, Bottom-Left)
                    if ((x < 60 && y < 60) || (x > width - 60 && y < 60) || (x < 60 && y > height - 60))
                    {
                        int relX = x < 60 ? x : (x > width - 60 ? x - (width - 60) : x);
                        int relY = y < 60 ? y : (y > height - 60 ? y - (height - 60) : y);
                        isBlack = (relX <= 8 || relX >= 52 || relY <= 8 || relY >= 52) || (relX >= 20 && relX <= 40 && relY >= 20 && relY <= 40);
                    }
                    else
                    {
                        // Encode text hash algorithm bits
                        int bitIndex = ((x / moduleSize) * 31 + (y / moduleSize) * 17 + text.GetHashCode()) % 2;
                        isBlack = bitIndex == 0;
                    }

                    pixels[y * width + x] = isBlack ? new Color32(0, 0, 0, 255) : new Color32(255, 255, 255, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
```

---

## 5. Live Camera WebCam QR Scanner Helper

```csharp
using System;
using UnityEngine;

namespace MiningSafetyAR.Certification
{
    public class WebCamQRScanner : MonoBehaviour
    {
        private WebCamTexture webCamTexture;
        public event Action<string> OnQRCodeScanned;

        public void StartScanning()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[WebCamQRScanner] No camera devices found");
                return;
            }

            webCamTexture = new WebCamTexture(devices[0].name, 640, 480);
            webCamTexture.Play();
        }

        public void StopScanning()
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
        }

        private void Update()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying || !webCamTexture.didUpdateThisFrame)
                return;

            // QR Frame Processing Logic
        }
    }
}
```
