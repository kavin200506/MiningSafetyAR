using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Triggers the initial gas detector alarm after a delay (7 seconds).
    /// Plays a beep sound and shows a "!" warning indicator above the detector on the belt.
    /// Indicator follows the detector, disappears when worker grabs it.
    /// Integrates with GasLeakModuleManager for scoring.
    /// </summary>
    public class GasDetectorAlarmTrigger : MonoBehaviour
    {
        [Header("Alarm Settings")]
        [Tooltip("Delay in seconds after scene start before alarm triggers")]
        [SerializeField] private float alarmDelaySeconds = 7f;
        
        [Tooltip("Beep frequency in Hz")]
        [SerializeField] private float beepFrequency = 1200f;
        
        [Tooltip("Beep duration in seconds")]
        [SerializeField] private float beepDuration = 0.4f;
        
        [Tooltip("Number of beeps in the alarm sequence")]
        [SerializeField] private int beepCount = 3;
        
        [Tooltip("Interval between beeps in seconds")]
        [SerializeField] private float beepInterval = 0.15f;

        [Header("Popup Indicator")]
        [Tooltip("Prefab for the '!' warning indicator (world-space canvas with TextMeshPro)")]
        [SerializeField] private GameObject warningIndicatorPrefab;
        
        [Tooltip("Height above detector to show the indicator")]
        [SerializeField] private float indicatorHeight = 0.3f;
        
        [Tooltip("How long the indicator stays visible (if not grabbed)")]
        [SerializeField] private float indicatorDuration = 5f;
        
        [Tooltip("Scale of the indicator (world-space canvas scale)")]
        [SerializeField] private float indicatorScale = 0.02f;
        
        [Tooltip("Whether to show the visual '!' indicator (true = show, false = only beep sound)")]
        [SerializeField] private bool showVisualIndicator = false;

        [Header("References")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private MultiGasDetectorController detectorController;
        [SerializeField] private GasLeakModuleManager moduleManager;

        private bool hasTriggered = false;
        private GameObject activeIndicator;
        private Coroutine beepCoroutine;
        private bool isAlarmActive = false;

        private void Awake()
        {
            Debug.Log("[GAS_ALARM_DEBUG] Awake() called");
            if (audioSource == null) 
            {
                audioSource = GetComponent<AudioSource>();
                Debug.Log("[GAS_ALARM_DEBUG] AudioSource auto-assigned: " + (audioSource != null ? "FOUND" : "NULL"));
            }
            else
            {
                Debug.Log("[GAS_ALARM_DEBUG] AudioSource already assigned in Inspector");
            }
            
            if (detectorController == null) 
            {
                detectorController = GetComponent<MultiGasDetectorController>();
                Debug.Log("[GAS_ALARM_DEBUG] MultiGasDetectorController auto-assigned: " + (detectorController != null ? "FOUND" : "NULL"));
            }
            else
            {
                Debug.Log("[GAS_ALARM_DEBUG] MultiGasDetectorController already assigned in Inspector");
            }
            
            if (moduleManager == null)
            {
                moduleManager = FindFirstObjectByType<GasLeakModuleManager>();
                Debug.Log("[GAS_ALARM_DEBUG] GasLeakModuleManager auto-assigned: " + (moduleManager != null ? "FOUND" : "NULL"));
            }
            else
            {
                Debug.Log("[GAS_ALARM_DEBUG] GasLeakModuleManager already assigned in Inspector");
            }
        }

        private void Start()
        {
            Debug.Log("[GAS_ALARM_DEBUG] Start() called, alarmDelaySeconds=" + alarmDelaySeconds + ", enabled=" + enabled + ", gameObject.activeInHierarchy=" + gameObject.activeInHierarchy);
            
            // Auto-create warning indicator if no prefab assigned
            if (warningIndicatorPrefab == null)
            {
                Debug.Log("[GAS_ALARM_DEBUG] No warningIndicatorPrefab assigned, creating default...");
                CreateDefaultWarningIndicator();
                Debug.Log("[GAS_ALARM_DEBUG] Default warning indicator created: " + (warningIndicatorPrefab != null ? "SUCCESS" : "FAILED"));
            }
            else
            {
                Debug.Log("[GAS_ALARM_DEBUG] warningIndicatorPrefab already assigned: " + warningIndicatorPrefab.name);
            }
            
            // Start alarm sequence after delay
            Debug.Log("[GAS_ALARM_DEBUG] Invoking TriggerAlarm in " + alarmDelaySeconds + " seconds");
            Invoke(nameof(TriggerAlarm), alarmDelaySeconds);
            Debug.Log("[GAS_ALARM_DEBUG] Invoke scheduled successfully");
            
            // Also schedule a backup check in case Invoke fails
            StartCoroutine(BackupAlarmCheck());
        }
        
        private System.Collections.IEnumerator BackupAlarmCheck()
        {
            yield return new WaitForSeconds(alarmDelaySeconds + 0.5f);
            if (!hasTriggered)
            {
                Debug.Log("[GAS_ALARM_DEBUG] BackupAlarmCheck: Alarm not triggered yet, forcing TriggerAlarm");
                TriggerAlarm();
            }
        }

        private void CreateDefaultWarningIndicator()
        {
            Debug.Log("[GAS_ALARM_DEBUG] CreateDefaultWarningIndicator() called");
            
            warningIndicatorPrefab = new GameObject("WarningIndicatorPrefab");
            warningIndicatorPrefab.hideFlags = HideFlags.DontSave;
            Debug.Log("[GAS_ALARM_DEBUG] Created prefab GameObject");
            
            // Add Canvas component (world-space)
            Canvas canvas = warningIndicatorPrefab.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.localScale = Vector3.one * indicatorScale; // Use configurable scale
            Debug.Log("[GAS_ALARM_DEBUG] Added Canvas, renderMode=" + canvas.renderMode + ", scale=" + canvas.transform.localScale);
            
            // Add CanvasScaler
            var scaler = warningIndicatorPrefab.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            Debug.Log("[GAS_ALARM_DEBUG] Added CanvasScaler, dynamicPixelsPerUnit=" + scaler.dynamicPixelsPerUnit);
            
            // Add GraphicRaycaster (optional)
            warningIndicatorPrefab.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Debug.Log("[GAS_ALARM_DEBUG] Added GraphicRaycaster");
            
            // Create the "!" text
            GameObject textObj = new GameObject("WarningText");
            textObj.transform.SetParent(warningIndicatorPrefab.transform, false);
            Debug.Log("[GAS_ALARM_DEBUG] Created WarningText child");
            
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "!";
            tmp.fontSize = 200;
            tmp.color = Color.red;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false; // Don't block raycasts
            Debug.Log("[GAS_ALARM_DEBUG] Added TextMeshProUGUI: text='" + tmp.text + "', fontSize=" + tmp.fontSize + ", color=" + tmp.color);
            
            // Add outline for visibility
            var outline = textObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3, -3);
            Debug.Log("[GAS_ALARM_DEBUG] Added Outline");
            
            // Add a pulse animation script
            textObj.AddComponent<WarningPulseAnimation>();
            Debug.Log("[GAS_ALARM_DEBUG] Added WarningPulseAnimation");
            
            // Ensure the canvas is properly configured
            var canvasConfig = warningIndicatorPrefab.GetComponent<Canvas>();
            if (canvasConfig != null)
            {
                canvasConfig.overrideSorting = true;
                canvasConfig.sortingOrder = 100;
                canvasConfig.enabled = true;
            }
            
            Debug.Log("[GAS_ALARM_DEBUG] CreateDefaultWarningIndicator completed successfully");
        }

        private void TriggerAlarm()
        {
            Debug.Log("[GAS_ALARM_DEBUG] TriggerAlarm() called, hasTriggered=" + hasTriggered);
            
            if (hasTriggered) 
            {
                Debug.Log("[GAS_ALARM_DEBUG] Already triggered, returning early");
                return;
            }
            hasTriggered = true;
            isAlarmActive = true;
            
            Debug.Log("[GAS_ALARM] Triggering initial gas detector alarm!");
            
            // Notify module manager for scoring
            if (moduleManager != null)
            {
                moduleManager.NotifyAlarmTriggered();
                Debug.Log("[GAS_ALARM_DEBUG] Notified GasLeakModuleManager of alarm trigger");
            }
            
            // Start beep sequence
            Debug.Log("[GAS_ALARM_DEBUG] Starting PlayBeepSequence coroutine");
            beepCoroutine = StartCoroutine(PlayBeepSequence());
            
            // Show warning indicator above detector
            Debug.Log("[GAS_ALARM_DEBUG] Calling ShowWarningIndicator()");
            ShowWarningIndicator();
        }

        private System.Collections.IEnumerator PlayBeepSequence()
        {
            Debug.Log("[GAS_ALARM_DEBUG] PlayBeepSequence started, beepCount=" + beepCount + ", beepInterval=" + beepInterval);
            
            for (int i = 0; i < beepCount; i++)
            {
                // Stop beeping if alarm was cancelled (detector grabbed)
                if (!isAlarmActive)
                {
                    Debug.Log("[GAS_ALARM_DEBUG] Alarm cancelled, stopping beep sequence");
                    yield break;
                }
                
                Debug.Log("[GAS_ALARM_DEBUG] Playing beep " + (i+1) + "/" + beepCount);
                PlayBeep();
                yield return new WaitForSeconds(beepInterval);
            }
            
            Debug.Log("[GAS_ALARM_DEBUG] PlayBeepSequence completed");
            beepCoroutine = null;
        }

        private void PlayBeep()
        {
            Debug.Log("[GAS_ALARM_DEBUG] PlayBeep() called, audioSource=" + (audioSource != null ? "OK" : "NULL"));
            
            if (audioSource == null) 
            {
                Debug.LogError("[GAS_ALARM_DEBUG] AudioSource is NULL! Cannot play beep.");
                return;
            }
            
            if (!audioSource.isActiveAndEnabled)
            {
                Debug.LogError("[GAS_ALARM_DEBUG] AudioSource is not active and enabled!");
            }
            
            // Ensure audio source is properly configured for 3D sound
            audioSource.spatialBlend = 1.0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 0.5f;
            audioSource.maxDistance = 10f;
            audioSource.volume = 1.0f;
            
            // Generate beep at runtime
            Debug.Log("[GAS_ALARM_DEBUG] Generating beep clip...");
            AudioClip beepClip = GenerateBeep(beepFrequency, beepDuration);
            Debug.Log("[GAS_ALARM_DEBUG] Beep clip generated: " + (beepClip != null ? beepClip.name + " (samples=" + beepClip.samples + ", length=" + beepClip.length + "s)" : "NULL"));
            
            if (beepClip != null)
            {
                Debug.Log("[GAS_ALARM_DEBUG] Calling audioSource.PlayOneShot()");
                audioSource.PlayOneShot(beepClip, 1.0f);
                Debug.Log("[GAS_ALARM_DEBUG] PlayOneShot called, isPlaying=" + audioSource.isPlaying + ", time=" + audioSource.time);
            }
            else
            {
                Debug.LogError("[GAS_ALARM_DEBUG] Generated beep clip is NULL!");
            }
        }

        private AudioClip GenerateBeep(float frequency, float duration)
        {
            Debug.Log("[GAS_ALARM_DEBUG] GenerateBeep(freq=" + frequency + ", dur=" + duration + ")");
            
            float sampleRate = 44100f;
            int samples = Mathf.RoundToInt(sampleRate * duration);
            Debug.Log("[GAS_ALARM_DEBUG] Sample rate: " + sampleRate + ", samples: " + samples + ", expected duration: " + duration + "s");
            
            float[] data = new float[samples];
            
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 8f);
                float wave = Mathf.Sin(2f * Mathf.PI * frequency * t) > 0 ? 1f : -1f;
                data[i] = wave * envelope * 0.5f;
            }
            
            // Check data has non-zero values
            float maxVal = 0f;
            for (int i = 0; i < Mathf.Min(100, samples); i++)
            {
                if (Mathf.Abs(data[i]) > maxVal) maxVal = Mathf.Abs(data[i]);
            }
            Debug.Log("[GAS_ALARM_DEBUG] Wave data max amplitude (first 100): " + maxVal);
            
            AudioClip clip = AudioClip.Create("GasDetectorBeep_Runtime", samples, 1, (int)sampleRate, false);
            Debug.Log("[GAS_ALARM_DEBUG] AudioClip created: " + (clip != null ? "OK (length=" + clip.length + "s)" : "NULL"));
            
            if (clip != null)
            {
                bool setDataResult = clip.SetData(data, 0);
                Debug.Log("[GAS_ALARM_DEBUG] SetData result: " + setDataResult);
                
                // Verify the clip data
                float[] verifyData = new float[Mathf.Min(10, samples)];
                clip.GetData(verifyData, 0);
                float verifyMax = 0f;
                for (int i = 0; i < verifyData.Length; i++)
                {
                    if (Mathf.Abs(verifyData[i]) > verifyMax) verifyMax = Mathf.Abs(verifyData[i]);
                }
                Debug.Log("[GAS_ALARM_DEBUG] Verified clip data max amplitude: " + verifyMax);
            }
            
            return clip;
        }

        private void ShowWarningIndicator()
        {
            Debug.Log("[GAS_ALARM_DEBUG] ShowWarningIndicator() called, showVisualIndicator=" + showVisualIndicator);
            
            // Skip visual indicator if disabled
            if (!showVisualIndicator)
            {
                Debug.Log("[GAS_ALARM_DEBUG] Visual indicator disabled, only playing beep sound");
                return;
            }
            
            if (warningIndicatorPrefab == null) 
            {
                Debug.LogError("[GAS_ALARM_DEBUG] warningIndicatorPrefab is NULL!");
                return;
            }
            
            // Position above the detector
            Vector3 indicatorPos = transform.position + Vector3.up * indicatorHeight;
            Debug.Log("[GAS_ALARM_DEBUG] Indicator position: " + indicatorPos + " (detector pos: " + transform.position + ", height: " + indicatorHeight + ")");
            
            // Instantiate the indicator AS CHILD OF DETECTOR so it follows it
            Debug.Log("[GAS_ALARM_DEBUG] Instantiating warning indicator as child of detector...");
            activeIndicator = Instantiate(warningIndicatorPrefab, transform);
            activeIndicator.name = "GasDetector_WarningIndicator";
            // Set local position relative to detector
            activeIndicator.transform.localPosition = Vector3.up * indicatorHeight;
            activeIndicator.transform.localRotation = Quaternion.identity;
            
            Debug.Log("[GAS_ALARM_DEBUG] Instantiation result: " + (activeIndicator != null ? activeIndicator.name : "NULL") + " (parent: " + (activeIndicator.transform.parent != null ? activeIndicator.transform.parent.name : "NULL") + ")");
            
            if (activeIndicator != null)
            {
                // Ensure the indicator is active and visible
                activeIndicator.SetActive(true);
                
                // Check the canvas
                Canvas canvas = activeIndicator.GetComponent<Canvas>();
                Debug.Log("[GAS_ALARM_DEBUG] Canvas on indicator: " + (canvas != null ? "FOUND (renderMode=" + canvas.renderMode + ", scale=" + canvas.transform.localScale + ")" : "NULL"));
                
                // Check the TextMeshPro
                var tmp = activeIndicator.GetComponentInChildren<TextMeshProUGUI>();
                Debug.Log("[GAS_ALARM_DEBUG] TextMeshProUGUI: " + (tmp != null ? "FOUND (text='" + tmp.text + "', fontSize=" + tmp.fontSize + ", color=" + tmp.color + ", enabled=" + tmp.enabled + ")" : "NULL"));
                
                // Ensure canvas and text are enabled
                if (canvas != null) canvas.enabled = true;
                if (tmp != null) tmp.enabled = true;
                
                // Force the canvas to render on top
                if (canvas != null) canvas.overrideSorting = true;
                if (canvas != null) canvas.sortingOrder = 100;
                
                // Start the follow + fade coroutine
                Debug.Log("[GAS_ALARM_DEBUG] Starting FollowAndFade coroutine");
                StartCoroutine(FollowAndFade());
            }
        }

        private System.Collections.IEnumerator FollowAndFade()
        {
            Debug.Log("[GAS_ALARM_DEBUG] FollowAndFade() started, indicatorDuration=" + indicatorDuration);
            
            if (activeIndicator == null) 
            {
                Debug.LogError("[GAS_ALARM_DEBUG] activeIndicator is NULL at start!");
                yield break;
            }
            
            Canvas canvas = activeIndicator.GetComponent<Canvas>();
            if (canvas == null) 
            {
                Debug.LogError("[GAS_ALARM_DEBUG] Canvas is NULL on activeIndicator!");
                yield break;
            }
            
            Camera mainCam = Camera.main;
            Debug.Log("[GAS_ALARM_DEBUG] Camera.main: " + (mainCam != null ? mainCam.name : "NULL"));
            float elapsed = 0f;
            Vector3 startScale = activeIndicator.transform.localScale;
            Debug.Log("[GAS_ALARM_DEBUG] Start scale: " + startScale + ", indicator localPos: " + activeIndicator.transform.localPosition);
            
            int frameCount = 0;
            isAlarmActive = true;
            
            while (elapsed < indicatorDuration && activeIndicator != null && isAlarmActive)
            {
                elapsed += Time.deltaTime;
                frameCount++;
                
                // Check if detector was grabbed - if so, stop alarm and hide indicator
                if (detectorController != null && detectorController.IsHeld)
                {
                    Debug.Log("[GAS_ALARM_DEBUG] Detector grabbed! Stopping alarm and hiding indicator.");
                    StopAlarm();
                    yield break;
                }
                
                // Ensure indicator is still active and properly positioned
                if (!activeIndicator.activeInHierarchy)
                {
                    Debug.LogWarning("[GAS_ALARM_DEBUG] Indicator became inactive, reactivating...");
                    activeIndicator.SetActive(true);
                }
                
                // Keep indicator at correct local position relative to detector
                activeIndicator.transform.localPosition = Vector3.up * indicatorHeight;
                
                // Billboard to camera (face the camera)
                if (mainCam != null)
                {
                    activeIndicator.transform.LookAt(mainCam.transform.position);
                    activeIndicator.transform.Rotate(0, 180, 0); // Flip to face camera
                }
                else
                {
                    if (frameCount == 1) Debug.LogWarning("[GAS_ALARM_DEBUG] Camera.main is NULL, cannot billboard");
                }
                
                // Pulse scale
                float pulse = 1f + 0.2f * Mathf.Sin(elapsed * 10f);
                activeIndicator.transform.localScale = startScale * pulse;
                
                // Fade out in last 20% of duration
                if (elapsed > indicatorDuration * 0.8f)
                {
                    float fadeProgress = (elapsed - indicatorDuration * 0.8f) / (indicatorDuration * 0.2f);
                    var tmp = canvas.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        Color color = tmp.color;
                        color.a = Mathf.Lerp(1f, 0f, fadeProgress);
                        tmp.color = color;
                        if (frameCount % 30 == 0) Debug.Log("[GAS_ALARM_DEBUG] Fading: progress=" + fadeProgress + ", alpha=" + color.a);
                    }
                }
                
                if (frameCount == 1) Debug.Log("[GAS_ALARM_DEBUG] First frame - indicator active: " + activeIndicator.activeInHierarchy + ", pos: " + activeIndicator.transform.position + ", localPos: " + activeIndicator.transform.localPosition);
                if (frameCount % 60 == 0) Debug.Log("[GAS_ALARM_DEBUG] Frame " + frameCount + " - elapsed: " + elapsed + "s, indicator active: " + activeIndicator.activeInHierarchy);
                
                yield return null;
            }
            
            Debug.Log("[GAS_ALARM_DEBUG] FollowAndFade loop ended, elapsed=" + elapsed + ", frames=" + frameCount + ", isAlarmActive=" + isAlarmActive);
            
            if (activeIndicator != null && isAlarmActive)
            {
                Debug.Log("[GAS_ALARM_DEBUG] Destroying indicator (timeout)");
                Destroy(activeIndicator);
                activeIndicator = null;
            }
            
            isAlarmActive = false;
        }
        
        private void StopAlarm()
        {
            Debug.Log("[GAS_ALARM_DEBUG] StopAlarm() called");
            isAlarmActive = false;
            
            // Stop beep coroutine
            if (beepCoroutine != null)
            {
                StopCoroutine(beepCoroutine);
                beepCoroutine = null;
                Debug.Log("[GAS_ALARM_DEBUG] Beep coroutine stopped");
            }
            
            // Hide indicator immediately
            if (activeIndicator != null)
            {
                Debug.Log("[GAS_ALARM_DEBUG] Destroying indicator (grabbed)");
                Destroy(activeIndicator);
                activeIndicator = null;
            }
        }

        // Allow external trigger (e.g., from GasLeakModuleManager step start)
        public void TriggerAlarmManually()
        {
            Debug.Log("[GAS_ALARM_DEBUG] TriggerAlarmManually() called, hasTriggered=" + hasTriggered);
            if (!hasTriggered)
            {
                CancelInvoke(nameof(TriggerAlarm));
                TriggerAlarm();
            }
        }

        private bool firstCheckReported = false;
        private bool secondCheckReported = false;

        // Debug: Allow testing via key press (using new Input System)
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                Debug.Log("[GAS_ALARM_DEBUG] 'T' key pressed - manually triggering alarm");
                TriggerAlarmManually();
            }
