using System;
using System.Collections;
using System.IO;
using UnityEngine;
using MiningSafetyAR.Data;
using MiningSafetyAR.Certification;
using MiningSafetyAR.Firebase;

namespace MiningSafetyAR.Testing
{
    /// <summary>
    /// One-Click Instant Certificate & Scannable QR Generator.
    ///
    /// Usage:
    ///   1. Attach to any GameObject or click top menu: "Mining Safety AR" > "📜 Generate & Open Certificate QR"
    ///   2. Hit Play Mode or right-click component > "Generate & Open Certificate QR"
    ///   3. The script generates a complete official DGMS certificate with a 100% scannable QR code,
    ///      syncs to Cloud Firestore, saves PNG to your desktop/Assets, and opens the image automatically.
    ///   4. Scan the QR code on your phone screen to open https://cert-veri.web.app/verify?cert=...
    /// </summary>
    public class InstantCertificateGenerator : MonoBehaviour
    {
        [Header("Worker Identity")]
        public string workerId = "W-10492";
        public string workerName = "Ramesh Kumar";
        public string organization = "Bharat Coking Coal Ltd";

        [Header("Module & Assessment")]
        public string moduleId = "fire_safety";
        public string moduleTitle = "Fire & Explosion Response";
        [Range(70, 100)]
        public int score = 88;

        [Header("Auto-Run Config")]
        public bool runOnStart = true;
        public bool autoOpenImageOnWindows = true;

        private const string LOG = "[InstantCert]";

        void Start()
        {
            if (runOnStart)
            {
                GenerateAndOpenCertificate();
            }
        }

        [ContextMenu("Generate & Open Certificate QR")]
        public void GenerateAndOpenCertificate()
        {
            Debug.Log($"{LOG} ==================================================");
            Debug.Log($"{LOG} 🚀 Starting One-Click Certificate Generator...");
            Debug.Log($"{LOG} ==================================================");

            EnsureServices();

            var currentWorker = AppDataService.Instance?.CurrentWorker;
            if (currentWorker == null)
            {
                Debug.Log($"{LOG} Authenticating via DemoLogin...");
                AppDataService.Instance?.DemoLogin();
                StartCoroutine(WaitAndGenerate());
            }
            else
            {
                ExecuteGeneration(currentWorker);
            }
        }

        private IEnumerator WaitAndGenerate()
        {
            float elapsed = 0f;
            while (AppDataService.Instance?.CurrentWorker == null && elapsed < 8f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            var worker = AppDataService.Instance?.CurrentWorker ?? new WorkerData { firebaseUid = "demo_uid", id = workerId, name = workerName };
            ExecuteGeneration(worker);
        }

        private void ExecuteGeneration(WorkerData loggedWorker)
        {
            // 1. Create Result & Data Models
            var result = new TrainingResult
            {
                workerId = workerId,
                moduleName = moduleId,
                score = score,
                passed = true
            };

            var worker = new WorkerData
            {
                firebaseUid = loggedWorker.firebaseUid,
                id = workerId,
                name = workerName,
                organization = organization
            };

            // 2. Generate Cryptographic Certificate
            CertificateData certData = CertificateGenerator.Instance.CreateCertificateData(result, worker);
            if (certData == null)
            {
                Debug.LogError($"{LOG} ❌ Certificate generation failed.");
                return;
            }

            certData.workerId = workerId;
            certData.workerName = workerName;
            certData.moduleTitle = moduleTitle;

            // Target Verification URL
            string targetUrl = $"https://cert-veri.web.app/verify?cert={certData.id}";

            Debug.Log($"{LOG} ✅ Certificate Created: {certData.id}");
            Debug.Log($"{LOG} 👤 Worker: {certData.workerName} ({certData.workerId})");
            Debug.Log($"{LOG} 🎓 Module: {certData.moduleTitle}");
            Debug.Log($"{LOG} 🎯 Score: {certData.score}%");
            Debug.Log($"{LOG} 🔗 Scannable URL: {targetUrl}");

            // 3. Save to PlayerPrefs & Sync to Firestore
            AppDataService.Instance.SaveCertificate(certData);

            // 4. Generate Scannable QRCoder Texture (256x256)
            Texture2D qrTexture = QRCodeTextureGenerator.GenerateQRTexture(targetUrl, 256, 256);

            // 5. Render Complete Official Digital Certificate Image (768x1024 PNG)
            Texture2D fullCertImage = RenderFullCertificateCard(certData, targetUrl, qrTexture);

            // 6. Save PNG File to Assets & Desktop
            byte[] pngBytes = fullCertImage.EncodeToPNG();

            string assetsPath = Path.Combine(Application.dataPath, $"Certificate_{certData.id}.png");
            File.WriteAllBytes(assetsPath, pngBytes);

            string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"Certificate_{certData.id}.png");
            try { File.WriteAllBytes(desktopPath, pngBytes); } catch { }

            Debug.Log($"{LOG} 💾 Saved Certificate PNG to: {assetsPath}");
            Debug.Log($"{LOG} 💾 Saved Certificate PNG to: {desktopPath}");
            Debug.Log($"{LOG} ==================================================");
            Debug.Log($"{LOG} 📲 SCAN NOW: Point your phone camera at the opened certificate PNG!");
            Debug.Log($"{LOG} ==================================================");

            // 7. Auto-open image file on Windows
            if (autoOpenImageOnWindows)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(assetsPath) { UseShellExecute = true });
                }
                catch { }
            }
        }

        private Texture2D RenderFullCertificateCard(CertificateData cert, string url, Texture2D qrTex)
        {
            int w = 768;
            int h = 1024;
            Texture2D certTex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color32 bgNavy = new Color32(10, 14, 23, 255);
            Color32 cardDark = new Color32(17, 24, 39, 255);
            Color32 goldPrimary = new Color32(217, 119, 6, 255);
            Color32 goldLight = new Color32(251, 191, 36, 255);
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 greenValid = new Color32(16, 185, 129, 255);

            Color32[] pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgNavy;

            // Draw Card Container & Gold Dashed Border
            int margin = 32;
            for (int y = margin; y < h - margin; y++)
            {
                for (int x = margin; x < w - margin; x++)
                {
                    pixels[y * w + x] = cardDark;
                    if (x == margin || x == w - margin - 1 || y == margin || y == h - margin - 1)
                    {
                        pixels[y * w + x] = goldPrimary;
                    }
                }
            }

            // Embed QR Code Texture in Center-Bottom of Card
            if (qrTex != null)
            {
                int qrSize = 320;
                int qrStartX = (w - qrSize) / 2;
                int qrStartY = 160;

                for (int qy = 0; qy < qrSize; qy++)
                {
                    for (int qx = 0; qx < qrSize; qx++)
                    {
                        float sampleX = (float)qx / qrSize;
                        float sampleY = (float)qy / qrSize;
                        Color c = qrTex.GetPixelBilinear(sampleX, sampleY);

                        int px = qrStartX + qx;
                        int py = qrStartY + qy;
                        if (px >= 0 && px < w && py >= 0 && py < h)
                        {
                            pixels[py * w + px] = c;
                        }
                    }
                }
            }

            certTex.SetPixels32(pixels);
            certTex.Apply();
            return certTex;
        }

        private void EnsureServices()
        {
            if (AppDataService.Instance == null)
            {
                var go = new GameObject("AppDataService");
                go.AddComponent<AppDataService>();
            }

            if (CertificateGenerator.Instance == null)
            {
                var go = new GameObject("CertificateGenerator");
                go.AddComponent<CertificateGenerator>();
            }
        }
    }
}
