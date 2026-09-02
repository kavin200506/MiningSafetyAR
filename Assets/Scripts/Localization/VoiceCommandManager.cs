using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using MiningSafetyAR.Data;
using MiningSafetyAR.UI;

// ---------------------------------------------------------------------------
// VOSK OFFLINE PLUGIN GUARD
// To activate offline Vosk STT (Hindi, Tamil, Indian English):
//  1. Import the vosk-unity package from https://github.com/alphacep/vosk-unity
//  2. Add VOSK_AVAILABLE to Project Settings → Player → Scripting Define Symbols
//  3. Place model folders in Assets/StreamingAssets/VoskModels/{hi,ta,en-in}/
//     Santali has NO Vosk model — it uses the keyword-matching fallback below.
// ---------------------------------------------------------------------------
#if VOSK_AVAILABLE
using Vosk;
#endif

namespace MiningSafetyAR.Localization
{
    // =========================================================================
    // IVoiceCommandTarget — implemented by any PageController that wants
    // to receive voice commands. VoiceCommandManager calls these methods
    // which in turn invoke the SAME private handlers as the ClickEvent callbacks,
    // so no parallel navigation logic is ever duplicated.
    // =========================================================================
    public interface IVoiceCommandTarget
    {
        /// <summary>Equivalent to tapping the "Next" button.</summary>
        void VoiceNext();

        /// <summary>Equivalent to selecting MCQ option 1-4.</summary>
        void VoiceSelectOption(int oneBasedIndex);

        /// <summary>Equivalent to tapping the primary action/start button.</summary>
        void VoiceStart();

        /// <summary>Equivalent to tapping the "Yes" confirmation button (if present).</summary>
        void VoiceConfirm();

        /// <summary>Equivalent to tapping the "No" / Back button.</summary>
        void VoiceCancel();

        /// <summary>Repeat the current page instruction audio.</summary>
        void VoiceRepeat();

        /// <summary>Execute the PASS sub-step (Pull/Aim/Spray/Sweep) if in AR module.</summary>
        void VoicePassStep(string step);
    }

    // =========================================================================
    // VoiceCommand — canonical command identifiers
    // =========================================================================
    public enum VoiceCommand
    {
        Unknown,
        Next,
        Repeat,
        Start,
        Help,
        Yes,
        No,
        Pull,
        Aim,
        Spray,
        Sweep,
        Option1,
        Option2,
        Option3,
        Option4,
    }

