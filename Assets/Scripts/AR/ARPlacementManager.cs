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

        public bool HasDetectedPlane => planeManager != null && planeManager.trackables.count > 0;

        public event Action<Vector3, Quaternion> OnObjectPlaced;
        public event Action OnNoPlaneDetected;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
            occlusionManager = GetComponent<AROcclusionManager>() ?? FindFirstObjectByType<AROcclusionManager>();

            Debug.Log($"[DIAG] Startup check: placementIndicator assigned={placementIndicator != null}, defaultPlacementPrefab assigned={defaultPlacementPrefab != null}, occlusionManager assigned={occlusionManager != null}");
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            UpdatePlacementIndicator();
            CheckTouchInput();
        }

        private void CheckTouchInput()
        {
            bool tapDetected = false;

            // 1. Check New Input System Enhanced Touch (Mobile Touchscreen Taps)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapDetected = true;
                }
            }

            // 2. Check New Input System Pointer / Mouse / Tap Press
            if (!tapDetected && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                tapDetected = true;
            }

            // 3. Fallback Legacy Input
            if (!tapDetected && Input.touchCount > 0)
            {
                UnityEngine.Touch legacyTouch = Input.GetTouch(0);
                if (legacyTouch.phase == UnityEngine.TouchPhase.Began)
                {
                    tapDetected = true;
                }
            }

            if (tapDetected)
            {
                Debug.Log($"[DIAG] tapDetected={tapDetected}, HasDetectedPlane={HasDetectedPlane}, placementIndicator_isNull={placementIndicator == null}, placementIndicator_activeSelf={(placementIndicator != null ? placementIndicator.activeSelf.ToString() : "N/A")}");

                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Debug.Log("[ARPlacementManager] Placing object at reticle position");
                
                bool placed = PerformPlacementRaycast(screenCenter);
                if (!placed)
                {
                    OnNoPlaneDetected?.Invoke();
                }
            }
        }

        private void UpdatePlacementIndicator()
        {
            if (Time.time >= nextPlaneLogTime)
            {
                nextPlaneLogTime = Time.time + 1.0f;
                int trackableCount = planeManager != null ? planeManager.trackables.count : 0;
                Debug.Log($"[ARPlacementManager] Tracked Planes Count: {trackableCount}");
            }

            if (placementIndicator == null) return;

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
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

        public bool PerformPlacementRaycast(Vector2 touchPosition, GameObject prefabToSpawn = null)
        {
            bool depthSupported = occlusionManager != null && 
                                  occlusionManager.enabled && 
                                  occlusionManager.descriptor != null && 
                                  occlusionManager.descriptor.environmentDepthImageSupported == Supported.Supported;

            Pose hitPose = default;
            bool hitSuccess = false;

            // Step A - Depth
            if (depthSupported)
            {
                if (raycastManager.Raycast(touchPosition, hits, TrackableType.Depth) && hits.Count > 0)
                {
                    hitPose = hits[0].pose;
                    hitSuccess = true;
                    Debug.Log("[ARPlacementManager] Placed via Depth hit-test");
                }
            }

            // Step B - Plane
            if (!hitSuccess)
            {
                TrackableType planeTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds;
                if (raycastManager.Raycast(touchPosition, hits, planeTypes) && hits.Count > 0)
                {
                    hitPose = hits[0].pose;
                    hitSuccess = true;
                    Debug.Log("[ARPlacementManager] Placed via Plane hit-test");
                }
            }

            // Step C - Instant Placement
            if (!hitSuccess)
            {
                try
                {
                    ARRaycast instantRaycast = raycastManager.AddRaycast(touchPosition, 2.0f);
                    if (instantRaycast != null)
                    {
                        hitPose = instantRaycast.pose;
                        hitSuccess = true;
                        Debug.Log("[ARPlacementManager] Placed via Instant Placement (estimated distance 2.0m) — pose will refine automatically");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ARPlacementManager] Instant Placement unavailable: {ex.Message}");
                }
            }

            Debug.Log($"[DIAG] Raycast at {touchPosition}: hitSuccess={hitSuccess}, hits.Count={hits.Count}");

            if (!hitSuccess)
            {
                Debug.LogWarning($"[ARPlacementManager] All hit-test methods failed at touch position {touchPosition}");
                return false;
            }

            Camera mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            float camDist = mainCamera != null ? Vector3.Distance(mainCamera.transform.position, hitPose.position) : -1f;
            Debug.Log($"[DIAG] HitPose position={hitPose.position}, distance from camera={(mainCamera != null ? camDist.ToString("F2") : "N/A")}");

            GameObject targetPrefab = prefabToSpawn != null ? prefabToSpawn : defaultPlacementPrefab;

            if (spawnedObject == null)
            {
                if (targetPrefab != null)
                {
                    spawnedObject = Instantiate(targetPrefab, hitPose.position, hitPose.rotation);
                }
                else
                {
                    // Fallback 3D Safety Cube with valid URP Material
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
                Debug.Log($"[ARPlacementManager] Successfully spawned 3D object anchored at {hitPose.position}");
            }
            else
            {
                if (spawnedAnchor != null)
                {
                    DestroyImmediate(spawnedAnchor);
                }
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                Debug.Log($"[ARPlacementManager] Repositioned 3D object anchored to {hitPose.position}");
            }

            Renderer spawnedRenderer = spawnedObject != null ? spawnedObject.GetComponent<Renderer>() : null;
            Debug.Log($"[DIAG] spawnedObject={(spawnedObject != null ? spawnedObject.name : "NULL")}, position={spawnedObject?.transform.position}, activeInHierarchy={spawnedObject?.activeInHierarchy}, hasRenderer={(spawnedRenderer != null)}, rendererEnabled={(spawnedRenderer != null ? spawnedRenderer.enabled.ToString() : "N/A")}, usingPrefab={targetPrefab != null}");

            OnObjectPlaced?.Invoke(hitPose.position, hitPose.rotation);
            return true;
        }

        public void SetPlanesVisible(bool visible)
        {
            planeManager.enabled = visible;
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        public void ClearSpawnedObject()
        {
            if (spawnedObject != null)
            {
                Destroy(spawnedObject);
                spawnedObject = null;
            }
        }
    }
}
