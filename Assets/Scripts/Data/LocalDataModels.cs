using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public enum Language
    {
        English  = 0,   // PlayerPrefs int value — DO NOT renumber existing values
        Hindi    = 1,   // PlayerPrefs int value — DO NOT renumber existing values
        Santali  = 2,   // PlayerPrefs int value — DO NOT renumber existing values
        Tamil    = 3    // Added for voice command & TTS support (v2.0)
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
        public List<StepMetric> stepMetrics = new List<StepMetric>();

        // Geo Location fields for regional analytics
        public double latitude;
        public double longitude;
        public float locationAccuracyMeters;
        public string locationName;
        public bool hasLocation;
        public bool capturedOffline;

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
        public int v = 1;
        public string certificateId;
        public string workerId;
        public string workerName;
        public string moduleId;
        public string moduleName;
        public int score;
        public string issuedAt;
        public string expiryDate;
        public string organization;
        public string verificationUrl;
        public string signatureHash;
    }

    [Serializable]
    public class CertificateListWrapper
    {
        public List<CertificateData> certificates = new List<CertificateData>();
    }

    [Serializable]
    public struct StepMetric
    {
        public string stepName;
        public int errorCount;
        public float durationSeconds;
        public int score;
    }
}
