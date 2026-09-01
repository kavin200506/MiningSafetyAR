using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using MiningSafetyAR.Modules;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Implements Unity's official AR Foundation 6.x architecture (arfoundation-samples pattern)
    /// utilizing InputAction("<Pointer>/press"), EnhancedTouch, ARRaycastManager surface raycasting,
    /// and ARAnchor spatial locking.
    /// Spawns ONLY the Fire Hazard on floor plane taps. Fire Extinguisher 3D model is reserved strictly for 2D Image Tracking.
    /// Includes instant Editor Play Mode simulation (Mouse click raycasting & New Input System Hotkeys 'F', 'E', 'C').
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    [RequireComponent(typeof(ARPlaneManager))]
    public class ARPlacementManager : MonoBehaviour
    {
        public static ARPlacementManager Instance { get; private set; }

        [Header("Placement Prefab")]
        [SerializeField] private GameObject defaultPlacementPrefab;
        public GameObject DefaultPlacementPrefab
        {
            get => defaultPlacementPrefab;
            set => defaultPlacementPrefab = value;
        }

        [Header("Wall Placement Prefab")]
        [SerializeField] private GameObject wallExtinguisherPrefab;
        public GameObject WallExtinguisherPrefab
        {
            get => wallExtinguisherPrefab;
            set => wallExtinguisherPrefab = value;
        }

        [Header("Placement Visual Indicator")]
        [SerializeField] private GameObject placementIndicator;
        public GameObject PlacementIndicator
        {
            get => placementIndicator;
            set => placementIndicator = value;
        }

        private ARRaycastManager raycastManager;
        private ARPlaneManager planeManager;
        private AROcclusionManager occlusionManager;
        private List<ARRaycastHit> hits = new List<ARRaycastHit>();

        private GameObject spawnedObject;
        private ARAnchor spawnedAnchor;
        public GameObject SpawnedObject => spawnedObject;

        public event Action OnFireHazardConfirmed;
        public bool IsFloorPlacementActive => spawnedObject != null && isPlacementLocked;
        public Transform FireHazardTransform => spawnedObject?.transform;

        private GameObject spawnedWallObject;
        private ARAnchor spawnedWallAnchor;
        public GameObject SpawnedWallObject => spawnedWallObject;

        [Header("Placement Window Settings (3 Seconds)")]
        [SerializeField] private float placementWindowDuration = 3.0f;
        public float PlacementWindowDuration
        {
            get => placementWindowDuration;
            set => placementWindowDuration = value;
        }

        [SerializeField] private bool showTimerUI = true;
        public bool ShowTimerUI
        {
            get => showTimerUI;
            set => showTimerUI = value;
        }

        private bool hasFirstPlacementOccurred = false;
        private float placementStartTime = -1f;
        private bool isPlacementLocked = false;

        public bool HasFirstPlacementOccurred => hasFirstPlacementOccurred;
        public bool IsPlacementLocked => isPlacementLocked;
        public float RemainingPlacementTime => hasFirstPlacementOccurred && !isPlacementLocked ? 
            Mathf.Max(0f, placementWindowDuration - (Time.time - placementStartTime)) : 0f;

        [Header("Reposition & Rescale")]
        [SerializeField] private float rescaleStep = 0.05f;
        [SerializeField] private float minScale = 0.02f;
        [SerializeField] private float maxScale = 1.0f;
        public bool RepositionMode { get; set; }
        public bool RescaleMode { get; set; }
        public event Action<GameObject, float> OnObjectRescaled;

        private float nextPlaneLogTime = 0f;
        private InputAction pressAction;

        public bool HasDetectedPlane => planeManager != null && planeManager.trackables.count > 0;

        public event Action<Vector3, Quaternion> OnObjectPlaced;
        public event Action OnNoPlaneDetected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WARN] [ARPlacementManager] Duplicate ARPlacementManager instance detected! Destroying extra component.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
            occlusionManager = GetComponent<AROcclusionManager>() ?? FindFirstObjectByType<AROcclusionManager>();

            if (FindFirstObjectByType<ARStepCounterTracker>() == null)
            {
                gameObject.AddComponent<ARStepCounterTracker>();
                Debug.Log("[ARPlacementManager] Auto-attached ARStepCounterTracker component to AR Placement Manager.");
            }

            // Setup Unity Official AR Foundation InputAction Pointer Press Architecture
            pressAction = new InputAction("touch", binding: "<Pointer>/press");
            pressAction.started += OnPointerPressBegan;

            Debug.Log($"[DIAG] [ARPlacementManager] Initializing official AR Foundation sample architecture on {SystemInfo.deviceModel} (OS: {SystemInfo.operatingSystem})");
            Debug.Log($"[DIAG] [ARPlacementManager] Graphics API: {SystemInfo.graphicsDeviceType}, Screen Resolution: {Screen.width}x{Screen.height}");
            Debug.Log($"[DIAG] [ARPlacementManager] Component status: RaycastManager={raycastManager != null}, PlaneManager={planeManager != null}, OcclusionManager={occlusionManager != null}");
            Debug.Log($"[DIAG] [ARPlacementManager] Inspector assignments: placementIndicator={(placementIndicator != null ? placementIndicator.name : "NULL")}, defaultPlacementPrefab={(defaultPlacementPrefab != null ? defaultPlacementPrefab.name : "NULL")}");
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (pressAction != null)
            {
                pressAction.Enable();
            }

            if (planeManager != null)
            {
                planeManager.trackablesChanged.AddListener(OnPlanesChanged);
            }
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            if (pressAction != null)
            {
                pressAction.Disable();
            }

            if (planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
            }
        }

        private void OnDestroy()
        {
            if (pressAction != null)
            {
                pressAction.Dispose();
            }
        }

        public enum PlacementTargetMode
        {
            GroundFireHazard,
            WallFireExtinguisher
        }

        [Header("Placement Target Configuration")]
        [SerializeField] private PlacementTargetMode placementMode = PlacementTargetMode.GroundFireHazard;
        public PlacementTargetMode ActivePlacementMode
        {
            get => placementMode;
            set => placementMode = value;
        }

        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
        {
            if (eventArgs.added.Count > 0)
            {
                foreach (var plane in eventArgs.added)
                {
                    Debug.Log($"[DIAG] [ARPlacementManager] NEW PLANE DETECTED! ID: {plane.trackableId}, Alignment: {plane.alignment}, Center: {plane.center}, Size: {plane.size}");
                }
            }
            if (eventArgs.removed.Count > 0)
            {
                Debug.Log($"[DIAG] [ARPlacementManager] Planes removed count: {eventArgs.removed.Count}");
            }
        }

        /// <summary>
        /// Uses input handling for touch & mouse pointer events with fallback to Editor simulation.
        /// </summary>
        private void CheckTouchInput()
        {
            try
            {
                Vector2 tapPosition = Vector2.zero;
                bool tapDetected = false;

                // 1. New Input System Enhanced Touch (Mobile Touchscreen Taps)
                if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
                {
                    var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        tapPosition = touch.screenPosition;
                        tapDetected = true;
                    }
                }

                // 2. New Input System Pointer / Mouse / Tap Press
                if (!tapDetected && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                {
                    tapPosition = Pointer.current.position.ReadValue();
                    tapDetected = true;
                }

                if (tapDetected)
                {
                    Debug.Log($"[DIAG] [ARPlacementManager] Tap Detected at {tapPosition}! HasDetectedPlane={HasDetectedPlane}");

                    // 1. Try direct tap position raycast
                    bool placed = PerformPlacementRaycast(tapPosition);
                    
                    // 2. Fallback to screen-center reticle position if direct tap raycast missed plane polygon
                    if (!placed)
                    {
                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        Debug.Log($"[DIAG] [ARPlacementManager] Direct tap at {tapPosition} missed plane polygon — trying screen-center reticle fallback at {screenCenter}");
                        placed = PerformPlacementRaycast(screenCenter);
                    }

                    if (!placed)
                    {
                        Debug.LogWarning("[WARN] [ARPlacementManager] Placement raycast was unhandled or failed — firing OnNoPlaneDetected event.");
                        OnNoPlaneDetected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [ARPlacementManager] Exception during CheckTouchInput: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnPointerPressBegan(InputAction.CallbackContext context)
        {
            try
            {
                if (context.control.device is Pointer pointerDevice)
                {
                    Vector2 tapPosition = pointerDevice.position.ReadValue();
                    Debug.Log($"[DIAG] [ARPlacementManager] Pointer Press Began at {tapPosition}!");

                    bool placed = PerformPlacementRaycast(tapPosition);
                    if (!placed)
                    {
                        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                        Debug.Log($"[DIAG] [ARPlacementManager] Direct tap at {tapPosition} missed plane polygon — trying screen-center reticle fallback at {screenCenter}");
                        placed = PerformPlacementRaycast(screenCenter);
                    }

                    if (!placed)
                    {
                        Debug.LogWarning("[WARN] [ARPlacementManager] Placement raycast was unhandled or failed — firing OnNoPlaneDetected event.");
                        OnNoPlaneDetected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [ARPlacementManager] Exception during OnPointerPressBegan: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Update()
        {
            UpdatePlacementIndicator();
            CheckTouchInput();

#if UNITY_EDITOR
            // Unity New Input System Editor Keyboard Hotkeys for Instant Testing:
            // [F] -> Ignite Fire Hazard at Mouse Position
            // [E] -> Simulate Scanning 2D Marker & Load 3D Fire Extinguisher Model
            // [C] -> Clear All Objects
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
                {
                    Vector2 mousePos = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
                    Debug.Log($"[EDITOR_HOTKEY] 'I'/'F' key pressed — Igniting Fire Hazard at mouse position {mousePos}");
                    PerformPlacementRaycast(mousePos);
                }
                if (Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Debug.Log("[EDITOR_HOTKEY] 'E' key pressed — Simulating 2D FireExtinguisherMarker tracking in Editor!");
                    if (ARImageTrackingManager.Instance != null)
                    {
                        ARImageTrackingManager.Instance.SimulateMarkerTracked("FireExtinguisherMarker");
                    }
                }
                if (Keyboard.current.cKey.wasPressedThisFrame)
                {
                    Debug.Log("[EDITOR_HOTKEY] 'C' key pressed — Clearing all spawned objects.");
                    ClearSpawnedObject();
                    if (ARImageTrackingManager.Instance != null) ARImageTrackingManager.Instance.ClearAllMarkerObjects();
                }
            }
#endif

            if (hasFirstPlacementOccurred && !isPlacementLocked)
            {
                float elapsedTime = Time.time - placementStartTime;
                if (elapsedTime >= placementWindowDuration)
                {
                    ConfirmPlacementLock();
                }
            }
        }

        /// <summary>
        /// Instantly locks ground placement, triggers placement confirmation event,
        /// enables proximity safety checking, and starts the fire safety module drill.
        /// </summary>
        public void ConfirmPlacementLock()
        {
            if (isPlacementLocked) return;

            isPlacementLocked = true;
            Debug.Log($"[INFO] [ARPlacementManager] Surface placement is now LOCKED.");
            OnFireHazardConfirmed?.Invoke();

            // Enable proximity safety check around the placed fire
            if (ARProximitySafetyValidator.Instance != null && spawnedObject != null)
            {
                ARProximitySafetyValidator.Instance.EnableChecking(spawnedObject.transform);
            }

            // Start the fire safety module
            if (FireSafetyModuleManager.Instance != null)
            {
                FireSafetyModuleManager.Instance.StartModule();
            }
        }

        private void UpdatePlacementIndicator()
        {
            try
            {
                if (Time.time >= nextPlaneLogTime)
                {
                    nextPlaneLogTime = Time.time + 1.0f;
                    int trackableCount = planeManager != null ? planeManager.trackables.count : 0;
                    Debug.Log($"[INFO] [ARPlacementManager] Active Tracked Planes: {trackableCount}");
                }

                if (placementIndicator == null)
                {
                    return;
                }

                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                TrackableType surfaceTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes;
                
                if (raycastManager != null && raycastManager.Raycast(screenCenter, hits, surfaceTypes) && hits.Count > 0)
                {
                    Pose hitPose = hits[0].pose;
                    placementIndicator.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                    if (placementIndicator.activeSelf) placementIndicator.SetActive(false);
                }
                else
                {
                    if (placementIndicator.activeSelf) placementIndicator.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [ARPlacementManager] Exception during UpdatePlacementIndicator: {ex.Message}");
            }
        }

        public bool PerformPlacementRaycast(Vector2 touchPosition, GameObject prefabToSpawn = null)
        {
            try
            {
                Pose hitPose = default;
                bool hitSuccess = false;
                string hitTypeString = "";
                TrackableId hitTrackableId = TrackableId.invalidId;

                // Tier 1: Real AR Plane Surface
                TrackableType planeTypes = TrackableType.AllTypes | TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes;
                if (raycastManager != null && raycastManager.Raycast(touchPosition, hits, planeTypes) && hits.Count > 0)
                {
                    hitPose = hits[0].pose;
                    hitTrackableId = hits[0].trackableId;
                    hitSuccess = true;
                    hitTypeString = "Plane Surface";
                    Debug.Log($"[DIAG] [ARPlacementManager] Tier 1 Hit: Plane Surface at pose {hitPose.position}, trackableId={hitTrackableId}, hitDistance={hits[0].distance:F2}m");
                }
                // Tier 2: Environment Depth Map
                else if (occlusionManager != null && 
                         occlusionManager.enabled && 
                         occlusionManager.descriptor != null && 
                         occlusionManager.descriptor.environmentDepthImageSupported == Supported.Supported)
                {
                    if (raycastManager.Raycast(touchPosition, hits, TrackableType.Depth) && hits.Count > 0)
                    {
                        hitPose = hits[0].pose;
                        hitSuccess = true;
                        hitTypeString = "Depth Map";
                        Debug.Log($"[DIAG] [ARPlacementManager] Tier 2 Hit: Depth Map at pose {hitPose.position}, hitDistance={hits[0].distance:F2}m");
                    }
                }
                // NO auto-placement fallbacks — fire ONLY spawns when user taps on a real detected plane

                if (!hitSuccess)
                {
                    int planesCount = planeManager != null ? planeManager.trackables.count : 0;
                    lastPlacementErrorLog = $"Raycast tap missed plane surface! Active planes count: {planesCount}";
                    Debug.LogWarning($"[FAIL_DIAG] [ARPlacementManager] {lastPlacementErrorLog}");
                    return false;
                }

                // --- 2. Surface Alignment & Mode Determination (Requirements 2 & 3) ---
                ARPlane hitPlane = null;
                if (planeManager != null)
                {
                    if (hitTrackableId != TrackableId.invalidId)
                    {
                        hitPlane = planeManager.GetPlane(hitTrackableId);
                        if (hitPlane == null)
                        {
                            Debug.LogWarning($"[WARN] [ARPlacementManager] Raycast hit trackable ID '{hitTrackableId}' but GetPlane() returned null.");
                        }
                    }
                    else if (hitTypeString == "Plane Surface")
                    {
                        Debug.LogWarning("[WARN] [ARPlacementManager] Raycast hit plane surface but trackableId is invalid.");
                    }
                }
                else
                {
                    Debug.LogWarning("[WARN] [ARPlacementManager] ARPlaneManager is not assigned or unavailable.");
                }

                PlaneAlignment alignment = hitPlane != null ? hitPlane.alignment : PlaneAlignment.None;
                bool isHorizontalPlane = (alignment == PlaneAlignment.HorizontalUp || alignment == PlaneAlignment.HorizontalDown);
                bool isVerticalPlane = (alignment == PlaneAlignment.Vertical);

                if (hitPlane != null && !isHorizontalPlane && !isVerticalPlane)
                {
                    Debug.LogWarning($"[WARN] [ARPlacementManager] Hit plane '{hitPlane.trackableId}' has unsupported alignment '{alignment}'.");
                }

                // Determine whether target is Wall Extinguisher vs Ground Hazard
                bool isWallPlacement = (hitPlane != null && isVerticalPlane) || (hitPlane == null && placementMode == PlacementTargetMode.WallFireExtinguisher);

                // Requirement 6: Apply 3-second placement lockout ONLY to Ground Fire Hazard
                // Allow reposition and rescale modes to bypass the lock
                if (!isWallPlacement && isPlacementLocked && !RepositionMode && !RescaleMode)
                {
                    Debug.LogWarning("[WARN] [ARPlacementManager] Ground fire hazard placement tap blocked — 3-second placement window has expired.");
                    return false;
                }

                // Rescale mode: tap on existing objects to scale them
                if (RescaleMode && (spawnedObject != null || spawnedWallObject != null))
                {
                    return PerformRescale(hitPose);
                }

                // Step counter mode: if in ScanningForWall state and tapping on vertical plane, spawn extinguisher
                if (isVerticalPlane || isWallPlacement)
                {
                    bool alreadySpawned = (spawnedWallObject != null) || 
                                          (AR.ARStepCounterTracker.Instance != null && AR.ARStepCounterTracker.Instance.SpawnedExtinguisherInstance != null);

                    if (alreadySpawned)
                    {
                        Debug.LogWarning("[WARN] [ARPlacementManager] Wall Fire Extinguisher is already spawned! Blocking duplicate wall spawn.");
                        return false;
                    }

                    if (AR.ARStepCounterTracker.Instance != null && 
                        AR.ARStepCounterTracker.Instance.CurrentState == AR.ARStepCounterTracker.StepTrackerState.ScanningForWall)
                    {
                        AR.ARStepCounterTracker.Instance.SpawnExtinguisherOnWall(hitPose.position, hitPose.rotation);
                        return true;
                    }
                }

                Camera mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
                float camDist = mainCamera != null ? Vector3.Distance(mainCamera.transform.position, hitPose.position) : -1f;
                Debug.Log($"[DIAG] [ARPlacementManager] Final HitPose position={hitPose.position}, isWallPlacement={isWallPlacement}, Alignment={alignment}, Distance from Camera={(camDist >= 0 ? camDist.ToString("F2") + "m" : "N/A")}");

                // --- 3. Prefab Selection & Validation (Requirements 3 & 7) ---
                GameObject targetPrefab = prefabToSpawn;
                if (targetPrefab == null)
                {
                    targetPrefab = isWallPlacement ? wallExtinguisherPrefab : defaultPlacementPrefab;
                }

                if (targetPrefab == null)
                {
                    lastPlacementErrorLog = isWallPlacement
                        ? "Wall Fire Extinguisher prefab is not assigned in Inspector (ARPlacementManager.wallExtinguisherPrefab)!"
                        : "Ground Fire Hazard prefab is not assigned in Inspector (ARPlacementManager.defaultPlacementPrefab)!";
                    Debug.LogError($"[ERROR] [ARPlacementManager] {lastPlacementErrorLog}");
                    return false;
                }

                // --- 4. Rotation Calculation (Requirement 4) ---
                Quaternion spawnRotation;
                if (isWallPlacement)
                {
                    // Wall Fire Extinguisher: Orient relative to the vertical wall surface normal
                    spawnRotation = hitPose.rotation;
                }
                else
                {
                    // Ground Fire Hazard: Lay flat on horizontal floor facing camera
                    spawnRotation = Quaternion.Euler(0, mainCamera != null ? mainCamera.transform.eulerAngles.y : hitPose.rotation.eulerAngles.y, 0);
                }

                // --- 5. Spawning & Positioning ---
                if (isWallPlacement)
                {
                    if (spawnedWallObject == null)
                    {
                        spawnedWallObject = Instantiate(targetPrefab, hitPose.position, spawnRotation);
                        if (Application.isPlaying && !Application.isEditor)
                        {
                            spawnedWallAnchor = spawnedWallObject.AddComponent<ARAnchor>();
                        }
                        Debug.Log($"[INFO] [ARPlacementManager] Successfully spawned Wall Extinguisher '{targetPrefab.name}' on vertical wall plane at {hitPose.position}");
                    }
                    else
                    {
                        if (spawnedWallAnchor != null)
                        {
                            DestroyImmediate(spawnedWallAnchor);
                        }
                        spawnedWallObject.transform.SetPositionAndRotation(hitPose.position, spawnRotation);
                        if (Application.isPlaying && !Application.isEditor)
                        {
                            spawnedWallAnchor = spawnedWallObject.AddComponent<ARAnchor>();
                        }
                        Debug.Log($"[INFO] [ARPlacementManager] Repositioned Wall Extinguisher '{spawnedWallObject.name}' to {hitPose.position}");
                    }

                    lastPlacementDiagStatus = $"SUCCESS: Placed Wall Extinguisher on vertical wall at {hitPose.position}";
                    lastPlacementErrorLog = "";
                }
                else
                {
                    if (!hasFirstPlacementOccurred)
                    {
                        hasFirstPlacementOccurred = true;
                        placementStartTime = Time.time;
                        Debug.Log($"[INFO] [ARPlacementManager] First ground placement registered! 3-second placement window started at Time={placementStartTime:F2}s");
                    }

                    if (spawnedObject == null)
                    {
                        spawnedObject = Instantiate(targetPrefab, hitPose.position, spawnRotation);
                        if (Application.isPlaying && !Application.isEditor)
                        {
                            spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                        }
                        Debug.Log($"[INFO] [ARPlacementManager] Successfully spawned Ground Fire Hazard '{targetPrefab.name}' via {hitTypeString} at {hitPose.position}");
                    }
                    else
                    {
                        if (spawnedAnchor != null)
                        {
                            DestroyImmediate(spawnedAnchor);
                        }
                        spawnedObject.transform.SetPositionAndRotation(hitPose.position, spawnRotation);
                        if (Application.isPlaying && !Application.isEditor)
                        {
                            spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                        }
                        Debug.Log($"[INFO] [ARPlacementManager] Repositioned Ground Fire Hazard '{spawnedObject.name}' to {hitPose.position}");
                    }

                    // Ignite Fire Hazard
                    GroundFireController fireController = spawnedObject.GetComponent<GroundFireController>() ?? spawnedObject.GetComponentInChildren<GroundFireController>();
                    if (fireController == null)
                    {
                        fireController = spawnedObject.AddComponent<GroundFireController>();
                    }
                    fireController.IgniteFire();

                    OnFireHazardConfirmed?.Invoke();
                    lastPlacementDiagStatus = $"SUCCESS: Ignited Ground Fire hazard at {hitPose.position}";
                    lastPlacementErrorLog = "";
                }

                Debug.Log($"[DIAG] [ARPlacementManager] {lastPlacementDiagStatus}");
                OnObjectPlaced?.Invoke(hitPose.position, hitPose.rotation);
                return true;
            }
            catch (Exception ex)
            {
                lastPlacementErrorLog = $"Exception during placement: {ex.Message}";
                Debug.LogError($"[ERROR] [ARPlacementManager] Fatal exception in PerformPlacementRaycast: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Guarantees fire visibility on mobile by placing the fire 2 meters in front of the camera at ground level.
        /// Auto-adds SphereCollider (trigger) to the fire and ignites it immediately.
        /// </summary>
        public GameObject SpawnFireAtCamera()
        {
            if (spawnedObject != null)
            {
                Debug.Log("[ARPlacementManager] Fire hazard already exists in scene.");
                return spawnedObject;
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 camPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
            Vector3 camForward = mainCam != null ? mainCam.transform.forward : Vector3.forward;
            if (camForward == Vector3.zero) camForward = Vector3.forward;

            // Place fire 1.6 meters directly in front of camera, 0.4 meters down (centered on mobile screen)
            Vector3 spawnPos = camPos + (camForward.normalized * 1.6f) + Vector3.down * 0.4f;
            Quaternion spawnRot = Quaternion.identity;

            GameObject targetPrefab = defaultPlacementPrefab;
            if (targetPrefab == null)
            {
                targetPrefab = Resources.Load<GameObject>("Prefabs/FireHazard") ??
                               Resources.Load<GameObject>("FireHazard") ??
                               Resources.Load<GameObject>("GroundFireParticles");
            }

            if (targetPrefab != null)
            {
                spawnedObject = Instantiate(targetPrefab, spawnPos, spawnRot);
            }
            else
            {
                spawnedObject = new GameObject("GroundFireHazard");
                spawnedObject.transform.position = spawnPos;
                spawnedObject.AddComponent<GroundFireController>();
            }

            spawnedObject.name = "GroundFireHazard_MobileSpawn";

            var col = spawnedObject.GetComponentInChildren<Collider>();
            if (col == null)
            {
                var sc = spawnedObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 1.2f;
                sc.center = new Vector3(0f, 0.5f, 0f);
            }

            GroundFireController fireCtrl = spawnedObject.GetComponentInChildren<GroundFireController>();
            if (fireCtrl != null)
            {
                fireCtrl.IgniteFire();
            }

            hasFirstPlacementOccurred = true;
            ConfirmPlacementLock();
            OnObjectPlaced?.Invoke(spawnPos, spawnRot);
            OnFireHazardConfirmed?.Invoke();

            Debug.Log($"[ARPlacementManager] 🔥 Spawned Fire at Camera Forward: Pos={spawnPos}");
            return spawnedObject;
        }

        public void SetPlanesVisible(bool visible)
        {
            if (planeManager == null) return;
            planeManager.enabled = visible;
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
            Debug.Log($"[INFO] [ARPlacementManager] Set plane visibility to: {visible}");
        }

        /// <summary>
        /// Rescale mode: tap on a placed object to cycle its scale.
        /// First tap: scale up by rescaleStep. Second tap: scale down. Alternates.
        /// </summary>
        private bool PerformRescale(Pose hitPose)
        {
            // Find which spawned object is closer to the tap
            GameObject target = null;
            if (spawnedObject != null)
            {
                float distGround = Vector3.Distance(hitPose.position, spawnedObject.transform.position);
                if (spawnedWallObject != null)
                {
                    float distWall = Vector3.Distance(hitPose.position, spawnedWallObject.transform.position);
                    target = distGround <= distWall ? spawnedObject : spawnedWallObject;
                }
                else
                {
                    target = spawnedObject;
                }
            }
            else if (spawnedWallObject != null)
            {
                target = spawnedWallObject;
            }

            if (target == null) return false;

            // Scale up by step, wrap around to min when exceeding max
            Vector3 currentScale = target.transform.localScale;
            float nextScale = currentScale.x + rescaleStep;
            if (nextScale > maxScale)
                nextScale = minScale;

            target.transform.localScale = Vector3.one * nextScale;
            OnObjectRescaled?.Invoke(target, nextScale);
            Debug.Log($"[INFO] [ARPlacementManager] Rescaled '{target.name}' to {nextScale:F3}");
            return true;
        }

        public void ResetPlacementTimer()
        {
            hasFirstPlacementOccurred = false;
            placementStartTime = -1f;
            isPlacementLocked = false;
            Debug.Log("[INFO] [ARPlacementManager] Placement timer and lock state reset.");
        }

        public void ClearSpawnedObject()
        {
            if (spawnedObject != null)
            {
                Debug.Log($"[INFO] [ARPlacementManager] Destroying spawned ground hazard '{spawnedObject.name}'");
                Destroy(spawnedObject);
                spawnedObject = null;
                spawnedAnchor = null;
            }
            if (spawnedWallObject != null)
            {
                Debug.Log($"[INFO] [ARPlacementManager] Destroying spawned wall extinguisher '{spawnedWallObject.name}'");
                Destroy(spawnedWallObject);
                spawnedWallObject = null;
                spawnedWallAnchor = null;
            }

            // Disable proximity check when fire is cleared
            if (ARProximitySafetyValidator.Instance != null)
            {
                ARProximitySafetyValidator.Instance.DisableChecking();
            }

            ResetPlacementTimer();
        }

        private string lastPlacementDiagStatus = "Ready — Tap plane to ignite Fire hazard";
        private string lastPlacementErrorLog = "";

        private void OnGUI()
        {
            if (!showTimerUI) return;

            float screenWidth = Screen.width;
            float margin = 30f;

            // --- 1. TOP-RIGHT CORNER: PLACEMENT WINDOW TIMER HUD ---
            float topRightWidth = 560f;
            float topRightHeight = 130f;
            Rect topRightBoxRect = new Rect(screenWidth - topRightWidth - margin, margin, topRightWidth, topRightHeight);

            GUIStyle topRightStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            string timerTitle;
            string timerSubtitle;

            if (!hasFirstPlacementOccurred)
            {
                topRightStyle.normal.textColor = new Color(1.0f, 0.85f, 0.0f); // Amber / Gold
                timerTitle = "🔥 FIRE HAZARD PLACEMENT READY";
                timerSubtitle = "Tap floor to ignite fire hazard (3s window)";
            }
            else if (!isPlacementLocked)
            {
                float remainingSec = Mathf.Max(0f, placementWindowDuration - (Time.time - placementStartTime));
                topRightStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f); // Vivid Green
                timerTitle = $"⏱️ PLACEMENT WINDOW: {remainingSec:F1}s";
                timerSubtitle = "Tap other floor regions to adjust fire position";
            }
            else
            {
                topRightStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f); // Vivid Red
                timerTitle = "🔒 PLACEMENT LOCKED";
                timerSubtitle = "(3s Placement Window Expired)";
            }

            GUI.Box(topRightBoxRect, $"{timerTitle}\n<size=22>{timerSubtitle}</size>", topRightStyle);

            // --- 2. TOP-LEFT CORNER: TRAINING INSTRUCTION BOX ---
            float topLeftWidth = 640f;
            float topLeftHeight = 130f;
            Rect topLeftBoxRect = new Rect(margin, margin, topLeftWidth, topLeftHeight);

            GUIStyle topLeftStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            string hintTitle;
            string hintContent;

            if (!hasFirstPlacementOccurred)
            {
                topLeftStyle.normal.textColor = new Color(0.2f, 0.9f, 1.0f); // Bright Cyan
                hintTitle = "💡 SAFETY SIMULATION";
                hintContent = "Tap floor plane to start emergency fire simulation";
            }
            else
            {
                topLeftStyle.normal.textColor = new Color(1.0f, 0.85f, 0.0f); // Vivid Gold
                hintTitle = "🧯 EMERGENCY SAFETY HINT";
                hintContent = "Scan 2D Fire Extinguisher image to load 3D Fire Extinguisher";
            }

#if UNITY_EDITOR
            hintContent += "\n<color=#00FF00>[EDITOR SIM: Click Mouse / Key 'F'=Fire, Key 'E'=3D Extinguisher, Key 'C'=Clear]</color>";
#endif

            GUI.Box(topLeftBoxRect, $"<b>{hintTitle}</b>\n<size=22>{hintContent}</size>", topLeftStyle);

            // --- 3. BOTTOM-CENTER: LIVE PLACEMENT DIAGNOSTICS LOG HUD ---
            float bottomWidth = screenWidth - (margin * 2f);
            float bottomHeight = 160f;
            Rect bottomBoxRect = new Rect(margin, Screen.height - bottomHeight - margin, bottomWidth, bottomHeight);

            GUIStyle bottomStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };

            int detectedPlanesCount = planeManager != null ? planeManager.trackables.count : 0;
            string planesStatusStr = detectedPlanesCount > 0 ? $"<color=#00FF00>{detectedPlanesCount} Detected</color>" : "<color=#FFCC00>0 (Scanning floor...)</color>";

            string diagContent = $"<b>🧯 SAFETY AR SIMULATION STATUS:</b>\n" +
                                 $"• Mode: Fire Hazard Plane Placement | AR Planes: {planesStatusStr}\n" +
                                 $"• Status: <color=#00E5FF>{lastPlacementDiagStatus}</color>\n" +
                                 (!string.IsNullOrEmpty(lastPlacementErrorLog) ? $"<color=#FF4444>• ERROR: {lastPlacementErrorLog}</color>" : "<color=#00FF00>• System OK — Tap floor to ignite fire</color>");

            GUI.Box(bottomBoxRect, diagContent, bottomStyle);
        }

        /// <summary>
        /// Spawns fire hazard directly in front of the camera at ground level.
        /// Used as fallback when AR plane detection fails or for mobile-first flow.
        /// </summary>
        public GameObject SpawnFireAtCamera(float distanceInFront = 2f)
        {
            if (defaultPlacementPrefab == null)
            {
                Debug.LogError("[ARPlacementManager] Cannot spawn fire — defaultPlacementPrefab is null!");
                return null;
            }

            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[ARPlacementManager] Cannot spawn fire — no camera found!");
                return null;
            }

            Vector3 spawnPos = cam.transform.position + cam.transform.forward * distanceInFront;
            spawnPos.y = 0f; // Place on ground level

            Quaternion spawnRot = Quaternion.Euler(0, cam.transform.eulerAngles.y, 0);

            if (spawnedObject != null)
            {
                Destroy(spawnedObject);
            }

            spawnedObject = Instantiate(defaultPlacementPrefab, spawnPos, spawnRot);
            spawnedObject.name = "FireHazard_Spawned";

            // Ensure collider exists
            var col = spawnedObject.GetComponent<Collider>();
            if (col == null)
            {
                var sc = spawnedObject.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = 0.5f;
            }

            if (!Application.isEditor)
            {
                var anchor = spawnedObject.AddComponent<ARAnchor>();
                spawnedAnchor = anchor;
            }

            hasFirstPlacementOccurred = true;

            // Ignite
            var fireController = spawnedObject.GetComponent<GroundFireController>()
                               ?? spawnedObject.GetComponentInChildren<GroundFireController>();
            if (fireController != null)
            {
                fireController.IgniteFire();
            }

            Debug.Log($"[ARPlacementManager] Spawned fire at camera forward: {spawnPos}, distance={distanceInFront}m");
            OnObjectPlaced?.Invoke(spawnPos, spawnRot);
            OnFireHazardConfirmed?.Invoke();

            return spawnedObject;
        }
    }
}
