using UnityEngine;
using UnityEditor;
using MiningSafetyAR.Data;

public static class FixModuleDatabaseDynamic
{
    [MenuItem("Mining Safety AR/Fix - Reset ModuleDatabase to Dynamic (0%)")]
    public static void ResetModuleDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<ModuleDatabase>("Assets/Data/ModuleDatabase.asset");
        if (db == null) { Debug.LogError("ModuleDatabase not found"); return; }
        foreach (var m in db.modules)
        {
            m.progress = 0;
            m.bestScore = 0;
            m.attempts = 0;
            m.lastAttempt = "";
            m.certificateId = "";
            // Definition statuses: only heights locked, others NotStarted for new user
            if (m.id == "heights_safety") m.status = ModuleStatus.Locked;
            else if (m.id == "electrical_safety") m.status = ModuleStatus.NotStarted;
            else if (m.id == "machinery_safety") m.status = ModuleStatus.NotStarted;
            else m.status = ModuleStatus.NotStarted;
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Fix] ModuleDatabase reset: {db.modules.Count} modules set to NotStarted 0% (heights Locked)");
        foreach (var m in db.modules) Debug.Log($"[Fix] {m.id} status={m.status} progress={m.progress}");
    }
}
