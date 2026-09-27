using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Checkpoint zone in Tunnel 2 just before the exit turn. Never physically blocks player
    /// movement (isTrigger = true). Evaluates whether the worker equipped their SCSR mask:
    /// - If mask equipped: awards PPE safety points.
    /// - If mask missed: penalizes score and logs a mistake, but allows free movement out of the mine.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GasMaskGate : MonoBehaviour
    {
        [SerializeField] private BeltItemGrabController beltItemGrabController;
        [SerializeField] private GasLeakModuleManager moduleManager;
        [Tooltip("Auto-resolved from Camera.main if left unassigned.")]
        [SerializeField] private Transform player;
        [SerializeField] private float warningPromptRadius = 4f;

        private BoxCollider triggerZone;
        private bool hasEvaluated;

        private void Awake()
        {
            triggerZone = GetComponent<BoxCollider>();
            triggerZone.isTrigger = true; // Never block player movement physically!

            if (player == null && Camera.main != null) player = Camera.main.transform;
            if (moduleManager == null) moduleManager = FindFirstObjectByType<GasLeakModuleManager>();
            if (beltItemGrabController == null) beltItemGrabController = FindFirstObjectByType<BeltItemGrabController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasEvaluated) return;
            if (other.GetComponent<CharacterController>() == null && other.transform != player) return;

            hasEvaluated = true;
            bool equipped = beltItemGrabController != null && beltItemGrabController.HasEquippedScsr;

            if (equipped)
            {
                Debug.Log("[GAS_MASK_GATE] Worker passed checkpoint with SCSR mask equipped — Full score awarded.");
                if (moduleManager != null) moduleManager.OnPPESelected(true);
            }
            else
            {
                Debug.LogWarning("[GAS_MASK_GATE] Worker passed checkpoint WITHOUT SCSR mask — Score penalty registered.");
                if (moduleManager != null)
                {
                    moduleManager.OnPPESelected(false);
                }
                else
                {
                    Firebase.FirestoreService.Instance?.LogMistakeEvent(
                        "gas_safety", "main", MistakeTags.MissedPpeCheck, MistakeTags.MissedPpeCheckSeverity);
                }
            }
        }

        private void OnGUI()
        {
            if (hasEvaluated || player == null) return;
            if (Vector3.Distance(player.position, transform.position) > warningPromptRadius) return;

            bool equipped = beltItemGrabController != null && beltItemGrabController.HasEquippedScsr;
            if (equipped) return;

            GUIStyle style = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.yellow;
            GUI.Box(new Rect(Screen.width / 2 - 260, 40, 520, 50), "⚠ High gas zone ahead! Equip your SCSR mask to maintain full safety score.", style);
        }
    }
}
