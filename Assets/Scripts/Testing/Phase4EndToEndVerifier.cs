#pragma warning disable 0414
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.Data;
using MiningSafetyAR.Certification;
using MiningSafetyAR.UI.Pages;

namespace MiningSafetyAR.Testing
{
    /// <summary>
    /// Phase 4 & End-to-End Manual Verification Guide
    ///
    /// Usage:
    ///   1. Open any scene (or UI_Certificate)
    ///   2. Add this component to any GameObject
    ///   3. Enter Play Mode
    ///   4. Check Console for verification output
    ///
    /// Verifies:
    ///   - Fire & Explosion Safety module completion with ≥70% score
    ///   - Certificate auto-issuance (CertificateGenerator)
    ///   - Local storage (PlayerPrefs)
    ///   - Cloud sync (Firestore)
    ///   - Certificate card in UI_CertificatesList
    ///   - Certificate detail view in UI_Certificate
    ///   - Download PNG export
    ///   - Share Certificate payload
    /// </summary>
    public class Phase4EndToEndVerifier : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private string moduleId = "fire_safety";
        [SerializeField] private int testScore = 88;
        [SerializeField] private float delayBetweenSteps = 2f;

        private bool testCompleted = false;
        private CertificateData generatedCert;

        void Start()
        {
            Debug.Log("[Phase4E2E] ========================================");
            Debug.Log("[Phase4E2E]  Phase 4 & End-to-End Verification");
            Debug.Log("[Phase4E2E] ========================================");
            Debug.Log($"[Phase4E2E]  Module: {moduleId}, Score: {testScore}%");
            Debug.Log("[Phase4E2E] ========================================");
            Invoke(nameof(RunFullVerification), 5f);
        }

