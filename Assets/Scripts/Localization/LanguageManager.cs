using System;
using System.Collections.Generic;
using UnityEngine;
using MiningSafetyAR.Data;

namespace MiningSafetyAR.Localization
{
    public class LanguageManager : MonoBehaviour
    {
        public static LanguageManager Instance { get; private set; }

        [Header("Audio Settings")]
        [SerializeField] private AudioSource voiceoverAudioSource;

        /// <summary>True while a voiceover clip started via PlayVoiceover is still audibly playing.</summary>
        public bool IsVoiceoverPlaying => voiceoverAudioSource != null && voiceoverAudioSource.isPlaying;

        // ---------------------------------------------------------------------------
        // TTS Playback Rate
        // IMPORTANT: All spoken instruction audio is played at 0.85× normal speed
        // to improve comprehension for non-native speakers across all four languages.
        // Do NOT increase this above 1.0 without accessibility review.
        // ---------------------------------------------------------------------------
        [Header("TTS Settings")]
        [SerializeField] [Range(0.5f, 1.5f)] private float ttsPlaybackRate = 0.85f;

        /// <summary>
        /// Playback pitch applied to AudioSource when playing any voiceover clip.
        /// Default 0.85 — slower speech rate for improved comprehension.
        /// </summary>
        public float TtsPlaybackRate
        {
            get => ttsPlaybackRate;
            set => ttsPlaybackRate = Mathf.Clamp(value, 0.5f, 1.5f);
        }

        private Language currentLanguage = Language.English;
        public Language CurrentLanguage => currentLanguage;

        public event Action<Language> OnLanguageChanged;

        // Key is an int (Language enum cast). PlayerPrefs int values MUST stay
        // English=0, Hindi=1, Santali=2, Tamil=3 — DO NOT change these mappings.
        private const string LANGUAGE_PREF_KEY = "SelectedLanguage";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoSpawn()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("LanguageManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<LanguageManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSavedLanguage();
        }

        private void LoadSavedLanguage()
        {
            string strLang = PlayerPrefs.GetString(LANGUAGE_PREF_KEY, string.Empty);
            if (!string.IsNullOrEmpty(strLang))
            {
                currentLanguage = ParseLanguage(strLang);
            }
            else
            {
                int savedLang = PlayerPrefs.GetInt(LANGUAGE_PREF_KEY, (int)Language.English);
                if (!Enum.IsDefined(typeof(Language), savedLang)) savedLang = (int)Language.English;
                currentLanguage = (Language)savedLang;
            }

            Debug.Log($"[INFO] [LanguageManager] Loaded language: {currentLanguage}");
        }

        public static Language ParseLanguage(string langStr)
        {
            if (string.IsNullOrEmpty(langStr)) return Language.English;
            string lower = langStr.Trim().ToLowerInvariant();
            if (lower.StartsWith("hi")) return Language.Hindi;
            if (lower.StartsWith("sat") || lower.StartsWith("san")) return Language.Santali;
            if (lower.StartsWith("ta")) return Language.Tamil;
            return Language.English;
        }

        public void SetLanguage(Language language)
        {
            currentLanguage = language;
            PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language.ToString());
            PlayerPrefs.SetInt("SelectedLanguageInt", (int)language);
            PlayerPrefs.Save();

            Debug.Log($"[INFO] [LanguageManager] Language switched to: {currentLanguage} (stored as {language})");
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        public void SetLanguage(string languageName)
        {
            SetLanguage(ParseLanguage(languageName));
        }

