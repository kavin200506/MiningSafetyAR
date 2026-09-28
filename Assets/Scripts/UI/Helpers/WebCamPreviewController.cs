using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

using MiningSafetyAR.AR;

namespace MiningSafetyAR.UI.Helpers
{
    /// <summary>
    /// Manages a live front-facing WebCamTexture and presents it as the background of a UI Toolkit
    /// VisualElement (e.g. the circular viewport in FaceScannerOverlay.uxml). Owns the camera device
    /// lifecycle (permission request, open, pause, stop) independently of any face-detection pipeline —
    /// this is display-only; it does not run inference or read pixel data for anything but rendering.
    ///
    /// Usage: AddComponent this (or place it in the same GameObject as the page controller that owns
    /// the scanner UI), call PlayCamera() when the scanner screen opens, AttachToElement(viewport) once
    /// the camera is ready (or any time — it will pick up the feed as soon as PlayCamera() succeeds),
    /// and StopCamera() when the screen closes. OnDisable/OnDestroy also call StopCamera() defensively
    /// so the camera hardware is never left locked if the caller forgets.
    /// </summary>
    public class WebCamPreviewController : MonoBehaviour
    {
        /// <summary>How the (usually non-square) camera feed is fit into the target VisualElement's box.</summary>
        public enum BackgroundFitMode
        {
            /// <summary>Fill the element, cropping overflow — no letterboxing, no stretching. Default.</summary>
            Cover,
            /// <summary>Fit entirely inside the element, letterboxing if the aspect ratios differ.</summary>
            Contain,
            /// <summary>Stretch to exactly fill the element, ignoring aspect ratio. Rarely what you want.</summary>
            StretchToFill,
        }

        [Header("Camera Selection & Setup")]
        [Tooltip("Requested capture width. The platform camera driver may return a different actual resolution — always read webCamTexture.width/height, never assume this value.")]
        [SerializeField] int requestedWidth = 1280;
        [Tooltip("Requested capture height.")]
        [SerializeField] int requestedHeight = 720;
        [Tooltip("Requested capture frame rate.")]
        [SerializeField] int requestedFPS = 30;

        [Header("Timeouts")]
        [Tooltip("How long to wait for the user to respond to the OS camera-permission prompt before giving up.")]
        [SerializeField] float permissionTimeoutSeconds = 10f;
        [Tooltip("How long to wait for the camera driver to start producing frames (webCamTexture.width > 16) before giving up.")]
        [SerializeField] float cameraInitTimeoutSeconds = 5f;

        [Header("Rendering")]
        [SerializeField] BackgroundFitMode fitMode = BackgroundFitMode.Cover;
        [Tooltip("WebCamTexture.videoRotationAngle's sign relative to UI Toolkit's clockwise-positive Rotate.")]
        [SerializeField] bool invertRotationDirection = false;
        [Tooltip("Mirror feed horizontally (scaleX = -1) so front camera acts like a selfie mirror.")]
        [SerializeField] bool mirrorHorizontally = true;
        [Tooltip("Invert feed vertically (scaleY = -1) if preview appears upside down.")]
        [SerializeField] bool invertVertically = false;

        WebCamTexture webCamTexture;
        RenderTexture previewRenderTexture;
        VisualElement attachedElement;
        Coroutine playRoutine;

        /// <summary>Raised once the camera has successfully started producing frames.</summary>
        public event Action OnCameraStarted;

        /// <summary>
        /// Raised whenever starting the camera fails, with a short machine-readable reason:
        /// "camera_permission_denied", "no_camera_device", "webcamtexture_construction_failed",
        /// or "camera_init_timeout".
        /// </summary>
        public event Action<string> OnCameraError;

        /// <summary>The underlying WebCamTexture, or null if the camera has never been started / has been stopped.</summary>
        public WebCamTexture ActiveTexture => webCamTexture;

        /// <summary>True once the camera is actively playing and has produced at least one real frame.</summary>
        public bool IsReady => webCamTexture != null && webCamTexture.isPlaying && webCamTexture.width > 16;

        void OnDisable() => StopCamera();

        void OnDestroy() => StopCamera();

