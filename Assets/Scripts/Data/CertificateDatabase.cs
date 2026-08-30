using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "CertificateDatabase", menuName = "MiningSafetyAR/Certificate Database")]
    public class CertificateDatabase : ScriptableObject
    {
        public List<CertificateData> certificates = new List<CertificateData>();

        public List<CertificateData> GetAll() => certificates;
        public CertificateData GetById(string id) => certificates.Find(c => c.id == id);
        public List<CertificateData> GetByWorker(string workerId) => certificates.FindAll(c => c.workerId == workerId);
    }
}
