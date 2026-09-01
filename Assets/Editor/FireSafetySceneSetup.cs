using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MiningSafetyAR.Editor
{
    /// <summary>
    /// One-click scene wiring for ar_fire_safety.unity
    /// Menu: Tools > Mining Safety AR > Setup Fire Safety Scene
    /// </summary>
    public static class FireSafetySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/ar_fire_safety.unity";

        [MenuItem("Tools/Mining Safety AR/Setup Fire Safety Scene")]
        public static void SetupScene()
        {
            if (!EditorUtility.DisplayDialog("Setup Fire Safety Scene",
                "This will wire up ar_fire_safety.unity with all required GameObjects and components.\n\nContinue?",
                "Setup", "Cancel"))
                return;

            var currentScene = SceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            SetupLayers();
            CreateARSession();
            CreateARCamera();
            CreateARPlacement();
            CreateARProximityValidator();
            CreateFireSafetyModule();
            CreateHUDOverlay();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("[FireSafetySceneSetup] Scene setup complete!");
            EditorUtility.DisplayDialog("Done", "Fire Safety scene wired up successfully!", "OK");
        }

        private static void SetupLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            SetLayerIfEmpty(layers, 8, "FireHazard");
            SetLayerIfEmpty(layers, 9, "Grabbable");
            SetLayerIfEmpty(layers, 10, "HeldItem");

            tagManager.ApplyModifiedProperties();
            Debug.Log("[Setup] Layers configured: FireHazard(8), Grabbable(9), HeldItem(10)");
        }

        private static void SetLayerIfEmpty(SerializedProperty layers, int index, string name)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
                layer.stringValue = name;
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Debug.Log($"[Setup] Created: {name}");
            }
            return go;
        }

        private static void AddComponentByName(GameObject go, string typeName)
        {
            // Search all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == typeName && typeof(Component).IsAssignableFrom(type))
                    {
                        go.AddComponent(type);
                        return;
                    }
                }
            }
            Debug.LogWarning($"[Setup] Component type '{typeName}' not found in any assembly");
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
                comp = go.AddComponent<T>();
            return comp;
        }

        private static void CreateARSession()
        {
            GameObject go = FindOrCreate("ARSession");
            AddComponentByName(go, "ARSession");
            AddComponentByName(go, "ARInputManager");
        }

        private static void CreateARCamera()
        {
            GameObject go = FindOrCreate("AR Camera");
            Camera cam = EnsureComponent<Camera>(go);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            AddComponentByName(go, "ARCameraManager");
            AddComponentByName(go, "ARCameraBackground");
            AddComponentByName(go, "AROcclusionManager");
            AddComponentByName(go, "ARWaypointNavigationPointer");
        }

        private static void CreateARPlacement()
        {
            GameObject go = FindOrCreate("AR Placement");
            AddComponentByName(go, "ARRaycastManager");
            AddComponentByName(go, "ARPlaneManager");
            AddComponentByName(go, "ARAnchorManager");
            EnsureComponent<MiningSafetyAR.AR.ARPlacementManager>(go);
            EnsureComponent<MiningSafetyAR.AR.ARGuidanceController>(go);
        }

        private static void CreateARProximityValidator()
        {
            GameObject go = FindOrCreate("AR Proximity Validator");
            var validator = EnsureComponent<MiningSafetyAR.AR.ARProximitySafetyValidator>(go);

            SerializedObject so = new SerializedObject(validator);
            SerializedProperty distProp = so.FindProperty("safeDistanceThreshold");
            if (distProp != null) distProp.floatValue = 1.0668f;
            SerializedProperty hystProp = so.FindProperty("hysteresisMargin");
            if (hystProp != null) hystProp.floatValue = 0.15f;
            SerializedProperty penaltyProp = so.FindProperty("penaltyPoints");
            if (penaltyProp != null) penaltyProp.floatValue = 50f;
            so.ApplyModifiedProperties();
        }

        private static void CreateFireSafetyModule()
        {
            GameObject go = FindOrCreate("FireSafety Module");
            EnsureComponent<MiningSafetyAR.Modules.FireSafetyModuleManager>(go);
        }

        private static void CreateHUDOverlay()
        {
            GameObject go = FindOrCreate("HUD Overlay");
            UIDocument uidoc = EnsureComponent<UIDocument>(go);

            // Try to add ARSimulationPageController by name (avoid assembly reference issues)
            AddComponentByName(go, "ARSimulationPageController");

            // Try to assign the UXML asset
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/UI/Templates/Pages/ARSimulationPage.uxml");
            if (uxml != null && uidoc != null)
            {
                SerializedObject so = new SerializedObject(uidoc);
                // Try common property names for UIDocument
                SerializedProperty prop = so.FindProperty("m_VisualTreeAsset")
                    ?? so.FindProperty("visualTreeAsset")
                    ?? so.FindProperty("m_VisualAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = uxml;
                    so.ApplyModifiedProperties();
                    Debug.Log("[Setup] Assigned ARSimulationPage.uxml to HUD Overlay");
                }
                else
                {
                    Debug.LogWarning("[Setup] Could not find UXML property on UIDocument. Assign manually.");
                }
            }
            else
            {
                Debug.LogWarning("[Setup] ARSimulationPage.uxml not found at expected path.");
            }
        }
    }
}
