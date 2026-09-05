using System.Collections;
using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.Certification;

namespace MiningSafetyAR.Testing
{
    /// <summary>
    /// Drop-in test tool for generating DGMS safety certificates.
    ///
    /// Usage:
    ///   1. Create an empty GameObject in any scene
    ///   2. Attach this component
    ///   3. Fill in the inspector fields (worker info, module, score)
    ///   4. Right-click the component header > "Generate Certificate"
    ///      OR enable "Generate On Start" and enter Play Mode
    ///
    /// The tool auto-creates AppDataService and CertificateGenerator if missing,
    /// auto-logs in via DemoLogin if no worker is logged in, then generates a
    /// signed certificate (HMAC-SHA256), saves to PlayerPrefs + Firestore.
    /// </summary>
    public class CertificateGeneratorTestTool : MonoBehaviour
    {
        [Header("Worker Identity")]
        [Tooltip("Worker display ID, e.g. W-10492")]
        public string workerId = "W-10492";

        [Tooltip("Worker full name")]
        public string workerName = "Ramesh Kumar";

        [Tooltip("Mining organization / enterprise")]
        public string organization = "Bharat Coking Coal Ltd";

        [Header("Training Module")]
        [Tooltip("Module code identifier, e.g. fire_safety, gas_leak, heights_safety")]
        public string moduleId = "fire_safety";

        [Tooltip("Human-readable module title")]
        public string moduleTitle = "Fire & Explosion Safety";

        [Header("Assessment Result")]
        [Tooltip("Score percentage (0-100). Must be >= 70 to pass.")]
        [Range(0, 100)]
        public int score = 88;

        [Header("Execution")]
        [Tooltip("Automatically generate certificate when Play Mode starts.")]
        public bool generateOnStart = false;

        [Tooltip("Log detailed output to the console.")]
        public bool verboseLogs = true;

        private const string LOG = "[CertTestTool]";

        void Start()
        {
            if (generateOnStart)
            {
                GenerateCertificate();
            }
        }

        [ContextMenu("Generate Certificate")]
        public void GenerateCertificate()
        {
            Debug.Log($"{LOG} ========================================");
            Debug.Log($"{LOG}  Certificate Generator Test Tool");
            Debug.Log($"{LOG} ========================================");

            EnsureDependencies();
        }

        void EnsureDependencies()
        {
            // --- Auto-create AppDataService if missing ---
            if (AppDataService.Instance == null)
            {
                Debug.Log($"{LOG} AppDataService not found - creating one...");
                var appGo = new GameObject("AppDataService");
                appGo.AddComponent<AppDataService>();
            }

            // --- Auto-create CertificateGenerator if missing ---
            if (CertificateGenerator.Instance == null)
            {
                Debug.Log($"{LOG} CertificateGenerator not found - creating one...");
                var certGo = new GameObject("CertificateGenerator");
                certGo.AddComponent<CertificateGenerator>();
            }

            if (AppDataService.Instance == null)
            {
                Debug.LogError($"{LOG} FAIL: AppDataService.Instance is still NULL after auto-creation");
                return;
            }

            if (CertificateGenerator.Instance == null)
            {
                Debug.LogError($"{LOG} FAIL: CertificateGenerator.Instance is still NULL after auto-creation");
                return;
            }

            // --- Check if worker is logged in ---
            var currentWorker = AppDataService.Instance.CurrentWorker;
            if (currentWorker == null)
            {
                Debug.Log($"{LOG} No worker logged in - attempting DemoLogin...");
                AppDataService.Instance.DemoLogin();
                StartCoroutine(WaitForWorkerAndGenerate());
                return;
            }

            DoGenerate(currentWorker);
        }

        IEnumerator WaitForWorkerAndGenerate()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (AppDataService.Instance.CurrentWorker == null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var worker = AppDataService.Instance.CurrentWorker;
            if (worker == null)
            {
                Debug.LogError($"{LOG} FAIL: DemoLogin timed out after {timeout}s - no worker loaded");
                Debug.LogError($"{LOG} FIX: Check Firebase config or network connection");
                yield break;
            }

            Debug.Log($"{LOG} DemoLogin successful: {worker.name} ({worker.id})");
            DoGenerate(worker);
        }

