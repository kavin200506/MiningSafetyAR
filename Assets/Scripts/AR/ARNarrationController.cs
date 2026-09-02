using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using MiningSafetyAR.Localization;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Enum identifying all shared AR simulation narration events.
    /// Used as an integer index into parallel 4-language AudioClip arrays.
    /// </summary>
    public enum NarrationEvent
    {
        FirstPlaneFound   = 0,
        FireStarted       = 1,
        MidpointNudge     = 2,
        ExtinguisherFound = 3
    }

    /// <summary>
    /// Pure event listener that adds spoken audio narration to the existing AR simulation flow
    /// without modifying ARPlacementManager.cs or ARStepCounterTracker.cs.
    ///
    /// Auto-spawns before scene load via prefab at Assets/Resources/Prefabs/ARNarrationController.prefab.
    /// Dynamically re-subscribes to AR managers on scene load.
    /// Routes narration lines through LanguageManager.Instance.PlayVoiceover(en, hi, sat, ta).
    /// Dynamically loads clips from Resources/Audio/Narration/{lang}/{event} if inspector arrays are unassigned.
    /// Validates all transcripts at startup (≤2 sentences).
    /// </summary>
    public class ARNarrationController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton — auto-spawned before scene load
        // -------------------------------------------------------------------------
        public static ARNarrationController Instance { get; private set; }

        /// <summary>
        /// Auto-spawns the ARNarrationController singleton before any scene is loaded.
        /// 
        /// TIMING NOTE: FindFirstObjectByType<ARNarrationController>() CANNOT be used here
        /// because RuntimeInitializeLoadType.BeforeSceneLoad fires BEFORE any scene content is loaded
        /// into memory. Therefore, scene-placed GameObjects do not exist yet when this method runs.
        /// 
        /// PREFAB PATTERN: Assign custom Inspector-assigned AudioClips on the prefab at
        /// Assets/Resources/Prefabs/ARNarrationController.prefab (or place audio files in
        /// Assets/Resources/Audio/Narration/{lang}/{event}.wav). AutoSpawn() loads and instantiates
        /// this prefab at startup. Do NOT rely on scene-placed instances, as scene instances are
        /// destroyed by the singleton pattern on startup.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;

            GameObject prefab = Resources.Load<GameObject>("Prefabs/ARNarrationController");
            if (prefab != null)
            {
                GameObject go = UnityEngine.Object.Instantiate(prefab);
                go.name = "ARNarrationController";
                UnityEngine.Object.DontDestroyOnLoad(go);
                Instance = go.GetComponent<ARNarrationController>();
                Debug.Log("[INFO] ARNarrationController Auto-spawned from prefab at Resources/Prefabs/ARNarrationController.");
            }
            else
            {
                GameObject go = new GameObject("ARNarrationController");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Instance = go.AddComponent<ARNarrationController>();
                Debug.LogWarning("[WARN] ARNarrationController Prefab not found at Resources/Prefabs/ARNarrationController — created blank instance. Audio clips must be loaded via Resources fallback.");
            }
        }

        // -------------------------------------------------------------------------
        // Serialized Parallel AudioClip Arrays (Indexed by NarrationEvent enum)
        // -------------------------------------------------------------------------
        [Header("English Audio Clips (Indexed by NarrationEvent: 0..3)")]
        [SerializeField] private AudioClip[] englishClips = new AudioClip[4];

        [Header("Hindi Audio Clips (Indexed by NarrationEvent: 0..3)")]
        [SerializeField] private AudioClip[] hindiClips = new AudioClip[4];

        [Header("Tamil Audio Clips (Indexed by NarrationEvent: 0..3)")]
        [SerializeField] private AudioClip[] tamilClips = new AudioClip[4];

        [Header("Santali Audio Clips (Indexed by NarrationEvent: 0..3)")]
        [SerializeField] private AudioClip[] santaliClips = new AudioClip[4];

        // -------------------------------------------------------------------------
        // Transcripts for 2-sentence runtime validation at Awake
        // -------------------------------------------------------------------------
        [Header("English Transcripts (≤2 Sentences)")]
        [SerializeField] private string[] englishTexts = new string[4]
        {
            "Surface detected. Tap the floor to ignite the fire hazard.",
            "Fire hazard ignited. Walk around to search for the fire extinguisher.",
            "You are halfway there. Keep walking to discover the extinguisher.",
            "Extinguisher discovered. Tap the extinguisher to pick it up."
        };

        [Header("Hindi Transcripts (≤2 Sentences)")]
        [SerializeField] private string[] hindiTexts = new string[4]
        {
            "सतह मिल गई है। आग का खतरा शुरू करने के लिए फर्श पर टैप करें।",
            "आग शुरू हो गई है। अग्निशामक की खोज के लिए आसपास चलें।",
            "आप आधे रास्ते पर पहुंच गए हैं। चलते रहें।",
            "अग्निशामक मिल गया है। उठाने के लिए टैप करें।"
        };

        [Header("Tamil Transcripts (≤2 Sentences)")]
        [SerializeField] private string[] tamilTexts = new string[4]
        {
            "தரை கண்டறியப்பட்டது. தீ அபாயத்தை தொடங்க தரைப்பகுதியில் தட்டவும்.",
            "தீ விபத்து தொடங்கியது. தீயணைப்பான் கருவிகளை தேட நடந்து செல்லவும்.",
            "நீங்கள் பாதியை கடந்துவிட்டீர்கள். தொடர்ந்து நடந்து செல்லவும்.",
            "தீயணைப்பான் கண்டுபிடிக்கப்பட்டது. அதை எடுக்க தட்டவும்."
        };

        [Header("Santali Transcripts (≤2 Sentences)")]
        [SerializeField] private string[] santaliTexts = new string[4]
        {
            " Ot ñam ena. Sengel chalu lagid ot re tap me.",
            "Sengel chalu ena. Sengel irichij ñam lagid taam me.",
            "Tala dharag em tiog keda. Laha te taam me.",
            "Sengel irichij ñam ena. Sab lagid tap me."
        };

        // -------------------------------------------------------------------------
        // State & Guards
        // -------------------------------------------------------------------------
        private bool hasNarratedFirstPlane = false;
        private bool hasNarratedFireStarted = false;
        private bool hasNarratedMidpoint   = false;
        private bool hasNarratedExtinguisherFound = false;
        private bool isSimulationCompleted = false;

        private NarrationEvent? currentActiveEvent = null;
        private Coroutine repeatTimerCoroutine = null;

        private ARPlaneManager planeManager;

        // Non-overlapping narration queue
        private readonly Queue<NarrationEvent> narrationQueue = new Queue<NarrationEvent>();
        private bool isProcessingQueue = false;
        private AudioSource dedicatedAudioSource;

        // -------------------------------------------------------------------------
        // Lifecycle & Validation
        // -------------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            dedicatedAudioSource = GetComponent<AudioSource>();
            if (dedicatedAudioSource == null)
                dedicatedAudioSource = gameObject.AddComponent<AudioSource>();

            dedicatedAudioSource.spatialBlend = 0f;
            dedicatedAudioSource.playOnAwake = false;

            Debug.Log($"[INFO] ARNarrationController AudioSource config: spatialBlend={dedicatedAudioSource.spatialBlend}, volume={dedicatedAudioSource.volume}, mute={dedicatedAudioSource.mute}");

            ValidateAllTranscripts();
            LoadResourcesFallbackClips();
        }

        private static bool IsARScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return sceneName == "ar_fire_safety" ||
                   sceneName == "AR Plane Detection Placement" ||
                   sceneName == "UI_ARSimulation" ||
                   sceneName == "AR Image Tracking" ||
                   sceneName == "AR Occlusion" ||
                   sceneName == "AR_Placement_Demo" ||
                   sceneName == "AR_Simulation_Demo";
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

