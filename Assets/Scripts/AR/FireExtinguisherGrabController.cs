using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using MiningSafetyAR.Modules;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Implements a first-person "Grab & Carry" mechanic for the 3D Fire Extinguisher model.
    /// Incorporates touch tap detection, distance-gated raycasting (maxGrabDistance = 2.0m),
    /// smooth easing lerp to camera heldItemSlot, layer transition to HeldItem, and arrival detection at fire hazard.
    /// 
    /// Positioning Guidance for heldItemSlot transform:
    /// - Recommended starting localPosition relative to AR Camera: (0.18f, -0.22f, 0.35f)
    /// - Recommended starting localRotation: Quaternion.Euler(10f, -15f, 0f)
    /// This provides a natural-looking partially-visible held object in the bottom-right view of the screen.
    /// Fine-tune visually in the Inspector on-device.
    /// </summary>
    public class FireExtinguisherGrabController : MonoBehaviour
    {
        public static FireExtinguisherGrabController Instance { get; private set; }

        public enum GrabState
        {
            Unbound,
            Grabbing,
            Held,
            ArrivedAtFire
        }

        [Header("Grab & Carry State")]
        [SerializeField] private GrabState currentState = GrabState.Unbound;
        public GrabState CurrentState => currentState;
        public bool IsGrabbed => currentState == GrabState.Held || currentState == GrabState.ArrivedAtFire;

        [Header("Held Item Slot Configuration")]
        [Tooltip("Transform under the AR Camera where the held fire extinguisher will parent to when grabbed.")]
        [SerializeField] private Transform heldItemSlot;
        public Transform HeldItemSlot
        {
            get => heldItemSlot;
            set => heldItemSlot = value;
        }

        [Header("Layer Masks")]
        [SerializeField] private LayerMask grabbableLayerMask;
        [SerializeField] private LayerMask heldItemLayerMask;

        [Header("Grab Physics & Distance Settings")]
        [SerializeField] private float grabDuration = 0.4f;
        [SerializeField] private float maxGrabDistance = 6.0f;
        [SerializeField] private float maxRaycastDistance = 15.0f;
        [SerializeField] private float arrivalDistanceMeters = 1.2f;

        public float MaxGrabDistance { get => maxGrabDistance; set => maxGrabDistance = value; }
        public float MaxRaycastDistance { get => maxRaycastDistance; set => maxRaycastDistance = value; }

        private GameObject targetExtinguisher;
        private Coroutine grabCoroutine;
        private InputAction pressAction;
        private Ray lastRay;

        public GameObject TargetExtinguisher => targetExtinguisher;

        public event Action<GameObject> OnExtinguisherGrabbed;
        public event Action<Vector3> OnArrivedAtFireHazard;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Setup New Input System Pointer Press Action
            pressAction = new InputAction("grab_touch", binding: "<Pointer>/press");
            pressAction.started += OnPointerPressBegan;
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (pressAction != null)
            {
                pressAction.Enable();
            }

            // Subscribe to model loader and step counter discovery events
            FireExtinguisherModelLoader.OnModelLoaded += OnExtinguisherLoaded;
            if (ARStepCounterTracker.Instance != null)
            {
                ARStepCounterTracker.Instance.OnExtinguisherDiscovered += OnExtinguisherDiscoveredBySteps;
            }
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            if (pressAction != null)
            {
                pressAction.Disable();
            }

            FireExtinguisherModelLoader.OnModelLoaded -= OnExtinguisherLoaded;
            if (ARStepCounterTracker.Instance != null)
            {
                ARStepCounterTracker.Instance.OnExtinguisherDiscovered -= OnExtinguisherDiscoveredBySteps;
            }
        }

        private void OnDestroy()
        {
            if (pressAction != null)
            {
                pressAction.Dispose();
            }
        }

        private void Start()
        {
            EnsureHeldItemSlotExists();
            FindAndSetupExtinguisherInScene();
        }

        /// <summary>
        /// Ensures a default HeldItemSlot transform is attached to the AR Camera if none is assigned in the Inspector.
        /// </summary>
        public void EnsureHeldItemSlotExists()
        {
            if (heldItemSlot != null) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                Transform existingSlot = mainCam.transform.Find("HeldItemSlot");
                if (existingSlot != null)
                {
                    heldItemSlot = existingSlot;
                }
                else
                {
                    GameObject slotGO = new GameObject("HeldItemSlot");
                    slotGO.transform.SetParent(mainCam.transform, false);
                    slotGO.transform.localPosition = new Vector3(0.18f, -0.22f, 0.35f);
                    slotGO.transform.localRotation = Quaternion.Euler(10f, -15f, 0f);
                    heldItemSlot = slotGO.transform;
                    Debug.Log($"DIAG FireExtinguisherGrabController Auto-created HeldItemSlot under AR Camera at localPos={slotGO.transform.localPosition}");
                }
            }
        }

        /// <summary>
        /// Registers a 3D Fire Extinguisher GameObject for grabbing, adding generous colliders and setting its Grabbable layer.
        /// </summary>
        public void SetupExtinguisherForGrabbing(GameObject extinguisherGO)
        {
            if (extinguisherGO == null) return;

            targetExtinguisher = extinguisherGO;

            // Ensure generous BoxCollider exists on root targetExtinguisher for easy AR tapping
            BoxCollider box = targetExtinguisher.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = targetExtinguisher.AddComponent<BoxCollider>();
            }
            box.center = new Vector3(0, 0.3f, 0);
            box.size = new Vector3(0.8f, 1.2f, 0.8f);
            box.enabled = true;

            // Ensure all child colliders are also enabled
            Collider[] colliders = targetExtinguisher.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                c.enabled = true;
            }

            // Assign Grabbable layer
            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            if (grabbableLayer == -1) grabbableLayer = 6;
            SetLayerRecursively(targetExtinguisher, grabbableLayer);

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            float distToCam = (mainCam != null && targetExtinguisher != null) ? Vector3.Distance(mainCam.transform.position, targetExtinguisher.transform.position) : -1f;

            Debug.Log($"[SPAWN_DIAG] Target Extinguisher Registered: '{targetExtinguisher.name}' | WorldPos={targetExtinguisher.transform.position} | DistToCam={distToCam:F2}m | Layer='{LayerMask.LayerToName(targetExtinguisher.layer)}' ({targetExtinguisher.layer}) | BoxSize={box.size} | ChildColliders={colliders.Length}");
        }

        private void OnExtinguisherLoaded(GameObject extinguisherGO)
        {
            Debug.Log($"INFO FireExtinguisherGrabController Event: Model Loaded for '{extinguisherGO.name}' — setting up for grabbing.");
            SetupExtinguisherForGrabbing(extinguisherGO);
        }

        private void OnExtinguisherDiscoveredBySteps(Vector3 spawnPos)
        {
            if (ARStepCounterTracker.Instance != null && ARStepCounterTracker.Instance.SpawnedExtinguisherInstance != null)
            {
                Debug.Log($"INFO FireExtinguisherGrabController Event: Extinguisher Discovered via steps at {spawnPos} — setting up for grabbing.");
                SetupExtinguisherForGrabbing(ARStepCounterTracker.Instance.SpawnedExtinguisherInstance);
            }
        }

        private void FindAndSetupExtinguisherInScene()
        {
            if (targetExtinguisher != null) return;

            GameObject ext = GameObject.Find("Discovered_3D_FireExtinguisher") 
                ?? GameObject.Find("Real_3D_FireExtinguisher_GLTF")
                ?? GameObject.Find("FireExtinguisherModel");

            if (ext == null)
            {
                FireExtinguisherModelLoader loader = FindFirstObjectByType<FireExtinguisherModelLoader>();
                if (loader != null) ext = loader.gameObject;
            }

            if (ext != null)
            {
                SetupExtinguisherForGrabbing(ext);
            }
        }

        private void Update()
        {
            if (targetExtinguisher == null)
            {
                FindAndSetupExtinguisherInScene();
            }

            CheckTouchInput();
            CheckArrivalAtFireHazard();

#if UNITY_EDITOR
            // Unity Editor Play Mode Keyboard Simulation:
            // [G] -> Simulate Grabbing the 3D Fire Extinguisher
            // [Space] -> Move Camera / Player closer to Fire Hazard to simulate arrival
            if (Keyboard.current != null)
            {
                if (Keyboard.current.gKey.wasPressedThisFrame)
                {
                    Debug.Log("INFO FireExtinguisherGrabController [EDITOR_HOTKEY] 'G' key pressed — Simulating Grab on 3D Fire Extinguisher!");
                    SimulateGrab();
                }
                if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrabbed)
                {
                    Debug.Log("INFO FireExtinguisherGrabController [EDITOR_HOTKEY] 'Space' key pressed — Simulating arrival at Fire Hazard!");
                    SimulateArrivalAtFire();
                }
            }
