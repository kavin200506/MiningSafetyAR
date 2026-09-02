using System;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.UI.Helpers;

namespace MiningSafetyAR.UI.Pages
{
    public class LearningContentPageController : PageController, MiningSafetyAR.Localization.IVoiceCommandTarget
    {
        string moduleId;
        int currentSlide = 0;
        LearningSlide[] slides;

        Label slideCounter, slideTitle, tipText;
        VisualElement slideEmoji, slidePoints, learningFill;
        Button prevBtn, nextBtn, readyBtn, backBtn;

        [Serializable]
        class LearningSlide
        {
            public string emoji;
            public string iconClass;
            public string title;
            public string[] points;
            public string tip;
        }

        protected override void BindUI()
        {
            slideCounter = root.Q<Label>("slide-counter");
            slideEmoji = root.Q<VisualElement>("slide-emoji");
            slideTitle = root.Q<Label>("slide-title");
            tipText = root.Q<Label>("tip-text");
            slidePoints = root.Q("slide-points");
            learningFill = root.Q("learning-fill");
            prevBtn = root.Q<Button>("prev-btn");
            nextBtn = root.Q<Button>("next-btn");
            readyBtn = root.Q<Button>("ready-btn");
            backBtn = root.Q<Button>("back-btn");

            if (prevBtn != null) prevBtn.RegisterCallback<ClickEvent>(e => PrevSlide());
            if (nextBtn != null) nextBtn.RegisterCallback<ClickEvent>(e => NextSlide());
            if (readyBtn != null) readyBtn.RegisterCallback<ClickEvent>(e => OnReady());
            if (backBtn != null) backBtn.RegisterCallback<ClickEvent>(e => NavigationManager.Instance.GoBack());
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            LoadSlides();
        }