    // =========================================================================
    // VoiceCommandManager
    // =========================================================================
    /// <summary>
    /// Hybrid online/offline voice command system for Mining Safety AR.
    ///
    /// Online path:  Bhashini Streaming STT WebSocket (Hindi, Tamil, English, Santali).
    /// Offline path: Vosk-based STT (Hindi, Tamil, Indian English) — requires VOSK_AVAILABLE define.
    /// Santali offline: narrow pre-recorded keyword-matching (no Vosk model exists).
    ///
    /// Commands are dispatched via IVoiceCommandTarget into the existing PageController
    /// button handlers — no parallel navigation logic is created.
    ///
    /// HUD: OnGUI microphone toggle button consistent with ARPlacementManager.
    /// Cheat sheet: full-screen icon overlay triggered by "Help" voice command.
    /// </summary>
    public class VoiceCommandManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton — auto-spawned before scene load, same pattern as FirebaseBootstrap
        // -------------------------------------------------------------------------
        public static VoiceCommandManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            var go = new GameObject("VoiceCommandManager");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<VoiceCommandManager>();
            Debug.Log("[INFO] VoiceCommandManager Auto-spawned before scene load");
        }

        // -------------------------------------------------------------------------
        // Inspector fields
        // -------------------------------------------------------------------------
        [Header("Bhashini Online STT")]
        [Tooltip("Bhashini developer API key from https://bhashini.gov.in/ulca/user/register")]
        [SerializeField] private string bhashiniApiKey  = "";
        [SerializeField] private string bhashiniUserId  = "";
        [SerializeField] private string bhashiniWebSocketUrl = "wss://dhruva-api.bhashini.gov.in/services/inference/pipeline";

        [Header("Recognition Settings")]
        [SerializeField] [Range(1f, 5f)] private float onlineTimeoutSeconds = 3f;
        [SerializeField] [Range(0f, 1f)] private float confidenceThreshold = 0.55f;

        [Header("Santali Offline Keyword Samples (StreamingAssets)")]
        [Tooltip("Relative path under StreamingAssets for pre-recorded Santali keyword .wav files")]
        [SerializeField] private string santaliKeywordDir = "VoiceKeywords/Santali";

        [Header("HUD Settings")]
        [SerializeField] private bool showMicHud = true;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------
        private bool isListening       = false;
        private bool cheatSheetVisible = false;
        private string lastRecognizedText = "";
        private float lastConfidence      = 0f;

        // Microphone streaming
        private AudioClip micClip;
        private const int MIC_SAMPLE_RATE   = 16000;
        private const int MIC_CLIP_SECONDS  = 4;
        private string    activeMicDevice   = null;

        // -------------------------------------------------------------------------
        // Command grammar — 4 languages, max ~30 canonical words total
        //
        // IMPORTANT: All spoken instruction strings must be MAX TWO SENTENCES.
        // DO NOT ADD MORE THAN TWO SENTENCES to any VoiceCommandManager prompt string.
        // Future contributors: this limit ensures comprehension in noisy mining sites.
        // -------------------------------------------------------------------------
        private static readonly Dictionary<VoiceCommand, string[]> CommandKeywords =
            new Dictionary<VoiceCommand, string[]>
        {
            // English | Hindi      | Tamil         | Santali (phonetic)
            { VoiceCommand.Next,   new[] { "next",    "aage",    "aduththu", "agge"    } },
            { VoiceCommand.Repeat, new[] { "repeat",  "phir",    "meendum",  "dobara"  } },
            { VoiceCommand.Start,  new[] { "start",   "shuru",   "thodangu", "shuru"   } },
            { VoiceCommand.Help,   new[] { "help",    "madad",   "udavi",    "help"    } },
            { VoiceCommand.Yes,    new[] { "yes",     "haan",    "aam",      "haan"    } },
            { VoiceCommand.No,     new[] { "no",      "nahin",   "illai",    "nai"     } },
            { VoiceCommand.Pull,   new[] { "pull",    "kheencho","izhu",     "pull"    } },
            { VoiceCommand.Aim,    new[] { "aim",     "nishana", "kuri",     "nishana" } },
            { VoiceCommand.Spray,  new[] { "spray",   "chhidko", "theli",    "spray"   } },
            { VoiceCommand.Sweep,  new[] { "sweep",   "jhaado",  "veesu",    "sweep"   } },
            { VoiceCommand.Option1,new[] { "one",     "ek",      "ondru",    "ek"      } },
            { VoiceCommand.Option2,new[] { "two",     "do",      "irandu",   "do"      } },
            { VoiceCommand.Option3,new[] { "three",   "teen",    "moondru",  "teen"    } },
            { VoiceCommand.Option4,new[] { "four",    "chaar",   "naangu",   "chaar"   } },
        };

        // Icons for the text-free cheat sheet overlay (one per VoiceCommand)
        private static readonly Dictionary<VoiceCommand, string> CommandIcons =
            new Dictionary<VoiceCommand, string>
        {
            { VoiceCommand.Next,    "➡️"  },
            { VoiceCommand.Repeat,  "🔁"  },
            { VoiceCommand.Start,   "▶️"  },
            { VoiceCommand.Help,    "❓"  },
            { VoiceCommand.Yes,     "✅"  },
            { VoiceCommand.No,      "❌"  },
            { VoiceCommand.Pull,    "🔧"  },
            { VoiceCommand.Aim,     "🎯"  },
            { VoiceCommand.Spray,   "💦"  },
            { VoiceCommand.Sweep,   "↔️"  },
            { VoiceCommand.Option1, "①"  },
            { VoiceCommand.Option2, "②"  },
            { VoiceCommand.Option3, "③"  },
            { VoiceCommand.Option4, "④"  },
        };

        // -------------------------------------------------------------------------
        // Audio clips & Onboarding state
        // -------------------------------------------------------------------------
        private Dictionary<VoiceCommand, AudioClip[]> commandAudioClips =
            new Dictionary<VoiceCommand, AudioClip[]>();
        private AudioClip[] introAudioClips = new AudioClip[4]; // indexed by (int)Language
        private const string ONBOARDING_PREF_KEY = "VoiceOnboardingComplete";
        private VoiceCommand highlightedCommand = VoiceCommand.Unknown;
        private bool isOnboardingActive = false;
        private AudioSource cheatSheetAudioSource;

#if VOSK_AVAILABLE
        private VoskRecognizer voskRecognizer;
        private Model          voskModel;
