using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Step 2 checkpoint: past the TurnR corner, CO levels are still high, so the worker must
    /// look at (or grab) the multi-gas detector again before the drill will let them proceed to
    /// donning the SCSR mask. A trigger volume, never a hard gate — the BoxCollider stays a
    /// trigger (set in Awake, same convention as GasMaskGate/MineExitTrigger) so it never blocks
    /// movement; it only watches for the re-check while the worker is inside the zone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GasSecondCheckZone : MonoBehaviour
    {
        [SerializeField] private MultiGasDetectorController detectorController;
        [SerializeField] private GasLeakModuleManager moduleManager;

        private bool playerInside;
        private bool reported;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (detectorController == null) detectorController = FindFirstObjectByType<MultiGasDetectorController>();
            if (moduleManager == null) moduleManager = FindFirstObjectByType<GasLeakModuleManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<CharacterController>() != null)
            {
                playerInside = true;
                Debug.Log("[GAS_SCORING] Worker entered second-check zone (past TurnR).");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<CharacterController>() != null) playerInside = false;
        }

        private void Update()
        {
            if (reported || !playerInside || detectorController == null || moduleManager == null) return;

            if (detectorController.IsCheckingDetector || detectorController.IsHeld)
            {
                reported = true;
                moduleManager.NotifySecondDetectorCheck();
                Debug.Log("[GAS_SCORING] Second detector check reported (worker re-checked detector past TurnR).");
            }
        }
    }
}