        // -----------------------------------------------------------------------
        // PlayVoiceover — 4-language primary overload
        // -----------------------------------------------------------------------
        /// <summary>
        /// Plays the appropriate voiceover clip for the current language.
        /// All clips are played at TtsPlaybackRate (default 0.85×) for accessibility.
        ///
        /// IMPORTANT: Keep all spoken instruction strings to a MAXIMUM of two sentences.
        /// DO NOT exceed two sentences per audio clip — future contributors must honour this
        /// constraint to ensure comprehension on low-bandwidth / noisy mining environments.
        /// </summary>
        public void PlayVoiceover(AudioClip englishClip, AudioClip hindiClip,
                                  AudioClip santaliClip, AudioClip tamilClip)
        {
            if (voiceoverAudioSource == null)
            {
                voiceoverAudioSource = GetComponent<AudioSource>();
                if (voiceoverAudioSource == null)
                    voiceoverAudioSource = gameObject.AddComponent<AudioSource>();
            }

            voiceoverAudioSource.spatialBlend = 0f; // Force 2D sound for device speakers
            voiceoverAudioSource.volume = 1.0f;
            voiceoverAudioSource.playOnAwake = false;
            voiceoverAudioSource.mute = false;

            AudioClip clipToPlay = englishClip;
            switch (currentLanguage)
            {
                case Language.Hindi:
                    if (hindiClip != null) clipToPlay = hindiClip;
                    break;
                case Language.Santali:
                    if (santaliClip != null) clipToPlay = santaliClip;
                    break;
                case Language.Tamil:
                    if (tamilClip != null) clipToPlay = tamilClip;
                    break;
                // Language.English falls through to default (englishClip already set)
            }

            if (clipToPlay != null)
            {
                voiceoverAudioSource.Stop();
                voiceoverAudioSource.pitch = ttsPlaybackRate; // 0.85× for accessibility
                voiceoverAudioSource.clip = clipToPlay;
                voiceoverAudioSource.Play();
            }
        }

        // -----------------------------------------------------------------------
        // PlayVoiceover — 3-language backward-compat shim (existing callers)
        // -----------------------------------------------------------------------
        /// <summary>
        /// Backward-compatible overload for callers that pre-date Tamil support.
        /// Tamil falls back to the English clip automatically.
        /// </summary>
        public void PlayVoiceover(AudioClip englishClip, AudioClip hindiClip, AudioClip santaliClip)
            => PlayVoiceover(englishClip, hindiClip, santaliClip, tamilClip: null);

        // -----------------------------------------------------------------------
        // GetLocalizedText — 4-language primary overload
        // -----------------------------------------------------------------------
        public string GetLocalizedText(string textEN, string textHI, string textSAT, string textTA)
        {
            string chosenText = textEN;
            switch (currentLanguage)
            {
                case Language.Hindi:
                    chosenText = !string.IsNullOrEmpty(textHI) ? textHI : textEN;
                    break;
                case Language.Santali:
                    chosenText = !string.IsNullOrEmpty(textSAT) ? textSAT : textEN;
                    break;
                case Language.Tamil:
                    chosenText = !string.IsNullOrEmpty(textTA) ? textTA : textEN;
                    break;
                default:
                    chosenText = textEN;
                    break;
            }

            ValidateSentenceCount(chosenText);
            return chosenText;
        }

        /// <summary>
        /// Runtime check enforcing that spoken instructions do NOT exceed 2 sentences.
        /// Counts sentence-ending punctuation (., !, ?, and Devanagari danda ।).
        /// Logs a WARN if exceeded so content review catches violations instantly.
        /// </summary>
        public static void ValidateSentenceCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int count = 0;
            foreach (char c in text)
            {
                if (c == '.' || c == '!' || c == '?' || c == '।') count++;
            }
            if (count > 2)
            {
                Debug.LogWarning($"[WARN] [LanguageManager] Spoken instruction exceeds 2-sentence accessibility limit ({count} sentence terminators detected): '{text}'");
            }
        }

        // -----------------------------------------------------------------------
        // GetLocalizedText — 3-language backward-compat shim (existing callers)
        // -----------------------------------------------------------------------
        public string GetLocalizedText(string textEN, string textHI, string textSAT)
            => GetLocalizedText(textEN, textHI, textSAT, textTA: null);

        // -----------------------------------------------------------------------
        // Helper: returns the Bhashini language code for online STT/TTS
        // -----------------------------------------------------------------------
        public string GetBhashiniLanguageCode()
        {
            switch (currentLanguage)
            {
                case Language.Hindi:   return "hi";
                case Language.Tamil:   return "ta";
                case Language.Santali: return "sat"; // Bhashini supports Santali online
                default:               return "en";
            }
        }

        // -----------------------------------------------------------------------
        // Helper: returns a display flag emoji for the cheat sheet overlay
        // -----------------------------------------------------------------------
        public string GetLanguageFlagEmoji()
        {
            switch (currentLanguage)
            {
                case Language.Hindi:   return "🇮🇳 हिंदी";
                case Language.Tamil:   return "🇮🇳 தமிழ்";
                case Language.Santali: return "🪘 ᱥᱟᱱᱛᱟᱲᱤ";
                default:               return "🇬🇧 English";
            }
        }
    }
}
