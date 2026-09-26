using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// TrackedPoseDriver writes the AR camera's Transform directly every frame and never
    /// consults Physics, so without this nothing stops the camera passing straight through
    /// any collider (walls, the tunnel exterior, etc). This intercepts that raw per-frame
    /// camera delta, replays it through a CharacterController on the rig (which the physics
    /// engine DOES respect), and snaps the camera back onto whatever the controller actually
    /// achieved — so a step that would clip through a wall gets clamped at the wall instead.
    /// Only the horizontal (X/Z) component is clamped; vertical head tracking passes through
    /// untouched so it never fights the controller's own grounding.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ARLocomotionCollisionGuard : MonoBehaviour
    {
        [SerializeField] private Transform trackedCamera;

        private CharacterController controller;
        private ARWalkDiagnostics diagnostics;
        private Vector3 lastCameraWorldPos;
        private bool initialized;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            diagnostics = GetComponent<ARWalkDiagnostics>();
            if (trackedCamera == null && Camera.main != null) trackedCamera = Camera.main.transform;
        }

        private void LateUpdate()
        {
            if (trackedCamera == null) return;

            if (!initialized)
            {
                lastCameraWorldPos = trackedCamera.position;
                initialized = true;
                return;
            }

            Vector3 rawDelta = trackedCamera.position - lastCameraWorldPos;
            Vector3 horizontalDelta = new Vector3(rawDelta.x, 0f, rawDelta.z);

            // Undo the raw tracked movement so only the collision-clamped replay below actually lands.
            trackedCamera.position -= horizontalDelta;

            Vector3 rigPosBefore = transform.position;
            CollisionFlags flags = CollisionFlags.None;
            if (horizontalDelta.sqrMagnitude > 1e-8f)
            {
                flags = controller.Move(horizontalDelta);
            }
            Vector3 actualDelta = transform.position - rigPosBefore;

            trackedCamera.position += actualDelta;

            lastCameraWorldPos = trackedCamera.position;

            if (diagnostics != null) diagnostics.ReportMove(horizontalDelta, actualDelta, flags);
        }
    }
}
