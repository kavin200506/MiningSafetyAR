using UnityEngine;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Step 1 of the gas leak drill: the worker looks down (tilts the phone/head down) to check
    /// the belt-mounted multi-gas detector. Reads real AR camera orientation every frame — same
    /// data source the Fire module already uses for its own sweep/evacuation checks — no new
    /// sensor API, just a new threshold applied to it.
    ///
    /// The detector's belt position is a child of "Camera Offset" (the body anchor), not the
    /// camera itself — a camera-child object stays glued to the same screen position no matter
    /// how the camera rotates, so it would never actually get "revealed" by tilting down. Being
    /// body-anchored means the belt stays put in world/body space while the head (camera) tilts
    /// independently to look at it, exactly like a real belt.
    /// </summary>
    public class MultiGasDetectorController : MonoBehaviour
    {
        [Header("Gesture thresholds (proposed — needs on-device tuning, see enchanced_gas_module.md §2A)")]
        [Tooltip("Downward pitch (degrees) the camera must exceed to count as 'looking at the belt'.")]
        [SerializeField] private float pitchThresholdDegrees = 35f;
        [Tooltip("How long the pitch must stay past the threshold before the check registers — rejects a quick flick and absorbs a one-frame AR tracking hiccup.")]
        [SerializeField] private float sustainSeconds = 0.5f;

        [Header("References")]
        [SerializeField] private Transform trackedCamera;
        [Tooltip("World-space reading display — enabled only while the detector is being actively checked.")]
        [SerializeField] private GameObject readingDisplay;

        private float lookDownStartTime = -1f;
        private bool isCheckingDetector;
        private bool hasCheckedOnce;
        private bool isHeld;

        /// <summary>True while the worker is currently, actively looking at the detector (sustained).</summary>
        public bool IsCheckingDetector => isCheckingDetector;
        /// <summary>True from the first moment the sustained check ever completes, for the rest of the drill.</summary>
        public bool HasCheckedOnce => hasCheckedOnce;
        /// <summary>True while the worker is holding the detector (grabbed from belt).</summary>
        public bool IsHeld => isHeld;

        private void Awake()
        {
            if (trackedCamera == null && Camera.main != null) trackedCamera = Camera.main.transform;
        }

        private void Start()
        {
            SetReadingDisplayVisible(false);
        }

        /// <summary>
        /// Called by BeltItemGrabController when the detector is picked up / put back. While held,
        /// the reading stays on regardless of gaze — the worker is actively holding it up to read,
        /// not just glancing at the belt — so the normal tilt-gated logic below is bypassed entirely.
        /// </summary>
        public void SetHeld(bool held)
        {
            isHeld = held;
            if (held)
            {
                hasCheckedOnce = true;
                SetReadingDisplayVisible(true);
                Debug.Log("[GAS_DETECTOR] Held in hand — reading forced on");
            }
            else
            {
                // Returning to the belt: fall back to the normal tilt-gated behaviour starting fresh.
                lookDownStartTime = -1f;
                isCheckingDetector = false;
                SetReadingDisplayVisible(false);
            }
        }

        private void Update()
        {
            if (trackedCamera == null || isHeld) return;

            float pitch = Mathf.Asin(-trackedCamera.forward.y) * Mathf.Rad2Deg;
            bool lookingDown = pitch > pitchThresholdDegrees;

            if (lookingDown)
            {
                if (lookDownStartTime < 0f) lookDownStartTime = Time.time;

                bool sustained = (Time.time - lookDownStartTime) >= sustainSeconds;
                if (sustained && !isCheckingDetector)
                {
                    isCheckingDetector = true;
                    hasCheckedOnce = true;
                    SetReadingDisplayVisible(true);
                    Debug.Log($"[GAS_DETECTOR] Checked — pitch={pitch:F1} deg, sustained {(Time.time - lookDownStartTime):F2}s");
                }
            }
            else
            {
                lookDownStartTime = -1f;
                if (isCheckingDetector)
                {
                    isCheckingDetector = false;
                    SetReadingDisplayVisible(false);
                    Debug.Log("[GAS_DETECTOR] Looked away — reading hidden");
                }
            }
        }

        private void LateUpdate()
        {
            UpdateBeltFollow();
        }

        private void UpdateBeltFollow()
        {
            if (trackedCamera == null) return;

            // Compute body horizontal forward direction from camera heading
            Vector3 camFwd = trackedCamera.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f) camFwd.Normalize();
            else camFwd = Vector3.forward;

            // Target belt position directly under camera at waist level (0.75m below camera, 0.15m forward)
            Vector3 targetBeltPos = trackedCamera.position - new Vector3(0f, 0.75f, 0f) + camFwd * 0.15f;
            
            transform.position = targetBeltPos;
            transform.rotation = Quaternion.LookRotation(camFwd, Vector3.up);
        }

        private void SetReadingDisplayVisible(bool visible)
        {
            if (readingDisplay != null) readingDisplay.SetActive(visible);
        }
    }
}
