using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// TrackedPoseDriver writes the AR camera's (Main Camera) Transform directly every single
    /// frame from raw device/simulator tracking data, unconditionally — it never consults
    /// Physics, and it overwrites whatever we do to that Transform on the very next frame. That
    /// makes Main Camera the wrong place to apply a wall-blocking correction: any correction
    /// written there gets discarded a frame later (confirmed live — the camera kept reporting a
    /// position exactly matching the raw, un-blocked movement, meters past a wall the rig had
    /// correctly stopped at).
    ///
    /// The fix is to correct a DIFFERENT transform that TrackedPoseDriver never touches: "Camera
    /// Offset", the rig's direct child and the camera's direct parent. Main Camera's world
    /// position = XR Origin.position + Camera Offset.position (world) + TrackedPoseDriver's own
    /// raw local write to Main Camera. We never touch that last term — instead, each frame we
    /// measure how much of the raw tracked delta a CharacterController on the rig actually
    /// allowed (collision-clamped), and shift Camera Offset by the leftover (blocked) amount in
    /// the opposite direction. That correction persists, because nothing else is overwriting
    /// Camera Offset.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ARLocomotionCollisionGuard : MonoBehaviour
    {
        [SerializeField] private Transform trackedCamera;
        [Tooltip("The camera's parent (NOT touched by TrackedPoseDriver). Auto-resolved from trackedCamera.parent if left unassigned.")]
        [SerializeField] private Transform cameraOffset;

        [Tooltip("Sane maximum human walking speed (m/s). Any single-frame tracked delta implying a faster speed than this is treated as sensor/simulator glitch (a 'stuck' or teleporting pose), not real movement, and is clamped down to this speed before it ever reaches the CharacterController or Camera Offset. This is what actually stops the rig/camera divergence from ever growing large, regardless of what's injecting the bad delta.")]
        [SerializeField] private float maxWalkSpeedMetersPerSecond = 2.5f;

        private CharacterController controller;
        private ARWalkDiagnostics diagnostics;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            diagnostics = GetComponent<ARWalkDiagnostics>();
            if (trackedCamera == null && Camera.main != null) trackedCamera = Camera.main.transform;
            if (cameraOffset == null && trackedCamera != null) cameraOffset = trackedCamera.parent;
        }

        private void LateUpdate()
        {
            if (trackedCamera == null || cameraOffset == null) return;

            // Vector pointing from current Rig position to target camera position (horizontal X/Z)
            Vector3 rigPos = transform.position;
            Vector3 camLocalPos = trackedCamera.localPosition;

            Vector3 targetWorldPos = trackedCamera.position;
            Vector3 desiredHorizontalDelta = new Vector3(targetWorldPos.x - rigPos.x, 0f, targetWorldPos.z - rigPos.z);

            // Clamp max speed per frame to handle single-frame tracking jumps or simulator resets
            float maxDeltaThisFrame = maxWalkSpeedMetersPerSecond * Time.deltaTime;
            Vector3 moveDelta = desiredHorizontalDelta;
            if (moveDelta.magnitude > maxDeltaThisFrame)
            {
                moveDelta = moveDelta.normalized * maxDeltaThisFrame;
                Debug.LogWarning($"[LOCOMOTION_GUARD] Clamped runaway delta: raw={desiredHorizontalDelta} (|{desiredHorizontalDelta.magnitude:F2}|) -> clamped={moveDelta} (|{maxDeltaThisFrame:F3}|)");
            }

            Vector3 rigPosBefore = transform.position;
            CollisionFlags flags = CollisionFlags.None;
            if (moveDelta.sqrMagnitude > 1e-8f)
            {
                flags = controller.Move(moveDelta);
            }
            Vector3 actualDelta = transform.position - rigPosBefore;

            // Lock Camera Offset's local horizontal position to (-camLocalPos.x, -camLocalPos.z).
            // This ensures Main Camera's world X/Z matches the Rig's collision-checked X/Z 1:1,
            // preventing double-movement and stopping infinite negative position drift when blocked.
            cameraOffset.localPosition = new Vector3(-camLocalPos.x, cameraOffset.localPosition.y, -camLocalPos.z);

            if (diagnostics != null) diagnostics.ReportMove(moveDelta, actualDelta, flags);
        }
    }
}
