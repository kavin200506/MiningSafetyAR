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
        private Vector3 lastCameraWorldPos;
        private bool initialized;

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

            if (!initialized)
            {
                // Bootstrap baseline is the RIG's own starting position, not wherever the camera
                // already happens to be — Unity's Editor XR Device Simulator persists its last pose
                // across Play sessions and can restore the camera to an arbitrary distant point
                // before this very first frame runs. Falling through to the normal path below (not
                // blind-trusting camera.position as free) means that initial jump gets the exact
                // same collision-checked treatment as any other frame's movement, instead of being
                // silently accepted as free teleportation.
                lastCameraWorldPos = transform.position;
                initialized = true;
            }

            Vector3 rawDelta = trackedCamera.position - lastCameraWorldPos;
            Vector3 horizontalDelta = new Vector3(rawDelta.x, 0f, rawDelta.z);

            // What we actually feed the CharacterController this frame — clamped to a sane maximum
            // human walking speed. A real device/simulator pose glitch (stuck input, a teleporting
            // re-lock) shows up as one huge delta in a single frame; clamping it here means the rig
            // itself can never lurch/teleport, regardless of what's injecting the bad delta upstream.
            // `horizontalDelta` (the UNCLAMPED raw value) is kept as-is below — it's still needed to
            // size the Camera Offset correction, so the glitch-excess doesn't just get silently
            // dropped and left to visually diverge the camera from the rig it was clamped away from.
            Vector3 moveDelta = horizontalDelta;
            float maxDeltaThisFrame = maxWalkSpeedMetersPerSecond * Time.deltaTime;
            if (moveDelta.magnitude > maxDeltaThisFrame)
            {
                moveDelta = moveDelta.normalized * maxDeltaThisFrame;
                Debug.LogWarning($"[LOCOMOTION_GUARD] Clamped runaway delta: raw={horizontalDelta} (|{horizontalDelta.magnitude:F2}|) -> clamped={moveDelta} (|{maxDeltaThisFrame:F3}|)");
            }

            Vector3 rigPosBefore = transform.position;
            CollisionFlags flags = CollisionFlags.None;
            if (moveDelta.sqrMagnitude > 1e-8f)
            {
                flags = controller.Move(moveDelta);
            }
            Vector3 actualDelta = transform.position - rigPosBefore;
            // Against the FULL raw delta, not the clamped one — this way the correction covers both
            // wall-blocking AND the glitch-clamp excess in one term, so Camera Offset always pulls
            // the camera back to sit right where the rig actually ended up.
            Vector3 blockedExcess = horizontalDelta - actualDelta;

            // Cancel whatever portion of the raw movement didn't actually happen (wall block and/or
            // glitch clamp), via Camera Offset — this is the correction that actually sticks (see
            // class doc comment for why).
            if (blockedExcess.sqrMagnitude > 1e-10f)
            {
                cameraOffset.position -= blockedExcess;
            }

            lastCameraWorldPos = trackedCamera.position;

            if (diagnostics != null) diagnostics.ReportMove(moveDelta, actualDelta, flags);
        }
    }
}
