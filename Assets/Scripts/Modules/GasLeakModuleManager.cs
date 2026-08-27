using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Modules
{
    public class GasLeakModuleManager : BaseModuleManager
    {
        [Header("Module 2 Audio Clips")]
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

        private void Awake()
        {
            moduleType = ModuleType.GasLeakAndConfinedSpace;
            moduleName = "Gas Leak & Confined Space Protocol";
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
                    PlayStepAudio(step3AudioEN, step3AudioHI, step3AudioSAT);
                    break;
                case 3:
                    PlayStepAudio(step4AudioEN, step4AudioHI, step4AudioSAT);
                    break;
            }
        }

        public void OnGasLeakSourceIdentified(bool isCorrectSource)
        {
            if (currentStepIndex != 0 || !isModuleActive) return;

            if (isCorrectSource)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Selected incorrect location! Monitor the gas detector reading for methane/CO buildup.");
            }
        }

        public void OnPPESelected(bool selectedSCBA)
        {
            if (currentStepIndex != 1 || !isModuleActive) return;

            if (selectedSCBA)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Standard dust mask is insufficient for toxic/oxygen-deficient confined space! Use SCBA.");
            }
        }

        public void OnBuddySignaled(bool signalSent)
        {
            if (currentStepIndex != 2 || !isModuleActive) return;

            if (signalSent)
            {
                CompleteCurrentStep();
            }
            else
            {
                RegisterMistake("Never enter a confined space without verifying communications with your standby buddy!");
            }
        }

        public void OnIsolationValveClosed()
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
                case 0: return "Step 1: Use your multi-gas detector to pinpoint the toxic methane/CO gas leak source.";
                case 1: return "Step 2: Equip the Self-Contained Breathing Apparatus (SCBA) from the AR safety locker.";
                case 2: return "Step 3: Signal your standby buddy miner and establish radio contact before entering.";
                case 3: return "Step 4: Close the gas isolation valve and evacuate the confined workspace immediately.";
                default: return "Module Complete!";
            }
        }
    }
}