#endif

        // =========================================================================
        // Lifecycle
        // =========================================================================
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitVoskIfAvailable();
            FindMicDevice();
            LoadCommandAudioClips();
            CheckFirstLaunchOnboarding();
        }

        private void OnDestroy()
        {
#if VOSK_AVAILABLE
            voskRecognizer?.Dispose();
            voskModel?.Dispose();
#endif
        }

        // =========================================================================
        // Audio Clip Loading & Playback API
        // =========================================================================
        private void LoadCommandAudioClips()
        {
            cheatSheetAudioSource = GetComponent<AudioSource>();
            if (cheatSheetAudioSource == null)
                cheatSheetAudioSource = gameObject.AddComponent<AudioSource>();

            string[] langFolders = { "en", "hi", "sat", "ta" };

            // Load intro clips
            for (int l = 0; l < langFolders.Length; l++)
            {
                string introPath = $"Audio/VoiceCommands/{langFolders[l]}/Intro";
                introAudioClips[l] = Resources.Load<AudioClip>(introPath);
            }

            // Load command audio clips
            foreach (VoiceCommand cmd in Enum.GetValues(typeof(VoiceCommand)))
            {
                if (cmd == VoiceCommand.Unknown) continue;
                AudioClip[] clips = new AudioClip[4];
                for (int l = 0; l < langFolders.Length; l++)
                {
                    string path = $"Audio/VoiceCommands/{langFolders[l]}/{cmd}";
                    clips[l] = Resources.Load<AudioClip>(path);
                }
                commandAudioClips[cmd] = clips;
            }
            Debug.Log("[INFO] VoiceCommandManager Command audio clips initialized from Resources/Audio/VoiceCommands/");
        }

        private string GetCommandKeyword(VoiceCommand cmd, Language lang)
        {
            if (CommandKeywords.TryGetValue(cmd, out var keywords) && keywords != null && keywords.Length > 0)
            {
                int idx = (int)lang;
                if (idx >= 0 && idx < keywords.Length && !string.IsNullOrEmpty(keywords[idx]))
                    return keywords[idx];
                return keywords[0];
            }
            return cmd.ToString();
        }

        public void PlayCommandAudio(VoiceCommand cmd)
        {
            Language lang = LanguageManager.Instance != null
                ? LanguageManager.Instance.CurrentLanguage
                : Language.English;
            int langIdx = (int)lang;

            AudioClip clip = null;
            if (commandAudioClips.TryGetValue(cmd, out var clips) && clips != null && langIdx < clips.Length)
            {
                clip = clips[langIdx];
            }

            if (clip != null && cheatSheetAudioSource != null)
            {
                cheatSheetAudioSource.Stop();
                cheatSheetAudioSource.pitch = LanguageManager.Instance != null
                    ? LanguageManager.Instance.TtsPlaybackRate
                    : 0.85f;
                cheatSheetAudioSource.clip = clip;
                cheatSheetAudioSource.Play();
                Debug.Log($"[INFO] VoiceCommandManager Played pre-recorded audio clip for command={cmd}, lang={lang}");
                return;
            }

            // Fallback to Native Android Text-To-Speech for English, Hindi, Tamil if .wav clip missing
            string keyword = GetCommandKeyword(cmd, lang);
            SpeakNativeTTS(keyword, lang);
        }

        private void PlayIntroAudio()
        {
            Language lang = LanguageManager.Instance != null
                ? LanguageManager.Instance.CurrentLanguage
                : Language.English;
            int langIdx = (int)lang;

            AudioClip clip = (langIdx < introAudioClips.Length) ? introAudioClips[langIdx] : introAudioClips[0];
            if (clip != null && cheatSheetAudioSource != null)
            {
                cheatSheetAudioSource.Stop();
                cheatSheetAudioSource.pitch = LanguageManager.Instance != null
                    ? LanguageManager.Instance.TtsPlaybackRate
                    : 0.85f;
                cheatSheetAudioSource.clip = clip;
                cheatSheetAudioSource.Play();
                Debug.Log($"[INFO] VoiceCommandManager Played cheat sheet intro clip (lang={lang})");
                return;
            }

            string introText = lang switch
            {
                Language.Hindi   => "बोलने के लिए किसी भी आइकन पर टैप करें",
                Language.Tamil   => "பேச ஏதேனும் ஒரு ஐகானைத் தட்டவும்",
                Language.Santali => "ᱨᱚᱲ ᱞᱟᱹᱜᱤᱫ ᱡᱟᱦᱟᱸᱱᱟᱜ ᱟᱭᱠᱚᱱ ᱨᱮ ᱴᱮᱯ ᱢᱮ",
                _                => "Tap any icon to hear what to say"
            };

            SpeakNativeTTS(introText, lang);
        }

        private void SpeakNativeTTS(string text, Language lang)
        {
            string langCode = lang switch
            {
                Language.Hindi   => "hi",
                Language.Tamil   => "ta",
                Language.Santali => "sat",
                _                => "en"
            };

            if (lang == Language.Santali)
            {
                Debug.LogWarning($"[WARN] VoiceCommandManager Santali TTS fallback unavailable on native Android — place pre-recorded .wav clip in Resources/Audio/VoiceCommands/sat/");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidTTS.Speak(text, langCode);
#else
            Debug.Log($"[INFO] VoiceCommandManager [Native TTS Fallback] Spoke: '{text}' (lang={langCode})");
#endif
        }

        // =========================================================================
        // First-ever Launch Guided Voice Onboarding
        // =========================================================================
        private void CheckFirstLaunchOnboarding()
        {
            int complete = PlayerPrefs.GetInt(ONBOARDING_PREF_KEY, 0);
            if (complete == 0 && !isOnboardingActive)
            {
                Debug.Log("[INFO] VoiceCommandManager Triggering one-time first-launch guided voice onboarding");
                StartCoroutine(OnboardingWalkthroughCoroutine());
            }
        }

        private IEnumerator OnboardingWalkthroughCoroutine()
        {
            isOnboardingActive = true;
            cheatSheetVisible = true;

            // 5 highest-priority core commands
            VoiceCommand[] coreCommands = new[]
            {
                VoiceCommand.Help,
                VoiceCommand.Start,
                VoiceCommand.Next,
                VoiceCommand.Yes,
                VoiceCommand.No
            };

            yield return new WaitForSeconds(0.5f);
            PlayIntroAudio();
            yield return new WaitForSeconds(1.8f);

            foreach (var cmd in coreCommands)
            {
                highlightedCommand = cmd;
                PlayCommandAudio(cmd);
                yield return new WaitForSeconds(1.6f);
            }

            highlightedCommand = VoiceCommand.Unknown;
            cheatSheetVisible = false;
            isOnboardingActive = false;

            PlayerPrefs.SetInt(ONBOARDING_PREF_KEY, 1);
            PlayerPrefs.Save();
            Debug.Log("[INFO] VoiceCommandManager First-launch guided onboarding complete (flag saved)");
        }

        // =========================================================================
        // Public API — called by HUD button or external triggers
        // =========================================================================
        public void BeginListening()
        {
            if (isListening) return;
            isListening = true;
            StartCoroutine(ListenCoroutine());
        }

        public void StopListening()
        {
            isListening = false;
            if (Microphone.IsRecording(activeMicDevice))
                Microphone.End(activeMicDevice);
        }

        public void ToggleListening()
        {
            if (isListening) StopListening();
            else              BeginListening();
        }

        /// <summary>Shows the text-free icon cheat sheet overlay and plays intro voice hint.</summary>
        public void ShowCheatSheet()
        {
            cheatSheetVisible = true;
            highlightedCommand = VoiceCommand.Unknown;
            Debug.Log("[INFO] VoiceCommandManager Cheat sheet overlay opened");
            PlayIntroAudio();
        }

        public void HideCheatSheet()
        {
            cheatSheetVisible = false;
            highlightedCommand = VoiceCommand.Unknown;
        }

        // =========================================================================
        // Mic setup & Android Runtime Permissions
        // =========================================================================
        private void FindMicDevice()
        {
#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                Debug.Log("[INFO] VoiceCommandManager Requesting Android RECORD_AUDIO permission...");
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            }
#endif

            string[] devices = Microphone.devices;
            if (devices != null && devices.Length > 0)
            {
                activeMicDevice = devices[0];
                Debug.Log($"[INFO] VoiceCommandManager Microphone selected: '{activeMicDevice}'");
            }
            else
            {
                Debug.LogWarning("[WARN] VoiceCommandManager No microphone devices found yet — permission requested");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && activeMicDevice == null)
            {
                FindMicDevice();
            }
        }

        // =========================================================================
        // Main listen coroutine — decides online vs offline path
        // =========================================================================
        private IEnumerator ListenCoroutine()
        {
            if (activeMicDevice == null)
            {
                FindMicDevice();
                yield return null;
            }

            if (activeMicDevice == null)
            {
                Debug.LogWarning("[WARN] VoiceCommandManager Microphone not authorized or available — aborting listen");
                isListening = false;
                yield break;
            }

            // Start microphone
            micClip = Microphone.Start(activeMicDevice, loop: false,
                                       lengthSec: MIC_CLIP_SECONDS,
                                       frequency: MIC_SAMPLE_RATE);

            Debug.Log("[DIAG] VoiceCommandManager Mic recording started");

            // Give mic a frame to init
            yield return null;

            Language lang = LanguageManager.Instance != null
                ? LanguageManager.Instance.CurrentLanguage
                : Language.English;

            bool networkReachable = Application.internetReachability != NetworkReachability.NotReachable;

            if (networkReachable && !string.IsNullOrEmpty(bhashiniApiKey))
            {
                Debug.Log($"[DIAG] VoiceCommandManager Network reachable — attempting Bhashini online STT (lang={lang})");
                yield return StartCoroutine(BhashiniOnlinePath(lang));
            }
            else
            {
                string reason = !networkReachable
                    ? "NetworkReachability.NotReachable"
                    : "Bhashini API key not configured";
                Debug.Log($"[INFO] VoiceCommandManager Skipping online path: {reason} — going offline immediately");
                yield return StartCoroutine(OfflinePath(lang));
            }

            isListening = false;
        }

        // =========================================================================
        // ONLINE PATH — Bhashini Streaming STT
        // =========================================================================
        private IEnumerator BhashiniOnlinePath(Language lang)
        {
            string langCode = LanguageManager.Instance != null
                ? LanguageManager.Instance.GetBhashiniLanguageCode()
                : "en";

            bool onlineSucceeded = false;
            bool timedOut        = false;
            float elapsed        = 0f;

            // Collect ~1.5s of audio before sending
            yield return new WaitForSeconds(1.5f);
            elapsed += 1.5f;

            Microphone.End(activeMicDevice);

            // Encode audio to PCM16 bytes
            float[] samples = new float[micClip.samples];
            micClip.GetData(samples, 0);
            byte[] pcmBytes = ConvertToPCM16(samples);

            // Build Bhashini REST pipeline request (Streaming endpoint treated as HTTP for simplicity;
            // true WebSocket streaming requires NativeWebSocket or a custom plugin).
            // This implementation uses UnityWebRequest POST with PCM payload and a 3-second timeout guard.
            string url = $"https://dhruva-api.bhashini.gov.in/services/inference/asr?langCode={langCode}";

            using var www = new UnityWebRequest(url, "POST");
            www.uploadHandler   = new UploadHandlerRaw(pcmBytes);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", bhashiniApiKey);
            www.SetRequestHeader("userID",        bhashiniUserId);
            www.SetRequestHeader("Content-Type",  "audio/wav");

            var op = www.SendWebRequest();

            while (!op.isDone)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= onlineTimeoutSeconds)
                {
                    timedOut = true;
                    www.Abort();
                    Debug.LogWarning($"[WARN] VoiceCommandManager Bhashini STT timed out after {onlineTimeoutSeconds}s — falling back to offline");
                    break;
                }
                yield return null;
            }

            if (!timedOut && www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                // Bhashini response: {"output":[{"source":"<text>"}],"config":{"confidence":0.87}}
                BhashiniResponse parsed = TryParseBhashiniResponse(json);

                if (parsed != null && !string.IsNullOrEmpty(parsed.recognizedText))
                {
                    lastRecognizedText = parsed.recognizedText.ToLowerInvariant().Trim();
                    lastConfidence     = parsed.confidence;

                    Debug.Log($"[INFO] VoiceCommandManager Recognized via Bhashini (online, lang={lang}): '{lastRecognizedText}', confidence={lastConfidence:F2}");

                    VoiceCommand cmd = MatchCommand(lastRecognizedText);
                    DispatchCommand(cmd, parsed.confidence);
                    onlineSucceeded = true;
                }
                else
                {
                    Debug.LogWarning($"[WARN] VoiceCommandManager Bhashini returned empty/unparseable result — falling back to offline");
                }
            }
            else if (!timedOut)
            {
                Debug.LogError($"[ERROR] VoiceCommandManager Bhashini request failed: {www.error} — falling back to offline");
            }

            if (!onlineSucceeded)
            {
                yield return StartCoroutine(OfflinePath(lang));
            }
        }

        // =========================================================================
        // OFFLINE PATH — Vosk or Santali keyword matching
        // =========================================================================
        private IEnumerator OfflinePath(Language lang)
        {
            // Santali: no Vosk model exists — use narrow keyword matching
            if (lang == Language.Santali)
            {
                Debug.LogWarning("[WARN] VoiceCommandManager Santali offline recognition unavailable — using limited pre-recorded keyword matching, not full Vosk STT.");
                yield return StartCoroutine(SantaliKeywordMatch());
                yield break;
            }

            // Hindi / Tamil / English — use Vosk
            string modelSubdir = lang switch
            {
                Language.Hindi  => "hi",
                Language.Tamil  => "ta",
                _               => "en-in"   // Indian English default
            };

            Debug.Log($"[INFO] VoiceCommandManager Using OFFLINE Vosk STT — language={lang} (Hindi/Tamil/English fully supported offline)");

#if VOSK_AVAILABLE
            yield return StartCoroutine(VoskRecognizePath(modelSubdir, lang));
#else
            Debug.LogWarning("[WARN] VoiceCommandManager VOSK_AVAILABLE not defined — offline STT unavailable. " +
                             "Import vosk-unity and add VOSK_AVAILABLE to Scripting Define Symbols.");

            // Collect audio then fall through to local keyword match (best-effort)
            yield return new WaitForSeconds(1.5f);
            if (Microphone.IsRecording(activeMicDevice)) Microphone.End(activeMicDevice);

            float[] samples = new float[micClip != null ? micClip.samples : 0];
            if (micClip != null) micClip.GetData(samples, 0);

            // Without Vosk we cannot do real recognition; dispatch Unknown and replay help
            Debug.LogWarning("[WARN] VoiceCommandManager No STT engine available — dispatching Unknown (help prompt will replay)");
            DispatchCommand(VoiceCommand.Unknown, 0f);
#endif
            yield break;
        }

