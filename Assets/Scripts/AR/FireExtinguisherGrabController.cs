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
        [Tooltip("Uniform scale applied to the extinguisher when grabbed. Large/close first-person framing (crops bottom of model below screen edge) uses ~0.42.")]
        [SerializeField] private float extinguisherScale = 0.42f; // Increased from 0.15f to fill the lower screen foreground

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
        // Top-half framing, computed and verified live via Unity MCP (measured actual mesh
        // bounds against the camera frustum at 60 deg FOV / 0.474 aspect, solved for the
        // distance+height that puts the mesh's own vertical midpoint exactly at the bottom
        // screen edge with its top landing just below screen-center):
        // X=0: mesh bounds are already centered on this FBX's own pivot, no horizontal
        // correction needed (unlike the old glTF model).
        // Y=-0.717, Z=0.643: only the top half of the extinguisher (handle/gauge/valve/hose)
        // is visible, with its top edge landing just below the vertical middle of the screen;
        // the body is cropped off at the bottom edge. Confirmed with a live render.
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, -0.717f, 0.643f);
        [SerializeField] private Vector3 holdingRotationOffset = new Vector3(0f, 0f, 0f);
        [Tooltip("Rotation offset applied after pin pull so the Hose points forward (toward fire) and Handles face the user.")]
        [SerializeField] private Vector3 postPinPullRotationOffset = new Vector3(0f, 90f, 0f);
        private Vector3 currentHoldingRotationOffset = Vector3.zero;
        private Vector3 targetHoldingRotationOffset = Vector3.zero;
        [SerializeField] private float targetCenterViewportY = 0.08f;
        [SerializeField] private float lerpSpeed = 8f;
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

        [Header("Pin Drag Gesture")]
        [Tooltip("Minimum screen-space movement (pixels) before a press-then-release on the pin counts as a real drag rather than an accidental tap.")]
        [SerializeField] private float pinDragMinPixels = 6f;
        private bool isPinDragActive = false;
        private Vector2 pinDragStartScreenPos;
        private Vector2 pinDragPrevScreenPos;
        private Vector3 pinDragStartWorldPos;
        private Vector3 pinDragPrevWorldPos;
        private Camera pinDragCamera;
        private GameObject activeDraggedPinGO;
        private readonly List<Collider> pinTouchColliders = new List<Collider>();

        [Header("Pin Separation")]
        private Transform separatedPin;
        private Renderer pinRenderer;

        [Header("Foam Particle System")]
        [SerializeField] private ParticleSystem foamParticles;
        // Gas/foam must always appear to vent from Base_Ring, not the hose or camera — cached once
        // per grabbed instance so UpdateFoamSpray can re-read its live world position every frame
        // without a per-frame Find/GetComponent lookup.
        private Renderer foamEmissionRenderer;
        private static Texture2D sSoftSmokeTexture;
        // The real pin is 3 separate parts on the model (ring + 2 prongs); all of them get
        // hidden together when pulled and re-shown together on reset.
        private readonly List<Renderer> hiddenPinRenderers = new List<Renderer>();

        [Header("Handle Squeeze Gesture & Visual")]
        [Tooltip("Degrees Upper_Handle_Grip and Squeeze_Lever rotate around Lever_Pivot when fully squeezed.")]
        [SerializeField] private float handleSqueezeAngle = 20f;
        [Tooltip("How fast the handle presses in / springs back (higher = snappier).")]
        [SerializeField] private float handleSqueezeLerpSpeed = 10f;
        private float currentSqueezeAmount = 0f; // 0 = open/rest, 1 = fully squeezed
        private Transform upperHandleGripT, leverPivotT, squeezeLeverT;
        private bool handleSqueezeSetupDone = false;
        private readonly List<Collider> handleTouchColliders = new List<Collider>();

        [Header("Hose & Base Ring Animation")]
        [Tooltip("How fast the hose deploys toward Lever_Pivot / retracts back (higher = snappier).")]
        [SerializeField] private float hoseDeployLerpSpeed = 6f;
        [Tooltip("How many degrees the hose visually sways per degree/second of the extinguisher turning left/right.")]
        [SerializeField] private float hoseSwayResponsiveness = 0.35f;
        [Tooltip("Maximum extra sway angle applied to the hose while turning.")]
        [SerializeField] private float hoseSwayMaxAngle = 14f;
        [Tooltip("How fast the sway offset follows/settles (higher = snappier, less trailing lag).")]
        [SerializeField] private float hoseSwayLerpSpeed = 6f;
        private Transform hoseT, baseRingT, leverPivotForHoseT;
        private Renderer hoseRenderer, baseRingRenderer;
        private Transform hoseRefFrame; // hose's own parent — the stable rigid frame these are measured in
        private Vector3 hoseRestDirLocal; // rest direction (anchor -> free end), in hoseRefFrame local space
        private Vector3 hoseNearEndOffsetLocal; // hoseT's own pivot offset from its mesh's near/attached end, in hoseRefFrame local space
        private Quaternion hoseRestLocalRotation; // hoseT's original authored local rotation
        private Vector3 hoseOriginalLocalPos;
        private Quaternion hoseOriginalLocalRot;
        private bool hoseSetupDone = false;
        private float currentHoseDeployAmount = 0f; // 0 = rest (clipped to side), 1 = fully facing Lever_Pivot
        private float currentHoseSwayAngle = 0f; // currently-applied extra left/right sway, degrees
        private float lastHeldYawDegrees = 0f;
        private bool hasLastHeldYaw = false;

        [Header("Pin Pull Whole-Body Turn")]
        [Tooltip("Degrees the whole extinguisher turns (around its own up axis) as a single visual cue when the pin is pulled — its orientation at that moment counts as 0°.")]
        [SerializeField] private float pinPullTurnAngle = 90f;
        [Tooltip("How long the pin-pull turn animation takes, in seconds.")]
        [SerializeField] private float pinPullTurnDuration = 0.5f;
        private Coroutine pinPullTurnCoroutine;

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
            OnPinPulled += TriggerPinPullTurn;
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
            OnPinPulled -= TriggerPinPullTurn;
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
            // NOTE: AutoFrameHeldItem() is intentionally NOT called here. It recomputes
            // cameraOffset.y from targetCenterViewportY/distance and would overwrite the
            // manually-tuned close/cropped first-person cameraOffset above. Call it manually
            // (e.g. from the Editor) only if you need to re-derive a centered framing.
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

            // The model has no dedicated discharge-horn mesh; the "Hose" part (the black rubber
            // tube running from the valve) is the closest thing to a nozzle. Derive a spray origin
            // from the free end of its bounds — the corner farthest from the extinguisher's own
            // pivot, which is reliably the loose/hanging end rather than the end fixed to the valve.
            Transform hose = FindChildByName(targetExtinguisher.transform, "Hose");

            // Create NozzleTip if not found
            if (nozzleTip == null)
            {
                GameObject nozzleGO = new GameObject("NozzleTip");

                Renderer hoseRenderer = hose != null ? hose.GetComponent<Renderer>() : null;
                if (hoseRenderer != null)
                {
                    Vector3 pivot = targetExtinguisher.transform.position;
                    Bounds b = hoseRenderer.bounds;
                    Vector3 farCorner = b.center;
                    float bestDistSqr = -1f;
                    for (int xi = -1; xi <= 1; xi += 2)
                    {
                        for (int yi = -1; yi <= 1; yi += 2)
                        {
                            for (int zi = -1; zi <= 1; zi += 2)
                            {
                                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(xi, yi, zi));
                                float d = (corner - pivot).sqrMagnitude;
                                if (d > bestDistSqr) { bestDistSqr = d; farCorner = corner; }
                            }
                        }
                    }
                    nozzleGO.transform.position = farCorner;
                    // Parent to Hose itself (not the extinguisher root) so the spray point follows
                    // the hose's deploy/sway animation instead of staying fixed to the body.
                    nozzleGO.transform.SetParent(hose, true);
                }
                else
                {
                    nozzleGO.transform.SetParent(targetExtinguisher.transform, false);
                    nozzleGO.transform.localPosition = new Vector3(0f, 0.3f, 0.5f);
                }
                nozzleTip = nozzleGO.transform;
                Debug.Log($"[FireExtinguisherGrabController] Created NozzleTip at {nozzleTip.position} (from Hose free end: {hose != null})");
            }

            // The visible discharge point is always Base_Ring, regardless of where the aim raycast
            // (nozzleTip) points — cache its renderer so UpdateFoamSpray can read its live bounds
            // center every frame (bounds, not transform.position, for the same reason EnsureHoseSetup
            // uses bounds: this FBX's per-part pivots aren't reliably centered on their geometry).
            Transform baseRing = FindChildByName(targetExtinguisher.transform, "Base_Ring");
            foamEmissionRenderer = baseRing != null ? baseRing.GetComponent<Renderer>() : null;

            // Create foam particle system as root-level (world space simulation)
            if (foamParticles == null)
            {
                GameObject foamGO = new GameObject("FoamSprayParticles");
                foamGO.transform.position = foamEmissionRenderer != null ? foamEmissionRenderer.bounds.center : nozzleTip.position;
                foamGO.transform.rotation = nozzleTip.rotation;

                foamParticles = foamGO.AddComponent<ParticleSystem>();

                // Stop first to allow property changes
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // Main module — white CO2/foam discharge gas: a quick initial push that slows into a
                // slow-drifting, expanding puff (real extinguisher discharge, not a foam liquid jet).
                // Sized/counted to read clearly on a phone screen at AR held-item distance (~0.3-0.7m
                // from camera) rather than a subtle wisp that's hard to tell is even running.
                var mainModule = foamParticles.main;
                mainModule.duration = 5f;
                mainModule.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
                mainModule.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6f);
                mainModule.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
                mainModule.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
                mainModule.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.9f, 0.9f, 0.85f), new Color(1f, 1f, 1f, 1f));
                mainModule.maxParticles = 800;
                mainModule.loop = true;
                mainModule.playOnAwake = false;
                mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
                mainModule.gravityModifier = -0.03f; // faint upward drift, like venting gas rising

                // Emission — dense enough that the plume reads as a solid cloud, not scattered wisps
                var emission = foamParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = 180f;

                // Shape — a wide cone so the gas visibly billows outward and fills the frame
                var shape = foamParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 28f;
                shape.radius = 0.1f;

                // Slow the initial burst down into a drift, rather than a constant-speed jet — still
                // fast/far enough to spread across a good portion of the screen before it settles.
                var limitVel = foamParticles.limitVelocityOverLifetime;
                limitVel.enabled = true;
                limitVel.limit = new ParticleSystem.MinMaxCurve(1.2f);
                limitVel.dampen = 0.25f;

                // Rotation over lifetime — gentle tumble, sells the "billowing gas" look
                var rotationOverLifetime = foamParticles.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-40f * Mathf.Deg2Rad, 40f * Mathf.Deg2Rad);

                // Noise — subtle turbulence so the plume drifts unevenly instead of a rigid cone
                var noise = foamParticles.noise;
                noise.enabled = true;
                noise.strength = 0.25f;
                noise.frequency = 0.4f;
                noise.scrollSpeed = 0.3f;
                noise.damping = true;

                // Color over lifetime — white throughout, near-instant fade-in so it's immediately
                // visible, long fade-out
                var colorOverLifetime = foamParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.06f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;

                // Size over lifetime — grows substantially as it disperses, like real smoke/gas
                // expanding — this is most of what makes the plume cover screen space
                var sizeOverLifetime = foamParticles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3.5f));

                // Renderer — soft round sprite (procedural, no external asset dependency) instead of
                // a hard-edged quad, alpha-blended so puffs overlap softly like real gas/smoke.
                ParticleSystemRenderer renderer = foamGO.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                renderer.material = CreateSoftSmokeMaterial();

                // Stop by default
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                Debug.Log("[FireExtinguisherGrabController] Foam particle system created at Base_Ring");
            }
        }

        /// <summary>
        /// A small runtime-generated radial-gradient texture (white, soft alpha falloff) so smoke
        /// puffs render as soft circles instead of hard-edged squares — self-contained, no external
        /// texture asset dependency to wire up in the Inspector.
        /// </summary>
        private static Texture2D GetOrCreateSoftSmokeTexture()
        {
            if (sSoftSmokeTexture != null) return sSoftSmokeTexture;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ProceduralSoftSmoke";
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float maxDist = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, dist));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            sSoftSmokeTexture = tex;
            return sSoftSmokeTexture;
        }

        /// <summary>
        /// URP-transparent, alpha-blended, unlit material for the smoke sprite. Tries the dedicated
        /// URP particle shader first (correctly reads per-particle vertex color/alpha from
        /// colorOverLifetime), then falls back to "Sprites/Default" — a shader Unity keeps rendering
        /// correctly on every render pipeline (unlike most other built-in shaders, which show as
        /// magenta/invisible under URP) and which also honors vertex color, so the fade/size curves
        /// above still work even on the fallback path. This is the actual fix for particles that
        /// existed in the Hierarchy but never appeared on screen: the previous "Particles/Standard
        /// Unlit" built-in shader isn't URP-compatible and was very likely rendering invisible/pink.
        /// </summary>
        private static Material CreateSoftSmokeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Particles/Standard Unlit");

            var mat = new Material(shader);
            Texture2D tex = GetOrCreateSoftSmokeTexture();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            Color tint = Color.white;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            else mat.color = tint;

            // Force alpha-blended transparent surface (URP Lit/Unlit shaders default to Opaque).
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // 0 = Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");

            return mat;
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
                    CheckPinDragGesture();
                    CheckHandleSqueezeTouch();
                    UpdateHandleSqueezeVisual(); // Upper_Handle_Grip only — Squeeze_Lever no longer moves with it
                    // Hose/Base_Ring visual animation disabled per explicit request — replaced with
                    // a single whole-body 90° turn on pin pull instead (see TriggerPinPullTurn).
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
            if (targetExtinguisher == null) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return;

            currentHoldingRotationOffset = Vector3.Lerp(currentHoldingRotationOffset, targetHoldingRotationOffset, Time.deltaTime * lerpSpeed);

            EnsureHeldItemSlotExists();

            if (heldItemSlot != null)
            {
                heldItemSlot.localPosition = cameraOffset;
                heldItemSlot.localRotation = Quaternion.Euler(currentHoldingRotationOffset);

                if (targetExtinguisher.transform.parent != heldItemSlot)
                {
                    targetExtinguisher.transform.SetParent(heldItemSlot, false);
                }
                targetExtinguisher.transform.localPosition = Vector3.zero;
                targetExtinguisher.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Vector3 targetPosition = mainCam.transform.TransformPoint(cameraOffset);
                Quaternion targetRotation = mainCam.transform.rotation * Quaternion.Euler(currentHoldingRotationOffset);

                targetExtinguisher.transform.position = Vector3.Lerp(targetExtinguisher.transform.position, targetPosition, Time.deltaTime * lerpSpeed);
                targetExtinguisher.transform.rotation = Quaternion.Slerp(targetExtinguisher.transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
            }

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

        /// <summary>
        /// Reads the current touch/mouse pointer state in one place, so the pin-drag and
        /// handle-squeeze gestures don't each duplicate the touch-vs-pointer fallback logic.
        /// </summary>
        private bool TryReadPointer(out Vector2 pos, out bool active, out bool beganThisFrame)
        {
            pos = Vector2.zero;
            active = false;
            beganThisFrame = false;

            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }

            if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
            {
                var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
                pos = touch.screenPosition;
                beganThisFrame = touch.phase == UnityEngine.InputSystem.TouchPhase.Began;
                active = touch.phase != UnityEngine.InputSystem.TouchPhase.Ended
                       && touch.phase != UnityEngine.InputSystem.TouchPhase.Canceled;
                return true;
            }

            if (Pointer.current != null)
            {
                pos = Pointer.current.position.ReadValue();
                beganThisFrame = Pointer.current.press.wasPressedThisFrame;
                active = Pointer.current.press.isPressed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Casts a ray from the camera through a screen point and reports whether it hit one
        /// of the given colliders (used to gate the pin-drag and handle-squeeze gestures to
        /// only start when the player actually touches the right mesh).
        /// </summary>
        private bool ScreenPointHitsAny(Vector2 screenPos, List<Collider> targets)
        {
            if (targets.Count == 0) return false;
            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (mainCam == null) return false;

            Ray ray = mainCam.ScreenPointToRay(screenPos);
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRaycastDistance);
            foreach (var h in hits)
            {
                if (targets.Contains(h.collider)) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds (once) a generously-padded BoxCollider to a mesh part so it's an easy touch
        /// target on mobile despite being small at extinguisherScale. No-op if already present.
        /// </summary>
        private Collider AddTouchCollider(Transform part)
        {
            if (part == null) return null;
            BoxCollider col = part.GetComponent<BoxCollider>();
            if (col == null) col = part.gameObject.AddComponent<BoxCollider>();

            MeshFilter mf = part.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                col.center = mf.sharedMesh.bounds.center;
                col.size = mf.sharedMesh.bounds.size * 2.5f; // padding for easier finger targeting
            }
            col.isTrigger = false;
            return col;
        }

        private static readonly string[] PinPartNames = { "Carry_Handle" };

        /// <summary>
        /// Lazily adds touch colliders to the pin parts once the FBX mesh has actually loaded
        /// (SetupExtinguisherForGrabbing can run before the async model load finishes, so this
        /// can't be a one-shot call from there — it retries every frame until it succeeds).
        /// </summary>
        private void EnsurePinTouchColliders()
        {
            if (pinTouchColliders.Count > 0 || targetExtinguisher == null) return;

            foreach (string partName in PinPartNames)
            {
                Transform part = FindChildByName(targetExtinguisher.transform, partName);
                if (part == null) continue;
                Collider c = AddTouchCollider(part);
                if (c != null) pinTouchColliders.Add(c);
            }
        }

        /// <summary>
        /// P.A.S.S. Step 1 (touch-driven): while the pin hasn't been pulled yet, a press that
        /// starts on the pin (Carry_Handle) drags the separated pin in real time with the
        /// finger; releasing hands it off to physics to fall and settle.
        /// </summary>
        private void CheckPinDragGesture()
        {
            if (currentPassState != PassStepState.None) return;

            EnsurePinTouchColliders();

            if (!TryReadPointer(out Vector2 currentPos, out bool pressActive, out bool pressBeganThisFrame))
                return;

            if (pressBeganThisFrame)
            {
                if (ScreenPointHitsAny(currentPos, pinTouchColliders))
                {
                    BeginPinDrag(currentPos);
                }
                return;
            }

            if (!isPinDragActive) return;

            if (!pressActive)
            {
                EndPinDrag();
                return;
            }

            UpdatePinDrag(currentPos);
        }

        private void BeginPinDrag(Vector2 screenPos)
        {
            GameObject pinGO = BuildSeparatedPinVisual();
            if (pinGO == null) return;

            pinDragCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            activeDraggedPinGO = pinGO;
            pinDragStartScreenPos = screenPos;
            pinDragPrevScreenPos = screenPos;
            pinDragStartWorldPos = pinGO.transform.position;
            pinDragPrevWorldPos = pinGO.transform.position;
            isPinDragActive = true;

            ARSimulationLogger.LogState("PinDrag", "Started dragging pin");
        }

        private void UpdatePinDrag(Vector2 currentScreenPos)
        {
            if (activeDraggedPinGO == null || pinDragCamera == null) return;

            Vector2 screenDelta = currentScreenPos - pinDragStartScreenPos;

            // Convert screen-pixel delta to a world-space offset at the pin's depth, so the pin
            // visually tracks the finger 1:1 regardless of device resolution.
            float distance = Vector3.Distance(pinDragCamera.transform.position, pinDragStartWorldPos);
            float halfHeightWorld = distance * Mathf.Tan(pinDragCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float pixelToWorld = (2f * halfHeightWorld) / Mathf.Max(1, Screen.height);

            Vector3 worldOffset = pinDragCamera.transform.right * (screenDelta.x * pixelToWorld)
                                 + pinDragCamera.transform.up * (-screenDelta.y * pixelToWorld);

            pinDragPrevWorldPos = activeDraggedPinGO.transform.position;
            pinDragPrevScreenPos = currentScreenPos;
            activeDraggedPinGO.transform.position = pinDragStartWorldPos + worldOffset;
        }

        private void EndPinDrag()
        {
            isPinDragActive = false;
            if (activeDraggedPinGO == null) return;

            GameObject pinGO = activeDraggedPinGO;
            activeDraggedPinGO = null;

            float dragDistancePixels = Vector2.Distance(pinDragPrevScreenPos, pinDragStartScreenPos);
            if (dragDistancePixels < pinDragMinPixels)
            {
                // Accidental tap with no real movement — snap back to where it started rather
                // than confusingly "complete" the pull with no visible drag.
                pinGO.transform.position = pinDragStartWorldPos;
            }

            currentPassState = PassStepState.PinPulled;
            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: PIN PULLED (drag)");
            OnPinPulled?.Invoke();

            // Estimate a release velocity from the last frame of motion for a natural toss.
            Vector3 releaseVelocity = Time.deltaTime > 0.0001f
                ? (pinGO.transform.position - pinDragPrevWorldPos) / Time.deltaTime
                : Vector3.zero;
            releaseVelocity = Vector3.ClampMagnitude(releaseVelocity, 3.0f);

            DropPinWithPhysics(pinGO, releaseVelocity);
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
            currentHoldingRotationOffset = holdingRotationOffset;
            targetHoldingRotationOffset = holdingRotationOffset;
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
        /// P.A.S.S. Step 1: Pull the safety pin via the HUD button / keyboard shortcut (no drag
        /// gesture involved — used when the interaction didn't come from CheckPinDragGesture).
        /// Builds the same separated pin visual as the drag gesture and hands it to physics with
        /// a small default toss, rather than requiring an actual on-screen drag.
        /// </summary>
        public void PullPin(Vector2? swipeDirection = null)
        {
            if (currentPassState >= PassStepState.PinPulled) return; // idempotent: already pulled

            if (!IsGrabbed)
            {
                currentState = GrabState.Held;
            }

            currentPassState = PassStepState.PinPulled;
            Debug.Log("[FireExtinguisherGrabController] P.A.S.S. Step: PIN PULLED");
            OnPinPulled?.Invoke();
            TriggerPinPullTurn();

            GameObject pinGO = BuildSeparatedPinVisual();
            if (pinGO == null) return;

            Camera mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            Vector2 dir2D = (swipeDirection.HasValue && swipeDirection.Value.sqrMagnitude > 0.0001f)
                ? swipeDirection.Value.normalized
                : new Vector2(-1f, 0.3f);
            Vector3 tossDir = mainCam != null
                ? (mainCam.transform.right * dir2D.x + mainCam.transform.up * dir2D.y).normalized
                : new Vector3(dir2D.x, dir2D.y, 0f).normalized;

            DropPinWithPhysics(pinGO, tossDir * 0.6f);
        }

        /// <summary>
        /// Recursively finds a child transform with the given exact name (case-insensitive).
        /// </summary>
        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            return null;
        }

        /// <summary>
        /// Builds the separated pin visual (Carry_Handle, copied into its own independent
        /// GameObject, positioned/oriented exactly where it sits on the model) and hides the
        /// original on the model. Does NOT animate or attach physics — callers (drag gesture
        /// or PullPin) do that afterward. Returns null if targetExtinguisher is unset.
        /// </summary>
        private GameObject BuildSeparatedPinVisual()
        {
            if (targetExtinguisher == null) return null;

            List<Transform> pinParts = new List<Transform>();
            foreach (string partName in PinPartNames)
            {
                Transform part = FindChildByName(targetExtinguisher.transform, partName);
                if (part != null) pinParts.Add(part);
            }

            GameObject pinGO;

            if (pinParts.Count > 0)
            {
                // Anchor position/rotation on Carry_Handle (falls back to whichever part was found)
                Transform anchor = pinParts.Find(p => p.name == "Carry_Handle") ?? pinParts[0];
                pinGO = new GameObject("Separated_Pin");
                pinGO.transform.position = anchor.position;
                pinGO.transform.rotation = anchor.rotation;

                foreach (Transform part in pinParts)
                {
                    MeshFilter srcMF = part.GetComponent<MeshFilter>();
                    MeshRenderer srcMR = part.GetComponent<MeshRenderer>();
                    if (srcMF == null || srcMR == null || srcMF.sharedMesh == null) continue;

                    GameObject sub = new GameObject(part.name);
                    sub.transform.SetParent(pinGO.transform, true); // keep world position/rotation
                    sub.transform.position = part.position;
                    sub.transform.rotation = part.rotation;
                    sub.transform.localScale = part.lossyScale;

                    MeshFilter newMF = sub.AddComponent<MeshFilter>();
                    newMF.sharedMesh = srcMF.sharedMesh;
                    MeshRenderer newMR = sub.AddComponent<MeshRenderer>();
                    newMR.sharedMaterials = srcMR.sharedMaterials;

                    srcMR.enabled = false;
                    hiddenPinRenderers.Add(srcMR);
                }

                Debug.Log($"[FireExtinguisherGrabController] Hidden {pinParts.Count} original pin part(s) on model: {string.Join(", ", pinParts.ConvertAll(p => p.name))}");
            }
            else
            {
                // Named pin parts not found on this model — fall back to a procedural pin
                // positioned near the top valve assembly.
                Vector3 spawnPos = targetExtinguisher.transform.position + targetExtinguisher.transform.up * 0.28f + targetExtinguisher.transform.forward * 0.05f;
                Quaternion spawnRot = targetExtinguisher.transform.rotation;
                pinGO = CreateFallbackMetalPinGO(spawnPos, spawnRot);
                Debug.LogWarning("[FireExtinguisherGrabController] Named pin part (Carry_Handle) not found on model — using procedural fallback pin.");
            }

            separatedPin = pinGO.transform;
            pinRenderer = pinGO.GetComponentInChildren<MeshRenderer>();
            return pinGO;
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

        /// <summary>
        /// Hands the separated pin off to real physics: attaches a Rigidbody + Collider, gives it
        /// the release velocity plus a gentle tumble, and lets Unity's physics engine carry it to
        /// the floor under gravity. A safety net (SettlePinAfterPhysicsFall) freezes it once it
        /// either collides with something or falls past a maximum depth, in case no real ground
        /// collider is present nearby, then fades it out.
        /// </summary>
        private void DropPinWithPhysics(GameObject pinGO, Vector3 releaseVelocity)
        {
            if (pinGO == null) return;

            Rigidbody rb = pinGO.GetComponent<Rigidbody>();
            if (rb == null) rb = pinGO.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = releaseVelocity;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;

            SphereCollider col = pinGO.GetComponent<SphereCollider>();
            if (col == null) col = pinGO.AddComponent<SphereCollider>();
            col.radius = 0.03f;
            col.isTrigger = false;

            StartCoroutine(SettlePinAfterPhysicsFall(pinGO, pinGO.transform.position.y));
        }

        /// <summary>
        /// Waits for the falling pin to either come to rest (real collision) or drop past a
        /// maximum safety depth (no ground collider nearby), freezes it there, rests briefly,
        /// then shrinks it out and destroys it.
        /// </summary>
        private IEnumerator SettlePinAfterPhysicsFall(GameObject pinGO, float startY)
        {
            const float maxFallDistance = 1.2f;
            const float maxWaitSeconds = 3f;
            const float minFallTimeBeforeRestCheck = 0.25f;

            float elapsed = 0f;
            while (pinGO != null && elapsed < maxWaitSeconds)
            {
                elapsed += Time.deltaTime;
                Rigidbody rb = pinGO.GetComponent<Rigidbody>();
                bool cameToRest = rb != null && elapsed > minFallTimeBeforeRestCheck && rb.linearVelocity.sqrMagnitude < 0.0004f;
                bool hitSafetyDepth = (startY - pinGO.transform.position.y) >= maxFallDistance;

                if (cameToRest || hitSafetyDepth)
                {
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true;
                    }
                    break;
                }
                yield return null;
            }

            // Rest briefly, then shrink-and-fade out rather than an abrupt disappearance.
            yield return new WaitForSeconds(1.4f);
            if (pinGO == null) yield break;

            Vector3 restScale = pinGO.transform.localScale;
            float shrinkElapsed = 0f;
            const float shrinkDuration = 0.3f;
            while (shrinkElapsed < shrinkDuration)
            {
                if (pinGO == null) yield break;
                shrinkElapsed += Time.deltaTime;
                pinGO.transform.localScale = Vector3.Lerp(restScale, Vector3.zero, shrinkElapsed / shrinkDuration);
                yield return null;
            }

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
        /// Single whole-body visual cue for pin pull: treats the extinguisher's current orientation
        /// as "0°" and turns it an additional pinPullTurnAngle degrees around its own up axis. Runs
        /// independently of AimSequence — if the player squeezes the handle before this finishes,
        /// AimSequence's absolute look-at-fire rotation simply takes over from wherever this left off.
        /// </summary>
        private void TriggerPinPullTurn()
        {
            targetHoldingRotationOffset = holdingRotationOffset + postPinPullRotationOffset;
            Debug.Log($"[FireExtinguisherGrabController] 🔄 Pin pulled — smooth-rotating model by {postPinPullRotationOffset} so Hose faces FORWARD toward fire target and Handles face user.");
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

        /// <summary>
        /// Lazily caches Upper_Handle_Grip / Lever_Pivot / Squeeze_Lever and adds touch colliders
        /// to the first two (the press targets) once the FBX mesh has loaded. Retries every frame
        /// until it succeeds, same reasoning as EnsurePinTouchColliders.
        /// </summary>
        private void EnsureHandleSqueezeSetup()
        {
            if (handleSqueezeSetupDone || targetExtinguisher == null) return;

            upperHandleGripT = FindChildByName(targetExtinguisher.transform, "Upper_Handle_Grip");
            leverPivotT = FindChildByName(targetExtinguisher.transform, "Lever_Pivot");
            squeezeLeverT = FindChildByName(targetExtinguisher.transform, "Squeeze_Lever");

            if (upperHandleGripT == null || leverPivotT == null || squeezeLeverT == null) return; // model not loaded yet

            Collider a = AddTouchCollider(upperHandleGripT);
            Collider b = AddTouchCollider(leverPivotT);
            if (a != null) handleTouchColliders.Add(a);
            if (b != null) handleTouchColliders.Add(b);

            handleSqueezeSetupDone = true;
            Debug.Log("[FireExtinguisherGrabController] Handle squeeze setup complete (Upper_Handle_Grip, Lever_Pivot, Squeeze_Lever found).");
        }

        /// <summary>
        /// P.A.S.S. Step 3 (touch-driven): gas/foam only starts while the player is actively
        /// touching and holding Upper_Handle_Grip or Lever_Pivot, and only once the pin has
        /// already been pulled. Releasing stops the spray immediately.
        /// </summary>
        private void CheckHandleSqueezeTouch()
        {
            EnsureHandleSqueezeSetup();

            if (currentPassState < PassStepState.PinPulled)
            {
                if (isSqueezing) StopSqueezing();
                return;
            }

            if (!TryReadPointer(out Vector2 currentPos, out bool pressActive, out bool pressBeganThisFrame))
                return;

            if (pressBeganThisFrame)
            {
                if (ScreenPointHitsAny(currentPos, handleTouchColliders))
                {
                    StartSqueezing();
                }
                return;
            }

            if (isSqueezing && !pressActive)
            {
                StopSqueezing();
            }
        }

        /// <summary>
        /// Drives the physical press/release animation: rotates Upper_Handle_Grip and
        /// Squeeze_Lever together around Lever_Pivot toward a squeezed pose while isSqueezing is
        /// true, and springs them back to their rest pose otherwise. Applies the rotation as a
        /// per-frame delta (via RotateAround) driven by a tracked 0..1 squeeze amount, so it can
        /// never drift regardless of how many press/release cycles happen.
        /// </summary>
        private void UpdateHandleSqueezeVisual()
        {
            if (!handleSqueezeSetupDone) return;

            float targetAmount = isSqueezing ? 1f : 0f;
            float newAmount = Mathf.MoveTowards(currentSqueezeAmount, targetAmount, Time.deltaTime * handleSqueezeLerpSpeed);
            float deltaAmount = newAmount - currentSqueezeAmount;
            currentSqueezeAmount = newAmount;

            if (Mathf.Abs(deltaAmount) < 0.0001f) return;

            // Positive rotation around the model's local forward (Z) axis swings Upper_Handle_Grip
            // upward into Lever_Pivot (closing the squeeze); confirmed against live device testing
            // after the earlier in-editor check turned out backwards.
            // Squeeze_Lever intentionally does NOT rotate with it — only Upper_Handle_Grip animates.
            float deltaAngle = deltaAmount * handleSqueezeAngle;
            Vector3 axis = targetExtinguisher.transform.forward;
            Vector3 pivotPoint = leverPivotT.position;

            upperHandleGripT.RotateAround(pivotPoint, axis, deltaAngle);
        }

        /// <summary>
        /// Lazily caches Hose / Base_Ring / Lever_Pivot once the FBX mesh has loaded, and captures
        /// the hose's rest pose using renderer BOUNDS rather than transform.position — this FBX's
        /// per-part pivots are not reliably centered on their own geometry, so trusting raw
        /// transform positions for a pivot/anchor point can rotate the mesh through empty space
        /// (visibly detaching it). Bounds always reflect the actual visible mesh.
        /// Retries every frame until it succeeds, same reasoning as EnsureHandleSqueezeSetup.
        /// </summary>
        private void EnsureHoseSetup()
        {
            if (hoseSetupDone || targetExtinguisher == null) return;

            hoseT = FindChildByName(targetExtinguisher.transform, "Hose");
            baseRingT = FindChildByName(targetExtinguisher.transform, "Base_Ring");
            leverPivotForHoseT = FindChildByName(targetExtinguisher.transform, "Lever_Pivot");

            if (hoseT == null || baseRingT == null || leverPivotForHoseT == null) return; // model not loaded yet

            hoseRenderer = hoseT.GetComponent<Renderer>();
            baseRingRenderer = baseRingT.GetComponent<Renderer>();
            if (hoseRenderer == null || baseRingRenderer == null) return; // need bounds to place this correctly

            // Base_Ring is the fixed anchor the hose is always attached to (its bounds center, not
            // its raw transform.position, which may not sit on the visible ring at all).
            Vector3 anchorWorld = baseRingRenderer.bounds.center;

            // Find the hose's near end (closest to the anchor — the attached end) and far end
            // (farthest — the free end) by scanning its bounding-box corners against the anchor.
            Bounds hb = hoseRenderer.bounds;
            Vector3 nearEndWorld = anchorWorld;
            Vector3 farEndWorld = anchorWorld;
            float bestNear = float.MaxValue, bestFar = -1f;
            for (int xi = -1; xi <= 1; xi += 2)
            {
                for (int yi = -1; yi <= 1; yi += 2)
                {
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 corner = hb.center + Vector3.Scale(hb.extents, new Vector3(xi, yi, zi));
                        float d = (corner - anchorWorld).sqrMagnitude;
                        if (d < bestNear) { bestNear = d; nearEndWorld = corner; }
                        if (d > bestFar) { bestFar = d; farEndWorld = corner; }
                    }
                }
            }

            // Cache the rest direction and the hose's own pivot offset from its near/attached end,
            // both expressed in its parent's local space so they stay valid as the whole
            // extinguisher moves/rotates in the hand.
            hoseRefFrame = hoseT.parent != null ? hoseT.parent : targetExtinguisher.transform;
            Vector3 restDirWorld = farEndWorld - nearEndWorld;
            hoseRestDirLocal = restDirWorld.sqrMagnitude > 0.0001f
                ? hoseRefFrame.InverseTransformDirection(restDirWorld.normalized)
                : hoseRefFrame.InverseTransformDirection(Vector3.forward);
            hoseNearEndOffsetLocal = hoseRefFrame.InverseTransformVector(hoseT.position - nearEndWorld);
            hoseRestLocalRotation = hoseT.localRotation;
            hoseOriginalLocalPos = hoseT.localPosition;
            hoseOriginalLocalRot = hoseT.localRotation;

            hoseSetupDone = true;
            Debug.Log("[FireExtinguisherGrabController] Hose animation setup complete (bounds-based anchor at Base_Ring, target Lever_Pivot).");
        }

        /// <summary>
        /// Rebuilds Hose's world transform from scratch every frame (position AND rotation) rather
        /// than accumulating RotateAround deltas — this guarantees its near/attached end always
        /// lands exactly on Base_Ring's current position, so it can never end up visibly detached
        /// regardless of how the extinguisher is held or rotated.
        /// 1. Deploy — once the pin is pulled, blends from the rest direction to pointing straight
        ///    at Lever_Pivot (its far/free end ends up near Squeeze_Lever, which sits right next to
        ///    Lever_Pivot). Un-blends back to rest if PASS state is ever reset below PinPulled.
        /// 2. Sway — an extra small rotation layered on top of the blended direction while turning,
        ///    simulating the loose hose trailing the body's yaw, settling back to zero when still.
        /// Base_Ring itself is left untouched — it's the fixed anchor point, not an animated part.
        /// </summary>
        private void UpdateHoseAnimation()
        {
            EnsureHoseSetup();
            if (!hoseSetupDone) return;

            // 1. Advance deploy amount (0 = rest/side-clipped, 1 = fully facing Lever_Pivot)
            float deployTarget = currentPassState >= PassStepState.PinPulled ? 1f : 0f;
            currentHoseDeployAmount = Mathf.MoveTowards(currentHoseDeployAmount, deployTarget, Time.deltaTime * hoseDeployLerpSpeed);

            // 2. Advance sway offset (turn-following lag), degrees
            float currentYaw = targetExtinguisher.transform.eulerAngles.y;
            float targetSway = 0f;
            if (hasLastHeldYaw)
            {
                float yawDeltaPerFrame = Mathf.DeltaAngle(lastHeldYawDegrees, currentYaw);
                float yawSpeed = Time.deltaTime > 0f ? yawDeltaPerFrame / Time.deltaTime : 0f;
                targetSway = Mathf.Clamp(-yawSpeed * hoseSwayResponsiveness, -hoseSwayMaxAngle, hoseSwayMaxAngle);
            }
            lastHeldYawDegrees = currentYaw;
            hasLastHeldYaw = true;
            currentHoseSwayAngle = Mathf.Lerp(currentHoseSwayAngle, targetSway, Time.deltaTime * hoseSwayLerpSpeed);

            // 3. Read the anchor and Lever_Pivot fresh (bounds-based, world space) every frame.
            Vector3 anchorWorld = baseRingRenderer.bounds.center;
            Vector3 restDirWorld = hoseRefFrame.TransformDirection(hoseRestDirLocal);

            Renderer leverRenderer = leverPivotForHoseT.GetComponent<Renderer>();
            Vector3 targetPointWorld = leverRenderer != null ? leverRenderer.bounds.center : leverPivotForHoseT.position;
            Vector3 targetDirWorld = targetPointWorld - anchorWorld;
            targetDirWorld = targetDirWorld.sqrMagnitude > 0.0001f ? targetDirWorld.normalized : restDirWorld;

            // 4. Blend rest -> Lever_Pivot-facing direction, layer sway on top, then rebuild the
            //    hose's absolute world transform directly from this frame's blended direction.
            Vector3 blendedDir = Vector3.Slerp(restDirWorld, targetDirWorld, currentHoseDeployAmount);
            if (Mathf.Abs(currentHoseSwayAngle) > 0.001f)
            {
                blendedDir = Quaternion.AngleAxis(currentHoseSwayAngle, targetExtinguisher.transform.up) * blendedDir;
            }
            blendedDir.Normalize();

            Quaternion restWorldRotation = hoseRefFrame.rotation * hoseRestLocalRotation;
            Quaternion rotDelta = Quaternion.FromToRotation(restDirWorld, blendedDir);

            hoseT.rotation = rotDelta * restWorldRotation;

            Vector3 nearEndOffsetWorldRest = hoseRefFrame.TransformVector(hoseNearEndOffsetLocal);
            hoseT.position = anchorWorld + rotDelta * nearEndOffsetWorldRest;
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
                // Gas always visibly vents from Base_Ring — re-read its live world bounds center
                // every frame so the emission point tracks correctly as the extinguisher is held,
                // aimed, and turned. Falls back to nozzleTip only if Base_Ring wasn't found on this model.
                if (foamEmissionRenderer != null)
                {
                    foamParticles.transform.position = foamEmissionRenderer.bounds.center;
                }
                else if (nozzleTip != null)
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

            // Extinguish gate: player must be standing within maxSprayRange of an active fire AND
            // actively squeezing (already guaranteed by the early-return above) — a single clear
            // proximity check from the player's own position, replacing the old raycast/OverlapSphere/
            // distance three-layer fallback stack, which didn't actually require real proximity (its
            // last-resort layer matched fires up to maxSprayRange+8, i.e. up to 16m away) and was
            // fragile to aim/collider mismatches. No aim precision is required — walk close, squeeze,
            // the fire goes out.
            Vector3 playerPos = mainCam != null ? mainCam.transform.position : origin;
            var firesInScene = FindObjectsByType<GroundFireController>(FindObjectsSortMode.None);
            foreach (var fire in firesInScene)
            {
                if (fire == null || !fire.IsFireActive) continue;
                float distanceToFire = Vector3.Distance(playerPos, fire.transform.position);
                if (distanceToFire <= maxSprayRange)
                {
                    fire.ApplyFoamSuppression(fire.transform.position, Time.deltaTime);
                    fireHit = true;
                    Debug.DrawLine(origin, fire.transform.position, Color.green);
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
            targetHoldingRotationOffset = holdingRotationOffset;
            currentFoamCapacity = maxFoamCapacity;
            sweepAccumulated = 0f;

            if (foamParticles != null && foamParticles.isPlaying)
            {
                foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Re-enable the original pin parts (ring + prongs) if they were hidden
            foreach (Renderer r in hiddenPinRenderers)
            {
                if (r != null) r.enabled = true;
            }
            hiddenPinRenderers.Clear();

            // Cancel any pin drag in progress and remove its in-flight separated visual.
            isPinDragActive = false;
            if (activeDraggedPinGO != null)
            {
                Destroy(activeDraggedPinGO);
                activeDraggedPinGO = null;
            }
            pinTouchColliders.Clear(); // stale colliders from the previous extinguisher instance

            // Snap the lever back to its rest pose in one step (per-frame springs are for live
            // release, not a hard reset) so it can't end up stuck mid-squeeze after a re-grab.
            if (handleSqueezeSetupDone && upperHandleGripT != null && leverPivotT != null && currentSqueezeAmount != 0f)
            {
                float deltaAngle = -currentSqueezeAmount * handleSqueezeAngle; // undo the full accumulated rotation
                Vector3 axis = targetExtinguisher != null ? targetExtinguisher.transform.forward : Vector3.forward;
                upperHandleGripT.RotateAround(leverPivotT.position, axis, deltaAngle);
            }
            currentSqueezeAmount = 0f;
            handleSqueezeSetupDone = false; // re-discover parts next time (may be a new extinguisher instance)
            handleTouchColliders.Clear();
            upperHandleGripT = leverPivotT = squeezeLeverT = null;

            // Snap the hose back to its original authored local transform (not just "some" rest
            // pose) before clearing hoseSetupDone — otherwise, if this same instance gets re-grabbed
            // without respawning, EnsureHoseSetup would re-cache whatever mid-deploy pose the hose
            // is currently sitting in as the new "rest" pose, corrupting all future animation.
            if (hoseSetupDone && hoseT != null)
            {
                hoseT.localPosition = hoseOriginalLocalPos;
                hoseT.localRotation = hoseOriginalLocalRot;
            }
            currentHoseDeployAmount = 0f;
            currentHoseSwayAngle = 0f;
            hasLastHeldYaw = false;
            hoseSetupDone = false;
            hoseT = baseRingT = leverPivotForHoseT = null;
            hoseRenderer = baseRingRenderer = null;

            if (pinPullTurnCoroutine != null)
            {
                StopCoroutine(pinPullTurnCoroutine);
                pinPullTurnCoroutine = null;
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
