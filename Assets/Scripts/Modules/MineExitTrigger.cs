using UnityEngine;
using MiningSafetyAR.AR;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Marks the physical end of the drill — stepping into this zone means the worker has walked
    /// out of the confined space to safety. Turns Simulation mode off, which hides the virtual mine
    /// and hands back to the real AR camera passthrough (SimulationEnvironmentController), i.e.
    /// literally stepping out of the mine and back into the real world.
    /// Also handles case where player starts inside the trigger zone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MineExitTrigger : MonoBehaviour
    {
        [SerializeField] private SimulationEnvironmentController simulationEnvironmentController;

        private bool triggered;
        private CharacterController playerController;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (simulationEnvironmentController == null)
                simulationEnvironmentController = FindFirstObjectByType<SimulationEnvironmentController>();
            
            // Find player CharacterController
            var xrOrigin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (xrOrigin != null)
                playerController = xrOrigin.GetComponent<CharacterController>();
        }

        private void Start()
        {
            // Check if player is already inside the trigger zone (e.g., starts at exit)
            CheckPlayerInside();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            if (other.GetComponent<CharacterController>() == null) return;

            triggered = true;
            Debug.Log("[MINE_EXIT] Worker reached the exit — ending simulation, returning to AR passthrough.");
            ExitMine();
        }

        private void Update()
        {
            // Fallback: check every frame if player entered (in case OnTriggerEnter missed)
            if (!triggered)
            {
                CheckPlayerInside();
            }
        }

        private void CheckPlayerInside()
        {
            if (triggered || playerController == null) return;

            var col = GetComponent<BoxCollider>();
            if (col == null) return;

            // Check if player's position is inside trigger bounds
            Vector3 playerPos = playerController.transform.position;
            Vector3 localPos = transform.InverseTransformPoint(playerPos);
            Vector3 halfSize = col.size * 0.5f;
            halfSize.Scale(transform.lossyScale); // Account for parent scaling

            if (Mathf.Abs(localPos.x - col.center.x) <= halfSize.x &&
                Mathf.Abs(localPos.y - col.center.y) <= halfSize.y &&
                Mathf.Abs(localPos.z - col.center.z) <= halfSize.z)
            {
                triggered = true;
                Debug.Log("[MINE_EXIT] Worker already at exit — ending simulation, returning to AR passthrough.");
                ExitMine();
            }
        }

        private void ExitMine()
        {
            // Notify GasLeakModuleManager for scoring
            var moduleManager = FindFirstObjectByType<GasLeakModuleManager>();
            if (moduleManager != null)
            {
                moduleManager.NotifyExitReached();
                Debug.Log("[MINE_EXIT] Notified GasLeakModuleManager of exit reached");
            }

            if (simulationEnvironmentController != null) 
                simulationEnvironmentController.SetSimulationMode(false);
        }
    }
}