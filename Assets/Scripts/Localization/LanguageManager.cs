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

        private Language currentLanguage = Language.English;
        public Language CurrentLanguage => currentLanguage;

        public event Action<Language> OnLanguageChanged;

        private const string LANGUAGE_PREF_KEY = "SelectedLanguage";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            int savedLang = PlayerPrefs.GetInt(LANGUAGE_PREF_KEY, (int)Language.English);
            currentLanguage = (Language)savedLang;
        }

        public void SetLanguage(Language language)
        {
            if (currentLanguage == language) return;

            currentLanguage = language;
            PlayerPrefs.SetInt(LANGUAGE_PREF_KEY, (int)language);
            PlayerPrefs.Save();

            Debug.Log($"[LanguageManager] Language switched to: {currentLanguage}");
            OnLanguageChanged?.Invoke(currentLanguage);
        }

        public void PlayVoiceover(AudioClip englishClip, AudioClip hindiClip, AudioClip santaliClip)
        {
            if (voiceoverAudioSource == null)
            {
                Debug.LogWarning("[LanguageManager] Voiceover AudioSource is not assigned!");
                return;
            }

            AudioClip clipToPlay = englishClip;
            if (currentLanguage == Language.Hindi && hindiClip != null) clipToPlay = hindiClip;
            else if (currentLanguage == Language.Santali && santaliClip != null) clipToPlay = santaliClip;

            if (clipToPlay != null)
            {
                voiceoverAudioSource.Stop();
                voiceoverAudioSource.clip = clipToPlay;
                voiceoverAudioSource.Play();
            }
        }

        public string GetLocalizedText(string textEN, string textHI, string textSAT)
        {
            switch (currentLanguage)
            {
                case Language.Hindi:
                    return !string.IsNullOrEmpty(textHI) ? textHI : textEN;
                case Language.Santali:
                    return !string.IsNullOrEmpty(textSAT) ? textSAT : textEN;
                default:
                    return textEN;
            }
        }
    }
}
