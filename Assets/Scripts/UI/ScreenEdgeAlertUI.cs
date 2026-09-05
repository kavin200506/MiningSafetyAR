using UnityEngine;
using UnityEngine.UI;

namespace MiningSafetyAR.UI
{
    /// <summary>
    /// Full-screen red alert overlay confined to the screen edges (center stays clear so the
    /// AR view underneath is never obscured). Toggled on/off by the alarm button; pulses
    /// while active to read as an alarm rather than a static tint.
    /// Self-creates its Canvas/Image the first time it's needed, so no manual scene wiring
    /// is required. Includes procedural sprite fallback if custom shader is stripped at build time.
    /// </summary>
    public class ScreenEdgeAlertUI : MonoBehaviour
    {
        private static ScreenEdgeAlertUI _instance;
        public static ScreenEdgeAlertUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ScreenEdgeAlertUI>();
                    if (_instance == null)
                    {
                        var go = new GameObject("ScreenEdgeAlertUI");
                        _instance = go.AddComponent<ScreenEdgeAlertUI>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _ = Instance;
        }

        [SerializeField] private Color edgeColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private float edgeWidth = 0.28f;
        [SerializeField] private float pulseSpeed = 2.5f;
        [SerializeField] private float minIntensity = 0.35f;
        [SerializeField] private float maxIntensity = 0.85f;

        private GameObject canvasGO;
        private Image overlayImage;
        private Material overlayMaterial;
        private bool isActive;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            BuildOverlay();
        }

        private void BuildOverlay()
        {
            if (canvasGO != null) return;

            canvasGO = new GameObject("ScreenEdgeAlertCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var raycaster = canvasGO.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false; // overlay must never block AR touch input

            var imageGO = new GameObject("EdgeVignetteImage", typeof(Image));
            imageGO.transform.SetParent(canvasGO.transform, false);
            var rect = imageGO.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = imageGO.GetComponent<Image>();
            overlayImage.raycastTarget = false;

            Shader shader = Shader.Find("MiningSafetyAR/ScreenEdgeVignette");
            if (shader != null)
            {
                overlayMaterial = new Material(shader);
                overlayImage.material = overlayMaterial;
                overlayImage.color = Color.white;
                overlayMaterial.SetColor(ColorId, edgeColor);
                overlayMaterial.SetFloat(EdgeWidthId, edgeWidth);
                overlayMaterial.SetFloat(IntensityId, 0f);
            }
            else
            {
                Debug.LogWarning("[WARN] [ScreenEdgeAlertUI] Custom shader 'MiningSafetyAR/ScreenEdgeVignette' not found; using procedural vignette sprite fallback.");
                overlayImage.sprite = CreateProceduralVignetteSprite();
                overlayImage.type = Image.Type.Simple;
                overlayImage.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);
            }

            canvasGO.SetActive(false);
        }

        private Sprite CreateProceduralVignetteSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float v = (float)y / (size - 1);
                    float distX = Mathf.Min(u, 1f - u);
                    float distY = Mathf.Min(v, 1f - v);
                    float dist = Mathf.Min(distX, distY);
                    float edge = 1f - Mathf.Clamp01(dist / edgeWidth);
                    edge = edge * edge; // smooth falloff toward center
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public void SetAlertActive(bool active)
        {
            isActive = active;
            if (canvasGO != null)
            {
                canvasGO.SetActive(active);
            }
        }

        private void Update()
        {
            if (!isActive) return;

            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

            if (overlayMaterial != null)
            {
                overlayMaterial.SetFloat(IntensityId, intensity);
            }
            else if (overlayImage != null)
            {
                Color c = edgeColor;
                c.a = intensity;
                overlayImage.color = c;
            }
        }
    }
}
