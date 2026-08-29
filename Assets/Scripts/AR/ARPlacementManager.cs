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
        /// Uses only the new Input System (EnhancedTouch + Pointer) intentionally — mixing with legacy Input class caused Android build/runtime issues in reference implementations.
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
                    Debug.Log($"[DIAG] [ARPlacementManager] New Input System Tap Detected at {tapPosition}! HasDetectedPlane={HasDetectedPlane}, TotalTrackedPlanes={(planeManager != null ? planeManager.trackables.count : 0)}");

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
                    Debug.Log($"[DIAG] [ARPlacementManager] Pointer Press Began at {tapPosition}! HasDetectedPlane={HasDetectedPlane}, TotalTrackedPlanes={(planeManager != null ? planeManager.trackables.count : 0)}");

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

            if (hasFirstPlacementOccurred && !isPlacementLocked)
            {
                float elapsedTime = Time.time - placementStartTime;
                if (elapsedTime >= placementWindowDuration)
                {
                    isPlacementLocked = true;
                    Debug.Log($"[INFO] [ARPlacementManager] 3-second placement window expired! Surface placement is now LOCKED.");
                }
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
                    // Keep green reticle transparent/hidden per request, while keeping brown plane detection active
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
                if (isPlacementLocked)
                {
                    Debug.LogWarning("[WARN] [ARPlacementManager] Placement tap blocked — 3-second placement window has expired.");
                    return false;
                }

                Pose hitPose = default;
                bool hitSuccess = false;
                string hitTypeString = "";

                // Tier 1: Real AR Plane Surface (Matching reference project ARPlaceCube.cs: TrackableType.AllTypes)
                TrackableType planeTypes = TrackableType.AllTypes | TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes;
                if (raycastManager != null && raycastManager.Raycast(touchPosition, hits, planeTypes) && hits.Count > 0)
                {
                    hitPose = hits[0].pose;
                    hitSuccess = true;
                    hitTypeString = "Plane Surface";
                    Debug.Log($"[DIAG] [ARPlacementManager] Tier 1 Hit: Plane Surface at pose {hitPose.position}, hitDistance={hits[0].distance:F2}m");
                }
                // Tier 2: Environment Depth Map (if hardware depth is available)
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
                // Tier 3: Instant Placement Fallback
                else
                {
                    try
                    {
                        ARRaycast instantRaycast = raycastManager != null ? raycastManager.AddRaycast(touchPosition, 1.5f) : null;
                        if (instantRaycast != null)
                        {
                            hitPose = instantRaycast.pose;
                            hitSuccess = true;
                            hitTypeString = "Instant Placement";
                            Debug.Log($"[DIAG] [ARPlacementManager] Tier 3 Hit: Instant Placement at pose {hitPose.position}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[WARN] [ARPlacementManager] Instant Placement fallback exception: {ex.Message}");
                    }
                }

                if (!hitSuccess)
                {
                    Debug.LogWarning($"[WARN] [ARPlacementManager] ALL 3 hit-test tiers failed for touchPosition={touchPosition}. Active planes count={(planeManager != null ? planeManager.trackables.count : 0)}");
                    return false;
                }

                if (!hasFirstPlacementOccurred)
                {
                    hasFirstPlacementOccurred = true;
                    placementStartTime = Time.time;
                    Debug.Log($"[INFO] [ARPlacementManager] First surface placement registered! 3-second placement window started at Time={placementStartTime:F2}s");
                }

                Camera mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
                float camDist = mainCamera != null ? Vector3.Distance(mainCamera.transform.position, hitPose.position) : -1f;
                Debug.Log($"[DIAG] [ARPlacementManager] Final HitPose position={hitPose.position}, Distance from Camera={(camDist >= 0 ? camDist.ToString("F2") + "m" : "N/A")}");

                GameObject targetPrefab = prefabToSpawn != null ? prefabToSpawn : defaultPlacementPrefab;

                if (spawnedObject == null)
                {
                    if (targetPrefab != null)
                    {
                        spawnedObject = Instantiate(targetPrefab, hitPose.position, hitPose.rotation);
                        Debug.Log($"[DIAG] [ARPlacementManager] Instantiated targetPrefab asset '{targetPrefab.name}' at {hitPose.position}");
                    }
                    else
                    {
                        Debug.LogWarning("[WARN] [ARPlacementManager] defaultPlacementPrefab was null! Generating fallback 3D safety orange primitive cube.");
                        spawnedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        spawnedObject.name = "Safety Equipment (Cube)";
                        spawnedObject.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                        spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);

                        MeshRenderer mr = spawnedObject.GetComponent<MeshRenderer>();
                        if (mr != null)
                        {
                            Material mat = GraphicsSettings.currentRenderPipeline != null ? 
                                new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) : 
                                new Material(Shader.Find("Sprites/Default"));
                            mat.color = new Color(1.0f, 0.4f, 0.0f); // Safety Orange
                            mr.sharedMaterial = mat;
                        }
                    }

                    spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                    Debug.Log($"[INFO] [ARPlacementManager] Successfully spawned and anchored 3D object via {hitTypeString} at {hitPose.position}");

                    GroundFireController fireController = spawnedObject.GetComponent<GroundFireController>() ?? spawnedObject.GetComponentInChildren<GroundFireController>();
                    if (fireController != null)
                    {
                        fireController.IgniteFire();
                        Debug.Log($"[FIRE_DIAG] [ARPlacementManager] Triggered IgniteFire on newly spawned object '{spawnedObject.name}'");
                    }
                    else
                    {
                        Debug.LogWarning($"[FIRE_DIAG] [ARPlacementManager] Spawned object '{spawnedObject.name}' does NOT have GroundFireController attached!");
                    }
                }
                else
                {
                    if (spawnedAnchor != null)
                    {
                        DestroyImmediate(spawnedAnchor);
                    }
                    spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                    spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                    Debug.Log($"[INFO] [ARPlacementManager] Repositioned and re-anchored 3D object via {hitTypeString} to {hitPose.position}");

                    GroundFireController fireController = spawnedObject.GetComponent<GroundFireController>() ?? spawnedObject.GetComponentInChildren<GroundFireController>();
                    if (fireController != null)
                    {
                        fireController.IgniteFire();
                        Debug.Log($"[FIRE_DIAG] [ARPlacementManager] Triggered IgniteFire on repositioned object '{spawnedObject.name}'");
                    }
                }

                Renderer spawnedRenderer = spawnedObject != null ? spawnedObject.GetComponent<Renderer>() : null;
                Debug.Log($"[FIRE_DIAG] [ARPlacementManager] Object state: Name={(spawnedObject != null ? spawnedObject.name : "NULL")}, ActiveInHierarchy={spawnedObject?.activeInHierarchy}, Pos={spawnedObject?.transform.position}, CamDist={camDist:F2}m");

                OnObjectPlaced?.Invoke(hitPose.position, hitPose.rotation);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [ARPlacementManager] Fatal exception in PerformPlacementRaycast: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
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
                Debug.Log($"[INFO] [ARPlacementManager] Destroying spawned object '{spawnedObject.name}'");
                Destroy(spawnedObject);
                spawnedObject = null;
                spawnedAnchor = null;
            }
            ResetPlacementTimer();
        }

        private void OnGUI()
        {
            if (!showTimerUI) return;

            float screenWidth = Screen.width;
            float margin = 30f;

            // --- 1. TOP-RIGHT CORNER: PLACEMENT WINDOW TIMER HUD (DOUBLED SIZE) ---
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
                timerTitle = "🔥 HAZARD PLACEMENT READY";
                timerSubtitle = "Tap plane to spawn (3s window)";
            }
            else if (!isPlacementLocked)
            {
                float remainingSec = Mathf.Max(0f, placementWindowDuration - (Time.time - placementStartTime));
                topRightStyle.normal.textColor = new Color(0.2f, 1.0f, 0.4f); // Vivid Green
                timerTitle = $"⏱️ PLACEMENT WINDOW: {remainingSec:F1}s";
                timerSubtitle = "Tap other regions to adjust fire";
            }
            else
            {
                topRightStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f); // Vivid Red
                timerTitle = "🔒 FIRE PLACEMENT LOCKED";
                timerSubtitle = "(3s Placement Window Expired)";
            }

            GUI.Box(topRightBoxRect, $"{timerTitle}\n<size=22>{timerSubtitle}</size>", topRightStyle);

            // --- 2. TOP-LEFT CORNER: DYNAMIC TRAINING INSTRUCTION & SAFETY HINTS BOX (DOUBLED SIZE) ---
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
                hintTitle = "💡 SIMULATION INSTRUCTION";
                hintContent = "Just tap on plane surface to start the simulation";
            }
            else
            {
                topLeftStyle.normal.textColor = new Color(1.0f, 0.85f, 0.0f); // Vivid Gold
                hintTitle = "💡 SAFETY HINT";
                hintContent = "Search for fire extinguisher or emergency exit";
            }

            GUI.Box(topLeftBoxRect, $"<b>{hintTitle}</b>\n<size=22>{hintContent}</size>", topLeftStyle);
        }
    }
}
