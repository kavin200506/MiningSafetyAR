#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
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

            // 3. Add AR Managers to XR Origin (Stage 8 & 9 & 10)
            if (originGO.GetComponent<ARRaycastManager>() == null)
            {
                originGO.AddComponent<ARRaycastManager>();
            }
            if (originGO.GetComponent<ARPlaneManager>() == null)
            {
                originGO.AddComponent<ARPlaneManager>();
            }
            if (originGO.GetComponent<ARPlacementManager>() == null)
            {
                originGO.AddComponent<ARPlacementManager>();
            }

            // 4. Setup Managers GameObject (LocalScoreManager, LanguageManager, CloudSyncManager)
            GameObject managersGO = GameObject.Find("AppManagers");
            if (managersGO == null)
            {
                managersGO = new GameObject("AppManagers");
                managersGO.AddComponent<LocalScoreManager>();
                managersGO.AddComponent<LanguageManager>();
                managersGO.AddComponent<CloudSyncManager>();
                managersGO.AddComponent<AssessmentEngine>();
                Undo.RegisterCreatedObjectUndo(managersGO, "Create App Managers");
                Debug.Log("[ARSceneBuilder] Created AppManagers container with local score, language, sync, and assessment components.");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("AR Scene Setup Complete", 
                "Successfully set up AR Session, XR Origin, AR Raycast Manager, AR Plane Manager, AR Placement Manager, and App Managers in the scene!", 
                "OK");
        }
    }
}
#endif
