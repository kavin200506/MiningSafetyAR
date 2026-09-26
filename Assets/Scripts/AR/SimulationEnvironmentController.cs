using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Runtime switch between real AR mode (camera passthrough + real detected planes) and
    /// Simulation mode (virtual Mine environment standing in for the real room). Only touches
    /// visuals and ARPlacementManager.SimulationMode — all gameplay/scoring logic is untouched
    /// and keeps running exactly as it does in real AR mode, just fed poses from a different
    /// raycast source (see ARPlacementManager.TryRaycastSurface).
    /// </summary>
    public class SimulationEnvironmentController : MonoBehaviour
    {
        [Tooltip("Root GameObject of the virtual Mine environment (tunnel pieces, props). Shown when Simulation mode is ON, hidden otherwise.")]
        [SerializeField] private GameObject simulationEnvironmentRoot;

        [Tooltip("The AR camera's passthrough background. Disabled when Simulation mode is ON so the Mine environment is visible instead of the real camera feed.")]
        [SerializeField] private ARCameraBackground cameraBackground;

        [Tooltip("If true, Simulation mode is active on scene start.")]
        [SerializeField] private bool startInSimulationMode = false;

        [Tooltip("The UI checkbox that drives this controller. Wired here at runtime (rather than only as a serialized Inspector event) so the listener always receives the toggle's real value.")]
        [SerializeField] private Toggle simulationToggle;

        [Tooltip("Assumed height (meters) of the AR camera above the real floor at the moment Simulation mode is switched on. Only used as a fallback when 'playerRig' is not assigned — see its tooltip.")]
        [SerializeField] private float assumedCameraHeightAboveFloor = 1.5f;

        [Tooltip("The player's walkable rig (XR Origin) — when assigned, the environment is anchored to THIS transform's position instead of the camera's. This matters because CharacterController/collision (ARLocomotionCollisionGuard) is centered on the rig, not the camera; the camera's tracked pose can differ from the rig's position by an arbitrary offset the moment simulation starts (especially with the Editor's XR Device Simulator, which doesn't guarantee starting at the rig's origin), which previously caused the environment to anchor to the wrong point relative to where collision was actually centered — the player could spawn or walk into space the environment never got built at ('outside the cave'). Leave unassigned only for older scenes with no walkable rig, which fall back to the previous camera-relative anchoring.")]
        [SerializeField] private Transform playerRig;

        public bool IsSimulationMode { get; private set; }

        private void Start()
        {
            if (simulationToggle != null)
            {
                simulationToggle.onValueChanged.AddListener(OnSimulationToggleChanged);
                simulationToggle.SetIsOnWithoutNotify(startInSimulationMode);
            }
            SetSimulationMode(startInSimulationMode);
        }

        public void SetSimulationMode(bool enabled)
        {
            IsSimulationMode = enabled;

            if (simulationToggle != null && simulationToggle.isOn != enabled)
            {
                simulationToggle.SetIsOnWithoutNotify(enabled);
            }

            if (simulationEnvironmentRoot != null)
            {
                // Spawn the environment centered on wherever the player actually is and facing
                // right now, instead of a fixed world position — the AR session's world origin is
                // wherever tracking started, not a known/fixed point, so a hardcoded position would
                // only line up with the player by coincidence.
                if (enabled)
                {
                    Camera cam = Camera.main;
                    float facingY = cam != null ? cam.transform.eulerAngles.y : 0f;

                    if (playerRig != null)
                    {
                        // Anchor to the rig — this is what CharacterController/collision is actually
                        // centered on, so the environment lines up exactly with where the player can walk.
                        simulationEnvironmentRoot.transform.SetPositionAndRotation(
                            playerRig.position,
                            Quaternion.Euler(0f, facingY, 0f));
                    }
                    else if (cam != null)
                    {
                        Vector3 camPos = cam.transform.position;
                        float floorY = camPos.y - assumedCameraHeightAboveFloor;
                        simulationEnvironmentRoot.transform.SetPositionAndRotation(
                            new Vector3(camPos.x, floorY, camPos.z),
                            Quaternion.Euler(0f, facingY, 0f));
                    }
                }
                simulationEnvironmentRoot.SetActive(enabled);
            }

            if (cameraBackground != null)
            {
                cameraBackground.enabled = !enabled;
            }

            if (ARPlacementManager.Instance != null)
            {
                ARPlacementManager.Instance.SimulationMode = enabled;
            }

            Debug.Log($"[SimulationEnvironmentController] Simulation mode set to {enabled}.");
        }

        /// <summary>Wired directly to a UI Toggle's onValueChanged.</summary>
        public void OnSimulationToggleChanged(bool isChecked)
        {
            SetSimulationMode(isChecked);
        }
    }
}
