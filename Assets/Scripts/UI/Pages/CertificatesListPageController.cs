using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class CertificatesListPageController : PageController
    {
        TextField searchInput;
        Button backBtn, filterAll, filterFire, filterGas, filterValid;
        Label certCountLabel;
        VisualElement listContainer, emptyState;
        string activeFilter = "ALL";

        protected override void BindUI()
        {
            searchInput = root.Q<TextField>("cert-search-input");
            backBtn = root.Q<Button>("back-btn");
            filterAll = root.Q<Button>("filter-all");
            filterFire = root.Q<Button>("filter-fire");
            filterGas = root.Q<Button>("filter-gas");
            filterValid = root.Q<Button>("filter-valid");
            certCountLabel = root.Q<Label>("cert-count-label");
            listContainer = root.Q("cert-list-container");
            emptyState = root.Q("empty-state");

            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());

            if (searchInput != null)
            {
                searchInput.RegisterValueChangedCallback(e => RefreshList());
            }

            if (filterAll != null) filterAll.RegisterCallback<ClickEvent>(e => SetFilter("ALL"));
            if (filterFire != null) filterFire.RegisterCallback<ClickEvent>(e => SetFilter("fire_safety"));
            if (filterGas != null) filterGas.RegisterCallback<ClickEvent>(e => SetFilter("gas_leak"));
            if (filterValid != null) filterValid.RegisterCallback<ClickEvent>(e => SetFilter("VALID"));

            var tabHome = root.Q<Button>("tab-home");
            var tabTraining = root.Q<Button>("tab-training");
            var tabProgress = root.Q<Button>("tab-progress");
            var tabSettings = root.Q<Button>("tab-settings");
            if (tabHome != null) tabHome.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Dashboard"));
            if (tabTraining != null) tabTraining.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_TrainingCatalogue"));
            if (tabProgress != null) tabProgress.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Progress"));
            if (tabSettings != null) tabSettings.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.NavigateToTab("UI_Settings"));
        }

        public override void OnPageEnter()
        {
            RefreshList();
        }

        void SetFilter(string filter)
        {
            activeFilter = filter;
            UpdateFilterButtonStyles();
            RefreshList();
        }

        void UpdateFilterButtonStyles()
        {
            SetBtnStyle(filterAll, activeFilter == "ALL");
            SetBtnStyle(filterFire, activeFilter == "fire_safety");
            SetBtnStyle(filterGas, activeFilter == "gas_leak");
            SetBtnStyle(filterValid, activeFilter == "VALID");
        }

        void SetBtnStyle(Button btn, bool active)
        {
            if (btn == null) return;
            btn.style.backgroundColor = active ? new StyleColor(new Color(1f, 0.427f, 0f)) : new StyleColor(new Color(0.96f, 0.96f, 0.96f));
            btn.style.color = active ? new StyleColor(Color.white) : new StyleColor(new Color(0.4f, 0.4f, 0.4f));
        }

        void RefreshList()
        {
            if (listContainer == null) return;
            listContainer.Clear();

            var app = AppDataService.Instance;
            var worker = app != null ? app.CurrentWorker : null;
            var certs = app != null ? app.GetWorkerCertificates() : new List<CertificateData>();

            // Fallback demo data if list empty
            if (certs.Count == 0)
            {
                certs.Add(new CertificateData
                {
                    id = "JH-FIRE-849201",
                    workerName = worker != null ? worker.name : "Ramesh Kumar",
                    workerId = worker != null ? worker.id : "W-10492",
                    moduleId = "fire_safety",
                    moduleTitle = "Fire & Explosion Safety",
                    score = 88,
                    issuedDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                    expiryDate = System.DateTime.Now.AddYears(1).ToString("yyyy-MM-dd"),
                    organization = worker != null ? worker.organization : "Bharat Coking Coal Ltd",
                    status = "VALID"
                });

                certs.Add(new CertificateData
                {
                    id = "JH-GAS-002156",
                    workerName = worker != null ? worker.name : "Ramesh Kumar",
                    workerId = worker != null ? worker.id : "W-10492",
                    moduleId = "gas_leak",
                    moduleTitle = "Gas Leak & Confined Space",
                    score = 92,
                    issuedDate = System.DateTime.Now.ToString("yyyy-MM-dd"),
                    expiryDate = System.DateTime.Now.AddYears(1).ToString("yyyy-MM-dd"),
                    organization = worker != null ? worker.organization : "Bharat Coking Coal Ltd",
                    status = "VALID"
                });
            }

            string query = searchInput != null ? searchInput.value.Trim().ToLower() : "";

            var filtered = certs.Where(c =>
            {
                // Apply Search query
                if (!string.IsNullOrEmpty(query))
                {
                    bool matchId = c.id != null && c.id.ToLower().Contains(query);
                    bool matchTitle = c.moduleTitle != null && c.moduleTitle.ToLower().Contains(query);
                    if (!matchId && !matchTitle) return false;
                }

                // Apply Category Filter
                if (activeFilter == "fire_safety") return c.moduleId == "fire_safety";
                if (activeFilter == "gas_leak") return c.moduleId == "gas_leak";
                if (activeFilter == "VALID") return c.status == "VALID";

                return true;
            }).ToList();

            if (certCountLabel != null)
                certCountLabel.text = $"{filtered.Count} Certificate{(filtered.Count == 1 ? "" : "s")} Earned";

            if (emptyState != null)
                emptyState.style.display = filtered.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var cert in filtered)
            {
                var card = CreateCertificateCard(cert);
                listContainer.Add(card);
            }
        }

        VisualElement CreateCertificateCard(CertificateData cert)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.style.paddingTop = 12;
            card.style.paddingRight = 12;
            card.style.paddingBottom = 12;
            card.style.paddingLeft = 12;
            card.style.marginBottom = 10;
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;

            // Module Badge Icon
            var iconBox = new VisualElement();
            iconBox.style.width = 44;
            iconBox.style.height = 44;
            iconBox.style.backgroundColor = new StyleColor(new Color(1f, 0.94f, 0.88f));
            iconBox.style.borderTopLeftRadius = 8;
            iconBox.style.borderTopRightRadius = 8;
            iconBox.style.borderBottomLeftRadius = 8;
            iconBox.style.borderBottomRightRadius = 8;
            iconBox.style.alignItems = Align.Center;
            iconBox.style.justifyContent = Justify.Center;
            iconBox.style.marginRight = 12;

            var shieldIcon = new VisualElement();
            shieldIcon.AddToClassList("logo-shield");
            shieldIcon.style.width = 24;
            shieldIcon.style.height = 24;
            iconBox.Add(shieldIcon);
            card.Add(iconBox);

            // Details Container
            var details = new VisualElement();
            details.style.flexGrow = 1;

            var titleLabel = new Label(cert.moduleTitle ?? cert.moduleId);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.1f, 0.1f, 0.1f));

            var metaLabel = new Label($"ID: {cert.id} • Issued: {cert.issuedDate}");
            metaLabel.style.fontSize = 11;
            metaLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.55f));
            metaLabel.style.marginTop = 2;

            details.Add(titleLabel);
            details.Add(metaLabel);
            card.Add(details);

            // Score & Status Badge Column
            var rightCol = new VisualElement();
            rightCol.style.alignItems = Align.FlexEnd;

            var scoreLabel = new Label($"{cert.score}%");
            scoreLabel.style.fontSize = 14;
            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            scoreLabel.style.color = new StyleColor(new Color(0.1f, 0.1f, 0.1f));

            var badge = new Label("VALID");
            badge.AddToClassList("badge");
            badge.AddToClassList("badge--pass");
            badge.style.marginTop = 2;

            rightCol.Add(scoreLabel);
            rightCol.Add(badge);
            card.Add(rightCol);

            // Register click handler to open Single Certificate Detail View
            card.RegisterCallback<ClickEvent>(e =>
            {
                NavigationManager.Instance.NavigateTo("UI_Certificate", cert.moduleId ?? "fire_safety");
            });

            return card;
        }
    }
}
