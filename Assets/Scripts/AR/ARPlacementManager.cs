using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Implements Unity's official AR Foundation 6.x architecture (arfoundation-samples pattern)
    /// utilizing InputAction("<Pointer>/press"), ARRaycastManager surface raycasting,
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

        private void OnPointerPressBegan(InputAction.CallbackContext context)
        {
            try
            {
                if (context.control.device is Pointer pointerDevice)
                {
                    Vector2 tapPosition = pointerDevice.position.ReadValue();
                    Debug.Log($"[DIAG] [ARPlacementManager] Pointer Press Began at {tapPosition}! HasDetectedPlane={HasDetectedPlane}, TotalTrackedPlanes={(planeManager != null ? planeManager.trackables.count : 0)}");

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
                Debug.LogError($"[ERROR] [ARPlacementManager] Exception during OnPointerPressBegan: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Update()
        {
            UpdatePlacementIndicator();
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
                    if (!placementIndicator.activeSelf) placementIndicator.SetActive(true);
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

                // Tier 1: Real AR Plane Surface (Unity Samples standard: TrackableType.PlaneWithinPolygon)
                TrackableType planeTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Planes;
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
                }

                Renderer spawnedRenderer = spawnedObject != null ? spawnedObject.GetComponent<Renderer>() : null;
                Debug.Log($"[DIAG] [ARPlacementManager] Object state: Name={(spawnedObject != null ? spawnedObject.name : "NULL")}, ActiveInHierarchy={spawnedObject?.activeInHierarchy}, RendererEnabled={(spawnedRenderer != null ? spawnedRenderer.enabled.ToString() : "N/A")}");

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

        public void ClearSpawnedObject()
        {
            if (spawnedObject != null)
            {
                Debug.Log($"[INFO] [ARPlacementManager] Destroying spawned object '{spawnedObject.name}'");
                Destroy(spawnedObject);
                spawnedObject = null;
                spawnedAnchor = null;
            }
        }
    }
}
