using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Manages 2D Image Tracking in AR Foundation 6.x.
    /// Detects physical marker images (e.g. FireExtinguisherMarker, ExitSignMarker)
    /// and instantiates/repositions 3D safety equipment models over the physical markers.
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ARImageTrackingManager : MonoBehaviour
    {
        public static ARImageTrackingManager Instance { get; private set; }

        [Header("Marker Equipment Prefabs")]
        [SerializeField] private GameObject fireExtinguisherPrefab;
        public GameObject FireExtinguisherPrefab
        {
            get => fireExtinguisherPrefab;
            set => fireExtinguisherPrefab = value;
        }

        [SerializeField] private GameObject exitSignPrefab;
        public GameObject ExitSignPrefab
        {
            get => exitSignPrefab;
            set => exitSignPrefab = value;
        }

        [SerializeField] private GameObject alarmButtonPrefab;
        public GameObject AlarmButtonPrefab
        {
            get => alarmButtonPrefab;
            set => alarmButtonPrefab = value;
        }

        [Tooltip("Local position offset (relative to the fire extinguisher marker) at which the alarm button is spawned alongside it.")]
        [SerializeField] private Vector3 alarmButtonOffset = new Vector3(0.25f, 0f, 0f);

        [Header("Marker Image Reference Names")]
        [SerializeField] private string fireExtinguisherMarkerName = "FireExtinguisherMarker";
        public string FireExtinguisherMarkerName
        {
            get => fireExtinguisherMarkerName;
            set => fireExtinguisherMarkerName = value;
        }

        [SerializeField] private string exitSignMarkerName = "ExitSignMarker";
        public string ExitSignMarkerName
        {
            get => exitSignMarkerName;
            set => exitSignMarkerName = value;
        }

        private ARTrackedImageManager trackedImageManager;
        private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();

        public event Action<string, Vector3, Quaternion> OnMarkerTracked;
        public event Action<string> OnMarkerLost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WARN] [ARImageTrackingManager] Duplicate ARImageTrackingManager instance detected! Destroying extra component.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            trackedImageManager = GetComponent<ARTrackedImageManager>();
            Debug.Log($"[DIAG] [ARImageTrackingManager] Initialized ARImageTrackingManager. Manager assigned={trackedImageManager != null}");
        }

        private void OnEnable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
            }
        }

        private void OnDisable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
            }
        }

        private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
        {
            foreach (var trackedImage in eventArgs.added)
            {
                UpdateTrackedImage(trackedImage);
            }

            foreach (var trackedImage in eventArgs.updated)
            {
                UpdateTrackedImage(trackedImage);
            }

            foreach (var removedEntry in eventArgs.removed)
            {
                ARTrackedImage trackedImage = removedEntry.Value;
                if (trackedImage != null && trackedImage.referenceImage != null)
                {
                    string imageName = trackedImage.referenceImage.name;
                    if (spawnedObjects.TryGetValue(imageName, out GameObject spawnedObj))
                    {
                        if (spawnedObj != null)
                        {
                            Destroy(spawnedObj);
                        }
                        spawnedObjects.Remove(imageName);
                        Debug.Log($"[ARImageTrackingManager] Marker permanently removed: {imageName}, cleaning up spawned object.");
                    }
                }
            }
        }

        private void UpdateTrackedImage(ARTrackedImage trackedImage)
        {
            if (trackedImage == null || trackedImage.referenceImage == null) return;

            string imageName = trackedImage.referenceImage.name;
            TrackingState state = trackedImage.trackingState;

            if (state == TrackingState.Tracking)
            {
                GameObject spawnedObj = GetOrSpawnObjectForMarker(imageName, trackedImage.transform.position, trackedImage.transform.rotation);
                if (spawnedObj != null)
                {
                    spawnedObj.transform.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
                    if (!spawnedObj.activeSelf)
                    {
                        spawnedObj.SetActive(true);
                    }
                    Debug.Log($"[ARImageTrackingManager] Tracking marker: {imageName} at {trackedImage.transform.position}");
                    OnMarkerTracked?.Invoke(imageName, trackedImage.transform.position, trackedImage.transform.rotation);
                }
            }
            else if (state == TrackingState.Limited || state == TrackingState.None)
            {
                Debug.LogWarning($"[ARImageTrackingManager] Lost tracking on {imageName}");
                if (spawnedObjects.TryGetValue(imageName, out GameObject spawnedObj) && spawnedObj != null)
                {
                    if (spawnedObj.activeSelf)
                    {
                        spawnedObj.SetActive(false);
                    }
                }
                OnMarkerLost?.Invoke(imageName);
            }
        }

        private GameObject GetOrSpawnObjectForMarker(string imageName, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(imageName))
            {
                Debug.LogWarning("[WARN] [ARImageTrackingManager] GetOrSpawnObjectForMarker called with null or empty imageName.");
                return null;
            }

            if (spawnedObjects.TryGetValue(imageName, out GameObject existingObj) && existingObj != null)
            {
                return existingObj;
            }

            GameObject prefabToSpawn = null;
            if (string.Equals(imageName, fireExtinguisherMarkerName, StringComparison.OrdinalIgnoreCase))
            {
                prefabToSpawn = fireExtinguisherPrefab;
            }
            else if (string.Equals(imageName, exitSignMarkerName, StringComparison.OrdinalIgnoreCase))
            {
                prefabToSpawn = exitSignPrefab;
            }

            GameObject newObj = null;
            if (prefabToSpawn != null)
            {
                newObj = Instantiate(prefabToSpawn, position, rotation);
                newObj.name = $"{imageName}_ARObject";
            }
            else
            {
                Debug.LogWarning($"[WARN] [ARImageTrackingManager] Prefab for marker '{imageName}' was unassigned! Creating 3D marker container.");
                newObj = new GameObject($"{imageName}_3DMarkerVisual");
                newObj.transform.SetPositionAndRotation(position, rotation);
            }

            if (string.Equals(imageName, fireExtinguisherMarkerName, StringComparison.OrdinalIgnoreCase))
            {
                FireExtinguisherModelLoader loader = newObj.GetComponent<FireExtinguisherModelLoader>() ?? newObj.AddComponent<FireExtinguisherModelLoader>();
                _ = loader.Load3DModelAsync();

                // Spawn the emergency alarm button alongside the extinguisher, at the same
                // marker event, so both appear together as soon as the marker is tracked.
                if (alarmButtonPrefab != null)
                {
                    GameObject alarmButtonObj = Instantiate(alarmButtonPrefab, newObj.transform);
                    alarmButtonObj.name = "AlarmButton_ARObject";
                    alarmButtonObj.transform.localPosition = alarmButtonOffset;
                    alarmButtonObj.transform.localRotation = Quaternion.identity;
                }
                else
                {
                    Debug.LogWarning("[WARN] [ARImageTrackingManager] alarmButtonPrefab is unassigned; alarm button will not spawn with the fire extinguisher.");
                }
            }

            if (newObj != null)
            {
                spawnedObjects[imageName] = newObj;
            }

            return newObj;
        }

        /// <summary>
        /// Simulates 2D image marker tracking in Unity Editor Play Mode for rapid testing without deploying to mobile.
        /// </summary>
        public GameObject SimulateMarkerTracked(string markerName, Vector3 position = default, Quaternion rotation = default)
        {
            if (position == default)
            {
                Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
                position = cam != null ? cam.transform.position + cam.transform.forward * 1.2f : new Vector3(0, 0, 1.2f);
                rotation = Quaternion.identity;
            }

            GameObject obj = GetOrSpawnObjectForMarker(markerName, position, rotation);
            if (obj != null)
            {
                obj.transform.SetPositionAndRotation(position, rotation);
                if (!obj.activeSelf) obj.SetActive(true);
                Debug.Log($"[EDITOR_SIM] Simulated 2D Marker '{markerName}' tracked at {position}");
            }
            return obj;
        }

        public void ClearAllMarkerObjects()
        {
            foreach (var kvp in spawnedObjects)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            spawnedObjects.Clear();
        }
    }
}