        void Update()
        {
            if (webCamTexture == null || !webCamTexture.isPlaying) return;
            if (!webCamTexture.didUpdateThisFrame) return;

            EnsurePreviewRenderTexture();
            if (previewRenderTexture == null) return;

            Graphics.Blit(webCamTexture, previewRenderTexture);

            if (attachedElement != null)
            {
                if (attachedElement.style.backgroundImage.value.renderTexture != previewRenderTexture)
                {
                    attachedElement.style.backgroundImage = Background.FromRenderTexture(previewRenderTexture);
                }
                ApplyOrientationCorrection(attachedElement);
            }
        }

        // ================================================================
        // PUBLIC API — LIFECYCLE
        // ================================================================

        /// <summary>
        /// Starts (or resumes) the camera: verifies/requests OS camera permission, selects a
        /// front-facing device (falling back to device index 0 if none is reported as front-facing),
        /// opens WebCamTexture at the requested resolution/FPS, and waits for the first real frame.
        /// Safe to call again while already starting or already playing — both are no-ops.
        /// Failures are reported via OnCameraError rather than thrown, since camera availability is
        /// inherently environment-dependent (permissions, hardware, concurrent camera use elsewhere).
        /// </summary>
        public void PlayCamera()
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                Debug.Log("[INFO] [WebCamPreviewController] PlayCamera called but camera is already playing.");
                return;
            }

            if (playRoutine != null)
            {
                Debug.Log("[INFO] [WebCamPreviewController] PlayCamera called while a start sequence is already in progress — ignoring duplicate call.");
                return;
            }

