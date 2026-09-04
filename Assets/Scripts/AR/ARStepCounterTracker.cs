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
        [SerializeField] private bool showDebugUI = false;

        [Header("Prefabs")]
        [SerializeField] private GameObject fireExtinguisherPrefab;
        [Tooltip("Scale applied when the extinguisher first spawns on the wall — matches FireExtinguisherGrabController's held scale so it doesn't visibly resize when grabbed.")]
        [SerializeField] private float extinguisherSpawnScale = 0.42f;

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

            // Defensive reset: a prior run may have left ActivePlacementMode latched to
            // WallFireExtinguisher (see SpawnExtinguisherOnWall), which would misroute this
            // new ground-fire tap into spawning the extinguisher instead.
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.ActivePlacementMode = ARPlacementManager.PlacementTargetMode.GroundFireHazard;
            }

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
        /// Initiates 5-second wall scan: if a vertical wall is detected, mounts extinguisher on wall;
        /// otherwise automatically falls back to floor placement.
        /// </summary>
        private void TriggerExtinguisherDiscovery()
        {
            if (wallScanCoroutine != null)
            {
                StopCoroutine(wallScanCoroutine);
                wallScanCoroutine = null;
            }
            wallScanCoroutine = StartCoroutine(ScanForWallThenSpawnCoroutine(5.0f));
        }

        private IEnumerator ScanForWallThenSpawnCoroutine(float scanDuration)
        {
            currentState = StepTrackerState.ScanningForWall;
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.ActivePlacementMode = ARPlacementManager.PlacementTargetMode.WallFireExtinguisher;
            }

            float startTime = Time.time;
            ARPlaneManager planeMgr = FindFirstObjectByType<ARPlaneManager>();
            ARRaycastManager raycastMgr = ARRaycastManagerReference;
            var hits = new List<ARRaycastHit>();
            bool wallFound = false;
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            Debug.Log($"[SPAWN_DIAG] 🎯 STEP TARGET REACHED ({currentStepCount}/{targetSteps})! Initiating 5-second wall detection scan...");

            float lastLoggedTime = scanDuration;
            while (Time.time - startTime < scanDuration)
            {
                scanRemainingTime = Mathf.Max(0f, scanDuration - (Time.time - startTime));
                if (Mathf.Ceil(scanRemainingTime) < lastLoggedTime)
                {
                    lastLoggedTime = Mathf.Ceil(scanRemainingTime);
                    Debug.Log($"[WALL_SCAN_DIAG] ⏱️ 5s Wall Scan active: {lastLoggedTime:F0}s remaining...");
                }
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

                if (raycastMgr != null && raycastMgr.Raycast(screenCenter, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon | UnityEngine.XR.ARSubsystems.TrackableType.Planes))
                {
                    foreach (var hit in hits)
                    {
                        if (planeMgr != null && hit.trackableId != UnityEngine.XR.ARSubsystems.TrackableId.invalidId)
                        {
                            ARPlane hitPlane = planeMgr.GetPlane(hit.trackableId);
                            if (hitPlane != null && hitPlane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
                            {
                                spawnPos = hit.pose.position;
                                // hit.pose.up is the wall's outward normal (horizontal) for a vertical
                                // plane, not world-up — using it directly would lay the extinguisher on
                                // its side. Keep world-up as up, face outward along the wall's normal.
                                spawnRot = Quaternion.LookRotation(hit.pose.up, Vector3.up);
                                wallFound = true;
                                Debug.Log($"[WALL_SCAN_DIAG] 🧯 VERTICAL WALL DETECTED during 5s scan! Target position={spawnPos}");
                                break;
                            }
                        }
                    }
                }

                if (wallFound) break;
                yield return new WaitForSeconds(0.1f);
            }

            if (wallFound)
            {
                // The detected wall surface point can be arbitrarily far away depending on room
                // geometry, which previously had no distance limit here (unlike the fallback
                // branch below) — easily placing the extinguisher well outside the visible
                // frustum. Clamp it to stay close enough to guarantee visibility.
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (mainCam != null)
                {
                    float distToWall = Vector3.Distance(mainCam.transform.position, spawnPos);
                    const float maxVisibleSpawnDistance = 2.0f;
                    if (distToWall > maxVisibleSpawnDistance)
                    {
                        Vector3 dir = (spawnPos - mainCam.transform.position).normalized;
                        spawnPos = mainCam.transform.position + dir * maxVisibleSpawnDistance;
                        Debug.Log($"[SPAWN_DIAG] Wall hit was {distToWall:F2}m away — clamped spawn position to {maxVisibleSpawnDistance}m so it stays in view.");
                    }
                }
                SpawnExtinguisherOnWall(spawnPos, spawnRot);
            }
            else
            {
                Debug.Log("[WALL_SCAN_DIAG] ⏱️ No vertical wall plane detected after 5-second scan — falling back to floor/front placement.");
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();

                if (mainCam != null)
                {
                    Vector3 forward = mainCam.transform.forward;
                    forward.y = 0;
                    if (forward.sqrMagnitude < 0.01f) forward = mainCam.transform.up;
                    forward.Normalize();

                    spawnPos = mainCam.transform.position + (forward * 1.0f);
                    spawnPos.y = mainCam.transform.position.y - 0.5f;
                    spawnRot = Quaternion.Euler(0, mainCam.transform.eulerAngles.y, 0);
                }
                else
                {
                    spawnPos = new Vector3(0, -0.5f, 1.0f);
                    spawnRot = Quaternion.identity;
                }

                if (raycastMgr != null)
                {
                    hits.Clear();
                    Vector2 centerScreen = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    if (raycastMgr.Raycast(centerScreen, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon | UnityEngine.XR.ARSubsystems.TrackableType.Planes))
                    {
                        float dist = Vector3.Distance(mainCam != null ? mainCam.transform.position : Vector3.zero, hits[0].pose.position);
                        if (dist <= 2.0f)
                        {
                            spawnPos = hits[0].pose.position;
                        }
                    }
                }

                SpawnExtinguisherOnWall(spawnPos, spawnRot);
            }

            wallScanCoroutine = null;
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
        /// Called by ARPlacementManager or wall scan coroutine when spawning extinguisher.
        /// </summary>
        public void SpawnExtinguisherOnWall(Vector3 spawnPos, Quaternion spawnRot)
        {
            if (currentState != StepTrackerState.ScanningForWall) return;

            if (wallScanCoroutine != null)
            {
                StopCoroutine(wallScanCoroutine);
                wallScanCoroutine = null;
            }

            GameObject extinguisherInstance = null;

            if (fireExtinguisherPrefab == null)
            {
                fireExtinguisherPrefab = Resources.Load<GameObject>("Prefabs/FireExtinguisherModel") ??
                                         Resources.Load<GameObject>("FireExtinguisherModel");
            }

            if (fireExtinguisherPrefab != null)
            {
                extinguisherInstance = Instantiate(fireExtinguisherPrefab, spawnPos, spawnRot);
                extinguisherInstance.name = "Discovered_3D_FireExtinguisher";
                // The FBX model's own raw scale is ~1.6m tall (unscaled) — far larger than a real
                // extinguisher. FireExtinguisherGrabController only scales it down (to
                // extinguisherScale) once grabbed, so without this it sits on the wall at full
                // oversized scale beforehand, which can push it out of frame / look wrong.
                // Apply the same scale up front so it's correctly sized the whole time and
                // doesn't visibly "pop" in size at the moment of grab.
                extinguisherInstance.transform.localScale = Vector3.one * extinguisherSpawnScale;
            }
            else
            {
                // Create complete 3D Fire Red Extinguisher Model with URP Materials
                extinguisherInstance = new GameObject("Discovered_3D_FireExtinguisher");
                extinguisherInstance.transform.SetPositionAndRotation(spawnPos, spawnRot);

                // Body Cylinder (Red)
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "Extinguisher_RedBody";
                body.transform.SetParent(extinguisherInstance.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                body.transform.localScale = new Vector3(0.18f, 0.25f, 0.18f);

                // Destroy primitive collider to avoid interference
                Collider col = body.GetComponent<Collider>();
                if (col != null) Destroy(col);

                // Add main trigger capsule collider
                var mainCap = extinguisherInstance.AddComponent<CapsuleCollider>();
                mainCap.height = 0.6f;
                mainCap.radius = 0.12f;
                mainCap.center = new Vector3(0, 0.25f, 0);

                // Top Handle & Valve Assembly (Black)
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "Extinguisher_BlackHandle";
                handle.transform.SetParent(extinguisherInstance.transform, false);
                handle.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                handle.transform.localScale = new Vector3(0.08f, 0.08f, 0.12f);
                Collider handleCol = handle.GetComponent<Collider>();
                if (handleCol != null) Destroy(handleCol);

                // Yellow Safety Pin
                GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pin.name = "Green"; // Named "Green" or "Pin" for P.A.S.S. separation system
                pin.transform.SetParent(extinguisherInstance.transform, false);
                pin.transform.localPosition = new Vector3(0.06f, 0.53f, 0f);
                pin.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
                Collider pinCol = pin.GetComponent<Collider>();
                if (pinCol != null) Destroy(pinCol);

                // Nozzle Tip (Black)
                GameObject nozzle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                nozzle.name = "Extinguisher_NozzleTip";
                nozzle.transform.SetParent(extinguisherInstance.transform, false);
                nozzle.transform.localPosition = new Vector3(0f, 0.52f, 0.10f);
                nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                nozzle.transform.localScale = new Vector3(0.03f, 0.06f, 0.03f);
                Collider nozzleCol = nozzle.GetComponent<Collider>();
                if (nozzleCol != null) Destroy(nozzleCol);

                // Get compiled URP Lit shader
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ??
                                   Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                   Shader.Find("Universal Render Pipeline/Unlit");

                if (urpShader == null)
                {
                    Renderer[] sceneRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    foreach (Renderer r in sceneRenderers)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal"))
                        {
                            urpShader = r.sharedMaterial.shader;
                            break;
                        }
                    }
                }

                if (urpShader != null)
                {
                    Material redMat = new Material(urpShader);
                    redMat.SetColor("_BaseColor", new Color(0.88f, 0.08f, 0.08f));
                    redMat.SetFloat("_Metallic", 0.6f);
                    redMat.SetFloat("_Smoothness", 0.7f);
                    body.GetComponent<MeshRenderer>().material = redMat;

                    Material blackMat = new Material(urpShader);
                    blackMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));
                    blackMat.SetFloat("_Metallic", 0.4f);
                    blackMat.SetFloat("_Smoothness", 0.5f);
                    handle.GetComponent<MeshRenderer>().material = blackMat;
                    nozzle.GetComponent<MeshRenderer>().material = blackMat;

                    Material yellowMat = new Material(urpShader);
                    yellowMat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.0f));
                    pin.GetComponent<MeshRenderer>().material = yellowMat;
                }
            }

            // Ensure on Default layer (0) for Android mobile rendering
            SetLayerRecursively(extinguisherInstance, 0);

            spawnedExtinguisherInstance = extinguisherInstance;
            currentState = StepTrackerState.ExtinguisherDiscovered;

            // Wall-scan mode was only needed to route this one placement — release the latch
            // so subsequent ground taps (retesting, next fire ignition) go back to spawning fire.
            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.ActivePlacementMode = ARPlacementManager.PlacementTargetMode.GroundFireHazard;
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            float distToCam = mainCam != null ? Vector3.Distance(mainCam.transform.position, spawnPos) : -1f;
            int rendererCount = extinguisherInstance.GetComponentsInChildren<Renderer>(true).Length;
            Debug.Log($"[SPAWN_DIAG] 🧯 EXTINGUISHER SPAWNED! Name='{extinguisherInstance.name}' | WorldPos={spawnPos} | DistToCam={distToCam:F2}m | Scale={extinguisherInstance.transform.localScale} | RenderersCount={rendererCount} | State=Discovered(15-Steps+5s-Scan)");
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
            // Debug UI overlay step counter box completely disabled per user request
        }

        private static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, newLayer);
                }
            }
        }
    }
}
