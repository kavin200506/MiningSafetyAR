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
        [SerializeField] private Vector3 heldScsrLocalPosition = new Vector3(0f, -0.10f, 0.52f);
        [SerializeField] private Vector3 heldScsrLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 heldScsrLocalScale = new Vector3(0.85f, 0.85f, 0.85f);

        [Header("Animation Settings")]
        [SerializeField] private float animationDuration = 0.35f;

        [Header("Mask Don Gesture (Drag Up To Face)")]
        [Tooltip("Net upward screen-space drag distance (pixels) required before releasing counts as actually putting the SCSR mask on, rather than an accidental tap/slip. Same 'real drag vs accidental tap' idea as the fire module's pin-pull gesture.")]
        [SerializeField] private float maskDonDragMinUpwardPixels = 150f;
        [Tooltip("How high (world units) the mask visibly lifts off the belt while dragging, as live drag-progress feedback. The full fly-to-face animation only plays on a successful release (reuses Grab()) — this is just the mid-drag cue.")]
        [SerializeField] private float maskDonDragLiftHeight = 0.15f;

        [Header("UI Fallback")]
        [SerializeField] private bool showOnScreenGuiButton = true;

        private HeldItem currentlyHeld = HeldItem.None;
        private Coroutine activeAnimCoroutine;
        private bool isMaskDonDragActive = false;
        private Vector2 maskDonDragStartScreenPos;
        private Vector2 maskDonDragLastScreenPos;
        private Vector3 maskDonDragRestWorldPos;

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
            EnsureCollidersOnHierarchy(detector);
            EnsureCollidersOnHierarchy(scsr);

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

        private void EnsureCollidersOnHierarchy(Transform root)
        {
            if (root == null) return;

            // Remove any failed/invalid MeshColliders on complex sub-meshes
            MeshCollider[] oldMeshColliders = root.GetComponentsInChildren<MeshCollider>(true);
            foreach (var mc in oldMeshColliders)
            {
                Destroy(mc);
            }

            // Create a guaranteed, clean BoxCollider on root
            BoxCollider box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.gameObject.AddComponent<BoxCollider>();

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds localBounds = new Bounds();
                bool init = false;

                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    Bounds b = r.bounds;
                    Vector3 min = b.min;
                    Vector3 max = b.max;
                    Vector3[] corners = new Vector3[8]
                    {
                        new Vector3(min.x, min.y, min.z),
                        new Vector3(min.x, min.y, max.z),
                        new Vector3(min.x, max.y, min.z),
                        new Vector3(min.x, max.y, max.z),
                        new Vector3(max.x, min.y, min.z),
                        new Vector3(max.x, min.y, max.z),
                        new Vector3(max.x, max.y, min.z),
                        new Vector3(max.x, max.y, max.z),
                    };

                    foreach (Vector3 worldC in corners)
                    {
                        Vector3 localC = root.InverseTransformPoint(worldC);
                        if (!init)
                        {
                            localBounds = new Bounds(localC, Vector3.zero);
                            init = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localC);
                        }
                    }
                }

                if (init)
                {
                    Vector3 lossy = root.lossyScale;
                    float minWorldSize = 0.35f;
                    float szX = lossy.x > 0.0001f ? Mathf.Max(localBounds.size.x, minWorldSize / lossy.x) : 0.35f;
                    float szY = lossy.y > 0.0001f ? Mathf.Max(localBounds.size.y, minWorldSize / lossy.y) : 0.35f;
                    float szZ = lossy.z > 0.0001f ? Mathf.Max(localBounds.size.z, minWorldSize / lossy.z) : 0.35f;

                    box.center = localBounds.center;
                    box.size = new Vector3(szX, szY, szZ) * 1.3f; // 30% padding for easy tap targeting
                }
            }
            else
            {
                box.center = Vector3.zero;
                box.size = new Vector3(3f, 3f, 3f);
            }

            box.isTrigger = true;
            box.enabled = true;
        }

        private Vector3 GetItemWorldCenter(Transform t)
        {
            if (t == null) return Vector3.zero;
            Renderer r = t.GetComponentInChildren<Renderer>();
            return r != null ? r.bounds.center : t.position;
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

            // Mask-don drag gesture in progress takes over input entirely until it resolves
            // (success -> Grab(); short of the threshold -> snaps back to the belt).
            if (isMaskDonDragActive)
            {
                HandleMaskDonDrag();
                return;
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

            // --- Method 1: Precise 3D Physics Raycast & SphereCast ---
            Ray ray = trackedCamera.ScreenPointToRay(screenPos);
            
            // Check direct raycast hits first
            RaycastHit[] rayHits = Physics.RaycastAll(ray, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float detHitDist = float.MaxValue;
            float scsrHitDist = float.MaxValue;

            ProcessHits(rayHits, ref detHitDist, ref scsrHitDist);

            if (detHitDist < float.MaxValue || scsrHitDist < float.MaxValue)
            {
                if (scsrHitDist < detHitDist)
                {
                    Debug.Log($"[BELT_GRAB] Direct Raycast hit SCSR Mask (dist={scsrHitDist:F2}m)");
                    TryGrabOrBeginMaskDon(screenPos);
                    return;
                }
                else
                {
                    Debug.Log($"[BELT_GRAB] Direct Raycast hit MultiGasDetector (dist={detHitDist:F2}m)");
                    Grab(HeldItem.Detector);
                    return;
                }
            }

            // Check spherecast beam (15cm beam width) for near-miss taps near item edges
            RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.15f, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            ProcessHits(sphereHits, ref detHitDist, ref scsrHitDist);

            if (detHitDist < float.MaxValue || scsrHitDist < float.MaxValue)
            {
                if (scsrHitDist < detHitDist)
                {
                    Debug.Log($"[BELT_GRAB] SphereCast hit SCSR Mask (dist={scsrHitDist:F2}m)");
                    TryGrabOrBeginMaskDon(screenPos);
                    return;
                }
                else
                {
                    Debug.Log($"[BELT_GRAB] SphereCast hit MultiGasDetector (dist={detHitDist:F2}m)");
                    Grab(HeldItem.Detector);
                    return;
                }
            }

            // --- Method 2: Viewport Distance Proximity Fallback ---
            float detScreenDist = float.MaxValue;
            float scsrScreenDist = float.MaxValue;

            if (detector != null)
            {
                Vector3 detScreenPos = trackedCamera.WorldToScreenPoint(GetItemWorldCenter(detector));
                if (detScreenPos.z > 0) detScreenDist = Vector2.Distance(screenPos, (Vector2)detScreenPos);
            }
            if (scsr != null)
            {
                Vector3 scsrScreenPos = trackedCamera.WorldToScreenPoint(GetItemWorldCenter(scsr));
                if (scsrScreenPos.z > 0) scsrScreenDist = Vector2.Distance(screenPos, (Vector2)scsrScreenPos);
            }

            float maxScreenRadius = 300f; // 300 pixels search radius on screen
            if (detScreenDist < maxScreenRadius || scsrScreenDist < maxScreenRadius)
            {
                if (scsrScreenDist < detScreenDist)
                {
                    Debug.Log($"[BELT_GRAB] Viewport proximity hit SCSR Mask (dist={scsrScreenDist:F1}px vs det={detScreenDist:F1}px)");
                    TryGrabOrBeginMaskDon(screenPos);
                    return;
                }
                else
                {
                    Debug.Log($"[BELT_GRAB] Viewport proximity hit MultiGasDetector (dist={detScreenDist:F1}px vs scsr={scsrScreenDist:F1}px)");
                    Grab(HeldItem.Detector);
                    return;
                }
            }
        }

        private void ProcessHits(RaycastHit[] hits, ref float detHitDist, ref float scsrHitDist)
        {
            if (hits == null) return;
            foreach (var hit in hits)
            {
                if (IsTargetOrChild(hit.transform, detector) || IsTargetOrChild(hit.transform, readingDisplay))
                {
                    if (hit.distance < detHitDist) detHitDist = hit.distance;
                }
                else if (IsTargetOrChild(hit.transform, scsr))
                {
                    if (hit.distance < scsrHitDist) scsrHitDist = hit.distance;
                }
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

        /// <summary>
        /// First-time SCSR donning requires a deliberate drag-upward gesture, not a tap — real-world
        /// relevance: putting a mask on your face isn't a single tap, same reasoning as the fire
        /// module's pin-pull drag. Once already equipped, picking it back up (e.g. to re-inspect)
        /// is a normal instant grab again, same as the detector.
        /// </summary>
        private void TryGrabOrBeginMaskDon(Vector2 screenPos)
        {
            if (!HasEquippedScsr)
            {
                BeginMaskDonDrag(screenPos);
            }
            else
            {
                Grab(HeldItem.Scsr);
            }
        }

        /// <summary>Ongoing press state (position + isPressed), as opposed to GetPointerDown()
        /// which only reports the frame a press began.</summary>
        private bool TryReadPointerState(out Vector2 pos, out bool active)
        {
            pos = Vector2.zero;
            active = false;

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pos = Touchscreen.current.primaryTouch.position.ReadValue();
                active = true;
                return true;
            }
            if (Pointer.current != null && Pointer.current.press.isPressed)
            {
                pos = Pointer.current.position.ReadValue();
                active = true;
                return true;
            }
            return false;
        }

        private void BeginMaskDonDrag(Vector2 screenPos)
        {
            if (scsr == null) return;
            isMaskDonDragActive = true;
            maskDonDragStartScreenPos = screenPos;
            maskDonDragLastScreenPos = screenPos;
            maskDonDragRestWorldPos = scsr.position;
            Debug.Log("[BELT_GRAB] Mask don drag started — drag upward toward your face to put it on.");
        }

        private void HandleMaskDonDrag()
        {
            // Once the finger lifts, TryReadPointerState reports isPressed=false with no usable
            // position — so the release point has to be whatever we last saw WHILE still pressed,
            // tracked into maskDonDragLastScreenPos below, not read fresh here.
            if (!TryReadPointerState(out Vector2 currentPos, out bool active) || !active)
            {
                EndMaskDonDrag(maskDonDragLastScreenPos);
                return;
            }

            maskDonDragLastScreenPos = currentPos;
            float netUpward = currentPos.y - maskDonDragStartScreenPos.y;
            float progress = Mathf.Clamp01(netUpward / maskDonDragMinUpwardPixels);

            // Live feedback only — a small lift off the belt as the player drags. The full fly-to-
            // face animation (AnimateReparent, in Grab()) only plays on a successful release, so we
            // don't need to solve cross-parent-space interpolation here.
            if (scsr != null)
            {
                scsr.position = maskDonDragRestWorldPos + Vector3.up * (maskDonDragLiftHeight * progress);
            }
        }

        private void EndMaskDonDrag(Vector2 releaseScreenPos)
        {
            isMaskDonDragActive = false;
            float netUpward = releaseScreenPos.y - maskDonDragStartScreenPos.y;

            if (netUpward >= maskDonDragMinUpwardPixels)
            {
                Debug.Log($"[BELT_GRAB] Mask don drag succeeded ({netUpward:F0}px >= {maskDonDragMinUpwardPixels}px) -> putting SCSR on.");
                Grab(HeldItem.Scsr);
            }
            else
            {
                Debug.Log($"[BELT_GRAB] Mask don drag too short ({netUpward:F0}px < {maskDonDragMinUpwardPixels}px) -> snapping back to belt.");
                if (scsr != null) scsr.position = maskDonDragRestWorldPos;
            }
        }

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