            playRoutine = StartCoroutine(PlayCameraRoutine());
        }

        /// <summary>Pauses the camera feed without releasing the device (cheap to resume with PlayCamera()).</summary>
        public void PauseCamera()
        {
            if (webCamTexture == null)
            {
                Debug.Log("[INFO] [WebCamPreviewController] PauseCamera called but no camera has been started.");
                return;
            }
            if (!webCamTexture.isPlaying)
            {
                Debug.Log("[INFO] [WebCamPreviewController] PauseCamera called but camera isn't currently playing.");
                return;
            }

            webCamTexture.Pause();
            Debug.Log("[INFO] [WebCamPreviewController] Camera paused.");
        }

        /// <summary>
        /// Stops and fully releases the camera device and its preview RenderTexture, and detaches from
        /// any attached VisualElement. Safe to call multiple times / when nothing is running. Always
        /// called from OnDisable() and OnDestroy() so the mobile camera hardware is never left locked.
        /// </summary>
        public void StopCamera()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (webCamTexture != null)
            {
                try
                {
                    webCamTexture.Stop();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WARN] [WebCamPreviewController] Exception while stopping WebCamTexture (continuing cleanup): {ex.Message}");
                }
                Destroy(webCamTexture);
                webCamTexture = null;
                Debug.Log("[INFO] [WebCamPreviewController] Camera stopped and released.");
            }

            if (previewRenderTexture != null)
            {
                previewRenderTexture.Release();
                Destroy(previewRenderTexture);
                previewRenderTexture = null;
            }

            DetachFromElement();
        }

        // ================================================================
        // PUBLIC API — UI TOOLKIT RENDERING
        // ================================================================

        /// <summary>
        /// Presents the live camera feed as `element`'s background image. Safe to call before the
        /// camera has finished starting — the background is (re)assigned as soon as PlayCamera()
        /// produces a real frame; orientation/fit styling is applied immediately either way. Calling
        /// this again with a different element switches the preview to the new target and clears the
        /// previous one's background.
        /// </summary>
        public void AttachToElement(VisualElement element)
        {
            if (element == null)
            {
                Debug.LogWarning("[WARN] [WebCamPreviewController] AttachToElement called with a null VisualElement.");
                return;
            }

            if (attachedElement != null && attachedElement != element)
            {
                ClearElementStyle(attachedElement);
            }

            attachedElement = element;
            ApplyFitMode(attachedElement);

            EnsurePreviewRenderTexture();
            if (previewRenderTexture != null)
            {
                attachedElement.style.backgroundImage = Background.FromRenderTexture(previewRenderTexture);
                ApplyOrientationCorrection(attachedElement);
            }

            Debug.Log($"[INFO] [WebCamPreviewController] Attached camera preview to VisualElement '{element.name}'.");
        }

        /// <summary>Detaches the preview from whichever VisualElement it's currently attached to, clearing its background/transform styling.</summary>
        public void DetachFromElement()
        {
            if (attachedElement == null) return;
            ClearElementStyle(attachedElement);
            attachedElement = null;
        }

        static void ClearElementStyle(VisualElement element)
        {
            element.style.backgroundImage = StyleKeyword.Null;
            element.style.rotate = StyleKeyword.Null;
            element.style.scale = StyleKeyword.Null;
        }

        // ================================================================
        // INTERNAL — START SEQUENCE
        // ================================================================

        IEnumerator PlayCameraRoutine()
        {
            bool permissionGranted = false;
            yield return EnsurePermission(granted => permissionGranted = granted);

            if (!permissionGranted)
            {
                Debug.LogWarning("[WARN] [WebCamPreviewController] Camera permission denied or unavailable — cannot start preview.");
                OnCameraError?.Invoke("camera_permission_denied");
                playRoutine = null;
                yield break;
            }

            if (webCamTexture == null)
            {
                string deviceName = SelectFrontFacingDeviceName(out bool anyDeviceFound);
                if (!anyDeviceFound)
                {
                    Debug.LogError("[ERROR] [WebCamPreviewController] No camera devices reported by WebCamTexture.devices.");
                    OnCameraError?.Invoke("no_camera_device");
                    playRoutine = null;
                    yield break;
                }

                try
                {
                    webCamTexture = !string.IsNullOrEmpty(deviceName)
                        ? new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFPS)
                        : new WebCamTexture(requestedWidth, requestedHeight, requestedFPS);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ERROR] [WebCamPreviewController] Failed to construct WebCamTexture: {ex.Message}");
                    OnCameraError?.Invoke("webcamtexture_construction_failed");
                    playRoutine = null;
                    yield break;
                }
            }

            webCamTexture.Play();

            float elapsed = 0f;
            while (webCamTexture.width <= 16 && elapsed < cameraInitTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (webCamTexture.width <= 16)
            {
                Debug.LogError($"[ERROR] [WebCamPreviewController] Camera failed to produce frames within {cameraInitTimeoutSeconds}s timeout.");
                OnCameraError?.Invoke("camera_init_timeout");
                playRoutine = null;
                yield break;
            }

            Debug.Log($"[INFO] [WebCamPreviewController] Camera ready: {webCamTexture.deviceName} ({webCamTexture.width}x{webCamTexture.height} @ requested {requestedFPS}fps).");

            EnsurePreviewRenderTexture();
            if (attachedElement != null && previewRenderTexture != null)
            {
                attachedElement.style.backgroundImage = Background.FromRenderTexture(previewRenderTexture);
                ApplyOrientationCorrection(attachedElement);
            }

            playRoutine = null;
            OnCameraStarted?.Invoke();
        }

        /// <summary>
        /// Picks a front-facing device name from WebCamTexture.devices, falling back to device index 0
        /// if none report isFrontFacing == true (some platforms/devices don't populate this flag
        /// reliably). Returns null (meaning "let Unity choose its default device") only if the device
        /// list itself could not be read as a single usable name; `anyDeviceFound` reports whether any
        /// camera exists at all.
        /// </summary>
        static string SelectFrontFacingDeviceName(out bool anyDeviceFound)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                anyDeviceFound = false;
                return null;
            }

            anyDeviceFound = true;

            foreach (var device in devices)
            {
                if (device.isFrontFacing)
                {
                    Debug.Log($"[INFO] [WebCamPreviewController] Selected front-facing camera device '{device.name}'.");
                    return device.name;
                }
            }

            Debug.LogWarning($"[WARN] [WebCamPreviewController] No device reported isFrontFacing == true — falling back to default device index 0 ('{devices[0].name}').");
            return devices[0].name;
        }

        /// <summary>
        /// Verifies (and if needed, requests) camera permission for the current platform. Android goes
        /// through the runtime-permission system via AndroidCameraPermissionHelper (spawning one if none
        /// exists in the scene); every other platform (including iOS) uses the cross-platform
        /// Application.HasUserAuthorization / RequestUserAuthorization(UserAuthorization.WebCam) API.
        /// </summary>
        IEnumerator EnsurePermission(Action<bool> callback)
        {
#if UNITY_ANDROID
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                callback?.Invoke(true);
                yield break;
            }

            var helper = FindFirstObjectByType<AndroidCameraPermissionHelper>();
            if (helper == null)
            {
                // AddComponent() runs Awake() synchronously, which already calls RequestCameraPermission().
                helper = new GameObject("AndroidCameraPermissionHelper_Auto").AddComponent<AndroidCameraPermissionHelper>();
            }
            else
            {
                helper.RequestCameraPermission();
            }

            float elapsed = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && elapsed < permissionTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            bool granted = Permission.HasUserAuthorizedPermission(Permission.Camera);
            if (!granted)
                Debug.LogWarning($"[WARN] [WebCamPreviewController] Android CAMERA permission not granted within {permissionTimeoutSeconds}s.");
            callback?.Invoke(granted);
