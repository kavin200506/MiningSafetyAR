using UnityEngine;
using MiningSafetyAR.AR;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Marks the physical end of the drill — stepping into this zone means the worker has walked
    /// out of the confined space to safety. Turns Simulation mode off, which hides the virtual mine
    /// and hands back to the real AR camera passthrough (SimulationEnvironmentController), i.e.
    /// literally stepping out of the mine and back into the real world.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class MineExitTrigger : MonoBehaviour
    {
        [SerializeField] private SimulationEnvironmentController simulationEnvironmentController;

        private bool triggered;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (simulationEnvironmentController == null)
                simulationEnvironmentController = FindFirstObjectByType<SimulationEnvironmentController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            if (other.GetComponent<CharacterController>() == null) return;

            triggered = true;
            Debug.Log("[MINE_EXIT] Worker reached the exit — ending simulation, returning to AR passthrough.");
            if (simulationEnvironmentController != null) simulationEnvironmentController.SetSimulationMode(false);
        }
    }
}
