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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public CertificatePayload GenerateCertificate(TrainingResult result)
        {
            if (result == null) return null;

            string certId = "CERT-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string rawPayload = $"{certId}|{result.workerId}|{result.moduleName}|{result.score}|{timestamp}";
            string signature = GenerateHMACSHA256(rawPayload, "DGMS_MINING_SAFETY_SECRET_KEY_2026");

            CertificatePayload payload = new CertificatePayload
            {
                certificateId = certId,
                workerId = result.workerId,
                moduleName = result.moduleName,
                score = result.score,
                issuedAt = timestamp,
                verificationUrl = baseVerificationUrl + certId,
                signatureHash = signature
            };

            Debug.Log($"[CertificateGenerator] Created Certificate: {certId} for Worker: {result.workerId}");
            return payload;
        }

        public string GetCertificateJsonPayload(TrainingResult result)
        {
            CertificatePayload payload = GenerateCertificate(result);
            return payload != null ? JsonUtility.ToJson(payload) : string.Empty;
        }

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
    }
}
