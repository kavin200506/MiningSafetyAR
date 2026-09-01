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
        [SerializeField] private Vector3 cameraOffset = new Vector3(0.10f, -0.15f, 0.45f);
        [SerializeField] private Vector3 holdingRotationOffset = Vector3.zero;
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
            EnhancedTouchSupport.Enable();
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
        }

        private void CheckTouchInput()
        {
            if (IsGrabbed) return;

            Vector2 tapPosition = Vector2.zero;
            bool tapDetected = false;

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

            // Fallback: legacy Input class for editor testing
            #if ENABLE_LEGACY_INPUT_MANAGER
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

            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            int layerMask = grabbableLayer != -1 ? (1 << grabbableLayer) : grabbableLayerMask.value;
            if (layerMask == 0) layerMask = ~0;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, layerMask))
            {
                GameObject hitGO = hit.collider.gameObject;
                bool isTargetMatch = (targetExtinguisher == null || hitGO == targetExtinguisher || hitGO.transform.IsChildOf(targetExtinguisher.transform));

                if (isTargetMatch && hit.distance <= maxGrabDistance)
                {
                    if (targetExtinguisher == null) targetExtinguisher = hit.collider.transform.root.gameObject;
                    InitiateGrabSequence();
                    return true;
                }
            }

            RaycastHit[] allHits = Physics.RaycastAll(ray, maxRaycastDistance);
            for (int i = 0; i < allHits.Length; i++)
            {
                var h = allHits[i];
                GameObject hitGO = h.collider.gameObject;
                bool isExtMatch = (targetExtinguisher != null && (hitGO == targetExtinguisher || hitGO.transform.IsChildOf(targetExtinguisher.transform)))
                               || (hitGO.name.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0)
                               || (hitGO.transform.root.name.IndexOf("Extinguisher", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isExtMatch && h.distance <= maxGrabDistance)
                {
                    if (targetExtinguisher == null) targetExtinguisher = h.collider.transform.root.gameObject;
                    InitiateGrabSequence();
                    return true;
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
            if (targetExtinguisher == null) return;

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
        /// Separates the Green (pin) mesh from the extinguisher, adds physics, and lets it fall.
        /// </summary>
        private void SeparatePinFromExtinguisher()
        {
            if (targetExtinguisher == null) return;

            // Find the Green mesh (safety pin) by name
            Transform greenMesh = null;
            foreach (Transform child in targetExtinguisher.GetComponentsInChildren<Transform>())
            {
                if (child.name.Contains("Green"))
                {
                    greenMesh = child;
                    break;
                }
            }

            if (greenMesh == null)
            {
                Debug.LogWarning("[FireExtinguisherGrabController] Could not find Green mesh for pin separation");
                return;
            }

            MeshFilter mf = greenMesh.GetComponent<MeshFilter>();
            MeshRenderer mr = greenMesh.GetComponent<MeshRenderer>();
            if (mf == null || mr == null)
            {
                Debug.LogWarning("[FireExtinguisherGrabController] Green mesh has no MeshFilter/MeshRenderer");
                return;
            }

            // Disable original pin
            pinOriginalTransform = greenMesh;
            mr.enabled = false;

            // Create separate pin object
            GameObject pinGO = new GameObject("Separated_Pin");
            pinGO.transform.position = greenMesh.position;
            pinGO.transform.rotation = greenMesh.rotation;
            pinGO.transform.localScale = greenMesh.lossyScale;

            // Copy mesh and material
            MeshFilter pinMF = pinGO.AddComponent<MeshFilter>();
            pinMF.sharedMesh = mf.sharedMesh;

            MeshRenderer pinMR = pinGO.AddComponent<MeshRenderer>();
            pinMR.sharedMaterials = mr.sharedMaterials;

            // Add convex collider for physics
            MeshCollider mc = pinGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;

            // Add rigidbody for gravity/fall
            Rigidbody rb = pinGO.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.05f;

            // Apply impulse force (pull out and away)
            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector3 pullDir = mainCam != null ? -mainCam.transform.forward + mainCam.transform.up * 0.5f : Vector3.up + Vector3.forward;
            rb.AddForce(pullDir.normalized * 3f, ForceMode.Impulse);
            rb.AddTorque(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);

            separatedPin = pinGO.transform;
            pinRenderer = pinMR;

            Debug.Log("[FireExtinguisherGrabController] Pin separated and falling with physics");
            Destroy(pinGO, 4f);
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

            // Rotate extinguisher to face the fire
            if (targetExtinguisher != null)
            {
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
                    Vector3 dirToFire = (fire.transform.position - targetExtinguisher.transform.position).normalized;
                    if (dirToFire.sqrMagnitude > 0.01f)
                    {
                        targetExtinguisher.transform.rotation = Quaternion.LookRotation(dirToFire);
                    }
                }
            }

            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: NOZZLE AIMED");
            OnNozzleAimed?.Invoke();
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
            if (!isSqueezing) return;

            GroundFireController fire = null;
            var allFires = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
            foreach (var f in allFires)
            {
                if (f != null && f.IsFireActive) { fire = f; break; }
            }
            if (fire == null) return;

            float hp = fire.CurrentFireHealth;
            float maxHp = fire.MaxFireHealth;
            float pct = fire.FireHealthNormalized * 100f;

            // Background box
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
            GUIStyle barBg = new GUIStyle(GUI.skin.box);
            GUIStyle barFill = new GUIStyle(GUI.skin.box);

            GUI.Label(new Rect(15, 15, 250, 25), "FIRE DEBUG", titleStyle);
            GUI.Label(new Rect(15, 40, 250, 20), $"HP: {hp:F1} / {maxHp}  ({pct:F0}%)", hpStyle);
            GUI.Label(new Rect(15, 60, 250, 20), $"Spray Range: {maxSprayRange:F1}m", hpStyle);

            // Health bar
            GUI.Box(new Rect(15, 82, 240, 12), "");
            float barWidth = Mathf.Clamp01(hp / maxHp) * 238f;
            GUI.color = hp > maxHp * 0.5f ? Color.red : (hp > maxHp * 0.25f ? Color.yellow : Color.green);
            GUI.DrawTexture(new Rect(16, 83, barWidth, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
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
