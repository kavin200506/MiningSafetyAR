using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// AR Step & Physical Walking Detector for Mining Safety AR.
    /// Tracks the player's physical steps and distance walked in real-world meters using AR Foundation 6-DOF camera tracking.
    /// Once the player ignites the fire and walks 5 to 15 steps searching the room:
    /// Scans for 5 seconds for a vertical wall plane. If detected, mounts the 3D Fire Extinguisher on the wall;
    /// otherwise falls back to the exact default floor placement!
    /// </summary>
    public class ARStepCounterTracker : MonoBehaviour
    {
        public static ARStepCounterTracker Instance { get; private set; }

        public enum StepTrackerState
        {
            Idle,
            SearchingForExtinguisher,
            ScanningForWall,
            ExtinguisherDiscovered
        }

        [Header("State & Step Settings")]
        [SerializeField] private StepTrackerState currentState = StepTrackerState.Idle;
        public StepTrackerState CurrentState => currentState;

        [SerializeField] private int minRequiredSteps = 7;
        [SerializeField] private int maxRequiredSteps = 13;
        [SerializeField] private float averageStepLengthMeters = 0.65f; // ~65cm per step

        [Header("Prefabs")]
        [SerializeField] private GameObject fireExtinguisherPrefab;

        private int targetSteps = 10;
        private int currentStepCount = 0;
        private float totalDistanceWalkedMeters = 0f;
        private float scanRemainingTime = 5.0f;
        private Coroutine wallScanCoroutine;

        public int CurrentStepCount => currentStepCount;
        public int TargetSteps => targetSteps;
        public float TotalDistanceWalkedMeters => totalDistanceWalkedMeters;

        private Vector3 lastCameraPosition;
        private float lastStepTime = 0f;
        private const float MinTimeBetweenSteps = 0.35f; // Human walking step frequency limit

        private GameObject spawnedExtinguisherInstance;
        public GameObject SpawnedExtinguisherInstance => spawnedExtinguisherInstance;

        public event Action<int, int> OnStepCountUpdated; // (currentSteps, targetSteps)
        public event Action<Vector3> OnExtinguisherDiscovered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                lastCameraPosition = mainCam.transform.position;
            }

            // Subscribe to fire ignition event from ARPlacementManager
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.OnObjectPlaced += OnFireHazardIgnited;
            }
        }

        private void OnDestroy()
        {
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.OnObjectPlaced -= OnFireHazardIgnited;
            }
            if (wallScanCoroutine != null)
            {
                StopCoroutine(wallScanCoroutine);
            }
        }

        /// <summary>
        /// Triggered when the user ignites the fire hazard on floor tap.
        /// Starts the physical step searching phase with a randomized target between 5 and 15 steps.
        /// </summary>
        public void OnFireHazardIgnited(Vector3 firePosition, Quaternion fireRotation)
        {
            if (wallScanCoroutine != null)
            {
                StopCoroutine(wallScanCoroutine);
                wallScanCoroutine = null;
            }

            // Randomize target steps between 7 and 13
            targetSteps = UnityEngine.Random.Range(minRequiredSteps, maxRequiredSteps + 1);
            currentStepCount = 0;
            totalDistanceWalkedMeters = 0f;
            currentState = StepTrackerState.SearchingForExtinguisher;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                lastCameraPosition = mainCam.transform.position;
            }

            Debug.Log($"[ARStepCounterTracker] 🔥 FIRE IGNITED! Emergency step tracking activated. Target steps to discover Fire Extinguisher: {targetSteps} steps.");
            OnStepCountUpdated?.Invoke(currentStepCount, targetSteps);
        }

        private void Update()
        {
            if (currentState != StepTrackerState.SearchingForExtinguisher)
                return;

            TrackPhysicalWalkingSteps();

#if UNITY_EDITOR
            // Unity Editor Play Mode Keyboard Simulation:
            // Press 'W' or Spacebar to simulate taking a physical step
            if (Keyboard.current != null && (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                RegisterSimulatedStep();
            }
#endif
        }

        /// <summary>
        /// Calculates physical displacement using 6-DOF AR Camera position tracking.
        /// Converts real displacement into human walking steps.
        /// </summary>
        private void TrackPhysicalWalkingSteps()
        {
            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return;

            Vector3 currentCamPos = mainCam.transform.position;
            // Measure horizontal distance moved (X-Z plane) to avoid height noise
            Vector3 camDisplacement = new Vector3(currentCamPos.x - lastCameraPosition.x, 0, currentCamPos.z - lastCameraPosition.z);
            float distMoved = camDisplacement.magnitude;

            if (distMoved > 0.05f) // Significant displacement detected
            {
                totalDistanceWalkedMeters += distMoved;
                lastCameraPosition = currentCamPos;

                // Check if distance walked equals a new step (~0.65m per step)
                int calculatedSteps = Mathf.FloorToInt(totalDistanceWalkedMeters / averageStepLengthMeters);
                if (calculatedSteps > currentStepCount && (Time.time - lastStepTime) >= MinTimeBetweenSteps)
                {
                    currentStepCount = calculatedSteps;
                    lastStepTime = Time.time;
                    Debug.Log($"[ARStepCounterTracker] 🚶 STEP DETECTED! Step {currentStepCount}/{targetSteps} (Total Distance: {totalDistanceWalkedMeters:F2}m)");
                    OnStepCountUpdated?.Invoke(currentStepCount, targetSteps);

                    if (currentStepCount >= targetSteps)
                    {
                        TriggerExtinguisherDiscovery();
                    }
                }
            }
        }

        /// <summary>
        /// Simulates taking a step for rapid Editor testing.
        /// </summary>
        public void RegisterSimulatedStep()
        {
            if (currentState != StepTrackerState.SearchingForExtinguisher) return;

            currentStepCount++;
            totalDistanceWalkedMeters += averageStepLengthMeters;
            lastStepTime = Time.time;
            ARSimulationLogger.LogKey("W/Space", $"Simulated Walking Step {currentStepCount}/{targetSteps}");
            Debug.Log($"[ARStepCounterTracker] [SIMULATED STEP] Step {currentStepCount}/{targetSteps}");
            OnStepCountUpdated?.Invoke(currentStepCount, targetSteps);

            if (currentStepCount >= targetSteps)
            {
                TriggerExtinguisherDiscovery();
            }
        }

        /// <summary>
        /// Called when the player reaches the required steps.
        /// Enters scanning state — waits for user to tap on a vertical wall plane.
        /// </summary>
        private void TriggerExtinguisherDiscovery()
        {
            currentState = StepTrackerState.ScanningForWall;
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.ActivePlacementMode = ARPlacementManager.PlacementTargetMode.WallFireExtinguisher;
            }
            Debug.Log($"[ARStepCounterTracker] Step target reached ({currentStepCount}/{targetSteps})! Ready to place extinguisher on wall. ActivePlacementMode set to WallFireExtinguisher.");
        }

        [ContextMenu("Simulate 1 Step Test")]
        public void SimulateStepTest()
        {
            RegisterSimulatedStep();
        }

        [ContextMenu("Simulate Wall Scanning Mode Test")]
        public void SimulateWallScanningModeTest()
        {
            currentStepCount = targetSteps;
            TriggerExtinguisherDiscovery();
        }

        /// <summary>
        /// Called by ARPlacementManager when user taps on a vertical plane.
        /// Spawns the extinguisher at the tapped position.
        /// </summary>
        public void SpawnExtinguisherOnWall(Vector3 spawnPos, Quaternion spawnRot)
        {
            if (currentState != StepTrackerState.ScanningForWall) return;

            GameObject extinguisherInstance;

            if (fireExtinguisherPrefab == null)
            {
                fireExtinguisherPrefab = Resources.Load<GameObject>("Prefabs/FireExtinguisherModel") ??
                                         Resources.Load<GameObject>("FireExtinguisherModel") ??
                                         Resources.Load<GameObject>("SafetyStation");
            }

            if (fireExtinguisherPrefab != null)
            {
                // Use the existing 3D prefab with PBR materials
                extinguisherInstance = Instantiate(fireExtinguisherPrefab, spawnPos, spawnRot);
                extinguisherInstance.name = "Discovered_3D_FireExtinguisher";
            }
            else
            {
                // Fallback: create basic container with glTF model loader
                extinguisherInstance = new GameObject("Discovered_3D_FireExtinguisher");
                extinguisherInstance.transform.SetPositionAndRotation(spawnPos, spawnRot);

                // Add glTF Model Loader to load real 3D model at runtime
                extinguisherInstance.AddComponent<FireExtinguisherModelLoader>();

                // Add fallback renderer with safe URP shader lookup
                var capsule = extinguisherInstance.AddComponent<CapsuleCollider>();
                capsule.height = 0.5f;
                capsule.radius = 0.08f;
                capsule.center = new Vector3(0, 0.25f, 0);

                var mf = extinguisherInstance.AddComponent<MeshFilter>();
                mf.sharedMesh = CreateCylinderMesh(0.08f, 0.5f, 12);

                var mr = extinguisherInstance.AddComponent<MeshRenderer>();
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ??
                                   Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                   Shader.Find("Universal Render Pipeline/Unlit");

                if (urpShader == null)
                {
                    Renderer[] sceneRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    foreach (Renderer r in sceneRenderers)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal Render Pipeline"))
                        {
                            urpShader = r.sharedMaterial.shader;
                            break;
                        }
                    }
                }

                if (urpShader != null)
                {
                    Material mat = new Material(urpShader);
                    mat.SetColor("_BaseColor", new Color(0.85f, 0.05f, 0.05f));
                    mat.SetFloat("_Metallic", 0.65f);
                    mat.SetFloat("_Smoothness", 0.75f);
                    mr.material = mat;
                }

                extinguisherInstance.AddComponent<FireExtinguisherGrabController>();
            }

            spawnedExtinguisherInstance = extinguisherInstance;
            currentState = StepTrackerState.ExtinguisherDiscovered;
            Debug.Log($"[ARStepCounterTracker] EXTINGUISHER SPAWNED on wall at {spawnPos}!");

            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.SetupExtinguisherForGrabbing(extinguisherInstance);
            }

            OnExtinguisherDiscovered?.Invoke(spawnPos);
        }

        private static Mesh CreateCylinderMesh(float radius, float height, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "ExtinguisherCylinder";
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            Vector3[] normals = new Vector3[(segments + 1) * 2];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[i] = new Vector3(x, 0, z);
                vertices[i + segments + 1] = new Vector3(x, height, z);
                normals[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                normals[i + segments + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            }

            for (int i = 0; i < segments; i++)
            {
                int ti = i * 6;
                triangles[ti] = i;
                triangles[ti + 1] = i + segments + 1;
                triangles[ti + 2] = i + 1;
                triangles[ti + 3] = i + 1;
                triangles[ti + 4] = i + segments + 1;
                triangles[ti + 5] = i + segments + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            return mesh;
        }

        private ARRaycastManager ARRaycastManagerReference => FindFirstObjectByType<ARRaycastManager>();

        private void OnGUI()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Splash") || sceneName.Contains("Login") || (sceneName.StartsWith("UI_") && sceneName != "UI_ARSimulation"))
                return;

            if (currentState == StepTrackerState.Idle)
                return;

            float screenWidth = Screen.width;
            float boxWidth = 640f;
            float boxHeight = 140f;
            float margin = 30f;
            Rect rect = new Rect((screenWidth - boxWidth) / 2f, margin + 140f, boxWidth, boxHeight);

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            if (currentState == StepTrackerState.SearchingForExtinguisher)
            {
                style.normal.textColor = new Color(1.0f, 0.85f, 0.0f);
                int remaining = Mathf.Max(0, targetSteps - currentStepCount);
                string text = $"<b>SEARCHING FOR EXTINGUISHER</b>\n" +
                              $"<size=24>Steps Taken: <color=#00FF00>{currentStepCount} / {targetSteps}</color> ({totalDistanceWalkedMeters:F1}m walked)</size>\n" +
                              $"<size=20><color=#00E5FF>Walk {remaining} more steps to find Extinguisher!</color></size>";
#if UNITY_EDITOR
                text += "\n<size=18><color=#00FF00>[EDITOR: Press 'W' or Spacebar to simulate steps]</color></size>";
#endif
                GUI.Box(rect, text, style);
            }
            else if (currentState == StepTrackerState.ScanningForWall)
            {
                style.normal.textColor = new Color(0.2f, 0.9f, 1.0f);
                string text = $"<b>READY TO PLACE EXTINGUISHER</b>\n" +
                              $"<size=22>Point camera at a vertical wall and TAP to place extinguisher!</size>\n" +
                              $"<size=18><color=#FFCC00>Extinguisher will spawn where you tap on the wall.</color></size>";
#if UNITY_EDITOR
                text += "\n<size=18><color=#00FF00>[EDITOR: Click on a wall plane]</color></size>";
#endif
                GUI.Box(rect, text, style);
            }
            else if (currentState == StepTrackerState.ExtinguisherDiscovered)
            {
                style.normal.textColor = new Color(0.2f, 1.0f, 0.4f);
                string text = $"<b>EXTINGUISHER DISCOVERED!</b>\n" +
                              $"<size=24>Walked <color=#00FF00>{currentStepCount} steps</color> ({totalDistanceWalkedMeters:F1}m)</size>\n" +
                              $"<size=20><color=#00FF00>Grab the extinguisher and put out the fire!</color></size>";
                GUI.Box(rect, text, style);
            }
        }
    }
}