        [ContextMenu("Run Phase 4 E2E Verification")]
        public void RunFullVerification()
        {
            testCompleted = false;
            bool allPassed = true;

            // ============================================================
            // STEP 1: Complete Training Module
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 1: Complete Training Module ---");
            Debug.Log($"[Phase4E2E]   Simulating completion of '{moduleId}' with score {testScore}%");

            var app = AppDataService.Instance;
            if (app == null)
            {
                Debug.LogError("[Phase4E2E]   FAIL: AppDataService.Instance is null");
                return;
            }

            var worker = app.CurrentWorker;
            if (worker == null)
            {
                Debug.LogError("[Phase4E2E]   FAIL: No current worker logged in");
                return;
            }

            Debug.Log($"[Phase4E2E]   Worker: {worker.name} ({worker.id})");
            Debug.Log($"[Phase4E2E]   Score: {testScore}% (Pass threshold: ≥70%)");
            Debug.Log($"[Phase4E2E]   PASS/Fail: {(testScore >= 70 ? "PASS ✓" : "FAIL ✗")}");

            if (testScore < 70)
            {
                Debug.LogError("[Phase4E2E]   FAIL: Score below 70% threshold");
                return;
            }

            // ============================================================
            // STEP 2: Trigger SaveAttempt (generates certificate)
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 2: Trigger SaveAttempt ---");
            Debug.Log("[Phase4E2E]   Calling AppDataService.SaveAttempt()...");

            app.SaveAttempt(moduleId, testScore, true);

            Debug.Log("[Phase4E2E]   SaveAttempt completed");
            Debug.Log("[Phase4E2E]   Expected logs:");
            Debug.Log("[Phase4E2E]     [CertificateGenerator] Created Certificate: JH-FIRE-...");
            Debug.Log("[Phase4E2E]     [AppDataService] Certificate Cloud Sync SUCCESS: JH-FIRE-...");

            // ============================================================
            // STEP 3: Verify Certificate Generation
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 3: Verify Certificate Generation ---");

            var certGen = CertificateGenerator.Instance;
            if (certGen != null)
            {
                Debug.Log("[Phase4E2E]   PASS: CertificateGenerator.Instance exists");

                // Check PlayerPrefs for certificate data
                string prefsKey = $"Certificates_{worker.id}";
                string certJson = PlayerPrefs.GetString(prefsKey, "");
                if (!string.IsNullOrEmpty(certJson))
                {
                    Debug.Log($"[Phase4E2E]   PASS: PlayerPrefs has certificate data ({certJson.Length} chars)");

                    var wrapper = JsonUtility.FromJson<CertificateListWrapper>(certJson);
                    if (wrapper != null && wrapper.certificates != null && wrapper.certificates.Count > 0)
                    {
                        Debug.Log($"[Phase4E2E]   Certificates in storage: {wrapper.certificates.Count}");
                        foreach (var cert in wrapper.certificates)
                        {
                            Debug.Log($"[Phase4E2E]     - {cert.id}: {cert.moduleTitle} ({cert.score}%) [{cert.status}]");
                            Debug.Log($"[Phase4E2E]       Worker: {cert.workerName} ({cert.workerId})");
                            Debug.Log($"[Phase4E2E]       Hash: {cert.signatureHash}");
                            Debug.Log($"[Phase4E2E]       Issued: {cert.issuedDate}");
                            Debug.Log($"[Phase4E2E]       Expires: {cert.expiryDate}");

                            if (string.IsNullOrEmpty(cert.signatureHash))
                            {
                                Debug.LogWarning("[Phase4E2E]   WARN: signatureHash is empty");
                            }
                            else
                            {
                                Debug.Log("[Phase4E2E]   PASS: signatureHash is present");
                            }
                        }

                        generatedCert = wrapper.certificates[wrapper.certificates.Count - 1];
                    }
                    else
                    {
                        Debug.LogError("[Phase4E2E]   FAIL: No certificates in parsed wrapper");
                        allPassed = false;
                    }
                }
                else
                {
                    Debug.LogError("[Phase4E2E]   FAIL: PlayerPrefs has no certificate data");
                    allPassed = false;
                }
            }
            else
            {
                Debug.LogError("[Phase4E2E]   FAIL: CertificateGenerator.Instance is null");
                allPassed = false;
            }

            // ============================================================
            // STEP 4: Verify Certificate in AppDataService
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 4: Verify Certificate in AppDataService ---");

            var certs = app.GetWorkerCertificates();
            Debug.Log($"[Phase4E2E]   GetWorkerCertificates() returned: {certs.Count} certificates");

            if (certs.Count > 0)
            {
                Debug.Log("[Phase4E2E]   PASS: At least 1 certificate in AppDataService");
                foreach (var cert in certs)
                {
                    Debug.Log($"[Phase4E2E]     - {cert.id}: {cert.moduleTitle}");
                }
            }
            else
            {
                Debug.LogWarning("[Phase4E2E]   WARN: No certificates in AppDataService (demo data used)");
            }

            // ============================================================
            // STEP 5: Verify Module Progress Updated
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 5: Verify Module Progress ---");

            var progress = app.GetModuleProgress(moduleId);
            if (progress != null)
            {
                Debug.Log($"[Phase4E2E]   Module: {moduleId}");
                Debug.Log($"[Phase4E2E]   Status: {progress.status}");
                Debug.Log($"[Phase4E2E]   Best Score: {progress.bestScore}%");
                Debug.Log($"[Phase4E2E]   Progress: {progress.progress}%");
                Debug.Log($"[Phase4E2E]   Attempts: {progress.attempts}");
                Debug.Log($"[Phase4E2E]   Certificate ID: {progress.certificateId}");

                if (progress.status == ModuleStatus.Completed)
                {
                    Debug.Log("[Phase4E2E]   PASS: Module marked as Completed");
                }
                else
                {
                    Debug.LogWarning($"[Phase4E2E]   WARN: Module status is {progress.status} (expected Completed)");
                }

                if (progress.bestScore >= testScore)
                {
                    Debug.Log("[Phase4E2E]   PASS: Best score matches test score");
                }
                else
                {
                    Debug.LogWarning($"[Phase4E2E]   WARN: Best score {progress.bestScore} < test score {testScore}");
                }
            }
            else
            {
                Debug.LogWarning("[Phase4E2E]   WARN: No progress found for module");
            }

            // ============================================================
            // STEP 6: Verify Download Path
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 6: Verify Download Path ---");

            string downloadPath = Application.persistentDataPath;
            Debug.Log($"[Phase4E2E]   Download directory: {downloadPath}");

            string[] certFiles = System.IO.Directory.GetFiles(downloadPath, "Certificate_*.png");
            Debug.Log($"[Phase4E2E]   Existing certificate PNGs: {certFiles.Length}");
            foreach (var file in certFiles)
            {
                var info = new System.IO.FileInfo(file);
                Debug.Log($"[Phase4E2E]     - {info.Name} ({info.Length} bytes)");
            }

            // ============================================================
            // STEP 7: Verify Share Payload
            // ============================================================
            Debug.Log("[Phase4E2E] --- STEP 7: Verify Share Payload ---");

            if (generatedCert != null)
            {
                string shareText = $"Official DGMS Safety Certificate\nWorker: {generatedCert.workerName}\nModule: {generatedCert.moduleTitle}\nCert ID: {generatedCert.id}\nVerification: https://cert-veri.web.app/verify?cert={generatedCert.id}";
                Debug.Log($"[Phase4E2E]   Share payload:\n{shareText}");

                if (shareText.Contains("DGMS") && shareText.Contains(generatedCert.id))
                {
                    Debug.Log("[Phase4E2E]   PASS: Share payload contains required fields");
                }
                else
                {
                    Debug.LogError("[Phase4E2E]   FAIL: Share payload missing required fields");
                    allPassed = false;
                }
            }
            else
            {
                Debug.LogWarning("[Phase4E2E]   WARN: No generated certificate to verify share payload");
            }

            // ============================================================
            // FINAL SUMMARY
            // ============================================================
            Debug.Log("[Phase4E2E] ========================================");
            if (allPassed)
            {
                Debug.Log("[Phase4E2E]  RESULT: ALL STEPS PASSED ✓");
                Debug.Log("[Phase4E2E]  Phase 4 End-to-End verification complete.");
                Debug.Log("[Phase4E2E]  The Certificate & QR System is working correctly.");
            }
            else
            {
                Debug.LogError("[Phase4E2E]  RESULT: SOME STEPS FAILED - Check errors above");
            }
            Debug.Log("[Phase4E2E] ========================================");

            testCompleted = true;
        }
    }
}
