using System.Text;
using UnityEngine;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Attach to the XR Origin (alongside CharacterController + ARLocomotionCollisionGuard).
    /// Purely diagnostic — logs where the player actually goes, exactly where/why movement gets
    /// blocked, and what's physically present at that position, so a real root cause can be read
    /// off the Console/log file instead of guessed at. Everything is prefixed [WALK_DIAG] so it's
    /// easy to filter out in Console search. Also draws a small on-screen HUD (separate from, and
    /// independent of, ARSimulationLogger's own disabled log panel) showing live position + the
    /// last block event, since a real device tester may not have Console access.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ARWalkDiagnostics : MonoBehaviour
    {
        [SerializeField] private Transform trackedCamera;
        [Tooltip("Minimum position change (meters) before logging a position sample — avoids flooding the log while standing still.")]
        [SerializeField] private float positionLogThreshold = 0.15f;
        [Tooltip("If the requested vs actual CharacterController.Move() delta differs by more than this fraction, it's logged as a block event.")]
        [SerializeField] private float blockDetectionSlack = 0.15f;
        [Tooltip("Radius used to list nearby colliders when a block or void is detected.")]
        [SerializeField] private float probeRadius = 3f;

        [Tooltip("Horizontal (X/Z) distance between rig and camera beyond which it's logged as unexpected drift — Camera Offset is (0,0,0) local in this rig, so any large horizontal gap here isn't an expected static offset, it's the rig failing to track the camera.")]
        [SerializeField] private float rigCameraDivergenceThreshold = 0.6f;

        private CharacterController controller;
        private Vector3 lastLoggedPos;
        private string lastBlockInfo = "(none yet)";
        private string lastVoidInfo = "(none yet)";
        private string lastDivergenceInfo = "(none yet)";
        private float nextVoidCheckTime;
        private float spawnY;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (trackedCamera == null && Camera.main != null) trackedCamera = Camera.main.transform;
        }

        private void Start()
        {
            lastLoggedPos = transform.position;
            spawnY = transform.position.y;
            LogDiag($"SPAWN rig={transform.position} camera={(trackedCamera != null ? trackedCamera.position.ToString() : "null")} " +
                     $"envRoot={DescribeEnvRoot()} mainCameraIdentity={(Camera.main != null ? Camera.main.GetInstanceID().ToString() : "null")}");
            DescribeNearby(transform.position, "SPAWN");
        }

        /// <summary>
        /// Called by ARLocomotionCollisionGuard right after each CharacterController.Move() so this
        /// class doesn't need its own duplicate Move() call (which would double-move the rig).
        /// </summary>
        public void ReportMove(Vector3 requestedDelta, Vector3 actualDelta, CollisionFlags flags)
        {
            float requestedMag = requestedDelta.magnitude;
            float actualMag = actualDelta.magnitude;

            if (requestedMag > 0.0001f && actualMag < requestedMag * (1f - blockDetectionSlack))
            {
                lastBlockInfo = $"pos={transform.position} requested={requestedDelta} (|{requestedMag:F3}|) actual={actualDelta} (|{actualMag:F3}|) flags={flags}";
                LogDiag($"BLOCKED {lastBlockInfo}");
                DescribeNearby(transform.position, "BLOCKED");
            }

            if ((transform.position - lastLoggedPos).sqrMagnitude >= positionLogThreshold * positionLogThreshold)
            {
                lastLoggedPos = transform.position;
                LogDiag($"POS rig={transform.position} camera={(trackedCamera != null ? trackedCamera.position.ToString() : "null")}");
            }

            CheckDivergenceAndSink();
        }

        /// <summary>
        /// Camera Offset is (0,0,0) local in this rig, so rig and camera should only differ
        /// horizontally by whatever real device tracking is currently contributing — a large,
        /// growing horizontal gap means the rig is failing to follow the camera (a real bug, not
        /// an expected static offset). Also flags the rig sinking below its spawn height, since
        /// CharacterController.Move() is only ever called with zero vertical input here and
        /// should never drop on its own.
        /// </summary>
        private void CheckDivergenceAndSink()
        {
            if (trackedCamera == null) return;

            Vector3 rigPos = transform.position;
            Vector3 camPos = trackedCamera.position;
            float horizontalGap = new Vector2(rigPos.x - camPos.x, rigPos.z - camPos.z).magnitude;

            if (horizontalGap > rigCameraDivergenceThreshold)
            {
                lastDivergenceInfo = $"rig={rigPos} cam={camPos} horizontalGap={horizontalGap:F2}m";
                LogDiag($"RIG_CAMERA_DIVERGENCE {lastDivergenceInfo}");
            }

            if (rigPos.y < spawnY - 0.1f)
            {
                LogDiag($"RIG_SANK_BELOW_SPAWN rig={rigPos} spawnY={spawnY:F3} dropped={(spawnY - rigPos.y):F3}m");
            }
        }

        private void Update()
        {
            if (Time.time >= nextVoidCheckTime)
            {
                nextVoidCheckTime = Time.time + 1.5f;
                bool nearby = Physics.CheckSphere(transform.position, probeRadius);
                bool floor = Physics.Raycast(transform.position, Vector3.down, 6f);
                bool ceiling = Physics.Raycast(transform.position, Vector3.up, 6f);
                if (!nearby || !floor)
                {
                    lastVoidInfo = $"pos={transform.position} nearbyAnyCollider={nearby} floorBelow={floor} ceilingAbove={ceiling}";
                    LogDiag($"POSSIBLE_VOID {lastVoidInfo}");
                    DescribeNearby(transform.position, "VOID_CHECK");
                }
            }
        }

        private void DescribeNearby(Vector3 pos, string tag)
        {
            Collider[] hits = Physics.OverlapSphere(pos, probeRadius);
            var sb = new StringBuilder();
            sb.Append($"[WALK_DIAG] {tag}_NEARBY at {pos} within {probeRadius}m: count={hits.Length}");
            foreach (var c in hits)
            {
                sb.Append($" | {c.name} ({c.GetType().Name}, layer={LayerMask.LayerToName(c.gameObject.layer)}, dist={Vector3.Distance(pos, c.ClosestPoint(pos)):F2}m)");
            }
            Debug.Log(sb.ToString());
        }

        private string DescribeEnvRoot()
        {
            GameObject env = GameObject.Find("GasMineEnvironment");
            return env == null ? "NOT FOUND" : $"pos={env.transform.position} rot={env.transform.rotation.eulerAngles} active={env.activeInHierarchy}";
        }

        private void LogDiag(string message) => Debug.Log($"[WALK_DIAG] {message}");

        private void OnGUI()
        {
            const int w = 520;
            GUI.Box(new Rect(10, 10, w, 126), "");
            GUI.Label(new Rect(18, 14, w - 16, 20), $"Rig: {transform.position}");
            GUI.Label(new Rect(18, 32, w - 16, 20), $"Cam: {(trackedCamera != null ? trackedCamera.position.ToString() : "null")}");
            GUI.Label(new Rect(18, 50, w - 16, 20), $"Last block: {lastBlockInfo}");
            GUI.Label(new Rect(18, 68, w - 16, 20), $"Last void check: {lastVoidInfo}");
            GUI.Label(new Rect(18, 86, w - 16, 20), $"Last divergence: {lastDivergenceInfo}");
            GUI.Label(new Rect(18, 104, w - 16, 20), $"Spawn Y: {spawnY:F3}");
        }
    }
}
