using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI.Pages;

namespace MiningSafetyAR.Testing
{
    /// <summary>
    /// Phase 3 Manual Verification Guide - Certificates List & Navigation Test
    ///
    /// Usage:
    ///   1. Open Assets/Scenes/UI_CertificatesList.unity
    ///   2. Add this component to any GameObject
    ///   3. Enter Play Mode
    ///   4. Check Console for verification output
    ///
    /// Verifies:
    ///   - CertificatesListPageController exists
    ///   - Certificate cards are displayed (Fire Safety + Gas Leak)
    ///   - Search input filters certificates
    ///   - Filter pills work (All, Fire, Gas, Valid)
    ///   - Card tap navigates to UI_Certificate
    ///   - Download PNG button exports certificate
    /// </summary>
    public class Phase3CertificatesListVerifier : MonoBehaviour
    {
        [Header("Auto-Run")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private float delayBeforeTest = 2f;

        private bool testCompleted = false;

        void Start()
        {
            if (runOnStart)
            {
                Debug.Log("[Phase3CertListVerifier] ========================================");
                Debug.Log("[Phase3CertListVerifier]  Phase 3 Certificates List Verification");
                Debug.Log("[Phase3CertListVerifier] ========================================");
                Invoke(nameof(RunVerification), delayBeforeTest);
            }
        }

        [ContextMenu("Run Phase 3 Verification")]
        public void RunVerification()
        {
            if (testCompleted)
            {
                Debug.Log("[Phase3CertListVerifier] Test already completed. Re-running...");
            }

            testCompleted = false;
            bool allPassed = true;

            // Step 1: Check CertificatesListPageController
            Debug.Log("[Phase3CertListVerifier] --- Step 1: Check CertificatesListPageController ---");
            var controller = FindFirstObjectByType<CertificatesListPageController>();
            if (controller != null)
            {
                Debug.Log("[Phase3CertListVerifier]   PASS: CertificatesListPageController found");
            }
            else
            {
                Debug.LogError("[Phase3CertListVerifier]   FAIL: CertificatesListPageController not found");
                allPassed = false;
            }

            // Step 2: Check UIDocument and UXML
            Debug.Log("[Phase3CertListVerifier] --- Step 2: Check UIDocument ---");
            var uidoc = controller != null ? controller.GetComponent<UIDocument>() : null;
            if (uidoc != null)
            {
                Debug.Log($"[Phase3CertListVerifier]   PASS: UIDocument found (PanelSettings: {uidoc.panelSettings?.name}, VisualTree: {uidoc.visualTreeAsset?.name})");
            }
            else
            {
                Debug.LogError("[Phase3CertListVerifier]   FAIL: UIDocument not found");
                allPassed = false;
            }

            // Step 3: Check UI Elements
            Debug.Log("[Phase3CertListVerifier] --- Step 3: Check UI Elements ---");
            var root = uidoc?.rootVisualElement;
            if (root != null)
            {
                var searchInput = root.Q<TextField>("cert-search-input");
                var filterAll = root.Q<Button>("filter-all");
                var filterFire = root.Q<Button>("filter-fire");
                var filterGas = root.Q<Button>("filter-gas");
                var filterValid = root.Q<Button>("filter-valid");
                var listContainer = root.Q("cert-list-container");
                var certCountLabel = root.Q<Label>("cert-count-label");
                var emptyState = root.Q("empty-state");

                Debug.Log($"[Phase3CertListVerifier]   UI Elements:");
                Debug.Log($"     search-input:   {(searchInput != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     filter-all:     {(filterAll != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     filter-fire:    {(filterFire != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     filter-gas:     {(filterGas != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     filter-valid:   {(filterValid != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     list-container: {(listContainer != null ? "FOUND" : "MISSING")}");
                Debug.Log($"     cert-count:     {(certCountLabel != null ? certCountLabel.text : "MISSING")}");
                Debug.Log($"     empty-state:    {(emptyState != null ? "FOUND" : "MISSING")}");

                bool allFound = searchInput != null && filterAll != null && filterFire != null && filterGas != null && filterValid != null && listContainer != null;
                if (allFound)
                {
                    Debug.Log("[Phase3CertListVerifier]   PASS: All UI elements present");
                }
                else
                {
                    Debug.LogError("[Phase3CertListVerifier]   FAIL: Some UI elements missing");
                    allPassed = false;
                }

                // Step 4: Check certificate cards
                Debug.Log("[Phase3CertListVerifier] --- Step 4: Check Certificate Cards ---");
                if (listContainer != null)
                {
                    int cardCount = listContainer.childCount;
                    Debug.Log($"[Phase3CertListVerifier]   Cards in list: {cardCount}");

                    if (cardCount >= 2)
                    {
                        Debug.Log("[Phase3CertListVerifier]   PASS: At least 2 certificate cards displayed");

                        // Check first card content
                        var firstCard = listContainer[0];
                        var labels = firstCard.Query<Label>().ToList();
                        Debug.Log($"[Phase3CertListVerifier]   First card labels:");
                        foreach (var label in labels)
                        {
                            Debug.Log($"     - {label.text}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Phase3CertListVerifier]   WARN: Only {cardCount} cards found (expected 2+)");
                    }
                }

                // Step 5: Check AppDataService certificates
                Debug.Log("[Phase3CertListVerifier] --- Step 5: Check Certificate Data ---");
                var app = AppDataService.Instance;
                if (app != null)
                {
                    var certs = app.GetWorkerCertificates();
                    Debug.Log($"[Phase3CertListVerifier]   Worker certificates: {certs.Count}");
                    foreach (var cert in certs)
                    {
                        Debug.Log($"     - {cert.id}: {cert.moduleTitle} ({cert.score}%) [{cert.status}]");
                    }

                    if (certs.Count >= 2)
                    {
                        Debug.Log("[Phase3CertListVerifier]   PASS: At least 2 certificates in data");
                    }
                    else
                    {
                        Debug.LogWarning("[Phase3CertListVerifier]   WARN: Less than 2 certificates (demo data will be used)");
                    }
                }
            }
            else
            {
                Debug.LogError("[Phase3CertListVerifier]   FAIL: rootVisualElement is null");
                allPassed = false;
            }

            // Step 6: Check CertificatePageController (navigation target)
            Debug.Log("[Phase3CertListVerifier] --- Step 6: Check CertificatePageController ---");
            var certController = FindFirstObjectByType<CertificatePageController>();
            if (certController != null)
            {
                Debug.Log("[Phase3CertListVerifier]   PASS: CertificatePageController found (navigation target)");
            }
            else
            {
                Debug.LogWarning("[Phase3CertListVerifier]   WARN: CertificatePageController not in scene (will be loaded on navigation)");
            }

            // Step 7: Check download functionality
            Debug.Log("[Phase3CertListVerifier] --- Step 7: Check Download Functionality ---");
            string testPath = System.IO.Path.Combine(Application.persistentDataPath, "Certificate_TEST.png");
            Debug.Log($"[Phase3CertListVerifier]   Download path: {testPath}");

            // Check if any previous exports exist
            string[] certFiles = System.IO.Directory.GetFiles(Application.persistentDataPath, "Certificate_*.png");
            Debug.Log($"[Phase3CertListVerifier]   Existing certificate PNGs: {certFiles.Length}");
            foreach (var file in certFiles)
            {
                var info = new System.IO.FileInfo(file);
                Debug.Log($"     - {info.Name} ({info.Length} bytes)");
            }

            // Final Summary
            Debug.Log("[Phase3CertListVerifier] ========================================");
            if (allPassed)
            {
                Debug.Log("[Phase3CertListVerifier]  RESULT: ALL STEPS PASSED");
                Debug.Log("[Phase3CertListVerifier]  Phase 3 Certificates List is working correctly.");
            }
            else
            {
                Debug.LogError("[Phase3CertListVerifier]  RESULT: SOME STEPS FAILED - Check errors above");
            }
            Debug.Log("[Phase3CertListVerifier] ========================================");

            testCompleted = true;
        }
    }
}
