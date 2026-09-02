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

        [SerializeField] private int minRequiredSteps = 5;
        [SerializeField] private int maxRequiredSteps = 15;
        [SerializeField] private float averageStepLengthMeters = 0.65f; // ~65cm per step

        private int targetSteps = 8;
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

            // Randomize target steps between minRequiredSteps (5) and maxRequiredSteps (15)
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
            Debug.Log($"[ARStepCounterTracker] [SIMULATED STEP] Step {currentStepCount}/{targetSteps}");
            OnStepCountUpdated?.Invoke(currentStepCount, targetSteps);

            if (currentStepCount >= targetSteps)
            {
                TriggerExtinguisherDiscovery();
            }
        }

        /// <summary>
        /// Called when the player reaches the required steps.
        /// Initiates a 5-second scan for a vertical wall plane. If found, places the extinguisher on the wall;
        /// otherwise places it on the floor using the exact original coordinates.
        /// </summary>
        private void TriggerExtinguisherDiscovery()
        {
            if (wallScanCoroutine != null)
            {
                StopCoroutine(wallScanCoroutine);
            }
            wallScanCoroutine = StartCoroutine(ScanForWallThenSpawnCoroutine(5.0f));
        }

        private IEnumerator ScanForWallThenSpawnCoroutine(float scanDuration)
        {
            currentState = StepTrackerState.ScanningForWall;
            float startTime = Time.time;
            ARPlaneManager planeMgr = FindFirstObjectByType<ARPlaneManager>();
            ARRaycastManager raycastMgr = ARRaycastManagerReference;
            var hits = new List<ARRaycastHit>();
            bool wallFound = false;
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            Debug.Log($"[ARStepCounterTracker] Step target reached ({currentStepCount}/{targetSteps})! Initiating 5-second wall detection scan...");

            while (Time.time - startTime < scanDuration)
            {
                scanRemainingTime = Mathf.Max(0f, scanDuration - (Time.time - startTime));
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);

                if (!wallFound && raycastMgr != null && raycastMgr.Raycast(screenCenter, hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinPolygon | UnityEngine.XR.ARSubsystems.TrackableType.Planes))
                {
                    foreach (var hit in hits)
                    {
                        if (planeMgr != null && hit.trackableId != UnityEngine.XR.ARSubsystems.TrackableId.invalidId)
                        {
                            ARPlane hitPlane = planeMgr.GetPlane(hit.trackableId);
                            if (hitPlane != null && hitPlane.alignment == UnityEngine.XR.ARSubsystems.PlaneAlignment.Vertical)
                            {
                                spawnPos = hit.pose.position;
                                spawnRot = hit.pose.rotation;
                                wallFound = true;
                                Debug.Log($"[ARStepCounterTracker] 🧯 VERTICAL WALL CONFIRMED during scan! Position: {spawnPos}");
                                break;
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(0.05f);
            }

            if (!wallFound)
            {
                Debug.Log("[ARStepCounterTracker] No vertical wall plane detected after 5-second scan — falling back to original floor placement.");
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();

                if (mainCam != null)
                {
                    Vector3 forward = mainCam.transform.forward;
                    forward.y = 0;
                    if (forward.sqrMagnitude < 0.01f) forward = mainCam.transform.up;
                    forward.Normalize();

                    // Spawn 1.0 meter in front of camera at ground level (exact original values)
                    spawnPos = mainCam.transform.position + (forward * 1.0f);
                    spawnPos.y = mainCam.transform.position.y - 0.5f;
                    spawnRot = Quaternion.Euler(0, mainCam.transform.eulerAngles.y, 0);
                }
                else
                {
                    spawnPos = new Vector3(0, -0.5f, 1.0f);
                    spawnRot = Quaternion.identity;
                }

                // Refine floor height if an AR plane is nearby (exact original logic)
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
            }

            // Create container and attach FireExtinguisherModelLoader
            GameObject container = new GameObject("Discovered_3D_FireExtinguisher");
            container.transform.SetPositionAndRotation(spawnPos, spawnRot);
            FireExtinguisherModelLoader loader = container.AddComponent<FireExtinguisherModelLoader>();

            spawnedExtinguisherInstance = container;
            currentState = StepTrackerState.ExtinguisherDiscovered;
            string placementType = wallFound ? "VERTICAL WALL" : "FLOOR";
            Debug.Log($"[ARStepCounterTracker] 🧯 DISCOVERY SUCCESS ({placementType})! 3D Fire Extinguisher spawned at {spawnPos}!");
            
            if (FireExtinguisherGrabController.Instance != null)
            {
                FireExtinguisherGrabController.Instance.SetupExtinguisherForGrabbing(container);
            }

            OnExtinguisherDiscovered?.Invoke(spawnPos);
            wallScanCoroutine = null;
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
                style.normal.textColor = new Color(1.0f, 0.85f, 0.0f); // Vivid Gold
                int remaining = Mathf.Max(0, targetSteps - currentStepCount);
                string text = $"<b>🚶 SEARCHING FOR EXTINGUISHER</b>\n" +
                              $"<size=24>Steps Taken: <color=#00FF00>{currentStepCount} / {targetSteps}</color> ({totalDistanceWalkedMeters:F1}m walked)</size>\n" +
                              $"<size=20><color=#00E5FF>Walk {remaining} more steps around room to find Extinguisher!</color></size>";
#if UNITY_EDITOR
                text += "\n<size=18><color=#00FF00>[EDITOR: Press 'W' or Spacebar to simulate steps]</color></size>";
#endif
                GUI.Box(rect, text, style);
            }
            else if (currentState == StepTrackerState.ScanningForWall)
            {
                style.normal.textColor = new Color(0.2f, 0.9f, 1.0f); // Bright Cyan
                string text = $"<b>🔍 SCANNING FOR WALL ({scanRemainingTime:F1}s)</b>\n" +
                              $"<size=22>Point camera at a wall to mount Extinguisher on wall!</size>\n" +
                              $"<size=18><color=#FFCC00>(If no wall detected in {scanRemainingTime:F1}s, places on floor)</color></size>";
                GUI.Box(rect, text, style);
            }
            else if (currentState == StepTrackerState.ExtinguisherDiscovered)
            {
                style.normal.textColor = new Color(0.2f, 1.0f, 0.4f); // Vivid Green
                string text = $"<b>🧯 EXTINGUISHER DISCOVERED!</b>\n" +
                              $"<size=24>Walked <color=#00FF00>{currentStepCount} steps</color> ({totalDistanceWalkedMeters:F1}m)</size>\n" +
                              $"<size=20><color=#00FF00>3D Fire Extinguisher spawned in front of you!</color></size>";
                GUI.Box(rect, text, style);
            }
        }
    }
}
