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

        [Tooltip("Max number of wall/corner planes to slide against within a single frame's move. CharacterController.Move() only resolves collision against ONE surface per call, so at a concave tunnel corner (two walls meeting at close to a right angle, e.g. every modular Turn piece) a single Move() can hit both faces and cancel the player's forward progress to ~zero, even though there is clearly room to walk around the bend. Pre-resolving the delta against multiple surfaces before handing it to Move() is what lets the player slide around that corner instead of stopping dead against it.")]
        [SerializeField] private int maxSlidePlanesPerFrame = 3;

        [Tooltip("Downward speed (m/s) used to follow the floor down into small dips, in addition to the horizontal tracked delta. The mine's floor is a sculpted, undulating rock surface, not flat, so a purely horizontal move can sail over a dip and meet its far (rising) edge almost head-on, which reads as a wall hit even though it's just uneven cave floor. This speed only ever applies over a floor surface actually detected within groundProbeDistance below the capsule (see FindGroundedDescent) — an earlier version applied this downward speed unconditionally into the wall-slide sweep and, separately, straight into Move() with no floor check at all, and the player fell through a gap in the mesh and free-fell indefinitely with nothing to stop them. It is never applied further than the real detected gap, and never applied at all when no floor is found nearby.")]
        [SerializeField] private float groundStickSpeedMetersPerSecond = 1.5f;

        [Tooltip("Max distance (m) below the capsule to look for floor before applying the ground-stick descent. Bounds how big a dip the rig will follow down in one motion — small enough to hug natural floor undulations, nowhere near enough to fall through the mine into the void.")]
        [SerializeField] private float groundProbeDistance = 0.35f;

        private CharacterController controller;
        private ARWalkDiagnostics diagnostics;
        private readonly RaycastHit[] slideHitBuffer = new RaycastHit[16];

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

            // Wall/corner sliding is resolved on the horizontal delta ONLY. The character's feet
            // are always touching the floor, so once a downward component is mixed in here, the
            // very first capsule sweep re-detects the ground it's already resting on as a "hit"
            // every single frame, and the slide loop burns its whole iteration budget resolving
            // against that near-zero-distance "obstacle" instead of ever letting real horizontal
            // movement through (confirmed live: this produced flags=None / zero actual movement
            // on every frame — a full standstill, not a wall). The ground-stick nudge is added
            // AFTER sliding is resolved, straight into Move(), so CharacterController's own
            // built-in slope/step handling deals with it instead.
            Vector3 slideResolvedDelta = ResolveSlideAgainstCorners(moveDelta);
            Vector3 finalDelta = slideResolvedDelta + FindGroundedDescent();

            Vector3 rigPosBefore = transform.position;
            CollisionFlags flags = CollisionFlags.None;
            if (finalDelta.sqrMagnitude > 1e-8f)
            {
                flags = controller.Move(finalDelta);
            }
            Vector3 actualDelta = transform.position - rigPosBefore;

            // Lock Camera Offset's local horizontal position to (-camLocalPos.x, -camLocalPos.z).
            // This ensures Main Camera's world X/Z matches the Rig's collision-checked X/Z 1:1,
            // preventing double-movement and stopping infinite negative position drift when blocked.
            cameraOffset.localPosition = new Vector3(-camLocalPos.x, cameraOffset.localPosition.y, -camLocalPos.z);

            if (diagnostics != null) diagnostics.ReportMove(moveDelta, actualDelta, flags);
        }

        /// <summary>
        /// Sweeps the controller's own capsule along desiredDelta and, on a hit, projects the
        /// leftover movement onto the hit surface's plane (a "slide") instead of letting it get
        /// silently absorbed. Repeated up to maxSlidePlanesPerFrame times so a second surface
        /// (the other wall of a corner) also gets a slide instead of cancelling the first one's
        /// leftover outright. Self-hits against the rig's own CharacterController are filtered
        /// out since the capsule cast would otherwise immediately "hit" the capsule doing the
        /// casting.
        /// </summary>
        private Vector3 ResolveSlideAgainstCorners(Vector3 desiredDelta)
        {
            const float skin = 0.02f;

            Vector3 remaining = desiredDelta;
            Vector3 resolved = Vector3.zero;

            float halfHeight = Mathf.Max(controller.height * 0.5f - controller.radius, 0f);
            float radius = Mathf.Max(controller.radius - skin, 0.01f);
            Vector3 centerWorld = transform.TransformPoint(controller.center);
            Vector3 point1 = centerWorld + Vector3.up * halfHeight;
            Vector3 point2 = centerWorld - Vector3.up * halfHeight;

            for (int i = 0; i < maxSlidePlanesPerFrame && remaining.sqrMagnitude > 1e-8f; i++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;

                int hitCount = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, slideHitBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
                RaycastHit closestHit = default;
                float closestDistance = float.PositiveInfinity;
                bool didHit = false;
                for (int h = 0; h < hitCount; h++)
                {
                    RaycastHit hit = slideHitBuffer[h];
                    if (hit.collider == controller) continue;
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        closestHit = hit;
                        didHit = true;
                    }
                }

                if (!didHit)
                {
                    resolved += remaining;
                    remaining = Vector3.zero;
                    break;
                }

                float safeDistance = Mathf.Max(closestDistance - skin, 0f);
                Vector3 allowed = direction * safeDistance;
                resolved += allowed;
                point1 += allowed;
                point2 += allowed;

                Vector3 leftover = remaining - allowed;
                remaining = Vector3.ProjectOnPlane(leftover, closestHit.normal);
            }

            return resolved;
        }

        /// <summary>
        /// Looks straight down from the capsule's feet for real floor within groundProbeDistance
        /// and returns a downward delta capped at whatever gap was actually found — never more.
        /// If no floor is found at all within that distance, returns Vector3.zero: this method
        /// must never be able to move the rig down through open space it can't see the bottom
        /// of, on pain of an unbounded fall with nothing to stop it (confirmed live: exactly that
        /// happened with the previous unconditional-downward-speed version).
        /// </summary>
        private Vector3 FindGroundedDescent()
        {
            float halfHeight = Mathf.Max(controller.height * 0.5f - controller.radius, 0f);
            Vector3 feet = transform.TransformPoint(controller.center) + Vector3.down * (halfHeight + controller.radius);

            if (Physics.Raycast(feet + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, groundProbeDistance + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                float gap = Mathf.Max(hit.distance - 0.05f, 0f);
                float descent = Mathf.Min(groundStickSpeedMetersPerSecond * Time.deltaTime, gap);
                return Vector3.down * descent;
            }

            return Vector3.zero;
        }
    }
}
