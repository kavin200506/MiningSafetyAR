using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Controller for managing Vefects URP ground fire hazards in AR safety training simulations.
    /// Controls multiple particle system states (flames, embers, smoke), fire ignition, instant extinguishment,
    /// and low-spec Android mobile optimizations.
    /// </summary>
    public class GroundFireController : MonoBehaviour
    {
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

        private bool isFireActive = false;
        public bool IsFireActive
        {
            get => isFireActive;
            private set => isFireActive = value;
        }

        private void Awake()
        {
            InitializeParticleSystems();
            ApplyLowSpecOptimizations();
        }

        public void InitializeParticleSystems()
        {
            if (groundFireParticles == null || groundFireParticles.Length == 0)
            {
                groundFireParticles = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        private void ApplyLowSpecOptimizations()
        {
            if (groundFireParticles == null || groundFireParticles.Length == 0) return;

            foreach (var ps in groundFireParticles)
            {
                if (ps == null) continue;

                // Disable Heat Haze / Smoke / Distortion child GameObjects if lowSpecMode is enabled
                if (lowSpecMode)
                {
                    string goName = ps.gameObject.name;
                    if (goName.Contains("Smoke") || goName.Contains("HeatHaze") || goName.Contains("Haze") || goName.Contains("Distortion"))
                    {
                        ps.gameObject.SetActive(false);
                        Debug.Log($"[GroundFireController] Low-spec mode: Disabled GPU-intensive child particle system '{goName}'");
                        continue;
                    }
                }

                // Reduce Max Particles if above 50, and reduce Emission Rate by ~35% (0.65 multiplier)
                var main = ps.main;
                if (main.maxParticles > 50)
                {
                    main.maxParticles = 50;
                }

                var emission = ps.emission;
                if (emission.enabled)
                {
                    emission.rateOverTimeMultiplier *= 0.65f;
                }
            }

            // Check non-particle child transforms (e.g. Distortion or Heat Haze quad objects)
            if (lowSpecMode)
            {
                foreach (Transform child in transform)
                {
                    if (child != null)
                    {
                        string name = child.name;
                        if (name.Contains("Smoke") || name.Contains("HeatHaze") || name.Contains("Haze") || name.Contains("Distortion"))
                        {
                            child.gameObject.SetActive(false);
                            Debug.Log($"[GroundFireController] Low-spec mode: Disabled child GameObject '{name}'");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ignites the ground fire hazard across all child particle systems and sets IsFireActive to true.
        /// </summary>
        [ContextMenu("Ignite Fire Test")]
        public void IgniteFire()
        {
            gameObject.SetActive(true);

            InitializeParticleSystems();
            ApplyLowSpecOptimizations();

            if (groundFireParticles != null)
            {
                foreach (var ps in groundFireParticles)
                {
                    if (ps != null && ps.gameObject.activeInHierarchy)
                    {
                        var main = ps.main;
                        main.loop = true;

                        if (!ps.isPlaying)
                        {
                            ps.Play(true);
                        }
                    }
                }
            }

            IsFireActive = true;
            Debug.Log($"[FIRE_DIAG] [GroundFireController] Fire ignited! GO={gameObject.name}, ActiveParticlesCount={groundFireParticles?.Length ?? 0}, LowSpecMode={lowSpecMode}");
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
                    if (ps != null)
                    {
                        if (ps.isPlaying)
                        {
                            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }
                }
            }

            IsFireActive = false;
            Debug.Log("[GroundFireController] Fire extinguished across all particle systems.");
        }

        private void OnGUI()
        {
            // Test UI controls removed per user request
        }
    }
}