#else
            if (Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                callback?.Invoke(true);
                yield break;
            }

            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            bool granted = Application.HasUserAuthorization(UserAuthorization.WebCam);
            if (!granted)
                Debug.LogWarning("[WARN] [WebCamPreviewController] WebCam user authorization was not granted.");
            callback?.Invoke(granted);
#endif
        }

        // ================================================================
        // INTERNAL — RENDERING
        // ================================================================

        /// <summary>(Re)creates previewRenderTexture to match the current WebCamTexture's actual (not requested) resolution.</summary>
        void EnsurePreviewRenderTexture()
        {
            if (webCamTexture == null || webCamTexture.width <= 16) return;

            if (previewRenderTexture != null &&
                (previewRenderTexture.width != webCamTexture.width || previewRenderTexture.height != webCamTexture.height))
            {
                previewRenderTexture.Release();
                Destroy(previewRenderTexture);
                previewRenderTexture = null;
            }

            if (previewRenderTexture == null)
            {
                previewRenderTexture = new RenderTexture(webCamTexture.width, webCamTexture.height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "WebCamPreviewController_PreviewRT"
                };
                previewRenderTexture.Create();
            }
        }

        void ApplyFitMode(VisualElement element)
        {
            switch (fitMode)
            {
                case BackgroundFitMode.Contain:
                    element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
                    break;
                case BackgroundFitMode.StretchToFill:
                    element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(Length.Percent(100), Length.Percent(100)));
                    break;
                case BackgroundFitMode.Cover:
                default:
                    element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
                    break;
            }
        }

        /// <summary>
        /// Rotates/flips the attached element so the feed reads upright despite the sensor's native
        /// orientation. videoRotationAngle is the degrees the raw frame needs rotating to appear
        /// upright. On Android device builds, Graphics.Blit into RenderTexture handles native GLES Y-flip,
        /// so scaleY is normalized upright without double-inverting.
        /// </summary>
        void ApplyOrientationCorrection(VisualElement element)
        {
            if (element == null || webCamTexture == null) return;

            // Ensure transform origin is center (50%, 50%) so scale and rotation pivot in the middle
            element.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(50), Length.Percent(50)));

            // Rotate based on videoRotationAngle
            float sign = invertRotationDirection ? 1f : -1f;
            float angle = sign * webCamTexture.videoRotationAngle;
            element.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));

            // Scale calculations:
            // 1. Horizontal mirror: front camera previews should mirror horizontally (scaleX = -1)
            float scaleX = mirrorHorizontally ? -1f : 1f;

            // 2. Vertical flip:
            // On Android device builds, Graphics.Blit from native camera texture to RenderTexture
            // already flips the Y axis due to GLES/Vulkan texture coordinate differences.
            // Applying webCamTexture.videoVerticallyMirrored ? -1 : 1 results in a double-flip (upside down).
#if UNITY_ANDROID && !UNITY_EDITOR
            float scaleY = invertVertically ? -1f : 1f;
#else
            bool defaultFlipY = webCamTexture.videoVerticallyMirrored;
            float scaleY = (defaultFlipY ^ invertVertically) ? -1f : 1f;
#endif

            element.style.scale = new StyleScale(new Scale(new Vector3(scaleX, scaleY, 1f)));
        }
    }
}
