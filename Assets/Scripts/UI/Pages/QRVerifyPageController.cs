using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class QRVerifyPageController : PageController
    {
        TextField certInput;
        Button verifyBtn, backBtn, viewCertBtn;
        Button qf1, qf2, qf3;
        VisualElement loadingCard, resultCard, verifyDetails;
        Label verifyBadge;

        protected override void BindUI()
        {
            certInput = root.Q<TextField>("cert-input");
            verifyBtn = root.Q<Button>("verify-btn");
            backBtn = root.Q<Button>("back-btn");
            viewCertBtn = root.Q<Button>("view-cert-btn");
            qf1 = root.Q<Button>("qf-1");
            qf2 = root.Q<Button>("qf-2");
            qf3 = root.Q<Button>("qf-3");
            loadingCard = root.Q("loading-card");
            resultCard = root.Q("result-card");
            verifyDetails = root.Q("verify-details");
            verifyBadge = root.Q<Label>("verify-badge");

            if (certInput != null)
            {
                certInput.SetEnabled(true);
                certInput.focusable = true;
                certInput.RegisterCallback<FocusInEvent>(e => ForceColors(certInput));
                ForceColors(certInput);
            }

            if (verifyBtn != null) verifyBtn.RegisterCallback<ClickEvent>(e => OnVerify());
            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
            if (qf1 != null) qf1.RegisterCallback<ClickEvent>(e => { if (certInput != null) certInput.value = "JH-FIRE-001928"; });
            if (qf2 != null) qf2.RegisterCallback<ClickEvent>(e => { if (certInput != null) certInput.value = "JH-GAS-002156"; });
            if (qf3 != null) qf3.RegisterCallback<ClickEvent>(e => { if (certInput != null) certInput.value = "INVALID-000"; });
            if (viewCertBtn != null) viewCertBtn.RegisterCallback<ClickEvent>(e => OnViewCertificate());

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        void ForceColors(TextField tf)
        {
            if (tf == null) return;
            tf.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f));
            tf.style.backgroundColor = new StyleColor(Color.white);
            foreach (var te in tf.Query<TextElement>().ToList()) te.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f));
        }

        async void OnVerify()
        {
            string certId = certInput != null ? certInput.value.Trim() : "";
            if (string.IsNullOrEmpty(certId)) return;
            if (loadingCard != null) loadingCard.style.display = DisplayStyle.Flex;
            if (resultCard != null) resultCard.style.display = DisplayStyle.None;
            await Task.Delay(800);
            var app = AppDataService.Instance;
            var cert = app != null ? app.GetCertificate(certId) : null;
            if (loadingCard != null) loadingCard.style.display = DisplayStyle.None;
            if (resultCard != null) resultCard.style.display = DisplayStyle.Flex;
            if (verifyDetails != null) verifyDetails.Clear();
            if (cert != null)
            {
                if (verifyBadge != null) { verifyBadge.text = "CERTIFICATE VALID"; verifyBadge.RemoveFromClassList("badge--fail"); verifyBadge.RemoveFromClassList("badge--pass"); verifyBadge.AddToClassList("badge--pass"); }
                AddDetail("Worker", cert.workerName);
                AddDetail("Worker ID", cert.workerId);
                AddDetail("Module", cert.moduleTitle);
                AddDetail("Score", $"{cert.score}%");
                AddDetail("Issued", cert.issuedDate);
                AddDetail("Expires", cert.expiryDate);
                AddDetail("Status", cert.status);
                if (viewCertBtn != null) viewCertBtn.style.display = DisplayStyle.Flex;
                viewCertBtn.userData = cert.moduleId;
            }
            else
            {
                if (verifyBadge != null) { verifyBadge.text = "INVALID CERTIFICATE"; verifyBadge.RemoveFromClassList("badge--pass"); verifyBadge.RemoveFromClassList("badge--fail"); verifyBadge.AddToClassList("badge--fail"); }
                var msg = new Label($"No certificate found with ID: {certId}");
                msg.style.color = new StyleColor(new Color(0.6f,0.6f,0.6f)); msg.style.fontSize = 12; msg.style.unityTextAlign = TextAnchor.MiddleCenter;
                if (verifyDetails != null) verifyDetails.Add(msg);
                if (viewCertBtn != null) viewCertBtn.style.display = DisplayStyle.None;
            }
        }

        void AddDetail(string label, string value)
        {
            var row = new VisualElement(); row.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row); row.style.marginBottom = 8;
            var l = new Label(label); l.style.flexGrow = 1; l.style.color = new StyleColor(new Color(0.44f,0.44f,0.44f)); l.style.fontSize = 12;
            var v = new Label(value); v.style.color = new StyleColor(new Color(0.1f,0.1f,0.1f)); v.style.fontSize = 12;
            row.Add(l); row.Add(v);
            if (verifyDetails != null) verifyDetails.Add(row);
        }

        void OnViewCertificate()
        {
            string mid = viewCertBtn != null && viewCertBtn.userData is string s ? s : "fire_safety";
            NavigationManager.Instance.NavigateTo("UI_Certificate", mid);
        }
    }
}
