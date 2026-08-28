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
            // 1. Check New Input System Enhanced Touch (Mobile Touchscreen Taps)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    Debug.Log($"[ARPlacementManager] Touch detected at {touch.screenPosition}");
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
                UnityEngine.Touch legacyTouch = Input.GetTouch(0);
                if (legacyTouch.phase == UnityEngine.TouchPhase.Began)
                {
                    Debug.Log($"[ARPlacementManager] Legacy Touch detected at {legacyTouch.position}");
                    PerformPlacementRaycast(legacyTouch.position);
                }
            }
        }

        private void UpdatePlacementIndicator()
        {
            if (placementIndicator == null) return;

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            if (raycastManager.Raycast(screenCenter, hits, TrackableType.Planes))
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
            // Raycast against all AR Planes and Feature Points
            TrackableType trackableTypes = TrackableType.Planes | TrackableType.FeaturePoint;
            bool hitSuccess = raycastManager.Raycast(touchPosition, hits, trackableTypes);

            Pose hitPose;
            if (hitSuccess && hits.Count > 0)
            {
                hitPose = hits[0].pose;
                Debug.Log($"[ARPlacementManager] Raycast hit AR Surface at {hitPose.position}");
            }
            else
            {
                // Fallback: Find Main Camera or any active Camera
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (mainCam != null)
                {
                    Vector3 spawnPos = mainCam.ScreenToWorldPoint(new Vector3(touchPosition.x, touchPosition.y, 1.2f));
                    hitPose = new Pose(spawnPos, Quaternion.LookRotation(mainCam.transform.forward));
                    Debug.Log($"[ARPlacementManager] AR surface detection initializing — Spawning at camera ray position {spawnPos}");
                }
                else
                {
                    Debug.LogWarning("[ARPlacementManager] Raycast failed and Camera is null.");
                    return false;
                }
            }

            GameObject targetPrefab = prefabToSpawn != null ? prefabToSpawn : defaultPlacementPrefab;
            
            if (spawnedObject == null)
            {
                if (targetPrefab != null)
                {
                    spawnedObject = Instantiate(targetPrefab, hitPose.position, hitPose.rotation);
                }
                else
                {
                    // Fallback Primitive 3D Cube if defaultPlacementPrefab was null
                    Debug.LogWarning("[ARPlacementManager] defaultPlacementPrefab was unassigned! Creating fallback 3D safety cube.");
                    spawnedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    spawnedObject.name = "Safety Equipment (Fallback)";
                    spawnedObject.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                    spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);

                    MeshRenderer mr = spawnedObject.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (defaultShader != null)
                        {
                            Material mat = new Material(defaultShader);
                            mat.color = new Color(1.0f, 0.4f, 0.0f); // Safety Orange
                            mr.sharedMaterial = mat;
                        }
                    }
                }
                Debug.Log($"[ARPlacementManager] Successfully spawned 3D object at {hitPose.position}");
            }
            else
            {
                spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                Debug.Log($"[ARPlacementManager] Repositioned 3D object to {hitPose.position}");
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
