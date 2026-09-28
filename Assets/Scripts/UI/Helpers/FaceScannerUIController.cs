using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiningSafetyAR.UI.Helpers
{
    /// <summary>
    /// Drives the visual state machine and micro-interactions for the Face Scanner overlay
    /// (FaceScannerOverlay.uxml / FaceScannerOverlay.uss). Pure UI-state logic — it never touches
    /// WebCamPreviewController or FaceVerificationService directly; the owning page controller is
    /// responsible for orchestrating the camera/inference flow and calling SetState/PlaySuccessAnimation/
    /// PlayFailureAnimation in response to what that flow reports.
    ///
    /// Usage: `var scannerUI = new FaceScannerUIController(root);` once the overlay's rootVisualElement
    /// is available, then drive it with SetState()/PlaySuccessAnimation()/PlayFailureAnimation() as the
    /// camera/inference flow progresses. Call Dispose() when the overlay closes (from the owning page's
    /// OnPageExit/OnDisable) to stop any in-flight scheduled animations and unregister click handlers.
    /// </summary>
    public class FaceScannerUIController
    {
        public enum ScannerState
        {
            /// <summary>Default state before scanning starts — prompts the user to align their face.</summary>
            Idle,
            /// <summary>Actively sampling camera frames — pulsing cyan ring, icons hidden.</summary>
            Scanning,
            /// <summary>Verification passed — green ring, checkmark pop-in, auto-proceeds after a short delay.</summary>
            Success,
            /// <summary>Verification failed — red ring, cross pop-in, shows a reason and a Retry button.</summary>
            Failure,
        }

        // Mirrors the state class names defined in FaceScannerOverlay.uss, applied to #scanner-circle-wrap.
        const string StateScanningClass = "scanner-state-scanning";
        const string StateSuccessClass = "scanner-state-success";
        const string StateFailureClass = "scanner-state-failure";
        static readonly string[] AllStateClasses = { StateScanningClass, StateSuccessClass, StateFailureClass };

        const string IconVisibleClass = "scanner-status-icon--visible";
        const string PulseClass = "scanner-pulse";

        const string CheckmarkGlyph = "✔"; // ✔ HEAVY CHECK MARK
        const string CrossGlyph = "✖";      // ✖ HEAVY MULTIPLICATION X

        const long PulseIntervalMs = 900;
        const long ShakeStepMs = 55;
        const long SuccessAutoProceedDelayMs = 800;
        static readonly float[] ShakeOffsetsPx = { -10f, 8f, -6f, 6f, -3f, 0f };

        readonly VisualElement circleWrap;  // #scanner-circle-wrap — state classes are applied here (see FaceScannerOverlay.uss)
        readonly VisualElement viewport;    // #scanner-viewport — shaken on Failure
        readonly VisualElement statusIcon;  // #scanner-status-icon
        readonly Label statusIconLabel;     // #scanner-status-icon-label
        readonly Label statusText;          // #scanner-status-text
        readonly Button retryButton;        // #scanner-retry-btn
        readonly Button cancelButton;       // #scanner-cancel-btn

        IVisualElementScheduledItem pulseSchedule;
        IVisualElementScheduledItem shakeSchedule;
        IVisualElementScheduledItem autoProceedSchedule;
        Action pendingRetryHandler;
        bool pulseOn;
        int shakeStep;

        public ScannerState CurrentState { get; private set; } = ScannerState.Idle;

        /// <summary>Raised when the user taps #scanner-cancel-btn. The controller does not act on this itself.</summary>
        public event Action OnCancelClicked;

        public FaceScannerUIController(VisualElement root)
        {
            if (root == null)
            {
                Debug.LogWarning("[WARN] [FaceScannerUIController] Constructed with a null root VisualElement — binding skipped.");
                return;
            }

            circleWrap = root.Q("scanner-circle-wrap");
            viewport = root.Q("scanner-viewport");
            statusIcon = root.Q("scanner-status-icon");
            statusIconLabel = root.Q<Label>("scanner-status-icon-label");
            statusText = root.Q<Label>("scanner-status-text");
            retryButton = root.Q<Button>("scanner-retry-btn");
            cancelButton = root.Q<Button>("scanner-cancel-btn");

            if (circleWrap == null || viewport == null || statusIcon == null || statusText == null)
            {
                Debug.LogWarning("[WARN] [FaceScannerUIController] One or more expected FaceScannerOverlay elements were not found under the given root — check it is FaceScannerOverlay's rootVisualElement.");
            }

            // Registered once for the controller's lifetime; PlayFailureAnimation() only ever swaps
            // which delegate pendingRetryHandler points at, so repeated failures never stack duplicate
            // click registrations (and therefore never double-invoke a retry callback).
            retryButton?.RegisterCallback<ClickEvent>(_ => pendingRetryHandler?.Invoke());
            cancelButton?.RegisterCallback<ClickEvent>(_ => OnCancelClicked?.Invoke());

            SetState(ScannerState.Idle);
        }

        // ================================================================
        // PUBLIC API
        // ================================================================

        /// <summary>
        /// Applies `newState`'s ring color/glow, icon, status text, and Retry-button visibility, and
        /// (re)starts/stops the pulsing and shake micro-animations as appropriate. `customMessage`
        /// overrides the state's default status text when provided (e.g. a specific failure reason).
        /// Cancels any in-flight success auto-proceed timer or failure shake from a previous state.
        /// </summary>
        public void SetState(ScannerState newState, string customMessage = null)
        {
            CurrentState = newState;

            StopPulse();
            StopShake();
            CancelAutoProceed();

            ApplyStateClass(newState);
            ApplyStatusIcon(newState);
            ApplyStatusText(newState, customMessage);
            ApplyRetryVisibility(newState);

            if (newState == ScannerState.Scanning) StartPulse();
        }

        /// <summary>
        /// Transitions to Success (green ring, checkmark pop-in, haptic feedback where supported), then
        /// invokes `onAnimationComplete` after a short auto-proceed delay so the result is visible to
        /// the user before the caller navigates away.
        /// </summary>
        public void PlaySuccessAnimation(Action onAnimationComplete)
        {
            SetState(ScannerState.Success);
            TriggerHapticFeedback();

            autoProceedSchedule = circleWrap?.schedule.Execute(() =>
            {
                autoProceedSchedule = null;
                onAnimationComplete?.Invoke();
            }).StartingIn(SuccessAutoProceedDelayMs);
        }

        /// <summary>
        /// Transitions to Failure (red ring, cross pop-in, `errorMessage` as the status text, a
        /// horizontal shake on the viewport, and a visible Retry button). `onRetryClicked` fires exactly
        /// once per Retry tap — call PlayFailureAnimation/SetState again for a subsequent attempt.
        /// </summary>
        public void PlayFailureAnimation(string errorMessage, Action onRetryClicked, Action onAnimationComplete = null)
        {
            pendingRetryHandler = onRetryClicked;
            SetState(ScannerState.Failure, errorMessage);
            StartShake();

            if (onAnimationComplete != null)
            {
                autoProceedSchedule = circleWrap?.schedule.Execute(() =>
                {
                    autoProceedSchedule = null;
                    onAnimationComplete?.Invoke();
                }).StartingIn(1500);
            }
        }

        /// <summary>Stops any in-flight scheduled animation and unregisters click handlers. Call from the owning page's OnPageExit/OnDisable.</summary>
        public void Dispose()
        {
            StopPulse();
            StopShake();
            CancelAutoProceed();
            pendingRetryHandler = null;
        }

        // ================================================================
        // STATE APPLICATION
        // ================================================================

        void ApplyStateClass(ScannerState state)
        {
            if (circleWrap == null) return;
            foreach (var c in AllStateClasses) circleWrap.RemoveFromClassList(c);

            switch (state)
            {
                case ScannerState.Idle:
                case ScannerState.Scanning:
                    circleWrap.AddToClassList(StateScanningClass);
                    break;
                case ScannerState.Success:
                    circleWrap.AddToClassList(StateSuccessClass);
                    break;
                case ScannerState.Failure:
                    circleWrap.AddToClassList(StateFailureClass);
                    break;
            }
        }

        void ApplyStatusIcon(ScannerState state)
        {
            if (statusIcon == null) return;

            var checkmarkShape = statusIcon.Q("scanner-checkmark-shape");
            var crossShape = statusIcon.Q("scanner-cross-shape");

            switch (state)
            {
                case ScannerState.Success:
                    if (checkmarkShape != null) checkmarkShape.style.display = DisplayStyle.Flex;
                    if (crossShape != null) crossShape.style.display = DisplayStyle.None;
                    if (statusIconLabel != null) statusIconLabel.text = CheckmarkGlyph;
                    statusIcon.AddToClassList(IconVisibleClass);
                    break;
                case ScannerState.Failure:
                    if (checkmarkShape != null) checkmarkShape.style.display = DisplayStyle.None;
                    if (crossShape != null) crossShape.style.display = DisplayStyle.Flex;
                    if (statusIconLabel != null) statusIconLabel.text = CrossGlyph;
                    statusIcon.AddToClassList(IconVisibleClass);
                    break;
                default: // Idle, Scanning — icon hidden per requirements
                    if (checkmarkShape != null) checkmarkShape.style.display = DisplayStyle.None;
                    if (crossShape != null) crossShape.style.display = DisplayStyle.None;
                    statusIcon.RemoveFromClassList(IconVisibleClass);
                    if (statusIconLabel != null) statusIconLabel.text = "";
                    break;
            }
        }

        void ApplyStatusText(ScannerState state, string customMessage)
        {
            if (statusText == null) return;

            if (!string.IsNullOrEmpty(customMessage))
            {
                statusText.text = customMessage;
                return;
            }

            statusText.text = state switch
            {
                ScannerState.Idle => "Align your face inside the circle",
                ScannerState.Scanning => "Scanning face...",
                ScannerState.Success => "Verification Successful!",
                ScannerState.Failure => "Face not recognized. Look directly at the camera and try again.",
                _ => statusText.text,
            };
        }

        void ApplyRetryVisibility(ScannerState state)
        {
            if (retryButton == null) return;
            retryButton.style.display = state == ScannerState.Failure ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void TriggerHapticFeedback()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Application.isEditor) return;
            try
            {
                Handheld.Vibrate();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WARN] [FaceScannerUIController] Handheld.Vibrate() failed: {ex.Message}");
            }
#endif
        }

        // ================================================================
        // ANIMATIONS (VisualElement.schedule — no MonoBehaviour/coroutine host needed)
        // ================================================================

        /// <summary>
        /// "Breathing" pulse for the Scanning state: periodically toggles the `scanner-pulse` modifier
        /// class on #scanner-circle-wrap, which FaceScannerOverlay.uss animates (scale + glow) via a
        /// USS transition each time the class flips. Unity USS has no @keyframes, so a looping effect
        /// has to be driven this way rather than declared purely in CSS.
        /// </summary>
        void StartPulse()
        {
            if (circleWrap == null) return;
            pulseOn = false;
            pulseSchedule = circleWrap.schedule.Execute(() =>
            {
                pulseOn = !pulseOn;
                if (pulseOn) circleWrap.AddToClassList(PulseClass);
                else circleWrap.RemoveFromClassList(PulseClass);
            }).Every(PulseIntervalMs);
        }

        void StopPulse()
        {
            pulseSchedule?.Pause();
            pulseSchedule = null;
            circleWrap?.RemoveFromClassList(PulseClass);
            pulseOn = false;
        }

        /// <summary>
        /// Subtle horizontal shake on #scanner-viewport for the Failure state: steps through a small
        /// fixed offset sequence (ending back at 0) via a repeating scheduled tick, self-cancelling once
        /// the sequence is exhausted.
        /// </summary>
        void StartShake()
        {
            if (viewport == null) return;
            shakeStep = 0;
            shakeSchedule = viewport.schedule.Execute(() =>
            {
                if (shakeStep >= ShakeOffsetsPx.Length)
                {
                    shakeSchedule?.Pause();
                    shakeSchedule = null;
                    return;
                }
                viewport.style.translate = new StyleTranslate(new Translate(ShakeOffsetsPx[shakeStep], 0));
                shakeStep++;
            }).Every(ShakeStepMs);
        }

        void StopShake()
        {
            shakeSchedule?.Pause();
            shakeSchedule = null;
            shakeStep = 0;
            if (viewport != null) viewport.style.translate = StyleKeyword.Null;
        }

        void CancelAutoProceed()
        {
            autoProceedSchedule?.Pause();
            autoProceedSchedule = null;
        }
    }
}
