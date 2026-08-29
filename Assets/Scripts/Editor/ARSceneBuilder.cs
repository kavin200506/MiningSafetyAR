#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;
using MiningSafetyAR.AR;
using MiningSafetyAR.Modules;
using MiningSafetyAR.Data;
using MiningSafetyAR.Assessment;
using MiningSafetyAR.Localization;
using MiningSafetyAR.Sync;

using UnityEditor.XR.ARSubsystems;

namespace MiningSafetyAR.Editor
{
    public class ARSceneBuilder : EditorWindow
    {
        [MenuItem("Mining Safety AR/Setup Core AR Scene")]
        public static void SetupARScene()
        {
            // 0. Verify & Fix Active Input Handling to "Both"
            EnsureActiveInputHandlingBoth();

            // 1. Remove duplicate Main Cameras outside XR Origin
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
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            Debug.Log("[ARSceneBuilder] Set ARPlaneManager requestedDetectionMode to Horizontal & Vertical.");

            ARPlacementManager placementManager = originGO.GetComponent<ARPlacementManager>();
            if (placementManager == null)
            {
                placementManager = originGO.AddComponent<ARPlacementManager>();
            }

            ARTrackedImageManager imageManager = originGO.GetComponent<ARTrackedImageManager>();
            if (imageManager == null)
            {
                imageManager = originGO.AddComponent<ARTrackedImageManager>();
            }

            EnsureReferenceImageLibrary(imageManager);

            ARImageTrackingManager imageTrackingManager = originGO.GetComponent<ARImageTrackingManager>();
            if (imageTrackingManager == null)
            {
                imageTrackingManager = originGO.AddComponent<ARImageTrackingManager>();
            }

            GameObject extPrefab = EnsureFireExtinguisherPrefab();
            if (extPrefab != null && imageTrackingManager.FireExtinguisherPrefab == null)
            {
                imageTrackingManager.FireExtinguisherPrefab = extPrefab;
            }

            GameObject exitPrefab = EnsureExitSignPrefab();
            if (exitPrefab != null && imageTrackingManager.ExitSignPrefab == null)
            {
                imageTrackingManager.ExitSignPrefab = exitPrefab;
            }

            Debug.Log("[ARSceneBuilder] Configured ARTrackedImageManager, ARImageTrackingManager, and 3D equipment marker prefabs on XR Origin.");

            // 5. Ensure AR Default Plane Prefab & Materials exist and are assigned
            GameObject planePrefab = EnsureARDefaultPlanePrefab();
            if (planePrefab != null)
            {
                planeManager.planePrefab = planePrefab;
                Debug.Log("[ARSceneBuilder] Assigned transparent AR Default Plane prefab with boundary outline to ARPlaneManager.");
            }

            // 6. Ensure Placement Indicator Reticle exists and is assigned
            GameObject indicatorGO = GameObject.Find("Placement Indicator");
            if (indicatorGO == null)
            {
                GameObject indicatorPrefab = EnsurePlacementIndicatorPrefab();
                if (indicatorPrefab != null)
                {
                    indicatorGO = (GameObject)PrefabUtility.InstantiatePrefab(indicatorPrefab);
                    indicatorGO.name = "Placement Indicator";
                    Undo.RegisterCreatedObjectUndo(indicatorGO, "Create Placement Indicator");
                }
            }

            if (indicatorGO != null && placementManager != null)
            {
                placementManager.PlacementIndicator = indicatorGO;
                Debug.Log("[ARSceneBuilder] Assigned Placement Indicator to ARPlacementManager.");
            }

            // 7. Ensure Placement Equipment Prefab exists and is assigned
            GameObject placementPrefab = EnsureSamplePlacementPrefab();
            if (placementPrefab != null)
            {
                placementManager.DefaultPlacementPrefab = placementPrefab;
                Debug.Log($"[ARSceneBuilder] Assigned '{placementPrefab.name}' as placement prefab to ARPlacementManager.");
            }

            // 8. Setup Managers GameObject
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

            // 9. Ensure AR Renderer Features exist on ALL mobile and PC renderer assets
            EnsureARRendererFeatures();

            // 10. Configure Build Player Settings for fast compilation & compatibility
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new UnityEngine.Rendering.GraphicsDeviceType[] {
                UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3,
                UnityEngine.Rendering.GraphicsDeviceType.Vulkan
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("AR Scene Setup Complete", 
                "Successfully configured AR Session, XR Origin, AROcclusionManager, AR Plane Manager (with horizontal/vertical surface detection & crisp cyan boundary outlines), Placement Indicator reticle, AR Placement Manager, App Managers, Active Input Handling (Both), and URP Renderer Features!", 
                "OK");
        }

        [MenuItem("Mining Safety AR/Validate Editor Settings")]
        public static void ValidateEditorSettings()
        {
            EnsureActiveInputHandlingBoth();
            EnsureARRendererFeatures();

            System.Text.StringBuilder report = new System.Text.StringBuilder();
            report.AppendLine("=== Mining Safety AR - Editor Settings Validation Report ===");

            // 1. Active Input Handling Mode
            int inputMode = GetActiveInputHandlerMode();
            string inputModeName = inputMode switch
            {
                0 => "Legacy Input Manager",
                1 => "Input System Package (New)",
                2 => "Both (Legacy + New Input System) [CORRECT]",
                _ => $"Unknown ({inputMode})"
            };
            report.AppendLine($"1. Active Input Handling: {inputModeName}");

            // 2. XR Simulation Plugin Status on Standalone / PC Target
            bool xrSimEnabled = false;
            string xrLoaderName = "None";
            var standaloneSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (standaloneSettings != null && standaloneSettings.Manager != null)
            {
                foreach (var loader in standaloneSettings.Manager.activeLoaders)
                {
                    if (loader != null)
                    {
                        xrLoaderName = loader.GetType().Name;
                        if (xrLoaderName.Contains("Simulation") || xrLoaderName.Contains("Mock"))
                        {
                            xrSimEnabled = true;
                            break;
                        }
                    }
                }
            }
            report.AppendLine($"2. Standalone/PC XR Plug-in Manager: Loader='{xrLoaderName}', XR Simulation Enabled={xrSimEnabled}");

            // 3. Renderer Features on Mobile & PC Renderer Assets
            report.AppendLine("3. URP Renderer Assets AR Background Feature Check:");
            string[] guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData != null)
                {
                    bool hasBgFeature = rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
                    report.AppendLine($"   - Asset [{rendererData.name}] ({path}): ARBackgroundRendererFeature Present={hasBgFeature}");
                }
            }

