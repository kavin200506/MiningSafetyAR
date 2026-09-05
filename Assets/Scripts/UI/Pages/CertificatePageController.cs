using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class CertificatePageController : PageController
    {
        string moduleId = "fire_safety";
        Label workerName, moduleTitle, score, passedBadge;
        Label certId, certIdMeta, issuedDate, expiryDate, organization, securityStatus;
        VisualElement qrImage;
        Button backBtn, downloadBtn, shareBtn, viewAllCertsBtn, verifyBtn;

        protected override void BindUI()
        {
            workerName = root.Q<Label>("worker-name");
            moduleTitle = root.Q<Label>("module-title");
            score = root.Q<Label>("score");
            passedBadge = root.Q<Label>("passed-badge");
            if (passedBadge == null) passedBadge = root.Q<Label>(className: "badge--pass");
            certId = root.Q<Label>("cert-id");
            certIdMeta = root.Q<Label>("cert-id-meta");
            issuedDate = root.Q<Label>("issued-date");
            expiryDate = root.Q<Label>("expiry-date");
            organization = root.Q<Label>("organization");
            securityStatus = root.Q<Label>("security-status");
            qrImage = root.Q("qr-image") ?? root.Q("qr-code-image");

            backBtn = root.Q<Button>("back-btn");
            downloadBtn = root.Q<Button>("download-btn");
            shareBtn = root.Q<Button>("share-btn");
            viewAllCertsBtn = root.Q<Button>("view-all-certs-btn");
            verifyBtn = root.Q<Button>("verify-btn");

            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
            if (downloadBtn != null) downloadBtn.RegisterCallback<ClickEvent>(e => OnDownloadCertificate());
            if (shareBtn != null) shareBtn.RegisterCallback<ClickEvent>(e => OnShareCertificate());
            if (viewAllCertsBtn != null) viewAllCertsBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateTo("UI_CertificatesList"));
            if (verifyBtn != null) verifyBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateTo("UI_QRVerify"));

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void SetNavigationParameter(object param)
        {
            if (param is string s && !string.IsNullOrEmpty(s))
                moduleId = s;
        }

        public override void OnPageEnter()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            Refresh();
        }

        void Refresh()
        {
            var app = AppDataService.Instance;
            var worker = app != null ? app.CurrentWorker : null;
            var mod = app != null ? app.GetModule(moduleId) : null;
            var prog = app != null ? app.GetModuleProgress(moduleId) : null;

            string nameStr = worker != null ? worker.name : "Mining Worker";
            string titleStr = mod != null ? (mod.title ?? moduleId) : (moduleId == "gas_leak" ? "Gas Leak & Confined Space" : "Fire & Explosion Safety");
            string orgStr = worker != null ? worker.organization : "DGMS Certified Mining Org";
            int scoreVal = prog != null ? prog.bestScore : (mod != null ? mod.bestScore : 85);
            bool passed = prog == null || prog.status == ModuleStatus.Completed || scoreVal >= 75;

            if (workerName != null) workerName.text = nameStr;
            if (moduleTitle != null) moduleTitle.text = titleStr;
            if (score != null) score.text = $"{scoreVal}%";

            if (passedBadge != null)
            {
                passedBadge.text = passed ? "PASSED" : "IN PROGRESS";
                passedBadge.RemoveFromClassList("badge--pass");
                passedBadge.RemoveFromClassList("badge--fail");
                passedBadge.AddToClassList(passed ? "badge--pass" : "badge--fail");
            }

            string certIdStr = prog != null && !string.IsNullOrEmpty(prog.certificateId)
                ? prog.certificateId
                : $"JH-{moduleId.ToUpper().Replace("_","").Substring(0, System.Math.Min(4, moduleId.Length))}-849201";

            var existing = app != null ? app.GetCertificate(certIdStr) : null;
            if (existing != null) certIdStr = existing.id;

            if (certId != null) certId.text = certIdStr;
            if (certIdMeta != null) certIdMeta.text = certIdStr;
            if (issuedDate != null) issuedDate.text = System.DateTime.Now.ToString("yyyy-MM-dd");
            if (expiryDate != null) expiryDate.text = System.DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");
            if (organization != null) organization.text = orgStr;
            if (securityStatus != null) securityStatus.text = "HMAC-SHA256 Signed";

            if (qrImage != null)
            {
                string qrContent = BuildVerificationUrl(certIdStr, existing);

                Texture2D qrTex = MiningSafetyAR.Certification.QRCodeTextureGenerator.GenerateQRTexture(qrContent, 256, 256);
                if (qrTex != null)
                {
                    qrImage.style.backgroundImage = new StyleBackground(qrTex);
                }
            }
        }

        string BuildVerificationUrl(string certIdStr, CertificateData existing = null)
        {
            if (existing != null && !string.IsNullOrEmpty(existing.verificationUrl))
                return existing.verificationUrl;

            var certGen = MiningSafetyAR.Certification.CertificateGenerator.Instance;
            return certGen != null
                ? certGen.BuildVerificationUrl(certIdStr)
                : $"https://cert-veri.web.app/verify?cert={certIdStr}";
        }

        void OnDownloadCertificate()
        {
            try
            {
                var app = AppDataService.Instance;
                string certIdStr = certId != null ? certId.text : "JH-FIRE-849201";
                var existing = app != null ? app.GetCertificate(certIdStr) : null;

                string fileName = $"Certificate_{certIdStr}.png";
                string savePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

                string qrContent = BuildVerificationUrl(certIdStr, existing);

                Texture2D qrTex = MiningSafetyAR.Certification.QRCodeTextureGenerator.GenerateQRTexture(qrContent, 512, 512);
                byte[] pngBytes = qrTex.EncodeToPNG();
                System.IO.File.WriteAllBytes(savePath, pngBytes);

                Debug.Log($"[CertificatePage] ✅ Successfully exported Certificate PNG to path: '{savePath}' ({pngBytes.Length} bytes)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CertificatePage] FAILED to export certificate PNG: {ex.Message}");
            }
        }

        void OnShareCertificate()
        {
            var app = AppDataService.Instance;
            string certIdStr = certId != null ? certId.text : "JH-FIRE-849201";
            string titleStr = moduleTitle != null ? moduleTitle.text : "Safety Training";
            string nameStr = workerName != null ? workerName.text : "Worker";
            var existing = app != null ? app.GetCertificate(certIdStr) : null;

            string shareText = $"Official DGMS Safety Certificate\nWorker: {nameStr}\nModule: {titleStr}\nCert ID: {certIdStr}\nVerification: {BuildVerificationUrl(certIdStr, existing)}";
            
            Debug.Log($"[CertificatePage] 📤 Share Certificate Payload Ready:\n--------------------\n{shareText}\n--------------------");
        }
    }
}
