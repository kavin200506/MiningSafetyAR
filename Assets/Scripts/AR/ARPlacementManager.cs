using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

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
        public GameObject SpawnedObject => spawnedObject;

        public event Action<Vector3, Quaternion> OnObjectPlaced;

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
            // 1. Check New Input System Enhanced Touch (Mobile Touchscreens)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    Debug.Log($"[ARPlacementManager] Enhanced Touch detected at {touch.screenPosition}");
                    PerformPlacementRaycast(touch.screenPosition);
                    return;
                }
            }

            // 2. Check New Input System Pointer / Mouse / Tap Press
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                Vector2 pointerPos = Pointer.current.position.ReadValue();
                Debug.Log($"[ARPlacementManager] Pointer press detected at {pointerPos}");
                PerformPlacementRaycast(pointerPos);
                return;
            }

            // 3. Fallback Legacy Input
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    Debug.Log($"[ARPlacementManager] Legacy Touch detected at {touch.position}");
                    PerformPlacementRaycast(touch.position);
                }
            }
        }

        private void UpdatePlacementIndicator()
        {
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
            // Perform raycast against detected AR planes (Estimated or Polygon)
            TrackableType trackableTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds;
            
            if (raycastManager.Raycast(touchPosition, hits, trackableTypes))
            {
                Pose hitPose = hits[0].pose;
                GameObject targetPrefab = prefabToSpawn != null ? prefabToSpawn : defaultPlacementPrefab;

                if (targetPrefab == null)
                {
                    Debug.LogWarning("[ARPlacementManager] No defaultPlacementPrefab assigned!");
                    return false;
                }

                if (spawnedObject == null)
                {
                    spawnedObject = Instantiate(targetPrefab, hitPose.position, hitPose.rotation);
                    Debug.Log($"[ARPlacementManager] Spawned 3D object at {hitPose.position}");
                }
                else
                {
                    spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                    Debug.Log($"[ARPlacementManager] Repositioned 3D object to {hitPose.position}");
                }

                OnObjectPlaced?.Invoke(hitPose.position, hitPose.rotation);
                return true;
            }
            else
            {
                Debug.Log($"[ARPlacementManager] Raycast from {touchPosition} did not hit an AR plane.");
            }
            return false;
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