            report.AppendLine("=============================================================");
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog("Editor Settings Validation", report.ToString(), "OK");
        }

        private static int GetActiveInputHandlerMode()
        {
            Object projectSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (projectSettingsAsset != null)
            {
                SerializedObject so = new SerializedObject(projectSettingsAsset);
                SerializedProperty prop = so.FindProperty("activeInputHandler");
                if (prop != null)
                {
                    return prop.intValue;
                }
            }
            return -1;
        }

        private static void EnsureActiveInputHandlingBoth()
        {
            Object projectSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (projectSettingsAsset != null)
            {
                SerializedObject so = new SerializedObject(projectSettingsAsset);
                SerializedProperty prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 1)
                {
                    prop.intValue = 1; // 1 = Input System Package (New) - Official supported mode for Android
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    Debug.Log("[ARSceneBuilder] Updated activeInputHandler to 1 ('Input System Package (New)') in ProjectSettings for Android compatibility.");
                }
            }
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
                    // 1. ARBackgroundRendererFeature for both Mobile & PC/Simulation Renderers
                    bool hasBgFeature = rendererData.rendererFeatures.Any(f => f is ARBackgroundRendererFeature);
                    if (!hasBgFeature)
                    {
                        ARBackgroundRendererFeature bgFeature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                        bgFeature.name = "AR Background Renderer Feature";
                        AssetDatabase.AddObjectToAsset(bgFeature, rendererData);
                        rendererData.rendererFeatures.Add(bgFeature);
                        EditorUtility.SetDirty(rendererData);
                        Debug.Log($"[ARSceneBuilder] Added ARBackgroundRendererFeature to URP Renderer Asset at {path}");
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
                            Debug.Log($"[ARSceneBuilder] Added ARCommandBufferSupportRendererFeature to URP Renderer Asset at {path}");
                        }
                    }
                }
            }
        }

        private static void FixMarkerTextureImporters()
        {
            string[] markerPaths = new string[] {
                "Assets/ImageTracking/FireExtinguisherMarker.jpg",
                "Assets/ImageTracking/ExitSignMarker.jpg",
                "Assets/ImageTracking/FireExtinguisherMarker.png",
                "Assets/ImageTracking/ExitSignMarker.png"
            };

            foreach (string path in markerPaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool modified = false;
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        modified = true;
                    }
                    if (importer.textureShape != TextureImporterShape.Texture2D)
                    {
                        importer.textureShape = TextureImporterShape.Texture2D;
                        modified = true;
                    }
                    if (modified)
                    {
                        importer.SaveAndReimport();
                        Debug.Log($"[ARSceneBuilder] Configured TextureImporter isReadable=true on {path}");
                    }
                }
            }
        }

        private static void EnsureReferenceImageLibrary(ARTrackedImageManager imageManager)
        {
            FixMarkerTextureImporters();

            string folderPath = "Assets/ImageTracking";
            string libraryPath = "Assets/ImageTracking/MiningSafetyImageLibrary.asset";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
                Debug.Log($"[ARSceneBuilder] Auto-created XRReferenceImageLibrary asset at {libraryPath}");
            }

            Texture2D extTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ImageTracking/FireExtinguisherMarker.jpg") ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ImageTracking/FireExtinguisherMarker.png");
            Texture2D exitTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ImageTracking/ExitSignMarker.jpg") ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ImageTracking/ExitSignMarker.png");

            if (library.count != 2)
            {
                while (library.count > 0)
                {
                    library.RemoveAt(0);
                }

                if (extTex != null)
                {
                    library.Add();
                    int fireIndex = library.count - 1;
                    library.SetTexture(fireIndex, extTex, true);
                    library.SetName(fireIndex, "FireExtinguisherMarker");
                    library.SetSize(fireIndex, new Vector2(0.2f, 0.2f));
                    library.SetSpecifySize(fireIndex, true);
                }

                if (exitTex != null)
                {
                    library.Add();
                    int exitIndex = library.count - 1;
                    library.SetTexture(exitIndex, exitTex, true);
                    library.SetName(exitIndex, "ExitSignMarker");
                    library.SetSize(exitIndex, new Vector2(0.2f, 0.2f));
                    library.SetSpecifySize(exitIndex, true);
                }

                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ARSceneBuilder] Rebuilt MiningSafetyImageLibrary using official XRReferenceImageLibraryExtensions API — {library.count} images configured.");
            }

            if (imageManager != null && imageManager.referenceLibrary == null)
            {
                imageManager.referenceLibrary = library;
                EditorUtility.SetDirty(imageManager);
                Debug.Log($"[ARSceneBuilder] Assigned MiningSafetyImageLibrary to ARTrackedImageManager.");
            }
        }

        private static GameObject EnsureARDefaultPlanePrefab()
        {
            GameObject refPlanePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AR Default Plane.prefab");
            if (refPlanePrefab != null)
            {
                Debug.Log("[ARSceneBuilder] Loaded and assigned exact AR Default Plane prefab from reference project.");
                return refPlanePrefab;
            }

            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/ARDefaultPlane.prefab";
            string materialPath = "Assets/Prefabs/ARDefaultPlaneMaterial.mat";
            string boundaryMaterialPath = "Assets/Prefabs/ARPlaneBoundaryMaterial.mat";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            // 1. Interior Surface Material (Semi-transparent Cyan)
            Material planeMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (planeMat == null && unlitShader != null)
            {
                planeMat = new Material(unlitShader);
                AssetDatabase.CreateAsset(planeMat, materialPath);
            }
            if (planeMat != null)
            {
                planeMat.color = new Color(0.2f, 0.7f, 1.0f, 0.25f);
                planeMat.SetFloat("_Surface", 1f); // Transparent
                planeMat.SetFloat("_Blend", 0f);   // Alpha blend
                planeMat.SetOverrideTag("RenderType", "Transparent");
                planeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                planeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                planeMat.SetInt("_ZWrite", 0);
                planeMat.DisableKeyword("_ALPHATEST_ON");
                planeMat.EnableKeyword("_ALPHABLEND_ON");
                planeMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(planeMat);
            }

            // 2. Boundary Outline Material (Vivid Solid Cyan)
            Material boundaryMat = AssetDatabase.LoadAssetAtPath<Material>(boundaryMaterialPath);
            if (boundaryMat == null && unlitShader != null)
            {
                boundaryMat = new Material(unlitShader);
                AssetDatabase.CreateAsset(boundaryMat, boundaryMaterialPath);
            }
            if (boundaryMat != null)
            {
                boundaryMat.color = new Color(0.0f, 0.9f, 1.0f, 0.85f);
                boundaryMat.SetFloat("_Surface", 1f);
                boundaryMat.SetFloat("_Blend", 0f);
                boundaryMat.SetOverrideTag("RenderType", "Transparent");
                boundaryMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                boundaryMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                boundaryMat.SetInt("_ZWrite", 0);
                boundaryMat.EnableKeyword("_ALPHABLEND_ON");
                boundaryMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(boundaryMat);
            }

            GameObject tempPlane = new GameObject("AR Default Plane");
            tempPlane.AddComponent<ARPlane>();
            tempPlane.AddComponent<ARPlaneMeshVisualizer>();
            tempPlane.AddComponent<MeshFilter>();
            
            MeshRenderer mr = tempPlane.AddComponent<MeshRenderer>();
            if (planeMat != null)
            {
                mr.sharedMaterial = planeMat;
            }

            LineRenderer lr = tempPlane.AddComponent<LineRenderer>();
            lr.startWidth = 0.015f;
            lr.endWidth = 0.015f;
            lr.useWorldSpace = false;
            if (boundaryMat != null)
            {
                lr.sharedMaterial = boundaryMat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempPlane, prefabPath);
            Object.DestroyImmediate(tempPlane);

            Debug.Log($"[ARSceneBuilder] Auto-created transparent AR Default Plane prefab with boundary outline at {prefabPath}");
            return savedPrefab;
        }

        private static GameObject EnsurePlacementIndicatorPrefab()
        {
            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/PlacementIndicator.prefab";
            string materialPath = "Assets/Prefabs/PlacementIndicatorMaterial.mat";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Material reticleMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (reticleMat == null)
            {
                Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (defaultShader != null)
                {
                    reticleMat = new Material(defaultShader);
                    AssetDatabase.CreateAsset(reticleMat, materialPath);
                }
            }
            if (reticleMat != null)
            {
                reticleMat.color = new Color(0.0f, 0.0f, 0.0f, 0.0f); // Fully Transparent Reticle (Hidden per user request)
                reticleMat.SetFloat("_Surface", 1f);
                reticleMat.SetFloat("_Blend", 0f);
                reticleMat.SetOverrideTag("RenderType", "Transparent");
                reticleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                reticleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                reticleMat.SetInt("_ZWrite", 0);
                reticleMat.DisableKeyword("_ALPHATEST_ON");
                reticleMat.EnableKeyword("_ALPHABLEND_ON");
                reticleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(reticleMat);
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            GameObject tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tempQuad.name = "Placement Indicator Reticle";
            tempQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Lay flat on floor
            tempQuad.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            MeshRenderer mr = tempQuad.GetComponent<MeshRenderer>();
            if (mr != null && reticleMat != null)
            {
                mr.sharedMaterial = reticleMat;
            }

            MeshCollider collider = tempQuad.GetComponent<MeshCollider>();
            if (collider != null) Object.DestroyImmediate(collider);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempQuad, prefabPath);
            Object.DestroyImmediate(tempQuad);

            Debug.Log($"[ARSceneBuilder] Auto-created Placement Indicator Reticle prefab at {prefabPath}");
            return savedPrefab;
        }

        private static GameObject EnsureSamplePlacementPrefab()
        {
            GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_Floor_01_Simple.prefab");
            if (firePrefab != null)
            {
                Debug.Log("[ARSceneBuilder] Loaded and assigned Vefects VFX_Fire_Floor_01_Simple.prefab as default placement object.");
                return firePrefab;
            }

            GameObject dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Dog/DogPrefab_Parent.prefab");
            if (dogPrefab != null)
            {
                return dogPrefab;
            }

            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/SampleAREquipment.prefab";
            string materialPath = "Assets/Prefabs/SampleAREquipmentMaterial.mat";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Material equipMat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (equipMat == null)
            {
                Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (defaultShader != null)
                {
                    equipMat = new Material(defaultShader);
                    equipMat.color = new Color(1.0f, 0.4f, 0.0f); // Safety Orange
                    AssetDatabase.CreateAsset(equipMat, materialPath);
                }
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempCube.name = "Sample AREquipment";
            tempCube.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            MeshRenderer mr = tempCube.GetComponent<MeshRenderer>();
            if (mr != null && equipMat != null)
            {
                mr.sharedMaterial = equipMat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempCube, prefabPath);
            Object.DestroyImmediate(tempCube);

            Debug.Log($"[ARSceneBuilder] Auto-created Sample AREquipment prefab at {prefabPath}");
            return savedPrefab;
        }

        private static GameObject EnsureFireExtinguisherPrefab()
        {
            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/FireExtinguisherModel.prefab";
            string materialPath = "Assets/Prefabs/FireExtinguisherMaterial.mat";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (defaultShader != null)
                {
                    mat = new Material(defaultShader);
                    mat.color = new Color(1.0f, 0.15f, 0.15f); // Vivid Safety Red
                    AssetDatabase.CreateAsset(mat, materialPath);
                }
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            GameObject tempCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tempCylinder.name = "Fire Extinguisher 3D";
            tempCylinder.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            MeshRenderer mr = tempCylinder.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempCylinder, prefabPath);
            Object.DestroyImmediate(tempCylinder);

            Debug.Log($"[ARSceneBuilder] Auto-created FireExtinguisherModel prefab at {prefabPath}");
            return savedPrefab;
        }

        private static GameObject EnsureExitSignPrefab()
        {
            string folderPath = "Assets/Prefabs";
            string prefabPath = "Assets/Prefabs/ExitSignModel.prefab";
            string materialPath = "Assets/Prefabs/ExitSignMaterial.mat";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat == null)
            {
                Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (defaultShader != null)
                {
                    mat = new Material(defaultShader);
                    mat.color = new Color(0.0f, 1.0f, 0.4f); // Vivid Emerald Green
                    AssetDatabase.CreateAsset(mat, materialPath);
                }
            }

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null) return existingPrefab;

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tempCube.name = "Exit Sign 3D";
            tempCube.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            MeshRenderer mr = tempCube.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempCube, prefabPath);
            Object.DestroyImmediate(tempCube);

            Debug.Log($"[ARSceneBuilder] Auto-created ExitSignModel prefab at {prefabPath}");
            return savedPrefab;
        }

        [MenuItem("Mining Safety AR/Add Vefects Fire VFX to Scene")]
        public static void AddGroundFireParticlesToScene()
        {
            string vefectsPrefabPath = "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_Floor_01_Simple.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(vefectsPrefabPath);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "VFX_Fire_Floor_01_Simple";
                instance.transform.position = new Vector3(0, 0, 1.5f);

                GroundFireController controller = instance.GetComponent<GroundFireController>();
                if (controller == null)
                {
                    controller = instance.AddComponent<GroundFireController>();
                }

                Undo.RegisterCreatedObjectUndo(instance, "Add Vefects Fire VFX to Scene");
                Selection.activeGameObject = instance;
                Debug.Log("[ARSceneBuilder] Added Vefects VFX_Fire_Floor_01_Simple to active Scene Hierarchy!");
            }
        }

        [MenuItem("Mining Safety AR/Set Default AR Placement Prefab to Vefects Fire")]
        public static void SetDefaultPlacementToGroundFire()
        {
            string vefectsPrefabPath = "Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_Floor_01_Simple.prefab";
            GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vefectsPrefabPath);
            int updatedCount = 0;
            
            ARPlacementManager placementManager = Object.FindFirstObjectByType<ARPlacementManager>();
            if (placementManager != null && firePrefab != null)
            {
                placementManager.DefaultPlacementPrefab = firePrefab;
                EditorUtility.SetDirty(placementManager);
                updatedCount++;
            }

            ARRaycastManager raycastManager = Object.FindFirstObjectByType<ARRaycastManager>();
            if (raycastManager != null && firePrefab != null)
            {
                raycastManager.raycastPrefab = firePrefab;
                EditorUtility.SetDirty(raycastManager);
                updatedCount++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            string msg = $"Successfully set 'VFX_Fire_Floor_01_Simple.prefab' as the active AR placement prefab on {updatedCount} components in scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'!\n\nNow click File -> Build And Run to test on your phone.";
            Debug.Log($"[ARSceneBuilder] {msg}");
            EditorUtility.DisplayDialog("Mining Safety AR — Prefab Assigned", msg, "OK");
        }

        [MenuItem("Mining Safety AR/Optimize Vefects Fire VFX for Mobile (ASTC & View Alignment)")]
        public static void OptimizeVefectsFireVFXForMobile()
        {
            int updatedTexturesCount = 0;
            int updatedParticleRenderersCount = 0;

            // 1. Optimize Texture Importers under Assets/Vefects
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture", new string[] { "Assets/Vefects" });
            foreach (string guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool modified = false;

                    if (importer.maxTextureSize > 512)
                    {
                        importer.maxTextureSize = 512;
                        modified = true;
                    }

                    // Android Platform Override: ASTC_6x6 & Max Size 512
                    TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                    if (!androidSettings.overridden || androidSettings.maxTextureSize > 512 || androidSettings.format != TextureImporterFormat.ASTC_6x6)
                    {
                        androidSettings.overridden = true;
                        androidSettings.maxTextureSize = 512;
                        androidSettings.format = TextureImporterFormat.ASTC_6x6;
                        importer.SetPlatformTextureSettings(androidSettings);
                        modified = true;
                    }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                        updatedTexturesCount++;
                        Debug.Log($"[ARSceneBuilder] Configured Android ASTC_6x6 & MaxSize 512 on Vefects texture: {path}");
                    }
                }
            }

            // 2. Set ParticleSystemRenderer Render Alignment to View (Billboard Camera Facing)
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/Vefects" });
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    ParticleSystemRenderer[] renderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
                    bool modified = false;
                    foreach (var psr in renderers)
                    {
                        if (psr != null && psr.alignment != ParticleSystemRenderSpace.View)
                        {
                            psr.alignment = ParticleSystemRenderSpace.View; // Billboard View Alignment
                            EditorUtility.SetDirty(psr);
                            updatedParticleRenderersCount++;
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        EditorUtility.SetDirty(prefab);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = $"Successfully optimized {updatedTexturesCount} Vefects textures (ASTC 6x6 / 512 Max Size) and {updatedParticleRenderersCount} ParticleSystemRenderers (View Alignment) for Mobile Android!";
            Debug.Log($"[ARSceneBuilder] {msg}");
            EditorUtility.DisplayDialog("Vefects Mobile Optimization Complete", msg, "OK");
        }
    }
}
#endif
