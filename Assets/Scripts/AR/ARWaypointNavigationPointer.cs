using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// AR Floor Waypoint Navigation Pointer.
    /// Projects a directional arrow on the floor that rotates to point toward
    /// a target (e.g., safety equipment station) using horizontal direction angle math.
    /// </summary>
    public class ARWaypointNavigationPointer : MonoBehaviour
    {
        public static ARWaypointNavigationPointer Instance { get; private set; }

        [Header("Pointer Configuration")]
        [SerializeField] private float showDistanceThreshold = 2.0f;
        [SerializeField] private float hideDistanceThreshold = 0.5f;
        [SerializeField] private float pointerSize = 48f;

        private Transform targetTransform;
        private Camera mainCamera;
        private bool isActive = false;

        private VisualElement pointerContainer;
        private Label pointerArrow;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Set the target transform the pointer should point toward.
        /// </summary>
        public void SetTarget(Transform target)
        {
            targetTransform = target;
            Debug.Log($"[ARWaypointNavigationPointer] Target set to: {(target != null ? target.name : "null")}");
        }

        /// <summary>
        /// Activate the navigation pointer. Call after SetTarget.
        /// </summary>
        public void Activate()
        {
            if (targetTransform == null)
            {
                Debug.LogWarning("[ARWaypointNavigationPointer] Cannot activate — no target set.");
                return;
            }

            isActive = true;
            EnsureUIElements();
            if (pointerContainer != null)
            {
                pointerContainer.style.display = DisplayStyle.Flex;
            }
            mainCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            Debug.Log("[ARWaypointNavigationPointer] Navigation pointer ACTIVATED.");
        }

        /// <summary>
        /// Deactivate and hide the navigation pointer.
        /// </summary>
        public void Deactivate()
        {
            isActive = false;
            if (pointerContainer != null)
            {
                pointerContainer.style.display = DisplayStyle.None;
            }
            Debug.Log("[ARWaypointNavigationPointer] Navigation pointer DEACTIVATED.");
        }

        /// <summary>
        /// Bind to a UIDocument root to use UI Toolkit elements for the pointer.
        /// If not called, the pointer uses a 3D world-space fallback.
        /// </summary>
        public void BindToUIDocument(VisualElement root)
        {
            if (root == null) return;

            pointerContainer = root.Q<VisualElement>("waypoint-pointer");
            pointerArrow = root.Q<Label>("waypoint-arrow");

            if (pointerContainer == null)
            {
                pointerContainer = new VisualElement();
                pointerContainer.name = "waypoint-pointer";
                pointerContainer.style.position = Position.Absolute;
                pointerContainer.style.width = pointerSize;
                pointerContainer.style.height = pointerSize;
                pointerContainer.style.display = DisplayStyle.None;
                pointerContainer.style.alignSelf = Align.Center;
                pointerContainer.style.justifyContent = Justify.Center;
                root.Add(pointerContainer);
            }

            if (pointerArrow == null)
            {
                pointerArrow = new Label();
                pointerArrow.name = "waypoint-arrow";
                pointerArrow.text = "▼";
                pointerArrow.style.fontSize = pointerSize;
                pointerArrow.style.color = new Color(0.2f, 0.6f, 1f, 0.9f);
                pointerArrow.style.unityTextAlign = TextAnchor.MiddleCenter;
                pointerArrow.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
                pointerContainer.Add(pointerArrow);
            }
        }

        private void Update()
        {
            if (!isActive || targetTransform == null || mainCamera == null) return;

            UpdatePointerPosition();
        }

        private void UpdatePointerPosition()
        {
            float distanceToTarget = Vector3.Distance(mainCamera.transform.position, targetTransform.position);

            if (distanceToTarget < hideDistanceThreshold)
            {
                if (pointerContainer != null && pointerContainer.style.display != DisplayStyle.None)
                {
                    pointerContainer.style.display = DisplayStyle.None;
                }
                return;
            }

            if (pointerContainer != null && pointerContainer.style.display == DisplayStyle.None)
            {
                pointerContainer.style.display = DisplayStyle.Flex;
            }

            Vector3 screenPos = mainCamera.WorldToViewportPoint(targetTransform.position);
            bool isBehind = screenPos.z < 0;

            if (isBehind)
            {
                screenPos.x = 1f - screenPos.x;
                screenPos.y = 0.05f;
            }

            float clampedX = Mathf.Clamp(screenPos.x, 0.08f, 0.92f);
            float clampedY = Mathf.Clamp(screenPos.y, 0.05f, 0.95f);

            if (pointerContainer != null)
            {
                pointerContainer.style.left = Length.Percent(clampedX * 100f);
                pointerContainer.style.top = Length.Percent((1f - clampedY) * 100f);
            }

            Vector3 camForward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            Vector3 targetDir = Vector3.ProjectOnPlane(targetTransform.position - mainCamera.transform.position, Vector3.up).normalized;
            float signedAngle = Vector3.SignedAngle(camForward, targetDir, Vector3.up);

            if (pointerArrow != null)
            {
#pragma warning disable CS0618
                pointerArrow.transform.rotation = Quaternion.Euler(0, 0, -signedAngle);
#pragma warning restore CS0618

                float distanceAlpha = Mathf.Clamp01((distanceToTarget - hideDistanceThreshold) / (showDistanceThreshold - hideDistanceThreshold));
                pointerArrow.style.color = new Color(0.2f, 0.6f, 1f, Mathf.Lerp(0.5f, 0.9f, distanceAlpha));
            }
        }

        private void EnsureUIElements()
        {
            if (pointerContainer != null) return;

            UIDocument uidoc = FindFirstObjectByType<UIDocument>();
            if (uidoc != null && uidoc.rootVisualElement != null)
            {
                BindToUIDocument(uidoc.rootVisualElement);
            }
        }

        [ContextMenu("Test Activate Waypoint Pointer")]
        public void TestActivateWaypointPointer()
        {
            GameObject mockTarget = GameObject.Find("MockTarget") ?? new GameObject("MockTarget");
            mockTarget.transform.position = transform.position + transform.forward * 3.0f + transform.right * 1.5f;
            SetTarget(mockTarget.transform);
            Activate();
        }

        [ContextMenu("Test Deactivate Waypoint Pointer")]
        public void TestDeactivateWaypointPointer()
        {
            Deactivate();
        }
    }
}
