using System;
using System.Collections;
using UnityEngine;

using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Helpers
{
    /// <summary>
    /// Orchestrates one face-verification or face-enrollment attempt across three independently-built
    /// pieces: WebCamPreviewController (cosmetic live preview in the scanner UI), FaceScannerUIController
    /// (visual state machine / animations), and FaceVerificationService (the actual on-device capture +
    /// BlazeFace detection + MobileFaceNet embedding + Firestore read/write pipeline).
    ///
    /// ARCHITECTURE NOTE — read before changing this file:
    /// Neither FaceVerificationService.VerifyFace(uid, Action&lt;FaceVerificationResult&gt;) nor
    /// EnrollFace(uid, Action&lt;bool,string&gt;) accepts a pre-captured/cropped frame — there is no such
    /// overload on either. Both own and manage their own private WebCamTexture internally (see
    /// FaceVerificationService.EnsureCamera/CaptureFaceWithLiveness) and crop around the ML-DETECTED face
    /// box (via BlazeFace), not around wherever the UI circle happens to be. So this bridge does NOT
    /// sample/crop WebCamPreviewController's frames itself — it calls VerifyFace()/EnrollFace() as-is and
    /// lets FaceVerificationService run its own capture pipeline. Feeding it an externally-cropped frame
    /// would need a new public overload added to FaceVerificationService, out of this file's scope, and
    /// would bypass its (more accurate) detected-face crop.
    ///
    /// This also means FaceVerificationService opens its OWN camera session (480x640, its own
    /// cameraRequestWidth/Height fields) independently of WebCamPreviewController's live-preview session
    /// (1280x720). Running two concurrent WebCamTexture sessions against the same physical front camera
    /// has NOT been verified on real hardware in this codebase, and most mobile camera stacks only
    /// support one exclusive session per device. As a mitigation (not a proven fix), this bridge PAUSES
    /// the preview right before calling VerifyFace()/EnrollFace() and resumes it once the result returns,
    /// so the two sessions' actual "playing" windows don't overlap. If dual-camera access still misbehaves
    /// on a target device, the real fix is exposing FaceVerificationService's internal texture so
    /// WebCamPreviewController can preview THAT instead of opening a second session — not something this
    /// file does on its own.
    /// </summary>
    public class FaceVerificationBridge : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("Optional — auto-discovered via GetComponent if left empty. Can also be set via Configure().")]
        [SerializeField] WebCamPreviewController webCamPreview;

        [Header("Timeouts")]
        [Tooltip("How long to wait for the live preview to report IsReady before proceeding anyway (the actual operation does not depend on the preview being ready).")]
        [SerializeField] float previewStartupTimeoutSeconds = 5f;
        [Tooltip("Hard ceiling on the actual face-scan window, independent of FaceVerificationService's own internal capture-window timeout. If its callback never fires for any reason, this forces a failure instead of hanging the UI forever.")]
        [SerializeField] float maxScanWindowSeconds = 10f;

        // FaceScannerUIController is a plain class (not a Component — see ToggleSwitchController for the
        // same pattern elsewhere in this codebase), so it can't be a [SerializeField]; it's supplied via
        // Configure() by whichever page controller constructed it against the overlay's rootVisualElement.
        FaceScannerUIController scannerUI;

        Coroutine activeRoutine;

        void Awake()
        {
            if (webCamPreview == null) webCamPreview = GetComponent<WebCamPreviewController>();
        }

        void OnDisable()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
        }

        /// <summary>Supplies (or replaces) the collaborators this bridge drives. Call before StartVerification()/StartEnrollment().</summary>
        public void Configure(WebCamPreviewController preview, FaceScannerUIController ui)
        {
            webCamPreview = preview;
            scannerUI = ui;
        }

        // ================================================================
        // PUBLIC API
        // ================================================================

        /// <summary>
        /// Runs one 1:1 verification attempt against `firebaseUid`'s already-enrolled embedding: ensures
        /// the preview camera is playing, shows the Scanning state, calls
        /// FaceVerificationService.VerifyFace(), and drives the scanner UI's success/failure presentation
        /// from the result. `onComplete` fires exactly once per call with (true, "verified") on success or
        /// (false, reasonCode) on failure/timeout — reasonCode matches FaceVerificationResult.failureReason
        /// (e.g. "no_face_detected", "similarity_below_threshold") or one of this bridge's own codes
        /// ("scan_timeout", "missing_uid", "service_unavailable"), so callers that need localized/
        /// page-specific messaging (see ModuleDetailPageController.ShowFailure for the existing pattern)
        /// can switch on it themselves. A tap on "Retry Scan" re-invokes this same method with the same
        /// uid/onComplete, so a retry produces its own fresh (true/false, ...) resolution independent of
        /// the attempt that failed.
        /// </summary>
        public void StartVerification(string firebaseUid, Action<bool, string> onComplete)
        {
            RunFaceOperation(firebaseUid, isEnrollment: false, onComplete);
        }

        /// <summary>
        /// Runs one enrollment attempt for `firebaseUid`: same orchestration as StartVerification (preview,
        /// Scanning state, timeout, success/failure UI, Retry wiring), but calls
        /// FaceVerificationService.EnrollFace() to capture and STORE a new embedding rather than compare
        /// against an existing one. Requires consent to already be on record (EnrollFace itself refuses
        /// with "consent_required" otherwise — call FaceVerificationService.RecordConsent() first).
        /// `onComplete` fires with (true, "enrolled") on success.
        /// </summary>
        public void StartEnrollment(string firebaseUid, Action<bool, string> onComplete)
        {
            RunFaceOperation(firebaseUid, isEnrollment: true, onComplete);
        }

        // ================================================================
        // INTERNAL
        // ================================================================

        void RunFaceOperation(string firebaseUid, bool isEnrollment, Action<bool, string> onComplete)
        {
            string opName = isEnrollment ? "StartEnrollment" : "StartVerification";

            if (string.IsNullOrEmpty(firebaseUid))
            {
                Debug.LogWarning($"[WARN] [FaceVerificationBridge] {opName} called with a null/empty firebaseUid.");
                onComplete?.Invoke(false, "missing_uid");
                return;
            }

            if (FaceVerificationService.Instance == null)
            {
                Debug.LogError($"[ERROR] [FaceVerificationBridge] FaceVerificationService.Instance is null — cannot {(isEnrollment ? "enroll" : "verify")}.");
                onComplete?.Invoke(false, "service_unavailable");
                return;
            }

            if (scannerUI == null)
            {
                Debug.LogWarning($"[WARN] [FaceVerificationBridge] No FaceScannerUIController configured (call Configure() first) — proceeding without visual feedback.");
            }

            if (activeRoutine != null)
            {
                Debug.LogWarning($"[WARN] [FaceVerificationBridge] {opName} called while a scan is already in progress — ignoring duplicate call.");
                return;
            }

            activeRoutine = StartCoroutine(FaceOperationRoutine(firebaseUid, isEnrollment, onComplete));
        }

        IEnumerator FaceOperationRoutine(string firebaseUid, bool isEnrollment, Action<bool, string> onComplete)
        {
            // 1. Bring up the cosmetic live preview so the user sees themselves in the scanner circle.
            //    The actual operation does not depend on this — FaceVerificationService uses its own
            //    camera session regardless — so a slow/failed preview never blocks the real scan.
            if (webCamPreview != null && !webCamPreview.IsReady)
            {
                webCamPreview.PlayCamera();
                float previewElapsed = 0f;
                while (!webCamPreview.IsReady && previewElapsed < previewStartupTimeoutSeconds)
                {
                    previewElapsed += Time.deltaTime;
                    yield return null;
                }
                if (!webCamPreview.IsReady)
                {
                    Debug.LogWarning($"[WARN] [FaceVerificationBridge] Live preview not ready after {previewStartupTimeoutSeconds}s — continuing without it (the operation uses its own camera session).");
                }
            }

            scannerUI?.SetState(FaceScannerUIController.ScannerState.Scanning);

            if (FaceVerificationService.Instance != null && webCamPreview != null)
            {
                FaceVerificationService.Instance.ExternalWebCamTexture = webCamPreview.ActiveTexture;
            }

            bool opDone = false;
            bool opPassed = false;
            string opReason = null;

            if (isEnrollment)
            {
                FaceVerificationService.Instance.EnrollFace(firebaseUid, (ok, resp) =>
                {
                    opPassed = ok;
                    opReason = resp;
                    opDone = true;
                });
            }
            else
            {
                FaceVerificationService.Instance.VerifyFace(firebaseUid, r =>
                {
                    opPassed = r != null && r.passed;
                    opReason = r != null ? r.failureReason : "unknown_error";
                    opDone = true;
                });
            }

            float elapsed = 0f;
            while (!opDone && elapsed < maxScanWindowSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Resume the live preview regardless of outcome — leaving it paused would look broken on
            // a retry (and while the Success/Failure state is shown).
            webCamPreview?.PlayCamera();

            if (!opDone)
            {
                // NOTE: FaceVerificationService's own EnrollFaceRoutine/VerifyFaceRoutine keeps running
                // after this point — there is no cancellation API for either — and its callback may still
                // fire later into the now-abandoned closure above, which is harmless (nothing reads it
                // once this coroutine has moved on). This bridge's own maxScanWindowSeconds (10s)
                // comfortably exceeds FaceVerificationService's own expected worst case (~3s camera init +
                // ~4s capture window by its own defaults), so hitting this path at all should be rare; if
                // it does happen and the user retries immediately, a second Enroll/VerifyFace call could
                // overlap with the still-running first one inside FaceVerificationService, which has no
                // internal busy-guard against that — a genuine (if unlikely) edge case that would need a
                // fix inside FaceVerificationService itself, not here.
                Debug.LogWarning($"[WARN] [FaceVerificationBridge] {(isEnrollment ? "Enrollment" : "Verification")} for worker {firebaseUid} did not complete within the {maxScanWindowSeconds}s scan window — forcing a timeout failure.");
                FailAndOfferRetry("scan_timeout", firebaseUid, isEnrollment, onComplete);
                activeRoutine = null;
                yield break;
            }

            if (opPassed)
            {
                Debug.Log($"[INFO] [FaceVerificationBridge] {(isEnrollment ? "Enrollment" : "Verification")} succeeded for worker {firebaseUid}.");
                string successReason = isEnrollment ? "enrolled" : "verified";
                if (scannerUI != null)
                {
                    scannerUI.PlaySuccessAnimation(() => onComplete?.Invoke(true, successReason));
                }
                else
                {
                    onComplete?.Invoke(true, successReason);
                }
            }
            else
            {
                string reason = string.IsNullOrEmpty(opReason) ? "unknown_error" : opReason;
                Debug.LogWarning($"[WARN] [FaceVerificationBridge] {(isEnrollment ? "Enrollment" : "Verification")} failed for worker {firebaseUid}: reason={reason}.");
                FailAndOfferRetry(reason, firebaseUid, isEnrollment, onComplete);
            }

            activeRoutine = null;
        }

        void FailAndOfferRetry(string reasonCode, string firebaseUid, bool isEnrollment, Action<bool, string> onComplete)
        {
            if (scannerUI != null)
            {
                scannerUI.PlayFailureAnimation(
                    DescribeFailureReason(reasonCode),
                    () => RunFaceOperation(firebaseUid, isEnrollment, onComplete),
                    () => onComplete?.Invoke(false, reasonCode)
                );
            }
            else
            {
                onComplete?.Invoke(false, reasonCode);
            }
        }

        /// <summary>
        /// English-only fallback message for immediate UI display. Deliberately NOT a full localized
        /// mapping — FaceConsentPageController and ModuleDetailPageController already each maintain their
        /// own LanguageManager-driven EN/HI/SAT/TA mapping for these same reason codes; duplicating that
        /// a third time here would be its own maintenance problem. Callers that need localized messaging
        /// should switch on the raw reasonCode passed to `onComplete` instead of this text.
        /// </summary>
        static string DescribeFailureReason(string reasonCode) => reasonCode switch
        {
            "no_face_detected" => "No face detected. Make sure your face is clearly visible and try again.",
            "multiple_faces_detected" => "More than one face detected. Make sure you're alone in frame and try again.",
            "similarity_below_threshold" => "Face did not match your enrolled profile.",
            "camera_unavailable" => "Could not access the camera. Check camera permissions and try again.",
            "capture_timed_out" => "Couldn't get a clear, steady look at your face in time. Try again.",
            "scan_timeout" => "This took too long. Please try again.",
            "not_enrolled" => "No face profile on record. Please complete enrollment first.",
            "model_version_mismatch_reenrollment_required" => "Your face profile needs to be re-enrolled.",
            "models_not_configured" or "anchors_not_configured" => "Face verification isn't set up on this device yet.",
            "consent_required" => "Consent is required before your face can be registered.",
            "embedding_failed" => "Couldn't process your face clearly. Please try again.",
            "service_unavailable" => "Face verification is unavailable right now.",
            "missing_uid" => "Missing worker account — please sign in again.",
            _ => "Something went wrong. Please try again.",
        };
    }
}
