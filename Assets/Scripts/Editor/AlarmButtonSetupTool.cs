using UnityEditor;
using UnityEngine;

namespace MiningSafetyAR.EditorTools
{
    /// <summary>
    /// One-off helper to save the in-scene "AlarmButtonModel" GameObject as a prefab
    /// under Assets/Prefabs, mirroring FireExtinguisherModel.prefab. Drag-and-drop from
    /// Hierarchy to Project was unreliable in this session, so do it via script instead.
    /// </summary>
    public static class AlarmButtonSetupTool
    {
        [MenuItem("Mining Safety AR/Save AlarmButtonModel As Prefab")]
        public static void SaveAlarmButtonModelPrefab()
        {
            GameObject sceneObj = GameObject.Find("AlarmButtonModel");
            if (sceneObj == null)
            {
                Debug.LogError("[AlarmButtonSetupTool] Could not find a GameObject named 'AlarmButtonModel' in the open scene.");
                return;
            }

            const string prefabPath = "Assets/Prefabs/AlarmButtonModel.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(sceneObj, prefabPath, out bool success);
            if (success)
            {
                Debug.Log($"[AlarmButtonSetupTool] Saved prefab at {prefabPath}");
                Object.DestroyImmediate(sceneObj);
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
            }
            else
            {
                Debug.LogError("[AlarmButtonSetupTool] Failed to save prefab.");
            }
        }
    }
}
