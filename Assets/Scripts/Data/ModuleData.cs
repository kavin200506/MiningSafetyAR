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
        public int timeManagement; // Fire & Explosion "Time" competency — dedicated field so it
                                    // doesn't collide with ppeSelection, which other modules'
                                    // real PPE quiz questions already write into (see
                                    // documents/technical_scoring_explained.md §2).
        public int quizScore;      // Fire & Explosion "Quiz" competency — end-of-module MCQ
                                    // percentage, shown as its own bar (decided 2026-09-05).
                                    // Other modules don't populate this field.
    }

    [Serializable]
    public class ModuleData
    {
        public string id;
        public string title;
        public string iconEmoji;
        public string parentId;
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
