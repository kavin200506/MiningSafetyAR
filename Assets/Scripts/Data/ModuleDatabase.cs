using System.Collections.Generic;
using UnityEngine;

namespace MiningSafetyAR.Data
{
    [CreateAssetMenu(fileName = "ModuleDatabase", menuName = "MiningSafetyAR/Module Database")]
    public class ModuleDatabase : ScriptableObject
    {
        public List<ModuleData> modules = new List<ModuleData>();

        public ModuleData GetById(string id) => modules.Find(m => m.id == id);
        public List<ModuleData> GetByStatus(ModuleStatus status) => modules.FindAll(m => m.status == status);
        public List<ModuleData> GetAll() => new List<ModuleData>(modules);
    }
}