        void LoadSlides()
        {
            slides = moduleId switch
            {
                "fire_safety" => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-fire", title="Fire Safety Basics", points=new[]{"Understand fire classes (A, B, C, D, K)","Know the fire triangle: Heat, Fuel, Oxygen","Identify evacuation routes in your workplace","Recognize fire hazards in mining environments"}, tip="Always know at least 2 exit routes from any location in the mine." },
                    new LearningSlide { iconClass="slide-icon-extinguisher", title="P.A.S.S. Technique", points=new[]{"P - Pull the pin","A - Aim at the base of the fire","S - Squeeze the handle","S - Sweep side to side"}, tip="Stand 6-8 feet away from the fire when using an extinguisher." },
                    new LearningSlide { iconClass="slide-icon-evacuation", title="Evacuation Procedures", points=new[]{"Sound the alarm immediately","Close doors behind you","Stay low in smoky conditions","Proceed to the designated assembly point"}, tip="Never use elevators during a fire evacuation." }
                },
                "gas_safety" => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-gas", title="Gas Leak Hazards", points=new[]{"Methane (CH4) is explosive at 5-15%","Carbon Monoxide (CO) is odorless and deadly","Hydrogen Sulfide (H2S) smells like rotten eggs","Always use a multi-gas detector"}, tip="If you smell gas, evacuate immediately and alert others." },
                    new LearningSlide { iconClass="slide-icon-ppe", title="PPE for Gas Hazards", points=new[]{"SCBA (Self-Contained Breathing Apparatus)","Gas-tight chemical suit","Personal gas monitor","Two-way radio for communication"}, tip="Always check your SCBA pressure before entering a confined space." },
                    new LearningSlide { iconClass="slide-icon-confined", title="Confined Space Protocol", points=new[]{"Get a confined space entry permit","Test atmosphere before entry","Have a standby buddy outside","Maintain constant radio contact"}, tip="Never enter a confined space alone. The standby buddy saves lives." }
                },
                "machinery_safety" => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-lockout", title="Lockout/Tagout", points=new[]{"De-energize equipment","Apply lock and tag","Verify zero energy","Only the locker removes it"}, tip="LOTO saves lives — never bypass it." },
                    new LearningSlide { iconClass="slide-icon-guarding", title="Machine Guarding", points=new[]{"Guards must be in place","Never remove guards","Report damaged guards","Keep hands away"}, tip="Guards are there for your fingers." },
                    new LearningSlide { iconClass="slide-icon-operation", title="Safe Operation", points=new[]{"Pre-shift inspection","Use correct PPE","Follow SOP","Report near misses"}, tip="Slow is smooth, smooth is fast." }
                },
                "electrical_safety" => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-electrical", title="Electrical Hazards", points=new[]{"Wet hands + live wire = danger","Damaged cables are lethal","Overloaded circuits spark fires","Grounding prevents shock"}, tip="Assume all wires are live until proven dead." },
                    new LearningSlide { iconClass="slide-icon-gloves", title="Safe Work Practices", points=new[]{"Lockout before work","Use insulated tools","Wear rubber gloves","Test before touch"}, tip="One hand rule: keep one hand in pocket near live." }
                },
                "heights_safety" => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-fall", title="Fall Protection", points=new[]{"Required above 1.8m","Harness must fit snug","Inspect before each use","Anchor point 5000kg"}, tip="A harness not inspected is a hazard." },
                    new LearningSlide { iconClass="slide-icon-ladder", title="Ladder & Scaffold Safety", points=new[]{"3-point contact on ladder","Scaffold needs guardrails","Secure planks","No loose boards"}, tip="If it wobbles, don't use it." }
                },
                _ => new LearningSlide[]
                {
                    new LearningSlide { iconClass="slide-icon-fire", title="Training Content", points=new[]{"Review safety protocols","Understand emergency procedures","Practice proper equipment usage"}, tip="Complete all slides to proceed to the AR simulation." }
                }
            };
        }

        public override void OnPageEnter()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            if (slides == null) LoadSlides();
            currentSlide = 0;
            RefreshSlide();
        }

        void RefreshSlide()
        {
            if (slides == null || slides.Length == 0) return;
            var slide = slides[currentSlide];
            if (slideCounter != null) slideCounter.text = $"{currentSlide + 1}/{slides.Length}";
            if (slideEmoji != null)
            {
                slideEmoji.Clear();
                if (!string.IsNullOrEmpty(slide.iconClass))
                    IconLoader.ApplyByClass(slideEmoji, slide.iconClass);
            }
            if (slideTitle != null) slideTitle.text = slide.title;
            if (tipText != null) tipText.text = slide.tip;
            if (slidePoints != null)
            {
                slidePoints.Clear();
                foreach (var point in slide.points)
                {
                    var l = new Label($"• {point}");
                    l.style.fontSize = 12;
                    l.style.color = new StyleColor(new Color(0.26f,0.26f,0.26f));
                    l.style.marginBottom = 6;
                    l.style.whiteSpace = WhiteSpace.Normal;
                    slidePoints.Add(l);
                }
            }
            float progress = (float)(currentSlide + 1) / slides.Length * 100f;
            if (learningFill != null) ProgressBarHelper.SetProgress(learningFill.parent as VisualElement ?? learningFill, progress);
            // Actually learningFill is the fill, its parent is track
            var track = learningFill?.parent as VisualElement;
            if (track != null) ProgressBarHelper.SetProgress(track, progress);
            else if (learningFill != null) learningFill.style.width = Length.Percent(progress);

            if (prevBtn != null) prevBtn.style.display = currentSlide > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            bool isLast = currentSlide == slides.Length - 1;
            if (nextBtn != null) nextBtn.style.display = isLast ? DisplayStyle.None : DisplayStyle.Flex;
            if (readyBtn != null) readyBtn.style.display = isLast ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PrevSlide() { if (currentSlide > 0) { currentSlide--; RefreshSlide(); } }
        void NextSlide() { if (currentSlide < slides.Length - 1) { currentSlide++; RefreshSlide(); } }
        void OnReady() => NavigationManager.Instance.NavigateTo("AR Plane Detection Placement", moduleId);

        #region IVoiceCommandTarget Implementation
        public void VoiceNext()
        {
            if (currentSlide < slides.Length - 1) NextSlide();
            else OnReady();
        }

        public void VoiceSelectOption(int oneBasedIndex) { }
        public void VoiceStart() => OnReady();
        public void VoiceConfirm() => VoiceNext();
        public void VoiceCancel() => NavigationManager.Instance.GoBack();
        public void VoiceRepeat() => RefreshSlide();
        public void VoicePassStep(string step) { }
        #endregion
    }
}
