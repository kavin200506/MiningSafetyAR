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

        [Tooltip("Emergency alarm button model, spawned alongside the wall fire extinguisher whenever it is placed.")]
        [SerializeField] private GameObject alarmButtonPrefab;
        public GameObject AlarmButtonPrefab
        {
            get => alarmButtonPrefab;
            set => alarmButtonPrefab = value;
        }

        [Tooltip("Local position offset (relative to the wall extinguisher) at which the alarm button is spawned beside it.")]
        [SerializeField] private Vector3 alarmButtonOffset = new Vector3(0.3f, 0f, 0f);

        private GameObject spawnedAlarmButtonObject;

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

        [SerializeField] private bool showTimerUI = false;
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

            // Start restricted to whatever plane type the fire hazard is allowed to spawn on
            // (Horizontal by default) — ARStepCounterTracker widens this to include Vertical once
            // the wall-scan phase begins, otherwise ARCore would never even track a vertical wall
            // for that scan to find.
            ApplyPlaneDetectionMode(fireHazardAllowedPlane);

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
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
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

        /// <summary>Which AR plane alignment(s) an object is allowed to spawn on.</summary>
        public enum AllowedPlaneType
        {
            Horizontal,
            Vertical,
            Both
        }

        [Header("Fire Hazard Plane Restriction")]
        [Tooltip("Which AR plane type(s) the fire hazard may spawn on. Default: Horizontal only (floor). Also controls what ARPlaneManager actually detects while the fire hazard is being placed — e.g. with Horizontal selected, vertical planes are not tracked at all during that phase.")]
        [SerializeField] private AllowedPlaneType fireHazardAllowedPlane = AllowedPlaneType.Horizontal;
        public AllowedPlaneType FireHazardAllowedPlane => fireHazardAllowedPlane;

        [Header("Fire Extinguisher Plane Restriction")]
        [Tooltip("Which AR plane type(s) the fire extinguisher may spawn on when manually tapped during the wall-scan phase. Default: Vertical only (wall-mounted).")]
        [SerializeField] private AllowedPlaneType extinguisherAllowedPlane = AllowedPlaneType.Vertical;
        public AllowedPlaneType ExtinguisherAllowedPlane => extinguisherAllowedPlane;

        private static bool IsAlignmentAllowed(PlaneAlignment alignment, AllowedPlaneType allowed)
        {
            bool isHorizontal = alignment == PlaneAlignment.HorizontalUp || alignment == PlaneAlignment.HorizontalDown;
            bool isVertical = alignment == PlaneAlignment.Vertical;
            switch (allowed)
            {
                case AllowedPlaneType.Horizontal: return isHorizontal;
                case AllowedPlaneType.Vertical: return isVertical;
                default: return isHorizontal || isVertical; // Both
            }
        }

        private static PlaneDetectionMode ToDetectionMode(AllowedPlaneType allowed)
        {
            switch (allowed)
            {
                case AllowedPlaneType.Horizontal: return PlaneDetectionMode.Horizontal;
                case AllowedPlaneType.Vertical: return PlaneDetectionMode.Vertical;
                default: return PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            }
        }

        /// <summary>
        /// Restricts ARPlaneManager's live detection to the given plane type(s). Call this when
        /// switching between placement phases (e.g. fire-hazard phase vs. wall-scan phase) so the
        /// AR system genuinely stops tracking the plane type that phase doesn't need, rather than
        /// relying only on code-side filtering after the fact.
        /// </summary>
        public void ApplyPlaneDetectionMode(AllowedPlaneType allowed)
        {
            if (planeManager == null) return;
            planeManager.requestedDetectionMode = ToDetectionMode(allowed);
            Debug.Log($"[ARPlacementManager] Plane detection mode set to {planeManager.requestedDetectionMode} (for {allowed}).");
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
        private int lastProcessedTapFrame = -1;

        private void CheckTouchInput()
        {
            if (Time.frameCount == lastProcessedTapFrame) return;

            try
            {
                Vector2 tapPosition = Vector2.zero;
                bool tapDetected = false;

                if (!EnhancedTouchSupport.enabled)
                {
                    EnhancedTouchSupport.Enable();
                }

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
                    if (IsPointerOverUI(tapPosition))
                        return;

                    lastProcessedTapFrame = Time.frameCount;
                    Debug.Log($"[DIAG] [ARPlacementManager] Tap Detected at {tapPosition}! HasDetectedPlane={HasDetectedPlane}");

                    // Perform placement raycast strictly at the tapped screen position
                    bool placed = PerformPlacementRaycast(tapPosition);

                    if (!placed)
                    {
                        Debug.Log("[DIAG] [ARPlacementManager] Placement raycast missed detected plane surface.");
                        OnNoPlaneDetected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [ARPlacementManager] Exception during CheckTouchInput: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private bool IsPointerOverUI(Vector2 tapPosition)
        {
            if (Localization.VoiceCommandManager.Instance != null &&
                Localization.VoiceCommandManager.Instance.IsTouchOverVoiceUI(tapPosition))
            {
                Debug.Log($"[DIAG] [ARPlacementManager] Tap at {tapPosition} blocked — touch is over Voice UI / Mic button.");
                return true;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                eventData.position = tapPosition;
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);
                if (results.Count > 0)
                {
                    Debug.Log($"[DIAG] [ARPlacementManager] Tap at {tapPosition} blocked — touch is over UI element '{results[0].gameObject.name}'.");
                    return true;
                }
            }

            return false;
        }

        private void OnPointerPressBegan(InputAction.CallbackContext context)
        {
            try
            {
                if (Time.frameCount == lastProcessedTapFrame) return;

                if (context.control.device is Pointer pointerDevice)
                {
                    Vector2 tapPosition = pointerDevice.position.ReadValue();

                    if (IsPointerOverUI(tapPosition))
                        return;

                    lastProcessedTapFrame = Time.frameCount;
                    Debug.Log($"[DIAG] [ARPlacementManager] Pointer Press Began at {tapPosition}!");

                    bool placed = PerformPlacementRaycast(tapPosition);
                    if (!placed)
                    {
                        Debug.Log("[DIAG] [ARPlacementManager] Placement raycast missed detected plane surface.");
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

                // Tier 1: Real AR Plane Surface only. Deliberately NOT TrackableType.AllTypes — that
                // flag alone already has every trackable kind set (feature points, estimated planes,
                // faces, images, depth), so OR-ing specific plane flags into it was a no-op. It made
                // this raycast accept any textured surface or noisy feature point as a "plane" hit,
                // which is what was causing the fire hazard to spawn floating in mid-air instead of
                // exactly on the plane, contradicting the "fire ONLY spawns on a real detected plane"
                // contract below. Restricting to just the plane-specific flags is what enforces that.
                TrackableType planeTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes;
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

                // Reject a plane hit that the determined target isn't configured to spawn on. With
                // the default Horizontal-only fire hazard / Vertical-only extinguisher, this is
                // normally unreachable because ApplyPlaneDetectionMode already stops the disallowed
                // plane type from being detected in the first place — this is a defense-in-depth
                // check that also makes a custom "Both" configuration behave correctly.
                if (hitPlane != null)
                {
                    AllowedPlaneType allowedFor = isWallPlacement ? extinguisherAllowedPlane : fireHazardAllowedPlane;
                    if (!IsAlignmentAllowed(alignment, allowedFor))
                    {
                        Debug.LogWarning($"[WARN] [ARPlacementManager] Rejected tap: plane alignment '{alignment}' is not allowed for the {(isWallPlacement ? "fire extinguisher" : "fire hazard")} (configured: {allowedFor}).");
                        return false;
                    }
                }

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

                // Tap-to-spawn for wall fire extinguisher is disabled.
                // The fire extinguisher and alarm button spawn automatically via ARStepCounterTracker.
                if (isVerticalPlane || isWallPlacement)
                {
                    Debug.Log("[ARPlacementManager] Tap-to-spawn for Wall Fire Extinguisher is disabled. It is automatically spawned by step counter tracking.");
                    return false;
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
                    // Wall Fire Extinguisher: for a vertical plane, hitPose.up is the wall's outward
                    // normal (horizontal), NOT world-up — using hitPose.rotation directly would rotate
                    // the model's own up-axis sideways, laying it flat instead of standing it upright.
                    // Keep world-up as up, and face outward along the wall's normal instead.
                    spawnRotation = Quaternion.LookRotation(hitPose.up, Vector3.up);
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

                        // Spawn the emergency alarm button at the same time as the extinguisher,
                        // as a child so it tracks the extinguisher's position/rotation together.
                        if (alarmButtonPrefab != null && spawnedAlarmButtonObject == null)
                        {
                            Vector3 alarmWorldPos = hitPose.position + (spawnRotation * alarmButtonOffset);
                            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                            Quaternion faceScreenRot = spawnRotation;
                            if (mainCam != null)
                            {
                                Vector3 dirToCam = mainCam.transform.position - alarmWorldPos;
                                dirToCam.y = 0f;
                                if (dirToCam.sqrMagnitude > 0.001f)
                                {
                                    faceScreenRot = Quaternion.LookRotation(dirToCam.normalized, Vector3.up);
                                }
                            }
                            spawnedAlarmButtonObject = Instantiate(alarmButtonPrefab, alarmWorldPos, faceScreenRot);
                            spawnedAlarmButtonObject.name = "Discovered_3D_AlarmButton";
                        }
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

                    if (spawnedObject != null)
                    {
                        // Ground Fire Hazard already exists — once spawned, its position must
                        // never change again for the rest of the scene, even from an accidental
                        // tap while moving the camera around (confirmed requirement 2026-09-05).
                        // Previously this branch called SetPositionAndRotation() here on every
                        // extra tap that landed on a plane before the 3-second lock elapsed,
                        // which is exactly what let the fire silently slide around. Ignore the
                        // tap entirely instead — no reposition, no re-ignite (re-igniting would
                        // have also reset its health back to full every time).
                        Debug.Log($"[INFO] [ARPlacementManager] Ground Fire Hazard already placed at {spawnedObject.transform.position} — ignoring tap, position is locked for the rest of the scene.");
                        lastPlacementDiagStatus = "Ground Fire Hazard is already placed — position locked.";
                        lastPlacementErrorLog = "";
                        Debug.Log($"[DIAG] [ARPlacementManager] {lastPlacementDiagStatus}");
                        return true;
                    }

                    spawnedObject = Instantiate(targetPrefab, hitPose.position, spawnRotation);
                    if (Application.isPlaying && !Application.isEditor)
                    {
                        spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                    }
                    Debug.Log($"[INFO] [ARPlacementManager] Successfully spawned Ground Fire Hazard '{targetPrefab.name}' via {hitTypeString} at {hitPose.position}");

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

            // Release the wall-placement latch in case a step-scan was interrupted mid-flight —
            // otherwise the next ground tap gets misrouted to spawn the extinguisher instead of fire.
            placementMode = PlacementTargetMode.GroundFireHazard;

            ResetPlacementTimer();
        }

        private string lastPlacementDiagStatus = "Ready — Tap plane to ignite Fire hazard";
        private string lastPlacementErrorLog = "";

        private static bool IsARScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName == "ar_fire_safety" ||
                   sceneName == "AR Plane Detection Placement" ||
                   sceneName == "UI_ARSimulation" ||
                   sceneName == "AR Image Tracking" ||
                   sceneName == "AR Occlusion" ||
                   sceneName == "AR_Placement_Demo" ||
                   sceneName == "AR_Simulation_Demo";
        }

        private void OnGUI()
        {
            // Debug UI overlay text boxes completely disabled per user request
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
