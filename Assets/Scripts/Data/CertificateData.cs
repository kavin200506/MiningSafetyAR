using System;

namespace MiningSafetyAR.Data
{
    [Serializable]
    public class CertificateData
    {
        public string id;
        public string workerName;
        public string workerId;
        public string moduleId;
        public string moduleTitle;
        public int score;
        public string issuedDate;
        public string expiryDate;
        public string organization;
        public string status;
    }
}
