namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Single source of truth for cross-file scoring numbers that were previously scattered
    /// and disagreeing (four different pass thresholds existed before this — see
    /// documents/scoring.md §1.6 and documents/technical_scoring_explained.md §1).
    /// </summary>
    public static class ScoringConstants
    {
        public const float PassThresholdPercentage = 70f;

        public const int GenericMistakePenalty = 25;
        public const int ProximityBreachPenalty = 50;

        public const float DrillWeight = 0.70f;
        public const float QuizWeight = 0.30f;
    }
}