#else
            if (Input.GetKeyDown(KeyCode.T))
            {
                Debug.Log("[GAS_ALARM_DEBUG] 'T' key pressed - manually triggering alarm");
                TriggerAlarmManually();
            }
#endif
            
            // Report FIRST detector check when worker looks at belt or grabs detector (after alarm)
            if (isAlarmActive && !firstCheckReported && detectorController != null && moduleManager != null)
            {
                bool checkedDetector = detectorController.IsHeld || detectorController.IsCheckingDetector;
                if (checkedDetector)
                {
                    firstCheckReported = true;
                    moduleManager.NotifyFirstDetectorCheck();
                    Debug.Log("[GAS_ALARM_DEBUG] First detector check reported (Held: " + detectorController.IsHeld + ", Looking: " + detectorController.IsCheckingDetector + ")");
                }
            }
            
            // Report SECOND detector check when worker re-checks after moving (Step 2)
            // Can be triggered manually with 'R' key for testing
            if (!secondCheckReported && moduleManager != null)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
#else
                if (Input.GetKeyDown(KeyCode.R))
#endif
                {
                    secondCheckReported = true;
                    moduleManager.NotifySecondDetectorCheck();
                    Debug.Log("[GAS_ALARM_DEBUG] Second detector check reported (manual 'R' key)");
                }
            }
            
            // Also check if detector was grabbed while alarm is active (stop alarm)
            if (isAlarmActive && detectorController != null && detectorController.IsHeld)
            {
                Debug.Log("[GAS_ALARM_DEBUG] Update: Detector grabbed (IsHeld=true), stopping alarm");
                StopAlarm();
            }
        }

        public bool HasTriggered => hasTriggered;
    }

    /// <summary>
    /// Simple pulse animation for the warning indicator
    /// </summary>
    public class WarningPulseAnimation : MonoBehaviour
    {
        private Vector3 startScale;
        
        private void Awake()
        {
            startScale = transform.localScale;
        }
        
        private void Update()
        {
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 8f);
            transform.localScale = startScale * pulse;
        }
    }
}