#if UNITY_EDITOR
        private void Update()
        {
            // Unity Editor Play Mode Testing Hotkey:
            // Press [P] -> Simulate AR Plane Detected narration event
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            {
                Debug.Log("[EDITOR_HOTKEY] 'P' Key pressed — Simulating AR Plane Detected event!");
                TriggerFirstPlaneNarration();
            }
        }
#endif

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Debug.Log($"[INFO] ARNarrationController Scene loaded: '{scene.name}' — resetting narration guards and re-subscribing...");
            hasNarratedFirstPlane = false;
            hasNarratedFireStarted = false;
            hasNarratedMidpoint   = false;
            hasNarratedExtinguisherFound = false;
            isSimulationCompleted = false;
            currentActiveEvent = null;

            if (repeatTimerCoroutine != null)
            {
                StopCoroutine(repeatTimerCoroutine);
                repeatTimerCoroutine = null;
            }

            narrationQueue.Clear();
            isProcessingQueue = false;

            UnsubscribeFromEvents();
            SubscribeToEvents();
        }

        private void LoadResourcesFallbackClips()
        {
            string[] langs = { "en", "hi", "sat", "ta" };
            AudioClip[][] clipArrays = { englishClips, hindiClips, santaliClips, tamilClips };

            int loadedCount = 0;
            int missingCount = 0;

            foreach (NarrationEvent evt in Enum.GetValues(typeof(NarrationEvent)))
            {
                int idx = (int)evt;
                for (int l = 0; l < langs.Length; l++)
                {
                    if (clipArrays[l][idx] == null)
                    {
                        string path = $"Audio/Narration/{langs[l]}/{evt}";
                        clipArrays[l][idx] = Resources.Load<AudioClip>(path);
                    }

                    if (clipArrays[l][idx] != null)
                        loadedCount++;
                    else
                        missingCount++;
                }
            }

            Debug.Log($"[INFO] ARNarrationController Resources clip load summary: {loadedCount} loaded, {missingCount} missing — see warnings above for details");
        }

        // -------------------------------------------------------------------------
        // Sentence Limit Validation (Awake)
        // -------------------------------------------------------------------------
        private void ValidateAllTranscripts()
        {
            ValidateLanguageTextArray("English", englishTexts);
            ValidateLanguageTextArray("Hindi", hindiTexts);
            ValidateLanguageTextArray("Tamil", tamilTexts);
            ValidateLanguageTextArray("Santali", santaliTexts);
        }

        private void ValidateLanguageTextArray(string langName, string[] texts)
        {
            if (texts == null) return;
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.IsNullOrEmpty(texts[i])) continue;
                int sentenceCount = 0;
                foreach (char c in texts[i])
                {
                    if (c == '.' || c == '!' || c == '?' || c == '।') sentenceCount++;
                }
                if (sentenceCount > 2)
                {
                    Debug.LogWarning($"[WARN] ARNarrationController Transcript '{((NarrationEvent)i)}' in lang={langName} exceeds 2-sentence limit ({sentenceCount} sentences): '{texts[i]}'");
                }
            }
        }

        // -------------------------------------------------------------------------
        // Event Subscription & Event Handlers
        // -------------------------------------------------------------------------
        private void SubscribeToEvents()
        {
            var pm = ARPlacementManager.Instance ?? FindFirstObjectByType<ARPlacementManager>();
            if (pm != null)
            {
                pm.OnObjectPlaced += OnObjectPlacedHandler;
                pm.OnNoPlaneDetected += OnNoPlaneDetectedHandler;

                planeManager = pm.GetComponent<ARPlaneManager>();
                if (planeManager != null)
                {
                    planeManager.trackablesChanged.AddListener(OnPlanesChanged);
                    if (planeManager.trackables.count > 0 && !hasNarratedFirstPlane)
                    {
                        Debug.Log("[INFO] ARNarrationController Active planes found on subscribe — triggering FirstPlaneFound narration.");
                        TriggerFirstPlaneNarration();
                    }
                }
                Debug.Log("[INFO] ARNarrationController Subscribed to ARPlacementManager events.");
            }

            var sct = ARStepCounterTracker.Instance ?? FindFirstObjectByType<ARStepCounterTracker>();
            if (sct != null)
            {
                sct.OnStepCountUpdated += OnStepCountUpdatedHandler;
                sct.OnExtinguisherDiscovered += OnExtinguisherDiscoveredHandler;
                Debug.Log("[INFO] ARNarrationController Subscribed to ARStepCounterTracker events.");
            }

            var grabCtrl = FireExtinguisherGrabController.Instance ?? FindFirstObjectByType<FireExtinguisherGrabController>();
            if (grabCtrl != null)
            {
                grabCtrl.OnExtinguisherGrabbed += OnExtinguisherGrabbedHandler;
                Debug.Log("[INFO] ARNarrationController Subscribed to FireExtinguisherGrabController events.");
            }

            if (pm == null && sct == null && grabCtrl == null)
            {
                Debug.Log("[INFO] ARNarrationController Waiting for AR managers to load in scene...");
            }
        }

        private void UnsubscribeFromEvents()
        {
            var pm = ARPlacementManager.Instance ?? FindFirstObjectByType<ARPlacementManager>();
            if (pm != null)
            {
                pm.OnObjectPlaced -= OnObjectPlacedHandler;
                pm.OnNoPlaneDetected -= OnNoPlaneDetectedHandler;
            }
            if (planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
                planeManager = null;
            }
            var sct = ARStepCounterTracker.Instance ?? FindFirstObjectByType<ARStepCounterTracker>();
            if (sct != null)
            {
                sct.OnStepCountUpdated -= OnStepCountUpdatedHandler;
                sct.OnExtinguisherDiscovered -= OnExtinguisherDiscoveredHandler;
            }
            var grabCtrl = FireExtinguisherGrabController.Instance ?? FindFirstObjectByType<FireExtinguisherGrabController>();
            if (grabCtrl != null)
            {
                grabCtrl.OnExtinguisherGrabbed -= OnExtinguisherGrabbedHandler;
            }
        }

        // -------------------------------------------------------------------------
        // Event Listeners
        // -------------------------------------------------------------------------
        private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (!hasNarratedFirstPlane && args.added != null && args.added.Count > 0)
            {
                Debug.Log($"[INFO] ARNarrationController ARPlane detected event received ({args.added.Count} new planes)! Triggering FirstPlaneFound narration.");
                TriggerFirstPlaneNarration();
            }
        }

        private void TriggerFirstPlaneNarration()
        {
            if (hasNarratedFirstPlane || isSimulationCompleted) return;
            hasNarratedFirstPlane = true;
            TriggerCommand(NarrationEvent.FirstPlaneFound);
        }

        private void OnObjectPlacedHandler(Vector3 pos, Quaternion rot)
        {
            if (hasNarratedFireStarted || isSimulationCompleted) return;
            hasNarratedFireStarted = true;
            Debug.Log($"[INFO] ARNarrationController OnObjectPlaced event received at {pos}! Triggering FireStarted narration.");
            hasNarratedMidpoint = false; // reset midpoint guard for walking phase
            TriggerCommand(NarrationEvent.FireStarted);
        }

        private void OnNoPlaneDetectedHandler()
        {
            Debug.Log("[INFO] ARNarrationController OnNoPlaneDetected event received.");
        }

        private void OnStepCountUpdatedHandler(int currentSteps, int targetSteps)
        {
            Debug.Log($"[INFO] ARNarrationController OnStepCountUpdated event received ({currentSteps}/{targetSteps} steps).");
            if (targetSteps > 0 && !hasNarratedMidpoint && currentSteps >= targetSteps / 2 && !isSimulationCompleted)
            {
                Debug.Log("[INFO] ARNarrationController Midpoint reached! Triggering MidpointNudge narration.");
                hasNarratedMidpoint = true;
                TriggerCommand(NarrationEvent.MidpointNudge);
            }
        }

        private void OnExtinguisherDiscoveredHandler(Vector3 position)
        {
            if (hasNarratedExtinguisherFound || isSimulationCompleted) return;
            hasNarratedExtinguisherFound = true;
            Debug.Log($"[INFO] ARNarrationController OnExtinguisherDiscovered event received at {position}! Triggering ExtinguisherFound narration.");
            TriggerCommand(NarrationEvent.ExtinguisherFound);
        }

        private void OnExtinguisherGrabbedHandler(GameObject obj)
        {
            Debug.Log("[INFO] ARNarrationController Extinguisher grabbed! Stopping all narration repeats.");
            isSimulationCompleted = true;
            currentActiveEvent = null;
            if (repeatTimerCoroutine != null)
            {
                StopCoroutine(repeatTimerCoroutine);
                repeatTimerCoroutine = null;
            }
        }

        // -------------------------------------------------------------------------
        // Command Trigger & 10-Second Repeat Timer
        // -------------------------------------------------------------------------
        private void TriggerCommand(NarrationEvent evt)
        {
            if (isSimulationCompleted) return;

            currentActiveEvent = evt;

            // Stop previous repeat timer
            if (repeatTimerCoroutine != null)
            {
                StopCoroutine(repeatTimerCoroutine);
                repeatTimerCoroutine = null;
            }

            // Play narration clip immediately
            EnqueueNarration(evt);

            // Start 10-second repeat timer
            repeatTimerCoroutine = StartCoroutine(RepeatCommandCoroutine(evt));
        }

        private IEnumerator RepeatCommandCoroutine(NarrationEvent evt)
        {
            while (!isSimulationCompleted && currentActiveEvent == evt)
            {
                // Wait 10 seconds between repeats
                yield return new WaitForSeconds(10.0f);

                if (!isSimulationCompleted && currentActiveEvent == evt)
                {
                    Debug.Log($"[INFO] ARNarrationController 10s repeat timer fired for '{evt}' — repeating spoken command.");
                    EnqueueNarration(evt);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Queued Non-Overlapping Narration Playback
        // -------------------------------------------------------------------------
        private void EnqueueNarration(NarrationEvent evt)
        {
            Debug.Log($"[INFO] ARNarrationController Enqueued narration event: '{evt}'");
            narrationQueue.Enqueue(evt);
            if (!isProcessingQueue)
            {
                StartCoroutine(ProcessNarrationQueue());
            }
        }

        private IEnumerator ProcessNarrationQueue()
        {
            isProcessingQueue = true;

            while (narrationQueue.Count > 0)
            {
                NarrationEvent evt = narrationQueue.Dequeue();
                int idx = (int)evt;

                Language currentLang = LanguageManager.Instance != null
                    ? LanguageManager.Instance.CurrentLanguage
                    : Language.English;

                AudioClip enClip  = (englishClips != null && idx < englishClips.Length) ? englishClips[idx] : null;
                AudioClip hiClip  = (hindiClips != null && idx < hindiClips.Length) ? hindiClips[idx] : null;
                AudioClip satClip = (santaliClips != null && idx < santaliClips.Length) ? santaliClips[idx] : null;
                AudioClip taClip  = (tamilClips != null && idx < tamilClips.Length) ? tamilClips[idx] : null;

                AudioClip activeClip = null;
                string activeText = "";

                switch (currentLang)
                {
                    case Language.Hindi:
                        activeClip = hiClip;
                        if (hindiTexts != null && idx < hindiTexts.Length) activeText = hindiTexts[idx];
                        break;
                    case Language.Santali:
                        activeClip = satClip;
                        if (santaliTexts != null && idx < santaliTexts.Length) activeText = santaliTexts[idx];
                        break;
                    case Language.Tamil:
                        activeClip = taClip;
                        if (tamilTexts != null && idx < tamilTexts.Length) activeText = tamilTexts[idx];
                        break;
                    default:
                        activeClip = enClip;
                        if (englishTexts != null && idx < englishTexts.Length) activeText = englishTexts[idx];
                        break;
                }

                // Fallback to Tamil audio clip (recorded voiceover) if target language clip is unassigned
                if (activeClip == null) activeClip = taClip ?? enClip ?? hiClip ?? satClip;

                string clipName = activeClip != null ? activeClip.name : "MISSING_CLIP";
                Debug.Log($"[INFO] ARNarrationController Narrating: '{evt}' in lang={currentLang} | Clip='{clipName}' | Spoken text='{activeText}'");

                if (activeClip == null)
                {
                    Debug.LogWarning($"[WARN] ARNarrationController Audio clip for '{evt}' in lang={currentLang} is unassigned/missing! To play audio, add .wav file to Resources/Audio/Narration/{(currentLang == Language.Hindi ? "hi" : currentLang == Language.Tamil ? "ta" : currentLang == Language.Santali ? "sat" : "en")}/{evt}.wav");
                }

                if (LanguageManager.Instance != null)
                {
                    LanguageManager.Instance.PlayVoiceover(enClip, hiClip, satClip, taClip);
                }
                else if (activeClip != null && dedicatedAudioSource != null)
                {
                    dedicatedAudioSource.Stop();
                    dedicatedAudioSource.clip = activeClip;
                    dedicatedAudioSource.pitch = 0.85f;
                    dedicatedAudioSource.Play();
                }

                float clipLength = activeClip != null ? activeClip.length / 0.85f : 2.5f;
                yield return new WaitForSeconds(clipLength + 0.3f);
            }

            isProcessingQueue = false;
        }
    }
}
