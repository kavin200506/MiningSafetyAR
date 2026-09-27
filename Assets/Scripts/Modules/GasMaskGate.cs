using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Physical barrier placed in Tunnel2 just before the exit turn, right after the high-gas
    /// check point. Blocks the CharacterController with a solid wall until the worker has equipped
    /// their SCSR mask at least once, then opens permanently — matches the drill: alarm sounds,
    /// check the detector, mask on, only then proceed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GasMaskGate : MonoBehaviour
    {
        [SerializeField] private BeltItemGrabController beltItemGrabController;
        [Tooltip("Auto-resolved from Camera.main if left unassigned — used only to show the warning prompt when the worker is near the gate without the mask on.")]
        [SerializeField] private Transform player;
        [SerializeField] private float warningPromptRadius = 3f;

        private BoxCollider wall;
        private bool opened;

        private void Awake()
        {
            wall = GetComponent<BoxCollider>();
            wall.isTrigger = false;
            if (player == null && Camera.main != null) player = Camera.main.transform;
        }

        private void Update()
        {
            if (opened) return;

            if (beltItemGrabController != null && beltItemGrabController.HasEquippedScsr)
            {
                opened = true;
                wall.enabled = false;
                Debug.Log("[GAS_MASK_GATE] SCSR mask equipped — gate opened.");
            }
        }

        private void OnGUI()
        {
            if (opened || player == null) return;
            if (Vector3.Distance(player.position, transform.position) > warningPromptRadius) return;

            GUIStyle style = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.yellow;
            GUI.Box(new Rect(Screen.width / 2 - 260, 40, 520, 50), "⚠ High gas levels detected! Equip your SCSR mask before proceeding.", style);
        }
    }
}
