using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Tap-to-grab / tap-empty-space-to-return for both the Belt-Mounted MultiGasDetector and SCSR.
    /// When tapped on the belt or when looking down, animates item up into close-up first-person inspection pose.
    /// Tapping empty space anywhere on screen returns the held item smoothly back to its waist belt slot.
    /// </summary>
    public class BeltItemGrabController : MonoBehaviour
    {
        private enum HeldItem { None, Detector, Scsr }

        [Header("References")]
        [SerializeField] private Camera trackedCamera;
        [SerializeField] private Transform beltSlot;
        [SerializeField] private Transform heldItemSlot;
        [SerializeField] private Transform detector;
        [SerializeField] private Transform scsr;
        [SerializeField] private Transform readingDisplay;
        [SerializeField] private MultiGasDetectorController detectorController;
        [SerializeField] private GasDetectorReadingOverlay readingOverlay;

        [Header("Detector Inspection Pose (Live Tunable)")]
        [SerializeField] private Vector3 heldLocalPosition = new Vector3(0f, -0.04f, 0.35f);
        [SerializeField] private Vector3 heldDetectorLocalEuler = new Vector3(-86.128f, 0f, 168.685f);
        [SerializeField] private Vector3 heldLocalScale = new Vector3(0.078f, 0.078f, 0.078f);

        [Header("SCSR Inspection Pose (Live Tunable)")]
        [SerializeField] private Vector3 heldScsrLocalPosition = new Vector3(0f, -0.05f, 0.35f);
        [SerializeField] private Vector3 heldScsrLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 heldScsrLocalScale = new Vector3(1.2f, 1.2f, 1.2f);

        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.35f;

        [Header("UI Fallback")]
        [SerializeField] private bool showOnScreenGuiButton = true;

        private HeldItem currentlyHeld = HeldItem.None;
        private Coroutine activeAnimCoroutine;

        /// <summary>True from the moment the worker first grabs the SCSR mask, for the rest of the
        /// drill — represents the mask being put on, not just currently held for inspection, so it
        /// stays true after the item is returned to the belt (unlike currentlyHeld).</summary>
        public bool HasEquippedScsr { get; private set; }

        // Cached original belt-local transforms so Release() restores exactly
        private Vector3 detectorBeltPos, detectorBeltEuler, detectorBeltScale;
        private Vector3 scsrBeltPos, scsrBeltEuler, scsrBeltScale;

        private void Awake()
        {
            if (trackedCamera == null) trackedCamera = Camera.main ?? FindFirstObjectByType<Camera>();

            if (detector != null)
            {
                detectorBeltPos = detector.localPosition;
                detectorBeltEuler = detector.localEulerAngles;
                detectorBeltScale = detector.localScale;
            }
            if (scsr != null)
            {
                scsrBeltPos = scsr.localPosition;
                scsrBeltEuler = scsr.localEulerAngles;
                scsrBeltScale = scsr.localScale;
            }
        }

        private void Start()
        {
            EnsureCollider(detector);
            EnsureCollider(scsr);

            if (heldItemSlot != null)
            {
                heldItemSlot.localPosition = Vector3.zero;
                heldItemSlot.localRotation = Quaternion.identity;
                heldItemSlot.localScale = Vector3.one;
            }

            if (readingDisplay == null && detector != null)
            {
                Transform rd = detector.Find("readingDisplay");
                if (rd != null) readingDisplay = rd;
            }
        }

        private void EnsureCollider(Transform t)
        {
            if (t == null) return;
            BoxCollider box = t.GetComponent<BoxCollider>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(3f, 3f, 3f);
            box.center = Vector3.zero;
            box.enabled = true;
            // This collider only exists so tap-to-grab (Physics.SphereCast/Raycast in Update()) can
            // register a hit on the belt item from anywhere on screen — it was never meant to be solid.
            // Left as a normal (non-trigger) collider, this 3x3x3 box follows the player at waist
            // height every frame (UpdateBeltFollow) and physically fights the player's own
            // CharacterController, wedging against it and blocking movement. Raycast/SphereCast still
            // hit trigger colliders by default, so marking it a trigger keeps grab detection working
            // while removing the unintended physical collision with the player who's carrying it.
            box.isTrigger = true;
        }

        private void Update()
        {
            // Live-tune held poses in Inspector while holding an item
            if (currentlyHeld == HeldItem.Detector && detector != null && activeAnimCoroutine == null)
            {
                detector.localPosition = heldLocalPosition;
                detector.localEulerAngles = heldDetectorLocalEuler;
                detector.localScale = heldLocalScale;
            }
            else if (currentlyHeld == HeldItem.Scsr && scsr != null && activeAnimCoroutine == null)
            {
                scsr.localPosition = heldScsrLocalPosition;
                scsr.localEulerAngles = heldScsrLocalEuler;
                scsr.localScale = heldScsrLocalScale;
            }

            if (!GetPointerDown(out Vector2 screenPos)) return;

            if (trackedCamera == null) trackedCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            if (trackedCamera == null) return;

            if (currentlyHeld != HeldItem.None)
            {
                // Tapping anywhere on screen while holding an item puts it back on the belt
                Debug.Log($"[BELT_GRAB] Tapped screen while holding {currentlyHeld} -> Returning to belt slot");
                Release();
                return;
            }

            // --- Method 1: 3D SphereCast & Raycast ---
            Ray ray = trackedCamera.ScreenPointToRay(screenPos);
            if (Physics.SphereCast(ray, 0.25f, out RaycastHit hit, 50f) || Physics.Raycast(ray, out hit, 50f))
            {
                Debug.Log($"[BELT_GRAB] Tap at {screenPos} -> Hit '{hit.transform.name}' (Root: '{hit.transform.root.name}')");

                if (IsTargetOrChild(hit.transform, detector) || IsTargetOrChild(hit.transform, readingDisplay))
                {
                    Grab(HeldItem.Detector);
                    return;
                }
                else if (IsTargetOrChild(hit.transform, scsr))
                {
                    Grab(HeldItem.Scsr);
                    return;
                }
            }

            // --- Method 2: Viewport Proximity Detection ---
            if (detector != null)
            {
                Vector3 detScreenPos = trackedCamera.WorldToScreenPoint(detector.position);
                if (detScreenPos.z > 0 && Vector2.Distance(screenPos, (Vector2)detScreenPos) < 250f)
                {
                    Grab(HeldItem.Detector);
                    return;
                }
            }
            if (scsr != null)
            {
                Vector3 scsrScreenPos = trackedCamera.WorldToScreenPoint(scsr.position);
                if (scsrScreenPos.z > 0 && Vector2.Distance(screenPos, (Vector2)scsrScreenPos) < 250f)
                {
                    Grab(HeldItem.Scsr);
                    return;
                }
            }

            // --- Method 3: Looking-Down Tilt Tap ---
            if (detectorController != null && detectorController.IsCheckingDetector)
            {
                // If tapping right side of screen -> grab detector; left side -> grab SCSR
                if (screenPos.x > Screen.width * 0.5f) Grab(HeldItem.Detector);
                else Grab(HeldItem.Scsr);
                return;
            }

            // --- Method 4: Tap Lower Screen Region ---
            if (screenPos.y < Screen.height * 0.4f)
            {
                if (screenPos.x > Screen.width * 0.5f) Grab(HeldItem.Detector);
                else Grab(HeldItem.Scsr);
                return;
            }
        }

        private bool IsTargetOrChild(Transform hitTransform, Transform target)
        {
            if (target == null || hitTransform == null) return false;
            return hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform);
        }

        private bool GetPointerDown(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

            // The legacy UnityEngine.Input reads (Input.GetMouseButtonDown / Input.GetTouch) used to
            // sit here first. This project has Active Input Handling set to the new Input System
            // package only (not "Both"), so those legacy calls throw InvalidOperationException on
            // every single Update() — before ever reaching the new-Input-System checks below, which
            // are the ones that actually work here. That silently broke tap-to-grab entirely (both
            // the detector and the mask), confirmed live via the Console spamming that exception
            // every frame while grab taps did nothing.
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                screenPosition = Pointer.current.position.ReadValue();
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (!showOnScreenGuiButton) return;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fontSize = 16;
            btnStyle.fontStyle = FontStyle.Bold;
            btnStyle.normal.textColor = Color.yellow;

            if (currentlyHeld == HeldItem.None)
            {
                if (GUI.Button(new Rect(Screen.width - 240, Screen.height - 90, 220, 40), "🔍 Inspect Gas Detector", btnStyle))
                {
                    Grab(HeldItem.Detector);
                }
                if (GUI.Button(new Rect(Screen.width - 240, Screen.height - 45, 220, 40), "🎒 Inspect SCSR Pouch", btnStyle))
                {
                    Grab(HeldItem.Scsr);
                }
            }
            else
            {
                if (GUI.Button(new Rect(Screen.width - 240, Screen.height - 90, 220, 60), "↩ Put Back on Belt", btnStyle))
                {
                    Release();
                }
            }
        }

        public void GrabDetectorExternal() => Grab(HeldItem.Detector);
        public void GrabScsrExternal() => Grab(HeldItem.Scsr);
        public void ReleaseExternal() => Release();

        private void Grab(HeldItem item)
        {
            currentlyHeld = item;
            if (activeAnimCoroutine != null) StopCoroutine(activeAnimCoroutine);

            if (item == HeldItem.Detector && detector != null)
            {
                activeAnimCoroutine = StartCoroutine(AnimateReparent(detector, heldItemSlot, heldLocalPosition, heldDetectorLocalEuler, heldLocalScale, animationDuration));
                if (detectorController != null) detectorController.SetHeld(true);
                if (readingDisplay != null) readingDisplay.gameObject.SetActive(true);
                if (readingOverlay != null) readingOverlay.ShowNextReading(animationDuration);
                Debug.Log($"[BELT_GRAB] Grabbed MultiGasDetector -> pos={heldLocalPosition}, rot={heldDetectorLocalEuler}");
            }
            else if (item == HeldItem.Scsr && scsr != null)
            {
                activeAnimCoroutine = StartCoroutine(AnimateReparent(scsr, heldItemSlot, heldScsrLocalPosition, heldScsrLocalEuler, heldScsrLocalScale, animationDuration));
                HasEquippedScsr = true;
                Debug.Log($"[BELT_GRAB] Grabbed SCSR -> pos={heldScsrLocalPosition}, rot={heldScsrLocalEuler}");
            }
        }

        private void Release()
        {
            if (activeAnimCoroutine != null) StopCoroutine(activeAnimCoroutine);

            if (currentlyHeld == HeldItem.Detector && detector != null)
            {
                activeAnimCoroutine = StartCoroutine(AnimateReparent(detector, beltSlot, detectorBeltPos, detectorBeltEuler, detectorBeltScale, animationDuration));
                if (detectorController != null) detectorController.SetHeld(false);
                if (readingOverlay != null) readingOverlay.Hide();
                Debug.Log("[BELT_GRAB] Returned multi-gas detector to belt");
            }
            else if (currentlyHeld == HeldItem.Scsr && scsr != null)
            {
                activeAnimCoroutine = StartCoroutine(AnimateReparent(scsr, beltSlot, scsrBeltPos, scsrBeltEuler, scsrBeltScale, animationDuration));
                Debug.Log("[BELT_GRAB] Returned SCSR to belt");
            }

            currentlyHeld = HeldItem.None;
        }

        private IEnumerator AnimateReparent(Transform t, Transform newParent, Vector3 targetLocalPos, Vector3 targetLocalEuler, Vector3 targetScale, float duration)
        {
            if (t == null || newParent == null)
            {
                activeAnimCoroutine = null;
                yield break;
            }

            t.SetParent(newParent, true);

            Vector3 startLocalPos = t.localPosition;
            Quaternion startLocalRot = t.localRotation;
            Vector3 startScale = t.localScale;

            Quaternion targetLocalRot = Quaternion.Euler(targetLocalEuler);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                t.localPosition = Vector3.Lerp(startLocalPos, targetLocalPos, progress);
                t.localRotation = Quaternion.Slerp(startLocalRot, targetLocalRot, progress);
                t.localScale = Vector3.Lerp(startScale, targetScale, progress);
                yield return null;
            }

            t.localPosition = targetLocalPos;
            t.localEulerAngles = targetLocalEuler;
            t.localScale = targetScale;

            activeAnimCoroutine = null;
        }
    }
}
