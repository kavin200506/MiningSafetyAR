using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MiningSafetyAR.AR
{
    [RequireComponent(typeof(ARRaycastManager))]
    [RequireComponent(typeof(ARPlaneManager))]
    public class ARPlacementManager : MonoBehaviour
    {
        public static ARPlacementManager Instance { get; private set; }

        [Header("Placement Prefab")]
        [SerializeField] private GameObject defaultPlacementPrefab;

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

        private void Update()
        {
            UpdatePlacementIndicator();

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
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
            if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                Pose hitPose = hits[0].pose;
                GameObject targetPrefab = prefabToSpawn != null ? prefabToSpawn : defaultPlacementPrefab;

                if (spawnedObject == null && targetPrefab != null)
                {
                    spawnedObject = Instantiate(targetPrefab, hitPose.position, hitPose.rotation);
                }
                else if (spawnedObject != null)
                {
                    spawnedObject.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
                }

                OnObjectPlaced?.Invoke(hitPose.position, hitPose.rotation);
                return true;
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
