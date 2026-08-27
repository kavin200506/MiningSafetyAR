using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public enum Language
    {
        English,
        Hindi,
        Santali
    }

    [Serializable]
    public enum ModuleType
    {
        FireAndExplosion,
        GasLeakAndConfinedSpace
    }

    [Serializable]
    public class TrainingResult
    {
        public string resultId;
        public string workerId;
        public string moduleName;
        public int score;
        public int maxScore;
        public float percentage;
        public bool passed;
        public int mistakesCount;
        public float completionTimeSeconds;
        public string timestamp;
        public bool synced;

        public TrainingResult()
        {
            resultId = Guid.NewGuid().ToString();
            timestamp = DateTime.UtcNow.ToString("o");
            synced = false;
        }
    }

    [Serializable]
    public class TrainingResultListWrapper
    {
        public List<TrainingResult> results = new List<TrainingResult>();
    }

    [Serializable]
    public class QuizQuestion
    {
        public string questionId;
        public string questionTextTextEN;
        public string questionTextTextHI;
        public string questionTextTextSAT;
        public string[] optionsEN;
        public string[] optionsHI;
        public string[] optionsSAT;
        public int correctOptionIndex;
    }

    [Serializable]
    public class CertificatePayload
    {
        public string certificateId;
        public string workerId;
        public string moduleName;
        public int score;
        public string issuedAt;
        public string verificationUrl;
        public string signatureHash;
    }
}
