using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Modules
{
    public class FireSafetyModuleManager : BaseModuleManager
    {
        [Header("Module 1 Audio Clips")]
        [SerializeField] private AudioClip step1AudioEN;
        [SerializeField] private AudioClip step1AudioHI;
        [SerializeField] private AudioClip step1AudioSAT;

        [SerializeField] private AudioClip step2AudioEN;
        [SerializeField] private AudioClip step2AudioHI;
        [SerializeField] private AudioClip step2AudioSAT;

        [SerializeField] private AudioClip step3AudioEN;
        [SerializeField] private AudioClip step3AudioHI;
        [SerializeField] private AudioClip step3AudioSAT;

        [SerializeField] private AudioClip step4AudioEN;
        [SerializeField] private AudioClip step4AudioHI;
        [SerializeField] private AudioClip step4AudioSAT;

        private bool pinPulled = false;
        private bool nozzleAimed = false;
        private bool handleSqueezed = false;
        private bool sweptSideToSide = false;

        private void Awake()
        {
            moduleType = ModuleType.FireAndExplosion;
            moduleName = "Fire & Explosion Response";
            totalSteps = 4;
        }

        protected override void OnStepStart(int stepIndex)
        {
            switch (stepIndex)
            {
                case 0:
                    PlayStepAudio(step1AudioEN, step1AudioHI, step1AudioSAT);
                    break;
                case 1:
                    PlayStepAudio(step2AudioEN, step2AudioHI, step2AudioSAT);
                    break;
                case 2:
                    pinPulled = nozzleAimed = handleSqueezed = sweptSideToSide = false;
                    PlayStepAudio(step3AudioEN, step3AudioHI, step3AudioSAT);
                    break;
                case 3:
                    PlayStepAudio(step4AudioEN, step4AudioHI, step4AudioSAT);
                    break;
            }
        }

        public void OnEmergencyExitTapped(bool isCorrectExit)
        {
            if (currentStepIndex != 0 || !isModuleActive) return;

            if (isCorrectExit)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Selected incorrect emergency exit! Find the primary marked fire exit.");
            }
        }

        public void OnExtinguisherSelected(bool isValidExtinguisherType)
        {
            if (currentStepIndex != 1 || !isModuleActive) return;

            if (isValidExtinguisherType)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Selected wrong extinguisher type for electrical/chemical fire!");
            }
        }

        public void PerformPASSSubStep(string passStep)
        {
            if (currentStepIndex != 2 || !isModuleActive) return;

            switch (passStep.ToUpper())
            {
                case "PULL":
                    pinPulled = true;
                    Debug.Log("[FireSafetyModule] PASS Step 1: Pin Pulled");
                    break;
                case "AIM":
                    if (pinPulled) nozzleAimed = true;
                    else RegisterMistake("Must pull the pin before aiming!");
                    break;
                case "SQUEEZE":
                    if (pinPulled && nozzleAimed) handleSqueezed = true;
                    else RegisterMistake("Must aim nozzle at the base of the fire before squeezing handle!");
                    break;
                case "SWEEP":
                    if (pinPulled && nozzleAimed && handleSqueezed) sweptSideToSide = true;
                    else RegisterMistake("Must squeeze handle before sweeping!");
                    break;
            }

            if (pinPulled && nozzleAimed && handleSqueezed && sweptSideToSide)
            {
                CompleteCurrentStep();
            }
        }

        public void OnSafetyZoneReached()
        {
            if (currentStepIndex != 3 || !isModuleActive) return;
            CompleteCurrentStep();
        }

        private void PlayStepAudio(AudioClip en, AudioClip hi, AudioClip sat)
        {
            if (Localization.LanguageManager.Instance != null)
            {
                Localization.LanguageManager.Instance.PlayVoiceover(en, hi, sat);
            }
        }

        public override string GetStepInstruction(int stepIndex)
        {
            switch (stepIndex)
            {
                case 0: return "Step 1: Identify the primary emergency exit door in the AR environment.";
                case 1: return "Step 2: Locate and select the correct CO2/Dry Powder Fire Extinguisher.";
                case 2: return "Step 3: Execute the P.A.S.S. technique: Pull Pin -> Aim at base -> Squeeze -> Sweep.";
                case 3: return "Step 4: Follow the AR evacuation arrows to reach the safe assembly zone.";
                default: return "Module Complete!";
            }
        }
    }
}
