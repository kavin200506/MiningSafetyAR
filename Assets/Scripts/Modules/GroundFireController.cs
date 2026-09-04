using System;
using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Controller for managing Vefects URP ground fire hazards in AR safety training simulations.
    /// Controls multiple particle system states (flames, embers, smoke), fire ignition, instant extinguishment,
    /// dynamic fire health with foam suppression, and low-spec Android mobile optimizations.
    /// </summary>
    public class GroundFireController : MonoBehaviour
    {
        public static GroundFireController Instance { get; private set; }

        [Header("Fire Visual Particles (Multi-System Array)")]
        [SerializeField] private ParticleSystem[] groundFireParticles;
        public ParticleSystem[] GroundFireParticles
        {
            get => groundFireParticles;
            set => groundFireParticles = value;
        }

        [Header("Low-Spec Mobile Optimization Settings")]
        [SerializeField] private bool lowSpecMode = true;
        public bool LowSpecMode
        {
            get => lowSpecMode;
            set => lowSpecMode = value;
        }

        [Header("Fire Health System")]
        [SerializeField] private float maxFireHealth = 40f;
        [SerializeField] private float foamPower = 25f;
        [Tooltip("Extra suppression (HP/sec) applied at full (1.0) sweep intensity, on top of foamPower. See documents/sweep.md.")]
        [SerializeField] private float sweepBonusRate = 50f;
        private float currentFireHealth;
        private Light fireLight;
        private float initialLightIntensity;
        private float initialEmissionRate;
        private bool isFireActive = false;

        public bool IsFireActive
        {
            get => isFireActive;
            private set => isFireActive = value;
        }

        public float CurrentFireHealth => currentFireHealth;
        public float MaxFireHealth => maxFireHealth;
        public float FireHealthNormalized => maxFireHealth > 0f ? Mathf.Clamp01(currentFireHealth / maxFireHealth) : 0f;

        public event Action OnFireExtinguished;

        private void Awake()
        {
            // Always set Instance to current active fire hazard in scene
            Instance = this;

            EnsureFireCollider();
            InitializeParticleSystems();
            ApplyLowSpecOptimizations();
        }

        private void Start()
        {
            // Unless explicitly ignited by user floor plane tap, keep fire hazard inactive on scene load
            if (!isFireActive)
            {
                ExtinguishFireInstant();
            }
        }

        private void EnsureFireCollider()
        {
            var existing = GetComponentInChildren<Collider>();
            if (existing == null)
            {
                var col = gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 1.2f;
                col.center = new Vector3(0f, 0.5f, 0f);
            }
        }

        public void InitializeParticleSystems()
        {
            if (groundFireParticles == null || groundFireParticles.Length == 0)
            {
                ParticleSystem[] found = GetComponentsInChildren<ParticleSystem>(true);
                if (found != null && found.Length > 0)
                {
                    groundFireParticles = found;
                }
            }
        }

        private void ApplyLowSpecOptimizations()
        {
            if (groundFireParticles == null || groundFireParticles.Length == 0) return;

            foreach (var ps in groundFireParticles)
            {
                if (ps == null) continue;

                string goName = ps.gameObject.name;

                // Only disable screen distortion shaders, never disable primary flame/smoke particles
                if (lowSpecMode && (goName.Contains("HeatHaze") || goName.Contains("Distortion")))
                {
                    ps.gameObject.SetActive(false);
                    continue;
                }

                var main = ps.main;
                main.loop = true;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = true;
                if (main.maxParticles > 120)
                {
                    main.maxParticles = 120;
                }

                var emission = ps.emission;
                emission.enabled = true;
            }
        }

        private void Ensure3DFireVisual()
        {
            MeshRenderer existingMR = GetComponentInChildren<MeshRenderer>(true);
            if (existingMR == null)
            {
                GameObject fireVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fireVisual.name = "3D_FireHazard_MeshVisual";
                fireVisual.transform.SetParent(transform, false);
                fireVisual.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                fireVisual.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);

                Collider c = fireVisual.GetComponent<Collider>();
                if (c != null) Destroy(c);

                MeshRenderer visMR = fireVisual.GetComponent<MeshRenderer>();
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");

                if (urpShader == null)
                {
                    Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    foreach (Renderer r in sceneRenderers)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal"))
                        {
                            urpShader = r.sharedMaterial.shader;
                            break;
                        }
                    }
                }

                if (urpShader != null)
                {
                    Material fireMat = new Material(urpShader);
                    fireMat.SetColor("_BaseColor", new Color(1.0f, 0.25f, 0.0f)); // Bright Fire Orange
                    if (fireMat.HasProperty("_EmissionColor"))
                    {
                        fireMat.EnableKeyword("_EMISSION");
                        fireMat.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.0f) * 2.0f);
                    }
                    visMR.material = fireMat;
                }
            }
        }

        /// <summary>
        /// Ignites the ground fire hazard across all child particle systems and sets IsFireActive to true.
        /// Also initializes fire health and captures reference values for dynamic scaling.
        /// </summary>
        [ContextMenu("Ignite Fire Test")]
        public void IgniteFire()
        {
            gameObject.SetActive(true);

            Ensure3DFireVisual();
            InitializeParticleSystems();
            ApplyLowSpecOptimizations();

            currentFireHealth = maxFireHealth;

            fireLight = GetComponentInChildren<Light>();
            if (fireLight != null)
            {
                initialLightIntensity = fireLight.intensity;
                fireLight.enabled = true;
            }

            if (groundFireParticles != null && groundFireParticles.Length > 0)
            {
                initialEmissionRate = 0f;

                foreach (var ps in groundFireParticles)
                {
                    if (ps == null) continue;

                    var main = ps.main;
                    main.loop = true;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.playOnAwake = true;

                    var emission = ps.emission;
                    if (initialEmissionRate <= 0f && emission.enabled)
                    {
                        initialEmissionRate = emission.rateOverTime.constant;
                    }

                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Play(true);
                }
            }

            IsFireActive = true;
            Debug.Log($"[FIRE_DIAG] [GroundFireController] Fire ignited! GO={gameObject.name}, Health={currentFireHealth}/{maxFireHealth}, ActiveParticlesCount={groundFireParticles?.Length ?? 0}, LowSpecMode={lowSpecMode}");
        }

        /// <summary>
        /// Applies foam suppression to the fire. Called by FireExtinguisherGrabController when foam raycast hits this fire.
        /// Reduces fire health and dynamically scales particle emission, light intensity, and visual size.
        /// sweepIntensity (0..1, see documents/sweep.md) scales the suppression rate on top of the
        /// base foamPower — 0 = standing still, 1 = genuine side-to-side sweeping at full intensity.
        /// </summary>
        public void ApplyFoamSuppression(Vector3 hitPoint, float deltaTime, float sweepIntensity = 0f)
        {
            if (!isFireActive || currentFireHealth <= 0f) return;

            float rate = foamPower + sweepBonusRate * Mathf.Clamp01(sweepIntensity);
            currentFireHealth -= rate * deltaTime;
            currentFireHealth = Mathf.Max(0f, currentFireHealth);

            float normalizedHealth = FireHealthNormalized;

            if (groundFireParticles != null)
            {
                foreach (var ps in groundFireParticles)
                {
                    if (ps != null && ps.isPlaying)
                    {
                        var emission = ps.emission;
                        emission.rateOverTimeMultiplier = normalizedHealth;
                    }
                }
            }

            if (fireLight != null)
            {
                fireLight.intensity = initialLightIntensity * normalizedHealth;
            }

            // Scale visual flames while maintaining hit-testable bounds
            transform.localScale = Vector3.one * (0.35f + 0.65f * normalizedHealth);

            Debug.Log($"[FIRE_DIAG] [GroundFireController] Foam applied! Health={currentFireHealth:F1}/{maxFireHealth}, Normalized={normalizedHealth:F2}, Rate={rate:F1}HP/s, SweepIntensity={sweepIntensity:F2}");

            if (currentFireHealth <= 0f)
            {
                ExtinguishFireInstant();
                OnFireExtinguished?.Invoke();
                Debug.Log("[FIRE_DIAG] [GroundFireController] Fire extinguished by foam suppression!");
            }
        }

        /// <summary>
        /// Instantly stops all ground fire particle systems and clears active particles.
        /// </summary>
        [ContextMenu("Extinguish Fire Test")]
        public void ExtinguishFireInstant()
        {
            if (groundFireParticles != null)
            {
                foreach (var ps in groundFireParticles)
                {
                    if (ps != null && ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            if (fireLight != null)
            {
                fireLight.enabled = false;
            }

            // Hide child renderers and deactivate entire fire hazard
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r != null) r.enabled = false;
            }

            IsFireActive = false;
            Debug.Log("[GroundFireController] Fire extinguished — hazard deactivated and vanished from scene.");
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isFireActive || currentFireHealth <= 0f) return;

            if (groundFireParticles != null)
            {
                foreach (var ps in groundFireParticles)
                {
                    if (ps == null) continue;
                    if (!ps.gameObject.activeInHierarchy) continue;
                    if (!ps.isPlaying && isFireActive)
                    {
                        ps.Play(true);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnGUI()
        {
            // Test UI controls removed per user request
        }
    }
}
