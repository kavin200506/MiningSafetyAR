#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.Rendering.Universal;
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
            // 1. Clean up old duplicate root Main Camera outside XR Origin
            Camera[] rootCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera cam in rootCameras)
            {
                if (cam != null && cam.transform.parent == null && cam.gameObject.name == "Main Camera")
                {
                    Undo.DestroyObjectImmediate(cam.gameObject);
                    Debug.Log("[ARSceneBuilder] Removed duplicate root Main Camera outside XR Origin.");
                }
            }

            // 2. Ensure AR Session exists
            ARSession arSession = Object.FindFirstObjectByType<ARSession>();
            if (arSession == null)
            {
                GameObject sessionGO = new GameObject("AR Session");
                arSession = sessionGO.AddComponent<ARSession>();
                sessionGO.AddComponent<ARInputManager>();
                sessionGO.AddComponent<AndroidCameraPermissionHelper>();
                Undo.RegisterCreatedObjectUndo(sessionGO, "Create AR Session");
                Debug.Log("[ARSceneBuilder] Created AR Session GameObject.");
            }

            // 3. Ensure XR Origin exists
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
                if (cameraGO.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() == null)
                {
                    cameraGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                }

                xrOrigin.CameraFloorOffsetObject = cameraOffset;
                xrOrigin.Camera = cam;

                Debug.Log("[ARSceneBuilder] Created XR Origin with AR Camera & TrackedPoseDriver.");
            }
            else
            {
                originGO = xrOrigin.gameObject;
            }

            if (xrOrigin.Camera != null)
            {
                if (xrOrigin.Camera.GetComponent<ARCameraManager>() == null) xrOrigin.Camera.gameObject.AddComponent<ARCameraManager>();
                if (xrOrigin.Camera.GetComponent<ARCameraBackground>() == null) xrOrigin.Camera.gameObject.AddComponent<ARCameraBackground>();
                if (xrOrigin.Camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() == null) xrOrigin.Camera.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();

                AROcclusionManager occlusionMgr = xrOrigin.Camera.GetComponent<AROcclusionManager>();
                if (occlusionMgr == null)
                {
                    occlusionMgr = xrOrigin.Camera.gameObject.AddComponent<AROcclusionManager>();
                }
                occlusionMgr.requestedEnvironmentDepthMode = UnityEngine.XR.ARSubsystems.EnvironmentDepthMode.Fastest;
                occlusionMgr.requestedOcclusionPreferenceMode = UnityEngine.XR.ARSubsystems.OcclusionPreferenceMode.PreferEnvironmentOcclusion;
                Debug.Log("[ARSceneBuilder] Configured AROcclusionManager on AR Camera.");
            }

            // 4. Add AR Managers to XR Origin
            if (originGO.GetComponent<ARRaycastManager>() == null)
            {
                originGO.AddComponent<ARRaycastManager>();
            }

            ARPlaneManager planeManager = originGO.GetComponent<ARPlaneManager>();
            if (planeManager == null)
            {
                planeManager = originGO.AddComponent<ARPlaneManager>();
            }

            ARPlacementManager placementManager = originGO.GetComponent<ARPlacementManager>();
            if (placementManager == null)
            {
                placementManager = originGO.AddComponent<ARPlacementManager>();
            }

            // 5. Ensure AR Default Plane Prefab exists and is assigned
            GameObject planePrefab = EnsureARDefaultPlanePrefab();
            if (planePrefab != null && planeManager.planePrefab == null)
            {
                planeManager.planePrefab = planePrefab;
                Debug.Log("[ARSceneBuilder] Assigned AR Default Plane prefab to ARPlaneManager.");
            }

            // 6. Ensure Placement Object Prefab exists and is assigned (Stage 10)
            GameObject placementPrefab = EnsureSamplePlacementPrefab();
            if (placementPrefab != null && placementManager.DefaultPlacementPrefab == null)
            {
                placementManager.DefaultPlacementPrefab = placementPrefab;
                Debug.Log("[ARSceneBuilder] Assigned Sample AR Equipment prefab to ARPlacementManager.");
            }

            // 7. Setup Managers GameObject (LocalScoreManager, LanguageManager, CloudSyncManager)
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

            // 8. Ensure URP Renderer has AR Background Renderer Feature & AR Command Buffer Support Feature
            EnsureARRendererFeatures();

            // 9. Enforce Android Min SDK Level 29 & prioritize OpenGLES3 for fast builds
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("AR Scene Setup Complete", 
                "Successfully set up AR Session, XR Origin, AR Raycast Manager, AR Plane Manager (with plane prefab), AR Placement Manager (with 3D object prefab), App Managers, and URP AR Renderer Features!", 
                "OK");
        }

        private static void EnsureARRendererFeatures()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");

            System.Type cmdFeatureType = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => {
                    try { return a.GetTypes(); } catch { return new System.Type[0]; }
                })
                .FirstOrDefault(t => t.Name == "ARCommandBufferSupportRendererFeature");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData != null)
                {
                    // 1. ARBackgroundRendererFeature
                    bool hasBgFeature = rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
                    if (!hasBgFeature)
                    {
                        ARBackgroundRendererFeature bgFeature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                        bgFeature.name = "AR Background Renderer Feature";
                        AssetDatabase.AddObjectToAsset(bgFeature, rendererData);
                        rendererData.rendererFeatures.Add(bgFeature);
                        EditorUtility.SetDirty(rendererData);
                        Debug.Log($"[ARSceneBuilder] Added ARBackgroundRendererFeature to URP Renderer at {path}");
                    }

                    // 2. ARCommandBufferSupportRendererFeature for Vulkan
                    if (cmdFeatureType != null)
                    {
                        bool hasCmdFeature = rendererData.rendererFeatures.Any(f => f != null && f.GetType() == cmdFeatureType);
                        if (!hasCmdFeature)
                        {
                            ScriptableRendererFeature cmdFeature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(cmdFeatureType);
                            cmdFeature.name = "AR Command Buffer Support Renderer Feature";
                            AssetDatabase.AddObjectToAsset(cmdFeature, rendererData);
                            rendererData.rendererFeatures.Add(cmdFeature);
                            EditorUtility.SetDirty(rendererData);
                            Debug.Log($"[ARSceneBuilder] Added ARCommandBufferSupportRendererFeature to URP Renderer at {path}");
                        }
                    }
                }
            }
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

                // Enable URP Transparency
                planeMat.SetFloat("_Surface", 1f); // 1 = Transparent
                planeMat.SetFloat("_Blend", 0f);   // 0 = Alpha blend mode
                planeMat.SetOverrideTag("RenderType", "Transparent");
                planeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                planeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                planeMat.SetInt("_ZWrite", 0);
                planeMat.DisableKeyword("_ALPHATEST_ON");
                planeMat.EnableKeyword("_ALPHABLEND_ON");
                planeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                planeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                mr.sharedMaterial = planeMat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempPlane, prefabPath);
            Object.DestroyImmediate(tempPlane);

            Debug.Log($"[ARSceneBuilder] Auto-created transparent AR Default Plane prefab at {prefabPath}");
            return savedPrefab;
        }

        private static GameObject EnsureSamplePlacementPrefab()
        {
            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/SampleAREquipment.prefab";

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempCube.name = "Sample AREquipment";
            tempCube.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            MeshRenderer mr = tempCube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (defaultShader != null)
                {
                    Material mat = new Material(defaultShader);
                    mat.color = new Color(1.0f, 0.4f, 0.0f); // Safety Orange
                    mr.sharedMaterial = mat;
                }
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempCube, prefabPath);
            Object.DestroyImmediate(tempCube);

            Debug.Log($"[ARSceneBuilder] Auto-created Sample AREquipment prefab at {prefabPath}");
            return savedPrefab;
        }
    }
}
#endif
