using System;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public class WorkerData
    {
        public string firebaseUid;
        public string id;
        public string name;
        public string organization;
        public string sector;
        public string phone;
        public string language;
        public string profilePicUrl;
        public string joinDate;
        public int overallProgress;
        public int certificatesEarned;
        public int totalAttempts;
    }
}
