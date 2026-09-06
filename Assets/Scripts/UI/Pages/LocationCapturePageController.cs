using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using MiningSafetyAR.UI;
using MiningSafetyAR.UI.Navigation;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.UI.Pages
{
    public class LocationCapturePageController : PageController
    {
        private string moduleId;
        private Label addressText;
        private Label statusSubtext;
        private VisualElement progressFill;
        private Label radarEmoji;

        protected override void BindUI()
        {
            addressText = root.Q<Label>("address-text");
            statusSubtext = root.Q<Label>("status-subtext");
            progressFill = root.Q("progress-fill");
            radarEmoji = root.Q<Label>("radar-emoji");
        }

        public override void SetNavigationParameter(object param)
        {
            moduleId = param as string;
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
        }

        public override void OnPageEnter()
        {
            if (string.IsNullOrEmpty(moduleId)) moduleId = "fire_safety";
            StartCoroutine(RunLocationCaptureSequence());
        }

        private IEnumerator RunLocationCaptureSequence()
        {
            if (addressText != null) addressText.text = "Acquiring regional GPS coordinates...";
            if (statusSubtext != null) statusSubtext.text = "📡 Connecting to location hardware...";
            if (progressFill != null) progressFill.style.width = Length.Percent(10);
            if (radarEmoji != null) radarEmoji.text = "🛰️";

            TrainingLocationCapture.EnsureInstance();

            bool isLocationComplete = false;
            LocationDataPayload capturedPayload = null;

            // Start Location Capture
            TrainingLocationCapture.Instance.CaptureLocation(
                onComplete: (payload) =>
                {
                    capturedPayload = payload;
                    isLocationComplete = true;
                },
                onSubStatusUpdate: (subStatus) =>
                {
                    if (statusSubtext != null) statusSubtext.text = subStatus;
                }
            );

            // Dedicated 5.0-second display window showcasing location acquisition & reverse geocoded address
            float totalDuration = 5.0f;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;

                float progressPct = Mathf.Clamp((elapsed / totalDuration) * 100f, 10f, 100f);
                if (progressFill != null) progressFill.style.width = Length.Percent(progressPct);

                if (elapsed >= 1.5f && elapsed < 3.2f)
                {
                    if (radarEmoji != null) radarEmoji.text = "📍";
                    if (!isLocationComplete)
                    {
                        if (statusSubtext != null) statusSubtext.text = "🔍 Reverse geocoding regional mining address...";
                    }
                    else if (capturedPayload != null && capturedPayload.hasLocation && addressText != null)
                    {
                        addressText.text = capturedPayload.locationName;
                    }
                }
                else if (elapsed >= 3.2f)
                {
                    if (isLocationComplete && capturedPayload != null && capturedPayload.hasLocation)
                    {
                        if (radarEmoji != null) radarEmoji.text = "✅";
                        if (addressText != null) addressText.text = capturedPayload.locationName;
                        if (statusSubtext != null) statusSubtext.text = $"📍 Verified Location (±{capturedPayload.horizontalAccuracy:F0}m accuracy)";
                    }
                    else if (isLocationComplete && capturedPayload != null && !capturedPayload.hasLocation)
                    {
                        if (radarEmoji != null) radarEmoji.text = "📍";
                        if (addressText != null) addressText.text = "Mining Safety Sector (Default)";
                        if (statusSubtext != null) statusSubtext.text = "GPS unavailable — using regional mining sector";
                    }
                }
            }

            // Ensure final address is set
            if (capturedPayload != null && capturedPayload.hasLocation && addressText != null)
            {
                addressText.text = capturedPayload.locationName;
            }

            yield return new WaitForSeconds(0.4f);

            // Navigate to AR Simulation Scene
            Debug.Log($"[LocationCapturePage] 5s Location verification completed ('{capturedPayload?.locationName}') -> Launching AR Simulation for module '{moduleId}'...");
            NavigationManager.Instance.NavigateTo("ar_fire_safety", moduleId);
        }
    }
}