#if VOSK_AVAILABLE
        private IEnumerator VoskRecognizePath(string modelSubdir, Language lang)
        {
            // Load model on demand if not already loaded for this language
            string modelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "VoskModels", modelSubdir);
            if (voskModel == null || /* model dir changed */ !modelPath.Contains(modelSubdir))
            {
                voskModel?.Dispose();
                voskRecognizer?.Dispose();
                voskModel      = new Model(modelPath);
                voskRecognizer = new VoskRecognizer(voskModel, MIC_SAMPLE_RATE);
                Debug.Log($"[INFO] VoiceCommandManager Vosk model loaded: {modelPath}");
            }

            // Wait for recording to complete (up to MIC_CLIP_SECONDS)
            yield return new WaitForSeconds(2.0f);
            if (Microphone.IsRecording(activeMicDevice)) Microphone.End(activeMicDevice);

            float[] samples = new float[micClip.samples];
            micClip.GetData(samples, 0);
            byte[] pcmBytes = ConvertToPCM16(samples);

            voskRecognizer.AcceptWaveform(pcmBytes, pcmBytes.Length);
            string resultJson = voskRecognizer.FinalResult();

            // Vosk result: {"text":"next"}
            string recognizedText = ParseVoskResult(resultJson).ToLowerInvariant().Trim();
            lastRecognizedText = recognizedText;
            lastConfidence     = 0.75f; // Vosk does not return a confidence score

            Debug.Log($"[INFO] VoiceCommandManager Vosk offline result (lang={lang}): '{recognizedText}'");

            VoiceCommand cmd = MatchCommand(recognizedText);
            DispatchCommand(cmd, lastConfidence);
        }

        private string ParseVoskResult(string json)
        {
            // Minimal JSON parse: {"text": "next"} → "next"
            int textIdx = json.IndexOf("\"text\"", StringComparison.Ordinal);
            if (textIdx < 0) return "";
            int colon = json.IndexOf(':', textIdx);
            if (colon < 0) return "";
            int q1 = json.IndexOf('"', colon);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
#endif

        // =========================================================================
        // SANTALI KEYWORD MATCHING — narrow pre-recorded keyword comparison
        // =========================================================================
        private IEnumerator SantaliKeywordMatch()
        {
            Debug.LogWarning("[WARN] VoiceCommandManager Santali offline mode limited to core high-priority commands (Help/Yes/No/Start/Next) with tightened 0.72 RMS threshold to prevent false positives from ambient noise or coughing.");

            // Collect 2s of audio
            yield return new WaitForSeconds(2.0f);
            if (Microphone.IsRecording(activeMicDevice)) Microphone.End(activeMicDevice);

            float[] userSamples = new float[micClip != null ? micClip.samples : 0];
            if (micClip != null) micClip.GetData(userSamples, 0);

            // Scoped core command set for Santali offline mode
            var coreCommands = new HashSet<VoiceCommand>
            {
                VoiceCommand.Help, VoiceCommand.Yes, VoiceCommand.No, VoiceCommand.Start, VoiceCommand.Next
            };

            VoiceCommand bestMatch    = VoiceCommand.Unknown;
            float        bestScore    = 0f;
            float        matchThresh  = 0.72f; // Tightened threshold (0.72) to reject ambient noise

            foreach (var kvp in CommandKeywords)
            {
                if (!coreCommands.Contains(kvp.Key)) continue; // Filter out low-priority keywords offline

                // Santali keyword is the 4th entry (index 3) in the array
                string keyword = kvp.Value.Length > 3 ? kvp.Value[3] : kvp.Value[0];
                string clipPath = System.IO.Path.Combine(
                    Application.streamingAssetsPath, santaliKeywordDir, keyword + ".wav");

                if (!System.IO.File.Exists(clipPath)) continue;

                // Load reference clip via UnityWebRequest
                using var req = UnityWebRequestMultimedia.GetAudioClip("file://" + clipPath, AudioType.WAV);
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success) continue;

                AudioClip refClip   = DownloadHandlerAudioClip.GetContent(req);
                float[] refSamples  = new float[refClip.samples];
                refClip.GetData(refSamples, 0);

                float score = ComputeRMSSimilarity(userSamples, refSamples);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = kvp.Key;
                }
            }

            if (bestScore >= matchThresh)
            {
                Debug.Log($"[INFO] VoiceCommandManager Santali keyword match: command={bestMatch}, score={bestScore:F2}");
                DispatchCommand(bestMatch, bestScore);
            }
            else
            {
                Debug.LogWarning($"[WARN] VoiceCommandManager Santali keyword match below threshold ({bestScore:F2} < {matchThresh}) — replaying help prompt");
                DispatchCommand(VoiceCommand.Unknown, bestScore);
            }
        }

        // =========================================================================
        // Command Matching — text → VoiceCommand
        // =========================================================================
        private VoiceCommand MatchCommand(string text)
        {
            if (string.IsNullOrEmpty(text)) return VoiceCommand.Unknown;

            string lower = text.ToLowerInvariant();
            foreach (var kvp in CommandKeywords)
            {
                foreach (string kw in kvp.Value)
                {
                    if (lower.Contains(kw))
                    {
                        Debug.Log($"[DIAG] VoiceCommandManager Text '{lower}' matched keyword '{kw}' → {kvp.Key}");
                        return kvp.Key;
                    }
                }
            }

            Debug.Log($"[DIAG] VoiceCommandManager No keyword match for '{lower}'");
            return VoiceCommand.Unknown;
        }

        // =========================================================================
        // Command Dispatch — routes to current PageController via IVoiceCommandTarget
        // =========================================================================
        private void DispatchCommand(VoiceCommand cmd, float confidence)
        {
            Language lang = LanguageManager.Instance != null
                ? LanguageManager.Instance.CurrentLanguage
                : Language.English;

            if (confidence < confidenceThreshold && cmd != VoiceCommand.Unknown)
            {
                Debug.LogWarning($"[WARN] VoiceCommandManager Confidence {confidence:F2} below threshold {confidenceThreshold} for cmd={cmd} — replaying help prompt");
                cmd = VoiceCommand.Unknown;
            }

            // Special: Help opens the cheat sheet on any page
            if (cmd == VoiceCommand.Help)
            {
                Debug.Log($"[INFO] VoiceCommandManager Matched action: ShowCheatSheet (lang={lang}, confidence={confidence:F2})");
                ShowCheatSheet();
                return;
            }

            // Find the current IVoiceCommandTarget
            IVoiceCommandTarget target = FindCurrentTarget();

            if (cmd == VoiceCommand.Unknown || target == null)
            {
                string reason = target == null ? "no IVoiceCommandTarget found in scene" : "no match";
                Debug.LogWarning($"[WARN] VoiceCommandManager {reason} — replaying help prompt (cmd={cmd}, lang={lang}, confidence={confidence:F2})");
                ShowCheatSheet(); // Show icon cheat sheet as the "help prompt"
                return;
            }

            Debug.Log($"[INFO] VoiceCommandManager Matched action: {cmd} (lang={lang}, confidence={confidence:F2}) → dispatching to {target.GetType().Name}");

            switch (cmd)
            {
                case VoiceCommand.Next:    target.VoiceNext();             break;
                case VoiceCommand.Start:   target.VoiceStart();            break;
                case VoiceCommand.Yes:     target.VoiceConfirm();          break;
                case VoiceCommand.No:      target.VoiceCancel();           break;
                case VoiceCommand.Repeat:  target.VoiceRepeat();           break;
                case VoiceCommand.Option1: target.VoiceSelectOption(1);    break;
                case VoiceCommand.Option2: target.VoiceSelectOption(2);    break;
                case VoiceCommand.Option3: target.VoiceSelectOption(3);    break;
                case VoiceCommand.Option4: target.VoiceSelectOption(4);    break;
                case VoiceCommand.Pull:    target.VoicePassStep("PULL");   break;
                case VoiceCommand.Aim:     target.VoicePassStep("AIM");    break;
                case VoiceCommand.Spray:   target.VoicePassStep("SQUEEZE");break; // "Spray" maps to SQUEEZE in P.A.S.S.
                case VoiceCommand.Sweep:   target.VoicePassStep("SWEEP");  break;
            }
        }

        private IVoiceCommandTarget FindCurrentTarget()
        {
            var objects = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var mb in objects)
            {
                if (mb is IVoiceCommandTarget target)
                    return target;
            }
            return null;
        }

        // =========================================================================
        // Bhashini response parsing (minimal, no external JSON library required)
        // =========================================================================
        [Serializable]
        private class BhashiniResponse
        {
            public string recognizedText;
            public float  confidence;
        }

        private BhashiniResponse TryParseBhashiniResponse(string json)
        {
            try
            {
                // Expected: {"output":[{"source":"recognized text"}],"config":{"confidence":0.87}}
                var result = new BhashiniResponse();

                int sourceIdx = json.IndexOf("\"source\"", StringComparison.Ordinal);
                if (sourceIdx >= 0)
                {
                    int colon = json.IndexOf(':', sourceIdx);
                    int q1    = json.IndexOf('"', colon);
                    int q2    = json.IndexOf('"', q1 + 1);
                    if (q1 >= 0 && q2 > q1)
                        result.recognizedText = json.Substring(q1 + 1, q2 - q1 - 1);
                }

                int confIdx = json.IndexOf("\"confidence\"", StringComparison.Ordinal);
                if (confIdx >= 0)
                {
                    int colon = json.IndexOf(':', confIdx);
                    int end   = json.IndexOfAny(new[] { ',', '}' }, colon);
                    if (end > colon)
                    {
                        string numStr = json.Substring(colon + 1, end - colon - 1).Trim();
                        if (float.TryParse(numStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float conf))
                            result.confidence = conf;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] VoiceCommandManager Failed to parse Bhashini response: {ex.Message}");
                return null;
            }
        }

        // =========================================================================
        // Audio helpers
        // =========================================================================
        private static byte[] ConvertToPCM16(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                bytes[i * 2]     = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        /// <summary>
        /// Computes a normalised RMS-based similarity score between two audio sample arrays.
        /// Returns 0.0 (no match) to 1.0 (identical energy profile).
        /// Used only for the Santali pre-recorded keyword matching path.
        /// </summary>
        private static float ComputeRMSSimilarity(float[] a, float[] b)
        {
            if (a.Length == 0 || b.Length == 0) return 0f;
            int len = Mathf.Min(a.Length, b.Length);

            double rmsA = 0, rmsB = 0;
            for (int i = 0; i < len; i++)
            {
                rmsA += a[i] * a[i];
                rmsB += b[i] * b[i];
            }
            rmsA = Math.Sqrt(rmsA / len);
            rmsB = Math.Sqrt(rmsB / len);

            if (rmsA + rmsB < 1e-6f) return 0f;
            float diff  = Mathf.Abs((float)(rmsA - rmsB));
            float norm  = (float)(rmsA + rmsB);
            return Mathf.Clamp01(1f - diff / norm);
        }

        // =========================================================================
        // Vosk init
        // =========================================================================
        private void InitVoskIfAvailable()
        {
#if VOSK_AVAILABLE
            Debug.Log("[INFO] VoiceCommandManager VOSK_AVAILABLE defined — Vosk offline STT ready");
#else
            Debug.Log("[INFO] VoiceCommandManager VOSK_AVAILABLE NOT defined — offline STT requires vosk-unity plugin and define symbol");
#endif
        }

        // =========================================================================
        // OnGUI — HUD microphone toggle + cheat sheet overlay
        // Consistent with ARPlacementManager OnGUI pattern (GUIStyle, richText, Screen.width)
        // =========================================================================
        /// <summary>
        /// Checks if a screen touch position (in Input System bottom-left origin coordinates)
        /// falls inside the Voice Mic button or active Cheat Sheet overlay.
        /// Used by ARPlacementManager to block AR raycasting when interacting with Voice UI.
        /// </summary>
        public bool IsTouchOverVoiceUI(Vector2 inputScreenPos)
        {
            if (cheatSheetVisible)
                return true; // Cheat sheet overlay covers full screen

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Splash") || sceneName.Contains("Login") || sceneName == "Login" || sceneName.Contains("Language"))
                return false;

            if (!showMicHud)
                return false;

            // Invert Y coordinate: GUI has (0,0) at top-left, Input has (0,0) at bottom-left
            float guiY = Screen.height - inputScreenPos.y;
            float w = 240f, h = 80f;
            float margin = 30f;
            Rect micRect = new Rect(Screen.width - w - margin, Screen.height - h - margin - 170f, w, h);

            return micRect.Contains(new Vector2(inputScreenPos.x, guiY));
        }

        private void OnGUI()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Splash") || sceneName.Contains("Login") || sceneName == "Login" || sceneName.Contains("Language"))
                return;

            DrawMicToggleButton();

            if (cheatSheetVisible)
                DrawCheatSheetOverlay();
        }

        private void DrawMicToggleButton()
        {
            if (!showMicHud) return;

            float w = 240f, h = 80f;
            float margin = 30f;
            // Bottom-right corner, above the placement diagnostics bar
            Rect rect = new Rect(Screen.width - w - margin, Screen.height - h - margin - 170f, w, h);

            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize    = 26,
                fontStyle   = FontStyle.Bold,
                alignment   = TextAnchor.MiddleCenter,
                richText    = true
            };

            if (isListening)
            {
                style.normal.textColor = new Color(1f, 0.25f, 0.25f); // Vivid red when recording
                string btnLabel = $"<b>🔴 Listening...</b>";
                if (GUI.Button(rect, btnLabel, style))
                    StopListening();
            }
            else
            {
                style.normal.textColor = new Color(0.2f, 1f, 0.4f); // Green when idle
                string btnLabel = $"<b>🎙️ Tap to Speak</b>";
                if (GUI.Button(rect, btnLabel, style))
                    BeginListening();
            }
        }

        private void DrawCheatSheetOverlay()
        {
            // Semi-transparent full-screen background
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;

            // Language label row at top
            string langLabel = LanguageManager.Instance != null
                ? LanguageManager.Instance.GetLanguageFlagEmoji()
                : "🇬🇧 English";

            GUIStyle langStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, 30f, Screen.width, 60f), langLabel, langStyle);

            // Icon grid — 4 columns
            const int cols     = 4;
            float iconSize     = 100f;
            float iconPadding  = 20f;
            float gridW        = cols * (iconSize + iconPadding) - iconPadding;
            float startX       = (Screen.width - gridW) / 2f;
            float startY       = 120f;

            int idx = 0;
            bool tappedIcon = false;

            foreach (var kvp in CommandIcons)
            {
                int row = idx / cols;
                int col = idx % cols;
                float x = startX + col * (iconSize + iconPadding);
                float y = startY + row * (iconSize + iconPadding);
                Rect iconRect = new Rect(x, y, iconSize, iconSize);

                bool isHighlighted = (kvp.Key == highlightedCommand);

                GUIStyle iconStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize  = 44,
                    alignment = TextAnchor.MiddleCenter,
                    richText  = true,
                    normal    = {
                        textColor  = Color.white,
                        background = MakeColorTexture(isHighlighted
                            ? new Color(1f, 0.5f, 0f, 0.95f) // Bright orange highlight during audio hint playback
                            : new Color(0.15f, 0.15f, 0.15f, 0.9f))
                    }
                };

                GUI.Box(iconRect, kvp.Value, iconStyle);

                // Handle tap directly on icon box -> play spoken command audio hint
                if (Event.current.type == EventType.MouseDown && iconRect.Contains(Event.current.mousePosition))
                {
                    tappedIcon = true;
                    highlightedCommand = kvp.Key;
                    PlayCommandAudio(kvp.Key);
                    Event.current.Use();
                }

                idx++;
            }

            // Dismiss instruction footer
            GUIStyle dismissStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 24,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            float dimY = startY + (Mathf.CeilToInt((float)CommandIcons.Count / cols)) * (iconSize + iconPadding) + 20f;
            GUI.Label(new Rect(0, dimY, Screen.width, 40f), "❌  Tap background to close | Tap icon to listen", dismissStyle);

            // Tapping background outside icons dismisses cheat sheet (unless onboarding walkthrough is running)
            if (Event.current.type == EventType.MouseDown && !tappedIcon && !isOnboardingActive)
            {
                HideCheatSheet();
                Event.current.Use();
            }
        }

        // Utility: create a 1×1 colored texture for GUIStyle backgrounds
        private static Texture2D MakeColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    internal class AndroidTTS
    {
        private static AndroidJavaObject tts;
        private static bool isInitialized = false;

        public static void Speak(string text, string langCode)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (tts == null)
                    {
                        var listener = new AndroidTTSInitListener();
                        tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
                    }

                    using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                    {
                        AndroidJavaObject locale = langCode switch
                        {
                            "hi" => new AndroidJavaObject("java.util.Locale", "hi", "IN"),
                            "ta" => new AndroidJavaObject("java.util.Locale", "ta", "IN"),
                            _    => localeClass.GetStatic<AndroidJavaObject>("ENGLISH")
                        };

                        tts.Call<int>("setLanguage", locale);
                        tts.Call<int>("speak", text, 0 /* QUEUE_FLUSH */, null, "VoiceCommandUtterance");
                        Debug.Log($"[INFO] VoiceCommandManager AndroidTTS Spoke: '{text}' (lang={langCode})");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ERROR] VoiceCommandManager AndroidTTS Exception: {ex.Message}");
            }
        }

        private class AndroidTTSInitListener : AndroidJavaProxy
        {
            public AndroidTTSInitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }
            public void onInit(int status)
            {
                if (status == 0)
                {
                    isInitialized = true;
                    Debug.Log("[INFO] VoiceCommandManager AndroidTTS initialized successfully.");
                }
            }
        }
    }
#endif
}
