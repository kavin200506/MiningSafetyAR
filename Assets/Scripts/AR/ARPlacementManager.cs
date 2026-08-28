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

        private ARRaycastManager raycastManager;
        private ARPlaneManager planeManager;
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
            // 1. Check New Input System Enhanced Touch (Mobile Touchscreen Taps)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (!HasDetectedPlane)
                    {
                        Debug.Log("[ARPlacementManager] Ignoring tap — no plane tracked yet.");
                        OnNoPlaneDetected?.Invoke();
                        return;
                    }
                    Debug.Log($"[ARPlacementManager] Touch detected at {touch.screenPosition}");
                    PerformPlacementRaycast(touch.screenPosition);
                    return;
                }
            }

            // 2. Check New Input System Pointer / Mouse / Tap Press
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                Vector2 pointerPos = Pointer.current.position.ReadValue();
                if (!HasDetectedPlane)
                {
                    Debug.Log("[ARPlacementManager] Ignoring tap — no plane tracked yet.");
                    OnNoPlaneDetected?.Invoke();
                    return;
                }
                Debug.Log($"[ARPlacementManager] Pointer press detected at {pointerPos}");
                PerformPlacementRaycast(pointerPos);
                return;
            }

            // 3. Fallback Legacy Input
            if (Input.touchCount > 0)
            {
                UnityEngine.Touch legacyTouch = Input.GetTouch(0);
                if (legacyTouch.phase == UnityEngine.TouchPhase.Began)
                {
                    if (!HasDetectedPlane)
                    {
                        Debug.Log("[ARPlacementManager] Ignoring tap — no plane tracked yet.");
                        OnNoPlaneDetected?.Invoke();
                        return;
                    }
                    Debug.Log($"[ARPlacementManager] Legacy Touch detected at {legacyTouch.position}");
                    PerformPlacementRaycast(legacyTouch.position);
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
            TrackableType planeTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds;
            bool hitSuccess = raycastManager.Raycast(touchPosition, hits, planeTypes);

            if (!hitSuccess || hits.Count == 0)
            {
                Debug.LogWarning($"[ARPlacementManager] No plane detected at touch position {touchPosition} — keep scanning the environment.");
                return false;
            }

            Pose hitPose = hits[0].pose;
            Debug.Log($"[ARPlacementManager] Raycast hit AR Surface plane at {hitPose.position}");

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
                    Destroy(spawnedAnchor);
                }
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                spawnedAnchor = spawnedObject.AddComponent<ARAnchor>();
                Debug.Log($"[ARPlacementManager] Repositioned 3D object anchored to {hitPose.position}");
            }

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
