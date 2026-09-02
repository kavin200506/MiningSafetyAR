using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Component attached to FireExtinguisherModel GameObjects to ensure the real 3D glTF model
    /// (with PBR textures, labels, and metal roughness) is loaded at runtime via GLTFast,
    /// replacing any legacy primitive red cylinder components.
    /// </summary>
    public class FireExtinguisherModelLoader : MonoBehaviour
    {
        [Header("Model Scaling Settings")]
        [SerializeField] private float modelScaleMultiplier = 1.0f;
        [SerializeField] private bool loadOnStart = true;

        private GameObject loadedGLTFModel;
        private bool isLoading = false;

        public static event Action<GameObject> OnModelLoaded;

        private async void Start()
        {
            if (loadOnStart && loadedGLTFModel == null && !isLoading)
            {
                await Load3DModelAsync();
            }
        }

        public async Task<GameObject> Load3DModelAsync()
        {
            if (loadedGLTFModel != null) return loadedGLTFModel;
            if (isLoading) return null;

            isLoading = true;
            try
            {
                // 1. Hide legacy fallback primitive renderers if present on root GameObject or existing children
                foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.enabled = false;
                }
                MeshFilter mf = GetComponent<MeshFilter>();
                CapsuleCollider cc = GetComponent<CapsuleCollider>();

                if (mf != null && (mf.sharedMesh == null || mf.sharedMesh.name.Contains("Cylinder")))
                {
                    Destroy(mf);
                }
                if (cc != null)
                {
                    Destroy(cc);
                }

                // 2. Asynchronously decode and instantiate the real 3D Draco glTF Fire Extinguisher model
                Debug.Log($"[FireExtinguisherModelLoader] Invoking GLTFastModelLoader.LoadFireExtinguisherModelAsync for '{gameObject.name}'...");

                loadedGLTFModel = await GLTFastModelLoader.LoadFireExtinguisherModelAsync(Vector3.zero, Quaternion.identity, transform);

                if (this == null || gameObject == null)
                {
                    Debug.LogWarning("[WARN] [FireExtinguisherModelLoader] Component or GameObject was destroyed during async glTF model loading.");
                    if (loadedGLTFModel != null)
                    {
                        Destroy(loadedGLTFModel);
                    }
                    return null;
                }

                if (loadedGLTFModel != null)
                {
                    loadedGLTFModel.name = "Real_3D_FireExtinguisher_GLTF";
                    loadedGLTFModel.transform.localPosition = Vector3.zero;
                    loadedGLTFModel.transform.localRotation = Quaternion.identity;
                    loadedGLTFModel.transform.localScale = Vector3.one * modelScaleMultiplier;

                    Debug.Log($"[FireExtinguisherModelLoader] Successfully attached real 3D Fire Extinguisher glTF model to '{gameObject.name}'!");
                    OnModelLoaded?.Invoke(gameObject);
                    if (FireExtinguisherGrabController.Instance != null)
                    {
                        FireExtinguisherGrabController.Instance.SetupExtinguisherForGrabbing(gameObject);
                    }
                }
                else
                {
                    Debug.LogWarning("[WARN] [FireExtinguisherModelLoader] glTF loading returned null. Re-enabling primitive fallback renderer if available.");
                    if (mr != null) mr.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [FireExtinguisherModelLoader] Exception while loading 3D Fire Extinguisher model: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                isLoading = false;
            }

            return loadedGLTFModel;
        }
    }
}
