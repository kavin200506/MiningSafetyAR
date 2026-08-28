#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using MiningSafetyAR.AR;
using MiningSafetyAR.Modules;
using MiningSafetyAR.Data;
using MiningSafetyAR.Assessment;
using MiningSafetyAR.Localization;
using MiningSafetyAR.Sync;

namespace MiningSafetyAR.Editor
{
    public class ARSceneBuilder : EditorWindow
    {
        [MenuItem("Mining Safety AR/Setup Core AR Scene")]
        public static void SetupARScene()
        {
            // 1. Ensure AR Session exists
            ARSession arSession = Object.FindFirstObjectByType<ARSession>();
            if (arSession == null)
            {
                GameObject sessionGO = new GameObject("AR Session");
                arSession = sessionGO.AddComponent<ARSession>();
                sessionGO.AddComponent<ARInputManager>();
                Undo.RegisterCreatedObjectUndo(sessionGO, "Create AR Session");
                Debug.Log("[ARSceneBuilder] Created AR Session GameObject.");
            }

            // 2. Ensure XR Origin exists
            XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>();
            GameObject originGO;
            if (xrOrigin == null)
            {
                originGO = new GameObject("XR Origin");
                xrOrigin = originGO.AddComponent<XROrigin>();
                Undo.RegisterCreatedObjectUndo(originGO, "Create XR Origin");

                // Camera Offset
                GameObject cameraOffset = new GameObject("Camera Offset");
                cameraOffset.transform.SetParent(originGO.transform, false);

                // Main Camera
                GameObject cameraGO = new GameObject("Main Camera");
                cameraGO.transform.SetParent(cameraOffset.transform, false);
                Camera cam = cameraGO.AddComponent<Camera>();
                cam.tag = "MainCamera";
                cameraGO.AddComponent<ARCameraManager>();
                cameraGO.AddComponent<ARCameraBackground>();

                xrOrigin.CameraFloorOffsetObject = cameraOffset;
                xrOrigin.Camera = cam;

                Debug.Log("[ARSceneBuilder] Created XR Origin with AR Camera.");
            }
            else
            {
                originGO = xrOrigin.gameObject;
            }

            // 3. Add AR Managers to XR Origin
            if (originGO.GetComponent<ARRaycastManager>() == null)
            {
                originGO.AddComponent<ARRaycastManager>();
            }

            ARPlaneManager planeManager = originGO.GetComponent<ARPlaneManager>();
            if (planeManager == null)
            {
                planeManager = originGO.AddComponent<ARPlaneManager>();
            }

            if (originGO.GetComponent<ARPlacementManager>() == null)
            {
                originGO.AddComponent<ARPlacementManager>();
            }

            // 4. Ensure AR Default Plane Prefab exists and is assigned
            GameObject planePrefab = EnsureARDefaultPlanePrefab();
            if (planePrefab != null && planeManager.planePrefab == null)
            {
                planeManager.planePrefab = planePrefab;
                Debug.Log("[ARSceneBuilder] Assigned AR Default Plane prefab to ARPlaneManager.");
            }

            // 5. Setup Managers GameObject (LocalScoreManager, LanguageManager, CloudSyncManager)
            GameObject managersGO = GameObject.Find("AppManagers");
            if (managersGO == null)
            {
                managersGO = new GameObject("AppManagers");
                managersGO.AddComponent<LocalScoreManager>();
                managersGO.AddComponent<LanguageManager>();
                managersGO.AddComponent<CloudSyncManager>();
                managersGO.AddComponent<AssessmentEngine>();
                Undo.RegisterCreatedObjectUndo(managersGO, "Create App Managers");
                Debug.Log("[ARSceneBuilder] Created AppManagers container.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("AR Scene Setup Complete", 
                "Successfully set up AR Session, XR Origin, AR Raycast Manager, AR Plane Manager (with visualizer plane prefab), AR Placement Manager, and App Managers in the scene!", 
                "OK");
        }

        private static GameObject EnsureARDefaultPlanePrefab()
        {
            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/ARDefaultPlane.prefab";

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            // Create temporary plane visualizer GameObject
            GameObject tempPlane = new GameObject("AR Default Plane");
            tempPlane.AddComponent<ARPlane>();
            tempPlane.AddComponent<ARPlaneMeshVisualizer>();
            tempPlane.AddComponent<MeshFilter>();
            
            MeshRenderer mr = tempPlane.AddComponent<MeshRenderer>();
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (defaultShader != null)
            {
                Material planeMat = new Material(defaultShader);
                planeMat.color = new Color(0.2f, 0.8f, 1.0f, 0.35f);
                mr.sharedMaterial = planeMat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempPlane, prefabPath);
            Object.DestroyImmediate(tempPlane);

            Debug.Log($"[ARSceneBuilder] Auto-created AR Default Plane prefab at {prefabPath}");
            return savedPrefab;
        }
    }
}
#endif