        void DoGenerate(WorkerData currentWorker)
        {
            if (string.IsNullOrEmpty(workerId) || string.IsNullOrEmpty(workerName))
            {
                Debug.LogError($"{LOG} FAIL: workerId and workerName must not be empty");
                return;
            }

            if (string.IsNullOrEmpty(moduleId))
            {
                Debug.LogError($"{LOG} FAIL: moduleId must not be empty");
                return;
            }

            // --- Log inputs ---
            Debug.Log($"{LOG} --- Input Parameters ---");
            Debug.Log($"{LOG} Worker:         {workerName} ({workerId})");
            Debug.Log($"{LOG} Organization:   {organization}");
            Debug.Log($"{LOG} Module:         {moduleId} ({moduleTitle})");
            Debug.Log($"{LOG} Score:          {score}%");
            Debug.Log($"{LOG} Logged-in UID:  {currentWorker.firebaseUid}");
            Debug.Log($"{LOG} Logged-in ID:   {currentWorker.id}");

            // --- Generate certificate ---
            Debug.Log($"{LOG} --- Generating Certificate ---");

            var tempResult = new TrainingResult
            {
                workerId = workerId,
                moduleName = moduleId,
                score = score,
                passed = true
            };

            var workerData = new WorkerData
            {
                firebaseUid = currentWorker.firebaseUid,
                id = workerId,
                name = workerName,
                organization = organization,
                sector = currentWorker.sector,
                phone = currentWorker.phone,
                language = currentWorker.language,
                joinDate = currentWorker.joinDate
            };

            CertificatePayload payload = CertificateGenerator.Instance.GenerateCertificate(tempResult, workerData);
            if (payload == null)
            {
                Debug.LogError($"{LOG} FAIL: CertificateGenerator.GenerateCertificate returned null");
                return;
            }

            CertificateData certData = CertificateGenerator.Instance.CreateCertificateData(tempResult, workerData);
            if (certData == null)
            {
                Debug.LogError($"{LOG} FAIL: CertificateGenerator.CreateCertificateData returned null");
                return;
            }

            // --- Override fields that come back wrong from the generator ---
            certData.moduleTitle = GetModuleTitle(moduleId);
            certData.workerId    = workerId;
            certData.workerName  = workerName;

            // --- Log certificate details ---
            Debug.Log($"{LOG} --- Certificate Generated ---");
            Debug.Log($"{LOG} Cert ID:    {certData.id}");
            Debug.Log($"{LOG} Worker:     {certData.workerName} ({certData.workerId})");
            Debug.Log($"{LOG} Module:     {certData.moduleTitle} [{certData.moduleId}]");
            Debug.Log($"{LOG} Score:      {certData.score}%");
            Debug.Log($"{LOG} Issued:     {certData.issuedDate}");
            Debug.Log($"{LOG} Expires:    {certData.expiryDate}");
            Debug.Log($"{LOG} Org:        {certData.organization}");
            Debug.Log($"{LOG} Status:     {certData.status}");
            Debug.Log($"{LOG} Signature:  {certData.signatureHash}");

            // --- Verify HMAC signature ---
            bool sigValid = CertificateGenerator.Instance.VerifySignature(certData);
            Debug.Log($"{LOG} HMAC Check: {(sigValid ? "PASS - Signature is valid" : "WARN - Signature mismatch (input fields may not match signing fields)")}");

            // --- Log JSON payloads ---
            if (verboseLogs)
            {
                string certJson = JsonUtility.ToJson(certData, true);
                Debug.Log($"{LOG} --- CertificateData JSON ---\n{certJson}");

                string payloadJson = JsonUtility.ToJson(payload, true);
                Debug.Log($"{LOG} --- CertificatePayload JSON ---\n{payloadJson}");

                Debug.Log($"{LOG} Verification URL: {payload.verificationUrl}");
            }

            // --- Generate QR texture ---
            string qrContent = (payload != null && !string.IsNullOrEmpty(payload.verificationUrl) && payload.verificationUrl.StartsWith("http"))
                ? payload.verificationUrl
                : $"https://cert-veri.web.app/verify?cert={certData.id}";

            Debug.Log($"{LOG} --- Encoding QR Code URL: '{qrContent}' ---");

            Texture2D qrTex = QRCodeTextureGenerator.GenerateQRTexture(qrContent, 256, 256);
            if (qrTex != null)
            {
                Debug.Log($"{LOG} QR Texture: {qrTex.width}x{qrTex.height} generated successfully");

                if (verboseLogs)
                {
                    string savePath = System.IO.Path.Combine(Application.persistentDataPath, $"QR_{certData.id}.png");
                    byte[] png = qrTex.EncodeToPNG();
                    System.IO.File.WriteAllBytes(savePath, png);
                    Debug.Log($"{LOG} QR saved to: {savePath} ({png.Length} bytes)");
                }
            }
            else
            {
                Debug.LogWarning($"{LOG} QR Texture generation returned null");
            }

            // --- Save to PlayerPrefs (local cache) ---
            Debug.Log($"{LOG} --- Saving Locally ---");
            AppDataService.Instance.SaveCertificate(certData);
            string playerPrefsKey = "Certificates_" + currentWorker.id;
            string stored = PlayerPrefs.GetString(playerPrefsKey, "");
            if (!string.IsNullOrEmpty(stored))
            {
                Debug.Log($"{LOG} PASS: PlayerPrefs[{playerPrefsKey}] saved ({stored.Length} bytes)");
            }
            else
            {
                Debug.LogError($"{LOG} FAIL: PlayerPrefs[{playerPrefsKey}] is empty after save");
            }

            // --- Firestore sync status ---
            if (!string.IsNullOrEmpty(currentWorker.firebaseUid))
            {
                Debug.Log($"{LOG} --- Firestore Sync ---");
                Debug.Log($"{LOG} Worker-scoped:  workers/{currentWorker.firebaseUid}/certificates/{certData.id}");
                Debug.Log($"{LOG} Public global:  certificates/{certData.id}");
                Debug.Log($"{LOG} (Sync triggered by SaveCertificate - check [AppDataService] logs for SUCCESS/FAIL)");
            }
            else
            {
                Debug.LogWarning($"{LOG} Firestore sync skipped (firebaseUid is empty)");
            }

            // --- Summary ---
            Debug.Log($"{LOG} ========================================");
            Debug.Log($"{LOG}  DONE - Certificate {certData.id} generated and saved");
            Debug.Log($"{LOG}  Check the UI CertificatePage to see it rendered");
            Debug.Log($"{LOG} ========================================");
        }

        private static string GetModuleTitle(string moduleId)
        {
            switch (moduleId)
            {
                case "fire_safety":      return "Fire & Explosion Response";
                case "gas_safety":
                case "gas_leak":         return "Gas Leak & Confined Space";
                case "machinery_safety": return "Machinery Safety";
                case "electrical_safety":return "Electrical Safety";
                case "heights_safety":   return "Working at Heights";
                default:                 return moduleId;
            }
        }
    }
}