#endif
        }

        /// <summary>
        /// Robust input handling combining EnhancedTouch, New Input System Pointer, Legacy Touch/Mouse, and Screen-Center fallback.
        /// </summary>
        private void CheckTouchInput()
        {
            if (IsGrabbed) return;

            Vector2 tapPosition = Vector2.zero;
            bool tapDetected = false;
            string tapSource = "None";

            // 1. EnhancedTouch (Mobile Touchscreen Taps)
            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.screenPosition;
                    tapDetected = true;
                    tapSource = "EnhancedTouch";
                }
            }

            // 2. New Input System Pointer Press
            if (!tapDetected && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                tapPosition = Pointer.current.position.ReadValue();
                tapDetected = true;
                tapSource = "InputSystemPointer";
            }

            // 3. Legacy Input Fallbacks
            if (!tapDetected && Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                tapPosition = Input.GetTouch(0).position;
                tapDetected = true;
                tapSource = "LegacyInputTouch";
            }
            if (!tapDetected && Input.GetMouseButtonDown(0))
            {
                tapPosition = Input.mousePosition;
                tapDetected = true;
                tapSource = "LegacyInputMouse";
            }

            if (tapDetected)
            {
                bool grabbed = TryGrabExtinguisherAtScreenPoint(tapPosition, tapSource);
                if (!grabbed)
                {
                    Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    TryGrabExtinguisherAtScreenPoint(screenCenter, tapSource + "_ScreenCenterFallback");
                }
            }
        }

        private void OnPointerPressBegan(InputAction.CallbackContext context)
        {
            if (IsGrabbed) return;

            if (context.control.device is Pointer pointerDevice)
            {
                Vector2 tapPos = pointerDevice.position.ReadValue();
                TryGrabExtinguisherAtScreenPoint(tapPos, "PointerPressEvent");
            }
        }

        /// <summary>
        /// Physics Raycast targeting Grabbable layer to detect direct tap on the Fire Extinguisher collider with distance gate and full diagnostics.
        /// </summary>
        public bool TryGrabExtinguisherAtScreenPoint(Vector2 screenPoint, string source = "DirectTap")
        {
            // Guard against double-grab
            if (IsGrabbed || currentState != GrabState.Unbound)
            {
                Debug.Log("[GRAB_VERDICT] REJECTED_ALREADY_HELD: Tap ignored because extinguisher is already held.");
                return false;
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null)
            {
                Debug.LogWarning("[WARN] FireExtinguisherGrabController Cannot raycast — Camera.main is NULL!");
                return false;
            }

            Ray ray = mainCam.ScreenPointToRay(screenPoint);
            lastRay = ray;

            float currentExtDist = (targetExtinguisher != null) ? Vector3.Distance(mainCam.transform.position, targetExtinguisher.transform.position) : -1f;

            Debug.Log($"[TAP_DIAG] Source='{source}' | TapPos=({screenPoint.x:F0}, {screenPoint.y:F0}) | ScreenRes=({Screen.width}x{Screen.height}) | CamPos={mainCam.transform.position} | TargetExtDist={currentExtDist:F2}m | RayOrigin={ray.origin} | RayDir={ray.direction}");

            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            int layerMask = grabbableLayer != -1 ? (1 << grabbableLayer) : grabbableLayerMask.value;
            if (layerMask == 0) layerMask = ~0; // Fallback to all layers if mask unassigned

            // 1. Primary Raycast against Grabbable layer
            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, layerMask))
            {
                GameObject hitGO = hit.collider.gameObject;
                bool isTargetMatch = (targetExtinguisher == null || hitGO == targetExtinguisher || hitGO.transform.IsChildOf(targetExtinguisher.transform));

                if (isTargetMatch)
                {
                    if (hit.distance > maxGrabDistance)
                    {
                        Debug.LogWarning($"[GRAB_VERDICT] REJECTED_DISTANCE: Extinguisher hit at {hit.distance:F2}m, which exceeds maxGrabDistance {maxGrabDistance:F2}m. Move closer to grab.");
                        return false;
                    }

                    if (targetExtinguisher == null)
                    {
                        targetExtinguisher = hit.collider.transform.root.gameObject;
                    }

                    Debug.Log($"[GRAB_VERDICT] SUCCESS: Direct Grabbable Raycast hit '{hitGO.name}' at distance {hit.distance:F2}m (<= {maxGrabDistance:F2}m threshold). Starting GrabSequence!");
                    InitiateGrabSequence();
                    return true;
                }
            }

            // 2. All-layer Fallback Raycast in case child glTF submeshes are on Default layer
            RaycastHit[] allHits = Physics.RaycastAll(ray, maxRaycastDistance);
            Debug.Log($"[RAY_DIAG] RaycastAll executed for Ray ({screenPoint}) | Intersected Colliders Count = {allHits.Length} | TargetExtinguisher = {(targetExtinguisher != null ? targetExtinguisher.name : "NULL")}");

            for (int i = 0; i < allHits.Length; i++)
            {
                var h = allHits[i];
                GameObject hitGO = h.collider.gameObject;
                bool isExtMatch = (targetExtinguisher != null && (hitGO == targetExtinguisher || hitGO.transform.IsChildOf(targetExtinguisher.transform)))
                               || (hitGO.name.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0)
                               || (hitGO.transform.root.name.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0);

                Debug.Log($"[RAY_DIAG]   -> Hit #{i+1}: '{hitGO.name}' (Layer='{LayerMask.LayerToName(hitGO.layer)}'/{hitGO.layer}) | HitDist={h.distance:F2}m | HitPoint={h.point} | IsExtinguisherMatch={isExtMatch}");

                if (isExtMatch)
                {
                    if (h.distance > maxGrabDistance)
                    {
                        Debug.LogWarning($"[GRAB_VERDICT] REJECTED_DISTANCE: All-Layer Fallback hit '{hitGO.name}' at {h.distance:F2}m, exceeding maxGrabDistance {maxGrabDistance:F2}m.");
                        return false;
                    }

                    if (targetExtinguisher == null)
                    {
                        targetExtinguisher = h.collider.transform.root.gameObject;
                    }

                    Debug.Log($"[GRAB_VERDICT] SUCCESS: All-Layer Fallback hit '{hitGO.name}' at distance {h.distance:F2}m (<= {maxGrabDistance:F2}m threshold). Starting GrabSequence!");
                    InitiateGrabSequence();
                    return true;
                }
            }

            Debug.LogWarning($"[GRAB_VERDICT] REJECTED_MISSED: Raycast missed extinguisher. TapPos=({screenPoint.x:F0},{screenPoint.y:F0}), TargetExtDist={currentExtDist:F2}m, TotalCollidersHit={allHits.Length}.");
            return false;
        }

        /// <summary>
        /// Initiates the grab sequence coroutine.
        /// </summary>
        public void InitiateGrabSequence()
        {
            if (IsGrabbed || currentState != GrabState.Unbound)
            {
                Debug.Log("INFO FireExtinguisherGrabController Grab tap ignored — extinguisher already held.");
                return;
            }

            if (targetExtinguisher == null)
            {
                FindAndSetupExtinguisherInScene();
            }

            if (targetExtinguisher == null)
            {
                Debug.LogWarning("WARN FireExtinguisherGrabController Cannot initiate grab — Target Fire Extinguisher GameObject is NULL!");
                return;
            }

            if (grabCoroutine != null)
            {
                StopCoroutine(grabCoroutine);
            }
            grabCoroutine = StartCoroutine(GrabSequence());
        }

        /// <summary>
        /// Coroutine that Lerps position and Slerps rotation over ~0.4s to heldItemSlot using an ease-out curve,
        /// parents to heldItemSlot, disables ARAnchor/colliders, and assigns HeldItem layer.
        /// </summary>
        private IEnumerator GrabSequence()
        {
            currentState = GrabState.Grabbing;
            Debug.Log("INFO FireExtinguisherGrabController Grab sequence started, lerping to heldItemSlot over 0.4s.");

            EnsureHeldItemSlotExists();

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 startPos = targetExtinguisher.transform.position;
            Quaternion startRot = targetExtinguisher.transform.rotation;

            float elapsedTime = 0f;

            while (elapsedTime < grabDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / grabDuration);
                // Ease-out curve
                float easeOutT = Mathf.Sin(t * Mathf.PI * 0.5f);

                Vector3 targetPos = heldItemSlot != null ? heldItemSlot.position : mainCam.transform.TransformPoint(0.18f, -0.22f, 0.35f);
                Quaternion targetRot = heldItemSlot != null ? heldItemSlot.rotation : mainCam.transform.rotation * Quaternion.Euler(10f, -15f, 0f);

                targetExtinguisher.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                targetExtinguisher.transform.rotation = Quaternion.Slerp(startRot, targetRot, easeOutT);
                yield return null;
            }

            // 1. Parent to heldItemSlot
            if (heldItemSlot != null)
            {
                targetExtinguisher.transform.SetParent(heldItemSlot, true);
                targetExtinguisher.transform.localPosition = Vector3.zero;
                targetExtinguisher.transform.localRotation = Quaternion.identity;
                targetExtinguisher.transform.localScale = Vector3.one;
            }

            // 2. Disable ARAnchor if present
            ARAnchor anchor = targetExtinguisher.GetComponent<ARAnchor>() ?? targetExtinguisher.GetComponentInParent<ARAnchor>();
            if (anchor != null)
            {
                Destroy(anchor);
                Debug.Log("DIAG FireExtinguisherGrabController Detached ARAnchor from grabbed extinguisher.");
            }

            // 3. Disable Colliders
            Collider[] colliders = targetExtinguisher.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            // 4. Change layer recursively to HeldItem layer
            int heldItemLayer = LayerMask.NameToLayer("HeldItem");
            if (heldItemLayer == -1) heldItemLayer = 7;
            SetLayerRecursively(targetExtinguisher, heldItemLayer);

            currentState = GrabState.Held;
            Debug.Log("INFO FireExtinguisherGrabController Grab sequence complete — extinguisher parented to camera, isGrabbed=true.");
            
            OnExtinguisherGrabbed?.Invoke(targetExtinguisher);
            grabCoroutine = null;
        }

        /// <summary>
        /// Continuously checks X-Z horizontal distance between AR Camera and active Fire Hazard.
        /// Fires OnArrivedAtFireHazard when distance <= arrivalDistanceMeters (1.2m).
        /// </summary>
        private void CheckArrivalAtFireHazard()
        {
            if (currentState != GrabState.Held) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return;

            // Locate active ground fire hazard
            GameObject fireHazard = ARPlacementManager.Instance != null ? ARPlacementManager.Instance.SpawnedObject : null;
            if (fireHazard == null)
            {
                GroundFireController fireCtrl = FindFirstObjectByType<GroundFireController>();
                if (fireCtrl != null) fireHazard = fireCtrl.gameObject;
            }

            if (fireHazard == null) return;

            Vector3 camPos = mainCam.transform.position;
            Vector3 firePos = fireHazard.transform.position;

            // X-Z horizontal distance calculation
            Vector3 hDisplacement = new Vector3(camPos.x - firePos.x, 0f, camPos.z - firePos.z);
            float distanceMeters = hDisplacement.magnitude;

            if (distanceMeters <= arrivalDistanceMeters)
            {
                currentState = GrabState.ArrivedAtFire;
                Debug.Log($"DIAG FireExtinguisherGrabController ARRIVED AT FIRE HAZARD! Horizontal Distance={distanceMeters:F2}m <= {arrivalDistanceMeters}m threshold.");
                OnArrivedAtFireHazard?.Invoke(firePos);
            }
        }

        public void SimulateGrab()
        {
            if (currentState == GrabState.Unbound)
            {
                InitiateGrabSequence();
            }
            else
            {
                Debug.Log("INFO FireExtinguisherGrabController Grab tap ignored — extinguisher already held.");
            }
        }

        public void SimulateArrivalAtFire()
        {
            if (currentState == GrabState.Held)
            {
                currentState = GrabState.ArrivedAtFire;
                Vector3 firePos = ARPlacementManager.Instance != null && ARPlacementManager.Instance.SpawnedObject != null
                    ? ARPlacementManager.Instance.SpawnedObject.transform.position
                    : Vector3.zero;
                Debug.Log($"INFO FireExtinguisherGrabController [EDITOR_SIM] Simulating Arrival at Fire Hazard at {firePos}");
                OnArrivedAtFireHazard?.Invoke(firePos);
            }
        }

        private static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        /// <summary>
        /// Scene View Gizmos showing maxGrabDistance wire sphere around camera and last raycast line.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(mainCam.transform.position, maxGrabDistance);

                if (lastRay.direction != Vector3.zero)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(lastRay.origin, lastRay.origin + lastRay.direction * maxGrabDistance);
                }
            }
        }

        private void OnGUI()
        {
            if (currentState == GrabState.Unbound && targetExtinguisher == null) return;

            float screenWidth = Screen.width;
            float boxWidth = 640f;
            float boxHeight = 130f;
            float margin = 30f;
            Rect rect = new Rect((screenWidth - boxWidth) / 2f, margin + 290f, boxWidth, boxHeight);

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            if (currentState == GrabState.Unbound && targetExtinguisher != null)
            {
                style.normal.textColor = new Color(1.0f, 0.85f, 0.0f); // Gold
                string text = $"<b>🧯 3D FIRE EXTINGUISHER DISCOVERED</b>\n" +
                              $"<size=20><color=#00E5FF>Tap 3D Extinguisher model to GRAB & CARRY (within {maxGrabDistance:F1}m)!</color></size>";
#if UNITY_EDITOR
                text += "\n<size=18><color=#00FF00>[EDITOR: Press 'G' key to simulate Grab]</color></size>";
#endif
                GUI.Box(rect, text, style);
            }
            else if (currentState == GrabState.Grabbing)
            {
                style.normal.textColor = new Color(0.2f, 0.9f, 1.0f); // Cyan
                GUI.Box(rect, "<b>✊ GRABBING FIRE EXTINGUISHER...</b>", style);
            }
            else if (currentState == GrabState.Held)
            {
                style.normal.textColor = new Color(0.2f, 1.0f, 0.4f); // Vivid Green
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                GameObject fireObj = ARPlacementManager.Instance != null ? ARPlacementManager.Instance.SpawnedObject : null;
                float dist = (mainCam != null && fireObj != null) 
                    ? Vector3.Distance(new Vector3(mainCam.transform.position.x, 0, mainCam.transform.position.z), new Vector3(fireObj.transform.position.x, 0, fireObj.transform.position.z)) 
                    : 0f;

                string text = $"<b>✊ EXTINGUISHER HELD IN HAND</b>\n" +
                              $"<size=20>Walk toward Fire Hazard (Distance: <color=#00FF00>{dist:F1}m</color>)</size>";
#if UNITY_EDITOR
                text += "\n<size=18><color=#00FF00>[EDITOR: Press 'Space' to simulate arrival]</color></size>";
#endif
                GUI.Box(rect, text, style);
            }
            else if (currentState == GrabState.ArrivedAtFire)
            {
                style.normal.textColor = new Color(1.0f, 0.35f, 0.35f); // Red Alert
                GUI.Box(rect, "<b>🔥 ARRIVED AT FIRE HAZARD!</b>\n<size=20><color=#00FF00>Ready to spray & extinguish fire!</color></size>", style);
            }
        }
    }
}
