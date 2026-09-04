using System;
using System.Threading.Tasks;
using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Component attached to FireExtinguisherModel GameObjects to ensure the real 3D fire
    /// extinguisher model (FBX, PBR textures, and named parts: Body, Hose, Pin_Pull_Ring,
    /// Pin_Prong_A/B, Gauge_Dial, Gauge_Rim, Valve_Body, Carry_Handle, Squeeze_Lever,
    /// Upper_Handle_Grip, Lever_Pivot, Lever_Mount_Bracket, Base_Ring, Label_Warning,
    /// Label_Instructions) is attached at runtime, replacing any legacy primitive fallback.
    ///
    /// The model is imported by Unity's built-in FBX importer at
    /// Assets/Resources/Models/FireExtinguisher.fbx and instantiated directly via
    /// Resources.Load — no runtime parsing/decoding is needed (unlike the previous
    /// glTF-based pipeline), so this completes synchronously despite the async signature
    /// kept here for call-site compatibility.
    /// </summary>
    public class FireExtinguisherModelLoader : MonoBehaviour
    {
        private const string ModelResourcePath = "Models/FireExtinguisher";

        [Header("Model Scaling Settings")]
        [SerializeField] private float modelScaleMultiplier = 1.0f;
        [SerializeField] private bool loadOnStart = true;

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
                // Hide any legacy fallback primitive renderers if present on this GameObject.
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

                GameObject fbxPrefab = Resources.Load<GameObject>(ModelResourcePath);
                if (fbxPrefab == null)
                {
                    Debug.LogError($"[ERROR] [FireExtinguisherModelLoader] Could not find FBX model at Resources/{ModelResourcePath}. Re-enabling primitive fallback renderers if available.");
                    foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                    {
                        renderer.enabled = true;
                    }
                    return Task.FromResult<GameObject>(null);
                }

                loadedModel = Instantiate(fbxPrefab, transform);
                loadedModel.name = "FireExtinguisher_3DModel";
                loadedModel.transform.localPosition = Vector3.zero;
                // The raw FBX's own baked orientation faces its front (label) away from the
                // container's forward axis, so both wall-mount placement rotation and
                // FireExtinguisherGrabController's holdingRotationOffset (which only rotate the
                // outer container, not this inner model) were showing the back consistently in
                // every context. Correcting it once here fixes both.
                loadedModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                loadedModel.transform.localScale = Vector3.one * modelScaleMultiplier;

                Debug.Log($"[FireExtinguisherModelLoader] Instantiated FBX Fire Extinguisher model onto '{gameObject.name}'.");
                OnModelLoaded?.Invoke(gameObject);
                if (FireExtinguisherGrabController.Instance != null)
                {
                    FireExtinguisherGrabController.Instance.SetupExtinguisherForGrabbing(gameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] [FireExtinguisherModelLoader] Exception while instantiating Fire Extinguisher FBX model: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                isLoading = false;
            }

            return Task.FromResult(loadedModel);
        }
    }
}
