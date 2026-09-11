namespace MiningSafetyAR.Data
{
    /// <summary>
    /// Mistake-tag taxonomy for the adaptive post-training MCQ system (see QuizSelectionService).
    /// Every tag here corresponds to an ACTUAL detection point already wired into a real AR
    /// simulation script — see the comment on each constant for exactly where. This is intentionally
    /// not a wishlist: Machinery/Electrical/Working-at-Heights have no AR content at all yet (per
    /// project tech debt), so they have no tags here — there is nothing real to detect.
    ///
    /// Severity scale (1-3, independent of the drill-scoring point penalties in ScoringConstants —
    /// this is a separate, coarser scale used only to weight adaptive question selection):
    ///   1 = minor technique issue (still completed the step)
    ///   2 = safety-relevant lapse (procedural/PPE/awareness failure)
    ///   3 = critical failure (drill-ending or life-safety-protocol violation)
    /// </summary>
    public static class MistakeTags
    {
        // ---- Fire & Explosion (fire_safety / submodule "main") — FireSafetyModuleManager + ARProximitySafetyValidator ----

        /// <summary>Camera stayed within the 3.5ft safety radius of the fire. ARProximitySafetyValidator.CheckProximity().</summary>
        public const string UnsafeProximityToFire = "unsafe_proximity_to_fire";
        public const int UnsafeProximityToFireSeverity = 2;

        /// <summary>Extinguisher foam ran out before the fire was put out. FireSafetyModuleManager.HandleExtinguisherDepleted().</summary>
        public const string ExtinguisherDepletedBeforeOut = "extinguisher_depleted_before_out";
        public const int ExtinguisherDepletedBeforeOutSeverity = 3;

        /// <summary>Emergency alarm button was never pressed during the whole drill. FireSafetyModuleManager.FinishModule().</summary>
        public const string AlarmNotActivated = "alarm_not_activated";
        public const int AlarmNotActivatedSeverity = 2;

        /// <summary>Average Squeeze-and-Sweep spray intensity below the quality threshold. FireSafetyModuleManager.HandleFireExtinguished().</summary>
        public const string PoorSweepTechnique = "poor_sweep_technique";
        public const int PoorSweepTechniqueSeverity = 1;

        /// <summary>Took meaningfully longer than the evacuation time budget to reach the safe point. FireSafetyModuleManager.CompleteEvacuation().</summary>
        public const string SlowEvacuation = "slow_evacuation";
        public const int SlowEvacuationSeverity = 1;

        // ---- Gas Leak & Confined Space (gas_safety / submodule "main") — GasLeakModuleManager ----
        // NOTE: this module's mistake-detection logic exists in code, but per project tech debt has
        // no AR scene wired up yet, so these tags aren't reachable through real gameplay today —
        // wired here so they activate automatically once that AR scene is built.

        /// <summary>Identified the wrong location as the gas leak source. GasLeakModuleManager.OnGasLeakSourceIdentified(false).</summary>
        public const string WrongHazardLocalization = "wrong_hazard_localization";
        public const int WrongHazardLocalizationSeverity = 1;

        /// <summary>Chose a standard dust mask instead of SCBA for a toxic/oxygen-deficient space. GasLeakModuleManager.OnPPESelected(false).</summary>
        public const string MissedPpeCheck = "missed_ppe_check";
        public const int MissedPpeCheckSeverity = 2;

        /// <summary>Attempted confined-space entry without confirming the buddy-system signal first. GasLeakModuleManager.OnBuddySignaled(false).</summary>
        public const string UnsafeZoneEntry = "unsafe_zone_entry";
        public const int UnsafeZoneEntrySeverity = 3;

        // ---- Requested-but-not-wired examples (flagged, not fabricated) ----
        // "wrong_ppe_sequence" would need a multi-step, order-checked PPE donning sequence — the Gas
        // module currently only has a single binary PPE choice, no sequence to get wrong.
        // "ignored_gas_alarm" would need an alarm mechanic in the Gas module analogous to Fire's
        // AlarmButtonInteractable — no such mechanic exists yet. Neither constant is defined here;
        // add them once the corresponding gameplay actually exists rather than tagging events that
        // can't really happen.
    }
}
