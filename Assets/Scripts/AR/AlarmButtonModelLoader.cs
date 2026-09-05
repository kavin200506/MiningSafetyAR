using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Component attached to AlarmButtonModel GameObjects to instantiate the real 3D
    /// emergency alarm button model (FBX + PBR base color/metallic textures) at runtime,
    /// mirroring FireExtinguisherModelLoader's Resources.Load pattern.
    /// </summary>
    public class AlarmButtonModelLoader : MonoBehaviour
    {
        private const string ModelResourcePath = "Models/AlarmButton";

        [Header("Model Transformation Settings")]
        [SerializeField] private float modelScaleMultiplier = 1.0f;
        [SerializeField] private Vector3 modelRotationOffset = new Vector3(0f, 90f, 0f);
        [SerializeField] private bool loadOnStart = true;

        public Vector3 ModelRotationOffset
        {
            get => modelRotationOffset;
            set => modelRotationOffset = value;
        }

        private GameObject loadedModel;
        private bool isLoading = false;

        public static event Action<GameObject> OnModelLoaded;

        private void Start()
        {
            if (loadOnStart && loadedModel == null && !isLoading)
            {
                _ = Load3DModelAsync();
            }
        }

        public Task<GameObject> Load3DModelAsync()
        {
            if (loadedModel != null) return Task.FromResult(loadedModel);
            if (isLoading) return Task.FromResult<GameObject>(null);

            isLoading = true;
            try
            {
                GameObject fbxPrefab = Resources.Load<GameObject>(ModelResourcePath);
                if (fbxPrefab == null)
                {
                    Debug.LogError($"[ERROR] [AlarmButtonModelLoader] Could not find FBX model at Resources/{ModelResourcePath}.");
                    return Task.FromResult<GameObject>(null);
                }

                loadedModel = Instantiate(fbxPrefab, transform);
                loadedModel.name = "AlarmButton_3DModel";
                loadedModel.transform.localPosition = Vector3.zero;
                loadedModel.transform.localRotation = Quaternion.Euler(modelRotationOffset);
                loadedModel.transform.localScale = Vector3.one * modelScaleMultiplier;

                BoxCollider col = loadedModel.GetComponent<BoxCollider>() ?? loadedModel.AddComponent<BoxCollider>();
                Renderer[] renderers = loadedModel.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    col.center = loadedModel.transform.InverseTransformPoint(bounds.center);
                    Vector3 localSize = loadedModel.transform.InverseTransformVector(bounds.size);
                    col.size = new Vector3(Mathf.Max(Mathf.Abs(localSize.x), 0.25f),
                                          Mathf.Max(Mathf.Abs(localSize.y), 0.25f),
                                          Mathf.Max(Mathf.Abs(localSize.z), 0.25f));
                }
                else
                {
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.3f, 0.3f, 0.3f);
                }
                col.isTrigger = false;

                // Attach the click handler to the same GameObject as the BoxCollider
                if (loadedModel.GetComponent<AlarmButtonInteractable>() == null)
                {
                    loadedModel.AddComponent<AlarmButtonInteractable>();
                }

                Debug.Log($"[AlarmButtonModelLoader] Instantiated FBX Alarm Button model onto '{gameObject.name}'.");
                OnModelLoaded?.Invoke(gameObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [AlarmButtonModelLoader] Exception while instantiating Alarm Button FBX model: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                isLoading = false;
            }

            return Task.FromResult(loadedModel);
        }
    }
}
