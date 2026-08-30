using System;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    public enum ModuleStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Locked
    }

    [Serializable]
    public class CompetencyScores
    {
        public int hazardRecognition;
        public int extinguisherUse;
        public int ppeSelection;
        public int evacuation;
        public int emergencyResponse;
    }

    [Serializable]
    public class ModuleData
    {
        public string id;
        public string title;
        public string iconEmoji;
        public string domain;
        public string duration;
        public string difficulty;
        // Definition defaults — dynamic per-worker values are in ModuleProgress (Firestore), not here
        public ModuleStatus status;
        public int progress;
        public int bestScore;
        public int attempts;
        public string lastAttempt;
        public string certificateId;
        public string color;
        [TextArea] public string description;
        public string[] objectives;
        public CompetencyScores competencyScores;
    }

    [Serializable]
    public class ModuleProgress
    {
        public string moduleId;
        public ModuleStatus status = ModuleStatus.NotStarted;
        public int progress = 0;
        public int bestScore = 0;
        public int attempts = 0;
        public string lastAttempt = "";
        public string certificateId = "";
        public CompetencyScores competencyScores;
    }
}
