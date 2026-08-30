using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class CertificatePageController : PageController
    {
        string moduleId;
        Label workerName, moduleTitle, score, passedBadge;
        Label certId, certIdMeta, issuedDate, expiryDate, organization;
        Button backBtn, downloadBtn, shareBtn, verifyBtn;

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
            backBtn = root.Q<Button>("back-btn");
            downloadBtn = root.Q<Button>("download-btn");
            shareBtn = root.Q<Button>("share-btn");
            verifyBtn = root.Q<Button>("verify-btn");

            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
            if (downloadBtn != null) downloadBtn.RegisterCallback<ClickEvent>(e => Debug.Log("[Certificate] Download - not implemented (would save PNG)"));
            if (shareBtn != null) shareBtn.RegisterCallback<ClickEvent>(e => Debug.Log("[Certificate] Share - not implemented"));
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

        public override void SetNavigationParameter(object param) => moduleId = param as string;

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
            if (worker == null || mod == null)
            {
                if (workerName != null) workerName.text = "Test Worker";
                if (moduleTitle != null) moduleTitle.text = moduleId;
                if (score != null) score.text = "85%";
                if (passedBadge != null) { passedBadge.text = "PASSED"; passedBadge.RemoveFromClassList("badge--fail"); passedBadge.AddToClassList("badge--pass"); }
                if (certId != null) certId.text = "JH-TEST-000001";
                if (certIdMeta != null) certIdMeta.text = "JH-TEST-000001";
                if (issuedDate != null) issuedDate.text = System.DateTime.Now.ToString("yyyy-MM-dd");
                if (expiryDate != null) expiryDate.text = System.DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");
                if (organization != null) organization.text = "Test Org";
                return;
            }
            bool passed = prog != null && prog.status == ModuleStatus.Completed;
            int displayScore = prog != null ? prog.bestScore : mod.bestScore;
            if (workerName != null) workerName.text = worker.name;
            if (moduleTitle != null) moduleTitle.text = mod.title ?? moduleId;
            if (score != null) score.text = $"{displayScore}%";
            if (passedBadge != null)
            {
                passedBadge.text = passed ? "PASSED" : "FAILED";
                passedBadge.RemoveFromClassList("badge--pass");
                passedBadge.RemoveFromClassList("badge--fail");
                passedBadge.AddToClassList(passed ? "badge--pass" : "badge--fail");
            }
            string certIdStr = prog != null && !string.IsNullOrEmpty(prog.certificateId)
                ? prog.certificateId
                : $"JH-{moduleId.ToUpper().Substring(0, System.Math.Min(4, moduleId.Length))}-{Random.Range(100000,999999)}";
            var existing = app.GetCertificate(certIdStr);
            if (existing != null) certIdStr = existing.id;
            if (certId != null) certId.text = certIdStr;
            if (certIdMeta != null) certIdMeta.text = certIdStr;
            if (issuedDate != null) issuedDate.text = string.IsNullOrEmpty(mod.lastAttempt) ? System.DateTime.Now.ToString("yyyy-MM-dd") : mod.lastAttempt;
            if (expiryDate != null) expiryDate.text = System.DateTime.Now.AddYears(1).ToString("yyyy-MM-dd");
            if (organization != null) organization.text = worker.organization;
        }
    }
}
