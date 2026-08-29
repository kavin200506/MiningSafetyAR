using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Standalone controller for managing ground fire hazards in AR safety training simulations.
    /// Controls particle system state, fire ignition, and instant extinguishment.
    /// </summary>
    public class GroundFireController : MonoBehaviour
    {
        [Header("Fire Visual Particles")]
        [SerializeField] private ParticleSystem groundFireParticles;
        public ParticleSystem GroundFireParticles
        {
            get => groundFireParticles;
            set => groundFireParticles = value;
        }

        private bool isFireActive = false;
        public bool IsFireActive
        {
            get => isFireActive;
            private set => isFireActive = value;
        }

        private void Awake()
        {
            if (groundFireParticles == null)
            {
                groundFireParticles = GetComponent<ParticleSystem>() ?? GetComponentInChildren<ParticleSystem>();
            }

            EnsureValidParticleMaterial();
        }

        private void EnsureValidParticleMaterial()
        {
            if (groundFireParticles == null) return;

            ParticleSystemRenderer psr = groundFireParticles.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                if (psr.sharedMaterial == null || psr.sharedMaterial.shader == null || psr.sharedMaterial.shader.name.Contains("InternalErrorShader") || psr.sharedMaterial.name.Contains("Default-Particle"))
                {
                    Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? 
                                               Shader.Find("Universal Render Pipeline/Unlit") ?? 
                                               Shader.Find("Sprites/Default");

                    if (urpParticleShader != null)
                    {
                        Material fireMat = new Material(urpParticleShader);
                        fireMat.name = "GroundFireURPMaterial";
                        if (fireMat.HasProperty("_BaseColor"))
                        {
                            fireMat.SetColor("_BaseColor", new Color(1.0f, 0.5f, 0.0f, 1.0f));
                        }
                        else
                        {
                            fireMat.color = new Color(1.0f, 0.5f, 0.0f, 1.0f);
                        }
                        psr.sharedMaterial = fireMat;
                        Debug.Log("[GroundFireController] Assigned clean URP Particle Material to eliminate pink magenta rendering.");
                    }
                }
            }
        }

        [Header("Testing Controls")]
        [SerializeField] private bool showTestUI = true;

        /// <summary>
        /// Ignites the ground fire hazard, plays particle effects, and sets IsFireActive to true.
        /// </summary>
        [ContextMenu("Ignite Fire Test")]
        public void IgniteFire()
        {
            gameObject.SetActive(true);

            if (groundFireParticles == null)
            {
                groundFireParticles = GetComponent<ParticleSystem>() ?? GetComponentInChildren<ParticleSystem>();
            }

            EnsureValidParticleMaterial();

            if (groundFireParticles != null)
            {
                var main = groundFireParticles.main;
                main.loop = true;

                if (!groundFireParticles.isPlaying)
                {
                    groundFireParticles.Play(true);
                }
            }

            IsFireActive = true;
            ParticleSystemRenderer psr = groundFireParticles != null ? groundFireParticles.GetComponent<ParticleSystemRenderer>() : null;
            Debug.Log($"[FIRE_DIAG] [GroundFireController] Fire ignited! GO={gameObject.name}, Position={transform.position}, ActiveInHierarchy={gameObject.activeInHierarchy}, psIsPlaying={(groundFireParticles != null ? groundFireParticles.isPlaying : false)}, psIsEmitting={(groundFireParticles != null ? groundFireParticles.isEmitting : false)}, psMat={(psr != null && psr.sharedMaterial != null ? psr.sharedMaterial.name : "NULL")}");
        }

        /// <summary>
        /// Instantly stops the ground fire particles and sets IsFireActive to false.
        /// </summary>
        [ContextMenu("Extinguish Fire Test")]
        public void ExtinguishFireInstant()
        {
            if (groundFireParticles != null)
            {
                if (groundFireParticles.isPlaying)
                {
                    groundFireParticles.Stop();
                }
            }

            IsFireActive = false;
            Debug.Log("[GroundFireController] Fire extinguished.");
        }

        private void OnGUI()
        {
            if (!showTestUI) return;

            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2f, 2f, 1f));
            GUILayout.BeginArea(new Rect(10, 10, 220, 140), "Fire Test Controls", GUI.skin.window);
            
            if (GUILayout.Button("🔥 Ignite Fire"))
            {
                IgniteFire();
            }

            if (GUILayout.Button("🧯 Extinguish Fire"))
            {
                ExtinguishFireInstant();
            }

            GUILayout.Label($"Status: {(IsFireActive ? "<color=red>ACTIVE FIRE</color>" : "<color=green>EXTINGUISHED</color>")}");
            GUILayout.EndArea();
        }
    }
}
