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
            // Process newly detected tracked images
            foreach (var trackedImage in eventArgs.added)
            {
                UpdateTrackedImage(trackedImage);
            }

            // Process updated tracked images
            foreach (var trackedImage in eventArgs.updated)
            {
                UpdateTrackedImage(trackedImage);
            }

            // Process removed tracked images
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
                // Fallback primitive 3D object if prefab is not assigned in Inspector
                Debug.LogWarning($"[WARN] [ARImageTrackingManager] Prefab for marker '{imageName}' was unassigned! Generating prominent 3D safety visual object.");
                
                PrimitiveType primType = string.Equals(imageName, fireExtinguisherMarkerName, StringComparison.OrdinalIgnoreCase) ? 
                    PrimitiveType.Cylinder : PrimitiveType.Cube;

                newObj = GameObject.CreatePrimitive(primType);
                newObj.name = $"{imageName}_3DMarkerVisual";
                newObj.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                newObj.transform.SetPositionAndRotation(position, rotation);

                MeshRenderer mr = newObj.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                    Material mat = defaultShader != null ? new Material(defaultShader) : new Material(Shader.Find("Sprites/Default"));

                    if (string.Equals(imageName, fireExtinguisherMarkerName, StringComparison.OrdinalIgnoreCase))
                    {
                        mat.color = new Color(1.0f, 0.15f, 0.15f, 1.0f); // Bright Safety Red
                    }
                    else
                    {
                        mat.color = new Color(0.0f, 1.0f, 0.4f, 1.0f);  // Bright Emerald Green
                    }

                    mr.sharedMaterial = mat;
                }
            }

            if (newObj != null)
            {
                spawnedObjects[imageName] = newObj;
            }

            return newObj;
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
