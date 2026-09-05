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
        [SerializeField] private string baseVerificationUrl = "https://cert-veri.web.app/verify?cert=";
        [SerializeField] private string secretKey = "DGMS_MINING_SAFETY_SECRET_KEY_2026";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // This component previously only existed inside the UI_CertificatesList scene with no
            // DontDestroyOnLoad — meaning Instance was null whenever a certificate was actually
            // earned from the AR training scene, silently skipping the whole Firestore save (see
            // AppDataService.UpdateLocalProgress's fallback branch + warning log). Persisting here
            // AND being auto-attached from AppDataService.Awake() (belt-and-suspenders) guarantees
            // it exists from app boot regardless of which scene loads first.
            DontDestroyOnLoad(gameObject);
        }

        public string BuildVerificationUrl(string certId)
        {
            string baseUrl = !string.IsNullOrEmpty(baseVerificationUrl) && baseVerificationUrl.StartsWith("http")
                ? baseVerificationUrl
                : "https://cert-veri.web.app/verify?cert=";
            return baseUrl + certId;
        }

        public CertificatePayload GenerateCertificate(TrainingResult result, WorkerData worker = null)
        {
            if (result == null) return null;

            string moduleCode = !string.IsNullOrEmpty(result.moduleName) 
                ? result.moduleName.ToUpper().Replace(" ", "_") 
                : "GEN";
            if (moduleCode.Length > 4) moduleCode = moduleCode.Substring(0, 4);

            string certId = $"JH-{moduleCode}-{UnityEngine.Random.Range(100000, 999999)}";

            // Date-only, matching exactly what CertificateData.issuedDate/expiryDate actually store
            // in Firestore — signing with any finer precision than what's persisted meant nothing
            // could ever recompute a matching signature from the saved document alone (a verifier,
            // including a separately hosted web app, only ever has the stored fields to work with).
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string expiry = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

            string workerName = worker != null && !string.IsNullOrEmpty(worker.name) ? worker.name : "Mining Worker";
            string org = worker != null && !string.IsNullOrEmpty(worker.organization) ? worker.organization : "DGMS Certified Mining Org";

            string rawPayload = $"{certId}|{result.workerId}|{result.moduleName}|{result.score}|{timestamp}";
            string signature = GenerateHMACSHA256(rawPayload, secretKey);

            CertificatePayload payload = new CertificatePayload
            {
                v = 1,
                certificateId = certId,
                workerId = result.workerId,
                workerName = workerName,
                moduleId = result.moduleName,
                moduleName = result.moduleName,
                score = result.score,
                issuedAt = timestamp,
                expiryDate = expiry,
                organization = org,
                verificationUrl = BuildVerificationUrl(certId),
                signatureHash = signature
            };

            Debug.Log($"[CertificateGenerator] Created Certificate: {certId} for Worker: {workerName} ({result.workerId})");
            return payload;
        }

        public CertificateData CreateCertificateData(TrainingResult result, WorkerData worker = null)
        {
            CertificatePayload payload = GenerateCertificate(result, worker);
            if (payload == null) return null;

            return new CertificateData
            {
                id = payload.certificateId,
                workerName = payload.workerName,
                workerId = payload.workerId,
                moduleId = payload.moduleId,
                moduleTitle = payload.moduleName,
                score = payload.score,
                issuedDate = DateTime.TryParse(payload.issuedAt, out DateTime dt) ? dt.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"),
                expiryDate = DateTime.TryParse(payload.expiryDate, out DateTime expDt) ? expDt.ToString("yyyy-MM-dd") : DateTime.Now.AddYears(1).ToString("yyyy-MM-dd"),
                organization = payload.organization,
                status = "VALID",
                signatureHash = payload.signatureHash,
                verificationUrl = payload.verificationUrl
            };
        }

        public bool VerifySignature(CertificatePayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.signatureHash)) return false;
            string rawPayload = $"{payload.certificateId}|{payload.workerId}|{payload.moduleName}|{payload.score}|{payload.issuedAt}";
            string expected = GenerateHMACSHA256(rawPayload, secretKey);
            return string.Equals(payload.signatureHash, expected, StringComparison.Ordinal);
        }

        public bool VerifySignature(CertificateData data)
        {
            if (data == null || string.IsNullOrEmpty(data.signatureHash)) return false;
            string rawPayload = $"{data.id}|{data.workerId}|{data.moduleId}|{data.score}|{data.issuedDate}";
            string expected = GenerateHMACSHA256(rawPayload, secretKey);
            return string.Equals(data.signatureHash, expected, StringComparison.Ordinal);
        }

        public string GetCertificateJsonPayload(TrainingResult result, WorkerData worker = null)
        {
            CertificatePayload payload = GenerateCertificate(result, worker);
            return payload != null ? JsonUtility.ToJson(payload) : string.Empty;
        }

        public string GenerateHMACSHA256(string rawData, string key = null)
        {
            string actualKey = !string.IsNullOrEmpty(key) ? key : secretKey;
            byte[] keyBytes = Encoding.UTF8.GetBytes(actualKey);
            byte[] textBytes = Encoding.UTF8.GetBytes(rawData);

            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(textBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
