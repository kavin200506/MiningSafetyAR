using UnityEditor;
using UnityEngine;
using MiningSafetyAR.Data;

public class TestDB
{
    [MenuItem("Tools/Mining Safety/Test DB")]
    public static void Test()
    {
        var db = Resources.Load<ModuleDatabase>("Data/ModuleDatabase");
        if (db == null)
        {
            Debug.LogError("DB is null");
            return;
        }
        Debug.Log($"DB has {db.modules.Count} modules.");
        int count = 0;
        foreach(var m in db.modules)
        {
            if (m.parentId == "fire_safety")
            {
                Debug.Log($"Found sub-module: {m.title} with parent {m.parentId}");
                count++;
            }
        }
        Debug.Log($"Total fire_safety sub-modules: {count}");
    }
}
