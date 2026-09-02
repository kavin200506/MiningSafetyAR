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
    /// First-person extinguisher rig with P.A.S.S. suppression engine.
    /// Handles grab, carry, pin pull, aim, squeeze/sweep, foam emission, and depletion.
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

        public enum PassStepState
        {
            None,
            PinPulled,
            NozzleAimed,
            HandleSqueezed,
            SweepComplete
        }

        [Header("Grab & Carry State")]
        [SerializeField] private GrabState currentState = GrabState.Unbound;
        public GrabState CurrentState => currentState;
        public bool IsGrabbed => currentState == GrabState.Held || currentState == GrabState.ArrivedAtFire;

        [Header("P.A.S.S. State")]
        [SerializeField] private PassStepState currentPassState = PassStepState.None;
        public PassStepState CurrentPassState => currentPassState;

        [Header("Held Item Slot Configuration")]
        [Tooltip("Transform under the AR Camera where the held fire extinguisher will parent to when grabbed.")]
        [SerializeField] private Transform heldItemSlot;
        public Transform HeldItemSlot
        {
            get => heldItemSlot;
            set => heldItemSlot = value;
        }

        [Header("Extinguisher Scale")]
        [Tooltip("Uniform scale applied to the extinguisher when grabbed (0.05-0.3 recommended for handheld size).")]
        [SerializeField] private float extinguisherScale = 0.15f;

        [Header("Layer Masks")]
        [SerializeField] private LayerMask grabbableLayerMask;
        [SerializeField] private LayerMask heldItemLayerMask;
        [SerializeField] private LayerMask fireLayerMask;

        [Header("Grab Physics & Distance Settings")]
        [SerializeField] private float grabDuration = 0.4f;
        [SerializeField] private float maxGrabDistance = 6.0f;
        [SerializeField] private float maxRaycastDistance = 15.0f;
        [SerializeField] private float arrivalDistanceMeters = 1.2f;

        [Header("P.A.S.S. Configuration")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, -0.3f, 0.6f);
        [SerializeField] private Vector3 holdingRotationOffset = new Vector3(0f, 180f, 0f);
        [SerializeField] private float targetCenterViewportY = 0.08f;
        [SerializeField] private float lerpSpeed = 5f;
        [SerializeField] private float maxSprayRange = 8.0f;
        [SerializeField] private Transform nozzleTip;

        [Header("Foam Capacity")]
        [SerializeField] private float maxFoamCapacity = 15f;
        private float currentFoamCapacity;
        public float FoamCapacityNormalized => maxFoamCapacity > 0f ? Mathf.Clamp01(currentFoamCapacity / maxFoamCapacity) : 0f;

        public float MaxGrabDistance { get => maxGrabDistance; set => maxGrabDistance = value; }
        public float MaxRaycastDistance { get => maxRaycastDistance; set => maxRaycastDistance = value; }

        private GameObject targetExtinguisher;
        private Coroutine grabCoroutine;
        private Coroutine aimCoroutine;
        private InputAction pressAction;
        private Ray lastRay;
        private bool isSqueezing = false;
        public bool IsSqueezing => isSqueezing;
        private float sweepAccumulated = 0f;
        private Vector3 lastSweepPosition;
        private float sweepThreshold = 0.3f;

        [Header("Pin Separation")]
        private Transform separatedPin;
        private Renderer pinRenderer;

        [Header("Foam Particle System")]
        [SerializeField] private ParticleSystem foamParticles;
        private Transform pinOriginalTransform;

        public GameObject TargetExtinguisher => targetExtinguisher;

        public event Action<GameObject> OnExtinguisherGrabbed;
        public event Action<Vector3> OnArrivedAtFireHazard;
        public event Action OnPinPulled;
        public event Action OnNozzleAimed;
        public event Action OnSprayStarted;
        public event Action OnSweepDetected;
        public event Action OnExtinguisherDepleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            pressAction = new InputAction("grab_touch", binding: "<Pointer>/press");
            pressAction.started += OnPointerPressBegan;
        }

        private void OnEnable()
        {
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
            if (pressAction != null)
            {
                pressAction.Enable();
            }

            FireExtinguisherModelLoader.OnModelLoaded += OnExtinguisherLoaded;
            if (ARStepCounterTracker.Instance != null)
            {
                ARStepCounterTracker.Instance.OnExtinguisherDiscovered += OnExtinguisherDiscoveredBySteps;
            }
        }

        private void OnDisable()
        {
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
                    slotGO.transform.localPosition = cameraOffset;
                    slotGO.transform.localRotation = Quaternion.Euler(holdingRotationOffset);
                    heldItemSlot = slotGO.transform;
                    Debug.Log($"[FireExtinguisherGrabController] Auto-created HeldItemSlot at localPos={cameraOffset}");
                }
            }
        }

        public void SetupExtinguisherForGrabbing(GameObject extinguisherGO)
        {
            if (extinguisherGO == null) return;

            targetExtinguisher = extinguisherGO;

            // Clamp extinguisher scale so it fits in hand (prevents enormous models)
            targetExtinguisher.transform.localScale = Vector3.one * extinguisherScale;

            BoxCollider box = targetExtinguisher.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = targetExtinguisher.AddComponent<BoxCollider>();
            }
            box.center = new Vector3(0, 0.3f, 0);
            box.size = new Vector3(0.8f, 1.2f, 0.8f);
            box.enabled = true;

            Collider[] colliders = targetExtinguisher.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                c.enabled = true;
            }

            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            if (grabbableLayer == -1) grabbableLayer = 9;
            SetLayerRecursively(targetExtinguisher, grabbableLayer);

            if (nozzleTip == null)
            {
                Transform n = targetExtinguisher.transform.Find("NozzleTip");
                if (n != null) nozzleTip = n;
                if (nozzleTip == null)
                {
                    foreach (Transform child in targetExtinguisher.GetComponentsInChildren<Transform>())
                    {
                        if (child.name.Contains("Nozzle") || child.name.Contains("nozzle"))
                        {
                            nozzleTip = child;
                            break;
                        }
                    }
                }
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            float distToCam = (mainCam != null && targetExtinguisher != null) ? Vector3.Distance(mainCam.transform.position, targetExtinguisher.transform.position) : -1f;

            Debug.Log($"[FireExtinguisherGrabController] Extinguisher registered: '{targetExtinguisher.name}' | Dist={distToCam:F2}m | Scale={extinguisherScale}");

            CreateNozzleAndFoamSystem();
            AutoFrameHeldItem();
        }

        /// <summary>
        /// Automatically calculates and applies the camera Y offset required to position
        /// the center of the 3D extinguisher model at targetCenterViewportY (default 0.08f)
        /// while forcing X offset to 0 (horizontally centered).
        /// </summary>
        public void AutoFrameHeldItem()
        {
            if (targetExtinguisher == null) return;

            Renderer[] renderers = targetExtinguisher.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning("[WARN] [FireExtinguisherGrabController] AutoFrameHeldItem failed — no Renderers found on targetExtinguisher.");
                return;
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            float fov = mainCam != null ? mainCam.fieldOfView : 60f;
            float distance = Mathf.Abs(cameraOffset.z);

            // Compute Y offset so that the object's center point lands at targetCenterViewportY (default 0.08):
            float computedY = (targetCenterViewportY - 0.5f) * 2.0f * distance * Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);

            // Force X = 0 always so extinguisher is horizontally centered on screen
            cameraOffset = new Vector3(0f, computedY, cameraOffset.z);

            if (heldItemSlot != null)
            {
                heldItemSlot.localPosition = cameraOffset;
            }

            Debug.Log($"[INFO] FireExtinguisherGrabController Auto-framed held item center: computed Y offset={cameraOffset.y:F3}, target center viewport Y={targetCenterViewportY:F2}");
        }

        /// <summary>
        /// Creates a NozzleTip transform and foam particle system on the extinguisher model.
        /// </summary>
        private void CreateNozzleAndFoamSystem()
        {
            if (targetExtinguisher == null) return;

            // Find nozzle mesh (BézierCurve = hose/nozzle, or Cone = discharge horn)
            Transform nozzleMesh = null;
            foreach (Transform child in targetExtinguisher.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("BezierCurve") || child.name.Contains("Cone") || child.name.Contains("Nozzle"))
                {
                    nozzleMesh = child;
                    break;
                }
            }

            // Create NozzleTip if not found
            if (nozzleTip == null)
            {
                GameObject nozzleGO = new GameObject("NozzleTip");
                if (nozzleMesh != null)
                {
                    // Parent to the root extinguisher transform (not the mesh) to avoid prefab corruption
                    nozzleGO.transform.SetParent(targetExtinguisher.transform, false);
                    nozzleGO.transform.position = nozzleMesh.position + nozzleMesh.forward * 0.15f;
                }
                else
                {
                    nozzleGO.transform.SetParent(targetExtinguisher.transform, false);
                    nozzleGO.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);
                }
                nozzleTip = nozzleGO.transform;
                Debug.Log($"[FireExtinguisherGrabController] Created NozzleTip at {nozzleTip.position}");
            }

            // Create foam particle system as root-level (world space simulation)
            if (foamParticles == null)
            {
                GameObject foamGO = new GameObject("FoamSprayParticles");
                foamGO.transform.position = nozzleTip.position;
                foamGO.transform.rotation = nozzleTip.rotation;

                foamParticles = foamGO.AddComponent<ParticleSystem>();

                // Stop first to allow property changes
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Main module
                var mainModule = foamParticles.main;
                mainModule.duration = 5f;
                mainModule.startLifetime = 0.8f;
                mainModule.startSpeed = 10f;
                mainModule.startSize = 0.08f;
                mainModule.startColor = new Color(1f, 1f, 1f, 0.9f);
                mainModule.maxParticles = 300;
                mainModule.loop = true;
                mainModule.playOnAwake = false;
                mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
                mainModule.gravityModifier = 0.05f;

                // Emission
                var emission = foamParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = 250f;

                // Shape
                var shape = foamParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 10f;
                shape.radius = 0.05f;

                // Color over lifetime (fade out)
                var colorOverLifetime = foamParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;

                // Size over lifetime (slight shrink)
                var sizeOverLifetime = foamParticles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));

                // Renderer
                ParticleSystemRenderer renderer = foamGO.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
                if (renderer.material != null)
                {
                    renderer.material.color = new Color(1f, 1f, 1f, 0.8f);
                }

                // Stop by default
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                Debug.Log("[FireExtinguisherGrabController] Foam particle system created at NozzleTip");
            }
        }

        private void OnExtinguisherLoaded(GameObject extinguisherGO)
        {
            SetupExtinguisherForGrabbing(extinguisherGO);
        }

        private void OnExtinguisherDiscoveredBySteps(Vector3 spawnPos)
        {
            if (ARStepCounterTracker.Instance != null && ARStepCounterTracker.Instance.SpawnedExtinguisherInstance != null)
            {
                SetupExtinguisherForGrabbing(ARStepCounterTracker.Instance.SpawnedExtinguisherInstance);
            }
        }

        private bool IsFireHazardObject(GameObject obj)
        {
            if (obj == null) return false;
            string name = obj.name;
            string rootName = obj.transform.root.name;

            if (name.Contains("GroundFire") || name.Contains("FireHazard") || name.Contains("Fire_Spawned") ||
                rootName.Contains("GroundFire") || rootName.Contains("FireHazard") || rootName.Contains("Fire_Spawned"))
            {
                return true;
            }

            if (obj.GetComponent<GroundFireController>() != null || obj.GetComponentInParent<GroundFireController>() != null || obj.GetComponentInChildren<GroundFireController>() != null)
            {
                return true;
            }

            return false;
        }

        private bool IsExtinguisherObject(GameObject obj)
        {
            if (obj == null) return false;
            if (IsFireHazardObject(obj)) return false;

            string name = obj.name;
            string rootName = obj.transform.root.name;

            if (obj.GetComponent<FireExtinguisherModelLoader>() != null || obj.GetComponentInParent<FireExtinguisherModelLoader>() != null)
            {
                return true;
            }

            if (name.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rootName.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        private void FindAndSetupExtinguisherInScene()
        {
            if (targetExtinguisher != null)
            {
                if (IsFireHazardObject(targetExtinguisher))
                {
                    targetExtinguisher = null;
                }
                else
                {
                    return;
                }
            }

            GameObject ext = GameObject.Find("Discovered_3D_FireExtinguisher")
                ?? GameObject.Find("Real_3D_FireExtinguisher_GLTF")
                ?? GameObject.Find("FireExtinguisherModel");

            if (ext == null)
            {
                FireExtinguisherModelLoader loader = FindFirstObjectByType<FireExtinguisherModelLoader>();
                if (loader != null && !IsFireHazardObject(loader.gameObject))
                {
                    ext = loader.gameObject;
                }
            }

            if (ext != null && IsExtinguisherObject(ext))
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

            if (!IsGrabbed)
            {
                CheckTouchInput();
            }
            else
            {
                CheckArrivalAtFireHazard();

                if (IsGrabbed)
                {
                    UpdateFoamSpray();
                    UpdateHeldPosition();
                }
            }

#if UNITY_EDITOR
            // Keyboard Controls (I, J, K, L) for Unity Editor Testing:
            // [I] -> Ignite Fire / Raycast Placement
            // [J] -> Grab Extinguisher into 1st-person view
            // [K] -> Pull Safety Pin & Aim Nozzle (P.A.S.S. Steps 2 & 3)
            // [L] -> Squeeze Handle & Stream Foam Spray (P.A.S.S. Step 4)
            bool pressI = (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame);
            bool pressJ = (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame);
            bool pressK = (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame);
            bool pressL = (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame);

#if ENABLE_LEGACY_INPUT_MANAGER
            pressI |= Input.GetKeyDown(KeyCode.I);
            pressJ |= Input.GetKeyDown(KeyCode.J);
            pressK |= Input.GetKeyDown(KeyCode.K);
            pressL |= Input.GetKeyDown(KeyCode.L);
#endif

            if (pressI)
            {
                ARSimulationLogger.LogKey("I", "Ignited Fire / Placement Raycast");
                if (ARPlacementManager.Instance != null)
                {
                    Vector2 mousePos = Pointer.current != null ? Pointer.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                    ARPlacementManager.Instance.PerformPlacementRaycast(mousePos);
                }
            }

            if (pressJ)
            {
                ARSimulationLogger.LogKey("J", "Grabbed Extinguisher into 1st-person camera view");
                SimulateGrab();
            }

            if (pressK)
            {
                ARSimulationLogger.LogKey("K", "Pulled Safety Pin & Aimed Nozzle");
                PullPin();
                AimNozzle();
            }

            if (pressL)
            {
                ARSimulationLogger.LogKey("L", "Squeezed Handle & Spraying Foam");
                StartSqueezing();
            }
#endif
        }

        private void UpdateHeldPosition()
        {
            if (targetExtinguisher == null || heldItemSlot == null) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return;

            Vector3 targetPosition = mainCam.transform.TransformPoint(cameraOffset);
            Quaternion targetRotation = mainCam.transform.rotation * Quaternion.Euler(holdingRotationOffset);

            targetExtinguisher.transform.position = Vector3.Lerp(targetExtinguisher.transform.position, targetPosition, Time.deltaTime * lerpSpeed);
            targetExtinguisher.transform.rotation = Quaternion.Slerp(targetExtinguisher.transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);

            // Maintain correct scale
            if (targetExtinguisher.transform.localScale != Vector3.one * extinguisherScale)
            {
                targetExtinguisher.transform.localScale = Vector3.one * extinguisherScale;
            }

            // Runtime verification log sampling center of model bounds in Viewport Y space
            Renderer[] renderers = targetExtinguisher.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }
                float sampledViewportY = mainCam.WorldToViewportPoint(combined.center).y;
                Debug.Log($"[DIAG] [FireExtinguisherGrabController] Held Item Center Viewport Y = {sampledViewportY:F3} (Target: {targetCenterViewportY:F2})");
            }
        }

        private void CheckTouchInput()
        {
            if (IsGrabbed) return;

            Vector2 tapPosition = Vector2.zero;
            bool tapDetected = false;

            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    tapPosition = touch.screenPosition;
                    tapDetected = true;
                }
            }

            if (!tapDetected && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                tapPosition = Pointer.current.position.ReadValue();
                tapDetected = true;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            // Fallback: legacy Input class for editor testing
            if (!tapDetected && Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                tapPosition = Input.GetTouch(0).position;
                tapDetected = true;
            }
            if (!tapDetected && Input.GetMouseButtonDown(0))
            {
                tapPosition = Input.mousePosition;
                tapDetected = true;
            }
#endif

            if (tapDetected)
            {
                bool grabbed = TryGrabExtinguisherAtScreenPoint(tapPosition);
                if (!grabbed)
                {
                    Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    TryGrabExtinguisherAtScreenPoint(screenCenter);
                }
            }
        }

        private void OnPointerPressBegan(InputAction.CallbackContext context)
        {
            if (IsGrabbed) return;

            if (context.control.device is Pointer pointerDevice)
            {
                Vector2 tapPos = pointerDevice.position.ReadValue();
                TryGrabExtinguisherAtScreenPoint(tapPos);
            }
        }

        public bool TryGrabExtinguisherAtScreenPoint(Vector2 screenPoint)
        {
            if (IsGrabbed || currentState != GrabState.Unbound) return false;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return false;

            Ray ray = mainCam.ScreenPointToRay(screenPoint);
            lastRay = ray;

            RaycastHit[] allHits = Physics.RaycastAll(ray, maxRaycastDistance);
            for (int i = 0; i < allHits.Length; i++)
            {
                var h = allHits[i];
                GameObject hitGO = h.collider.gameObject;

                if (IsFireHazardObject(hitGO))
                {
                    continue; // Skip Fire Hazard raycast hits!
                }

                bool isExtMatch = (targetExtinguisher != null && (hitGO == targetExtinguisher || hitGO.transform.IsChildOf(targetExtinguisher.transform)))
                               || IsExtinguisherObject(hitGO);

                if (isExtMatch && h.distance <= maxGrabDistance)
                {
                    if (targetExtinguisher == null)
                    {
                        targetExtinguisher = hitGO.transform.root.gameObject;
                    }
                    if (targetExtinguisher != null && !IsFireHazardObject(targetExtinguisher))
                    {
                        InitiateGrabSequence();
                        return true;
                    }
                }
            }

            return false;
        }

        public void InitiateGrabSequence()
        {
            if (IsGrabbed || currentState != GrabState.Unbound) return;

            if (targetExtinguisher == null)
            {
                FindAndSetupExtinguisherInScene();
            }
            if (targetExtinguisher == null || IsFireHazardObject(targetExtinguisher))
            {
                targetExtinguisher = null;
                return;
            }

            if (grabCoroutine != null) StopCoroutine(grabCoroutine);
            grabCoroutine = StartCoroutine(GrabSequence());
        }

        private IEnumerator GrabSequence()
        {
            currentState = GrabState.Grabbing;

            EnsureHeldItemSlotExists();

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 startPos = targetExtinguisher.transform.position;
            Quaternion startRot = targetExtinguisher.transform.rotation;

            float elapsedTime = 0f;
            while (elapsedTime < grabDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / grabDuration);
                float easeOutT = Mathf.Sin(t * Mathf.PI * 0.5f);

                Vector3 targetPos = heldItemSlot != null ? heldItemSlot.position : mainCam.transform.TransformPoint(cameraOffset);
                Quaternion targetRot = heldItemSlot != null ? heldItemSlot.rotation : mainCam.transform.rotation * Quaternion.Euler(holdingRotationOffset);

                targetExtinguisher.transform.position = Vector3.Lerp(startPos, targetPos, easeOutT);
                targetExtinguisher.transform.rotation = Quaternion.Slerp(startRot, targetRot, easeOutT);
                yield return null;
            }

            if (heldItemSlot != null)
            {
                targetExtinguisher.transform.SetParent(heldItemSlot, true);
                targetExtinguisher.transform.localPosition = Vector3.zero;
                targetExtinguisher.transform.localRotation = Quaternion.identity;
                targetExtinguisher.transform.localScale = Vector3.one * extinguisherScale;
            }

            ARAnchor anchor = targetExtinguisher.GetComponent<ARAnchor>() ?? targetExtinguisher.GetComponentInParent<ARAnchor>();
            if (anchor != null) Destroy(anchor);

            Collider[] colliders = targetExtinguisher.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;

            // Ensure on Default layer (0) so AR Camera always renders it on mobile devices
            SetLayerRecursively(targetExtinguisher, 0);
            Ensure3DExtinguisherMeshVisual(targetExtinguisher);

            currentState = GrabState.Held;
            currentPassState = PassStepState.None;
            currentFoamCapacity = maxFoamCapacity;

            Debug.Log("[FireExtinguisherGrabController] Grab complete — P.A.S.S. ready.");
            OnExtinguisherGrabbed?.Invoke(targetExtinguisher);
            grabCoroutine = null;
        }

        private void Ensure3DExtinguisherMeshVisual(GameObject extGO)
        {
            if (extGO == null) return;

            MeshRenderer mr = extGO.GetComponentInChildren<MeshRenderer>(true);
            if (mr == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "3D_Extinguisher_Body_Visual";
                body.transform.SetParent(extGO.transform, false);
                body.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                body.transform.localScale = new Vector3(0.25f, 0.4f, 0.25f);

                Collider c = body.GetComponent<Collider>();
                if (c != null) Destroy(c);

                MeshRenderer bodyMR = body.GetComponent<MeshRenderer>();
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");

                if (urpShader == null)
                {
                    Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                    foreach (Renderer r in sceneRenderers)
                    {
                        if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal"))
                        {
                            urpShader = r.sharedMaterial.shader;
                            break;
                        }
                    }
                }

                if (urpShader != null)
                {
                    Material redMat = new Material(urpShader);
                    redMat.SetColor("_BaseColor", new Color(0.9f, 0.1f, 0.1f));
                    bodyMR.material = redMat;
                }
            }
        }

        /// <summary>
        /// P.A.S.S. Step 1: Pull the safety pin.
        /// </summary>
        public void PullPin()
        {
            if (!IsGrabbed)
            {
                currentState = GrabState.Held;
            }

            currentPassState = PassStepState.PinPulled;
            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: PIN PULLED");
            OnPinPulled?.Invoke();

            SeparatePinFromExtinguisher();
        }

        /// <summary>
        /// Separates the Metal (pin) mesh from the extinguisher handle, hides it on the model,
        /// and plays a smooth pull-and-fall animation dropping the metal pin onto the ground.
        /// </summary>
        private void SeparatePinFromExtinguisher()
        {
            if (targetExtinguisher == null) return;

            Transform pinMesh = null;
            MeshFilter mf = null;
            MeshRenderer mr = null;

            // Search for dedicated pin child object or submesh (excluding main tank body/cylinder/red meshes)
            foreach (Transform child in targetExtinguisher.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = child.name.ToLowerInvariant();
                // Exclude main red body tank or cylinder tank components
                if (nameLower.Contains("body") || nameLower.Contains("tank") || nameLower.Contains("red") || nameLower.Contains("cylinder.body"))
                    continue;

                if (nameLower.Equals("metal") || nameLower.Contains("pin") || nameLower.Contains("green") || nameLower.Contains("circle.002") || nameLower.Contains("plane.014"))
                {
                    MeshFilter candidateMF = child.GetComponent<MeshFilter>();
                    MeshRenderer candidateMR = child.GetComponent<MeshRenderer>();
                    if (candidateMF != null && candidateMR != null && candidateMF.sharedMesh != null)
                    {
                        pinMesh = child;
                        mf = candidateMF;
                        mr = candidateMR;
                        break;
                    }
                }
            }

            // Fallback renderer search if name match didn't yield mesh
            if (pinMesh == null)
            {
                foreach (var r in targetExtinguisher.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string rName = r.gameObject.name.ToLowerInvariant();
                    if (rName.Contains("body") || rName.Contains("tank") || rName.Contains("red")) continue;

                    MeshFilter candidateMF = r.GetComponent<MeshFilter>();
                    if (candidateMF != null && candidateMF.sharedMesh != null && r.sharedMaterial != null)
                    {
                        string matName = r.sharedMaterial.name.ToLowerInvariant();
                        if ((matName.Contains("metal") || matName.Contains("pin") || matName.Contains("chrome")) && !matName.Contains("body") && !matName.Contains("red"))
                        {
                            pinMesh = r.transform;
                            mf = candidateMF;
                            mr = r;
                            break;
                        }
                    }
                }
            }

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (pinMesh != null && mr != null)
            {
                // Disable/hide original metal pin mesh on the extinguisher model
                pinOriginalTransform = pinMesh;
                mr.enabled = false;
                spawnPos = pinMesh.position;
                spawnRot = pinMesh.rotation;
                Debug.Log($"[FireExtinguisherGrabController] Hidden original pin mesh '{pinMesh.name}' on model.");
            }
            else
            {
                // Position near top valve assembly if specific submesh not found
                spawnPos = targetExtinguisher.transform.position + targetExtinguisher.transform.up * 0.28f + targetExtinguisher.transform.forward * 0.05f;
                spawnRot = targetExtinguisher.transform.rotation;
                Debug.Log("[FireExtinguisherGrabController] Using valve top position for separated metal pin spawn.");
            }

            GameObject pinGO;
            if (mf != null && mr != null && mf.sharedMesh != null)
            {
                pinGO = new GameObject("Separated_Metal_Pin");
                pinGO.transform.position = spawnPos;
                pinGO.transform.rotation = spawnRot;
                pinGO.transform.localScale = pinMesh != null ? pinMesh.lossyScale : Vector3.one * 0.15f;

                MeshFilter newMF = pinGO.AddComponent<MeshFilter>();
                newMF.sharedMesh = mf.sharedMesh;

                MeshRenderer newMR = pinGO.AddComponent<MeshRenderer>();
                newMR.sharedMaterials = mr.sharedMaterials;
            }
            else
            {
                pinGO = CreateFallbackMetalPinGO(spawnPos, spawnRot);
            }

            separatedPin = pinGO.transform;
            pinRenderer = pinGO.GetComponentInChildren<MeshRenderer>();

            StartCoroutine(AnimatePinFallSequence(pinGO));
        }

        private GameObject CreateFallbackMetalPinGO(Vector3 spawnPos, Quaternion spawnRot)
        {
            GameObject pinGO = new GameObject("Separated_Metal_Pin");
            pinGO.transform.position = spawnPos;
            pinGO.transform.rotation = spawnRot;

            // 1. Pull Ring
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Pin_Ring_Visual";
            ring.transform.SetParent(pinGO.transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.06f, 0.004f, 0.06f);

            Collider ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);

            // 2. Pin Shaft Rod
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Pin_Rod_Visual";
            rod.transform.SetParent(pinGO.transform, false);
            rod.transform.localPosition = new Vector3(0.035f, 0f, 0f);
            rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rod.transform.localScale = new Vector3(0.012f, 0.045f, 0.012f);

            Collider rodCol = rod.GetComponent<Collider>();
            if (rodCol != null) Destroy(rodCol);

            // Apply shiny chrome metal material
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (urpShader != null)
            {
                Material chromeMat = new Material(urpShader);
                chromeMat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.90f));
                chromeMat.SetFloat("_Metallic", 0.95f);
                chromeMat.SetFloat("_Smoothness", 0.90f);

                ring.GetComponent<MeshRenderer>().material = chromeMat;
                rod.GetComponent<MeshRenderer>().material = chromeMat;
            }

            return pinGO;
        }

        private IEnumerator AnimatePinFallSequence(GameObject pinGO)
        {
            if (pinGO == null) yield break;

            Vector3 startPos = pinGO.transform.position;
            Quaternion startRot = pinGO.transform.rotation;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 pullDir = mainCam != null ? (-mainCam.transform.right * 0.12f + mainCam.transform.up * 0.04f) : (Vector3.left * 0.12f);
            Vector3 pulledPos = startPos + pullDir;

            // Phase 1: Pull out horizontally from valve handle (0.15s)
            float elapsed = 0f;
            float pullDuration = 0.15f;
            while (elapsed < pullDuration)
            {
                if (pinGO == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullDuration);
                pinGO.transform.position = Vector3.Lerp(startPos, pulledPos, t);
                yield return null;
            }

            // Phase 2: Gravity drop accelerating to floor (0.55s)
            Vector3 dropStart = pinGO.transform.position;
            float dropDistance = 1.0f;
            Vector3 groundTarget = dropStart + Vector3.down * dropDistance + pullDir * 0.2f;

            elapsed = 0f;
            float dropDuration = 0.55f;
            Quaternion spinRot = Quaternion.Euler(UnityEngine.Random.Range(70, 160), UnityEngine.Random.Range(45, 90), UnityEngine.Random.Range(0, 360));

            while (elapsed < dropDuration)
            {
                if (pinGO == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dropDuration);
                float gravityT = t * t; // Acceleration under gravity

                pinGO.transform.position = Vector3.Lerp(dropStart, groundTarget, gravityT);
                pinGO.transform.rotation = Quaternion.Slerp(startRot, spinRot, t);
                yield return null;
            }

            if (pinGO != null)
            {
                pinGO.transform.position = groundTarget;
            }

            // Phase 3: Cleanup after 4 seconds resting on ground
            yield return new WaitForSeconds(4.0f);
            if (pinGO != null)
            {
                Destroy(pinGO);
            }
        }

        /// <summary>
        /// P.A.S.S. Step 2: Aim nozzle at base of fire and test spray.
        /// </summary>
        public void AimNozzle()
        {
            if (!IsGrabbed) currentState = GrabState.Held;
            if (currentPassState < PassStepState.PinPulled) PullPin();

            currentPassState = PassStepState.NozzleAimed;
            isSqueezing = false;

            if (aimCoroutine != null) StopCoroutine(aimCoroutine);
            aimCoroutine = StartCoroutine(AimSequence());

            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: NOZZLE AIMED");
            OnNozzleAimed?.Invoke();
        }

        private IEnumerator AimSequence()
        {
            if (targetExtinguisher == null) yield break;

            GroundFireController fire = null;
            var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
            foreach (var f in allFires)
            {
                if (f != null && f.IsFireActive) { fire = f; break; }
            }
            if (fire == null && ARPlacementManager.Instance != null && ARPlacementManager.Instance.SpawnedObject != null)
            {
                fire = ARPlacementManager.Instance.SpawnedObject.GetComponent<GroundFireController>();
            }

            if (fire != null)
            {
                Vector3 targetPos = fire.transform.position;
                Quaternion startRot = targetExtinguisher.transform.rotation;
                Vector3 dirToFire = (targetPos - targetExtinguisher.transform.position).normalized;

                if (dirToFire.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToFire);
                    float duration = 0.4f;
                    float elapsed = 0f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
                        targetExtinguisher.transform.rotation = Quaternion.Slerp(startRot, targetRot, easeT);
                        yield return null;
                    }
                    targetExtinguisher.transform.rotation = targetRot;
                }
            }
            aimCoroutine = null;
        }

        /// <summary>
        /// P.A.S.S. Step 3: Begin squeezing handle (starts foam spray).
        /// </summary>
        public void StartSqueezing()
        {
            if (!IsGrabbed) currentState = GrabState.Held;
            if (currentPassState < PassStepState.PinPulled) PullPin();
            if (currentPassState < PassStepState.NozzleAimed) AimNozzle();

            currentPassState = PassStepState.HandleSqueezed;
            isSqueezing = true;
            sweepAccumulated = 0f;
            lastSweepPosition = targetExtinguisher != null ? targetExtinguisher.transform.position : Vector3.zero;
            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: HANDLE SQUEEZED — spraying foam");
            OnSprayStarted?.Invoke();
        }

        /// <summary>
        /// Stop squeezing (release foam spray). PASS state remains at HandleSqueezed.
        /// </summary>
        public void StopSqueezing()
        {
            if (!isSqueezing) return;
            isSqueezing = false;
            Debug.Log("[FireExtinguisherGrabController] Squeeze RELEASED — foam spray stopped");

            if (foamParticles != null && foamParticles.isPlaying)
            {
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private GameObject foamRaycastBeam;
        private MeshRenderer beamRenderer;
        private Material beamMaterial;

        private void EnsureFoamRaycastVisualBeam()
        {
            if (foamRaycastBeam != null) return;

            foamRaycastBeam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            foamRaycastBeam.name = "Foam_Raycast_Visual_Beam";

            Collider col = foamRaycastBeam.GetComponent<Collider>();
            if (col != null) Destroy(col);

            beamRenderer = foamRaycastBeam.GetComponent<MeshRenderer>();
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");

            if (urpShader == null)
            {
                Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (Renderer r in sceneRenderers)
                {
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null && r.sharedMaterial.shader.name.Contains("Universal"))
                    {
                        urpShader = r.sharedMaterial.shader;
                        break;
                    }
                }
            }

            if (urpShader != null)
            {
                beamMaterial = new Material(urpShader);
                beamMaterial.SetColor("_BaseColor", new Color(0.0f, 0.9f, 1.0f, 0.75f));
                if (beamMaterial.HasProperty("_EmissionColor"))
                {
                    beamMaterial.EnableKeyword("_EMISSION");
                    beamMaterial.SetColor("_EmissionColor", new Color(0.0f, 0.9f, 1.0f) * 2.0f);
                }
                beamRenderer.material = beamMaterial;
            }

            foamRaycastBeam.SetActive(false);
        }

        private void UpdateFoamSpray()
        {
            EnsureFoamRaycastVisualBeam();

            if (!isSqueezing || currentPassState < PassStepState.HandleSqueezed)
            {
                // Ensure particles and visual beam stop when not squeezing
                if (foamParticles != null && foamParticles.isPlaying)
                {
                    foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                if (foamRaycastBeam != null && foamRaycastBeam.activeSelf)
                {
                    foamRaycastBeam.SetActive(false);
                }
                return;
            }

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();

            // Toggle foam particle emission
            if (foamParticles != null)
            {
                if (nozzleTip != null)
                {
                    foamParticles.transform.position = nozzleTip.position;
                }

                if (!foamParticles.isPlaying)
                {
                    var emission = foamParticles.emission;
                    emission.enabled = true;
                    foamParticles.Play();
                }

                Vector3 sprayDir = mainCam != null ? mainCam.transform.forward : Vector3.forward;
                var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
                foreach (var fi in allFires)
                {
                    if (fi != null && fi.IsFireActive)
                    {
                        sprayDir = (fi.transform.position - foamParticles.transform.position).normalized;
                        break;
                    }
                }
                foamParticles.transform.rotation = Quaternion.LookRotation(sprayDir);
            }

            currentFoamCapacity -= Time.deltaTime;
            if (currentFoamCapacity <= 0f)
            {
                currentFoamCapacity = 0f;
                isSqueezing = false;
                if (foamRaycastBeam != null) foamRaycastBeam.SetActive(false);
                Debug.Log("[FireExtinguisherGrabController] FOAM DEPLETED!");
                OnExtinguisherDepleted?.Invoke();
                return;
            }

            Vector3 origin = nozzleTip != null ? nozzleTip.position : (mainCam != null ? mainCam.transform.position : Vector3.zero);
            Vector3 dir = nozzleTip != null ? nozzleTip.forward : (mainCam != null ? mainCam.transform.forward : Vector3.forward);

            bool fireHit = false;

            // Layer 1: RaycastAll from nozzle/camera — hits fire colliders directly
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxSprayRange + 2.0f);
            foreach (var hit in hits)
            {
                GroundFireController fire = hit.collider.GetComponentInParent<GroundFireController>();
                if (fire == null) fire = hit.collider.GetComponent<GroundFireController>();
                if (fire != null && fire.IsFireActive)
                {
                    fire.ApplyFoamSuppression(hit.point, Time.deltaTime);
                    fireHit = true;
                    Debug.DrawLine(origin, hit.point, Color.cyan);
                    break;
                }
            }

            // Layer 2: OverlapSphere around spray origin — catches fire colliders even without raycast
            if (!fireHit)
            {
                Collider[] nearColliders = Physics.OverlapSphere(origin, 1.8f);
                foreach (var col in nearColliders)
                {
                    if (col == null) continue;
                    GroundFireController fire = col.GetComponentInParent<GroundFireController>();
                    if (fire == null) fire = col.GetComponent<GroundFireController>();
                    if (fire != null && fire.IsFireActive)
                    {
                        fire.ApplyFoamSuppression(origin, Time.deltaTime);
                        fireHit = true;
                        Debug.DrawLine(origin, col.transform.position, Color.green);
                        break;
                    }
                }
            }

            // Layer 3: FindObjectsByType proximity — guaranteed fallback
            if (!fireHit)
            {
                var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
                foreach (var f in allFires)
                {
                    if (f == null || !f.IsFireActive) continue;
                    float d = Vector3.Distance(origin, f.transform.position);
                    if (d <= maxSprayRange + 8.0f)
                    {
                        f.ApplyFoamSuppression(f.transform.position, Time.deltaTime);
                        fireHit = true;
                        Debug.DrawLine(origin, f.transform.position, Color.magenta);
                        break;
                    }
                }
            }

            // Update 3D Visual Raycast Laser Beam (1.5m - 3.0m)
            if (foamRaycastBeam != null)
            {
                foamRaycastBeam.SetActive(true);

                float beamLength = maxSprayRange + 0.5f;
                Vector3 beamCenter = origin + (dir.normalized * (beamLength / 2f));

                foamRaycastBeam.transform.position = beamCenter;
                foamRaycastBeam.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0f, 0f);
                foamRaycastBeam.transform.localScale = new Vector3(0.06f, beamLength / 2f, 0.06f);

                if (beamMaterial != null)
                {
                    Color beamColor = fireHit ? new Color(0.0f, 1.0f, 0.2f, 0.85f) : new Color(0.0f, 0.9f, 1.0f, 0.75f);
                    beamMaterial.SetColor("_BaseColor", beamColor);
                    if (beamMaterial.HasProperty("_EmissionColor"))
                    {
                        beamMaterial.SetColor("_EmissionColor", beamColor * 2.5f);
                    }
                }
            }

            if (fireHit)
            {
                var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
                foreach (var debugFire in allFires)
                {
                    if (debugFire != null && debugFire.IsFireActive)
                    {
                        Debug.Log($"[SPRAY] HIT! Fire HP: {debugFire.CurrentFireHealth:F1}/{debugFire.MaxFireHealth} ({debugFire.FireHealthNormalized * 100:F0}%)");
                        break;
                    }
                }
            }
            else
            {
                Debug.Log($"[FIRE_DIAG] No fire detected. Origin={origin}, Dir={dir}, Range={maxSprayRange}");
            }

            if (currentPassState == PassStepState.HandleSqueezed && targetExtinguisher != null)
            {
                Vector3 currentPos = targetExtinguisher.transform.position;
                float sweepDelta = Vector3.Distance(new Vector3(currentPos.x, 0, currentPos.z), new Vector3(lastSweepPosition.x, 0, lastSweepPosition.z));
                sweepAccumulated += (sweepDelta + 0.08f * Time.deltaTime);
                lastSweepPosition = currentPos;

                if (sweepAccumulated >= sweepThreshold && currentPassState == PassStepState.HandleSqueezed)
                {
                    currentPassState = PassStepState.SweepComplete;
                    Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: SWEEP COMPLETE");
                    OnSweepDetected?.Invoke();
                }
            }
        }

        private void CheckArrivalAtFireHazard()
        {
            if (currentState != GrabState.Held) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return;

            GameObject fireHazard = ARPlacementManager.Instance != null ? ARPlacementManager.Instance.SpawnedObject : null;
            if (fireHazard == null)
            {
                GroundFireController fireCtrl = FindFirstObjectByType<GroundFireController>();
                if (fireCtrl != null) fireHazard = fireCtrl.gameObject;
            }
            if (fireHazard == null) return;

            Vector3 camPos = mainCam.transform.position;
            Vector3 firePos = fireHazard.transform.position;
            Vector3 hDisplacement = new Vector3(camPos.x - firePos.x, 0f, camPos.z - firePos.z);
            float distanceMeters = hDisplacement.magnitude;

            if (distanceMeters <= arrivalDistanceMeters)
            {
                currentState = GrabState.ArrivedAtFire;
                Debug.Log($"[FireExtinguisherGrabController] ARRIVED at fire hazard! Distance={distanceMeters:F2}m");
                OnArrivedAtFireHazard?.Invoke(firePos);
            }
        }

        public void SimulateGrab()
        {
            if (targetExtinguisher == null)
            {
                FindAndSetupExtinguisherInScene();
            }

            if (targetExtinguisher == null)
            {
                GameObject ext = new GameObject("Discovered_3D_FireExtinguisher");
                Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
                if (mainCam != null)
                {
                    ext.transform.position = mainCam.transform.position + mainCam.transform.forward * 1.5f;
                }
                ext.AddComponent<FireExtinguisherModelLoader>();
                SetupExtinguisherForGrabbing(ext);
            }

            InitiateGrabSequence();
            currentState = GrabState.Held;
            Debug.Log("[FireExtinguisherGrabController] [TEST] Extinguisher GRABBED into 1st-person camera view!");
        }

        public void SimulateArrivalAtFire()
        {
            if (currentState == GrabState.Held)
            {
                currentState = GrabState.ArrivedAtFire;
                Vector3 firePos = ARPlacementManager.Instance != null && ARPlacementManager.Instance.SpawnedObject != null
                    ? ARPlacementManager.Instance.SpawnedObject.transform.position : Vector3.zero;
                OnArrivedAtFireHazard?.Invoke(firePos);
            }
        }

        /// <summary>
        /// Reset P.A.S.S. state for a new drill attempt.
        /// </summary>
        public void ResetPassState()
        {
            currentPassState = PassStepState.None;
            isSqueezing = false;
            currentFoamCapacity = maxFoamCapacity;
            sweepAccumulated = 0f;

            if (foamParticles != null && foamParticles.isPlaying)
            {
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Re-enable original pin if it was disabled
            if (pinOriginalTransform != null)
            {
                Renderer r = pinOriginalTransform.GetComponent<Renderer>();
                if (r != null) r.enabled = true;
                pinOriginalTransform = null;
            }

            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. state RESET.");
        }

        [ContextMenu("Test Pull Pin")]
        public void TestPullPin()
        {
            currentState = GrabState.Held;
            currentPassState = PassStepState.None;
            PullPin();
        }

        [ContextMenu("Test Aim Nozzle")]
        public void TestAimNozzle()
        {
            currentState = GrabState.Held;
            currentPassState = PassStepState.PinPulled;
            AimNozzle();
        }

        [ContextMenu("Test Start Spraying")]
        public void TestStartSpraying()
        {
            currentState = GrabState.Held;
            currentPassState = PassStepState.NozzleAimed;
            StartSqueezing();
        }

        [ContextMenu("Test Sweep Complete")]
        public void TestSweepComplete()
        {
            currentState = GrabState.Held;
            currentPassState = PassStepState.HandleSqueezed;
            sweepAccumulated = sweepThreshold;
            currentPassState = PassStepState.SweepComplete;
            Debug.Log("[FireExtinguisherGrabController] [TEST] Simulated Sweep Complete!");
            OnSweepDetected?.Invoke();
        }

        [ContextMenu("Test Foam Depletion")]
        public void TestFoamDepletion()
        {
            currentFoamCapacity = 0f;
            isSqueezing = false;
            Debug.Log("[FireExtinguisherGrabController] [TEST] Simulated Foam Depletion!");
            OnExtinguisherDepleted?.Invoke();
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

        private void OnGUI()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Splash") || sceneName.Contains("Login") || (sceneName.StartsWith("UI_") && sceneName != "UI_ARSimulation"))
                return;

            // 1. Draw Extinguisher Grab Status Banner
            if (currentState != GrabState.Unbound || targetExtinguisher != null)
            {
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

            // 2. Draw Fire Health Debug Bar when Spraying
            if (isSqueezing)
            {
                GroundFireController fire = null;
                var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
                foreach (var f in allFires)
                {
                    if (f != null && f.IsFireActive) { fire = f; break; }
                }
                if (fire != null)
                {
                    float hp = fire.CurrentFireHealth;
                    float maxHp = fire.MaxFireHealth;
                    float pct = fire.FireHealthNormalized * 100f;

                    GUI.Box(new Rect(10, 10, 260, 90), "");

                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 16,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.red }
                    };
                    GUIStyle hpStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        normal = { textColor = Color.white }
                    };

                    GUI.Label(new Rect(15, 15, 250, 25), "FIRE DEBUG", titleStyle);
                    GUI.Label(new Rect(15, 40, 250, 20), $"HP: {hp:F1} / {maxHp}  ({pct:F0}%)", hpStyle);
                    GUI.Label(new Rect(15, 60, 250, 20), $"Spray Range: {maxSprayRange:F1}m", hpStyle);

                    GUI.Box(new Rect(15, 82, 240, 12), "");
                    float barWidth = Mathf.Clamp01(hp / maxHp) * 238f;
                    GUI.color = hp > maxHp * 0.5f ? Color.red : (hp > maxHp * 0.25f ? Color.yellow : Color.green);
                    GUI.DrawTexture(new Rect(16, 83, barWidth, 10), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
        }

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
    }
}
