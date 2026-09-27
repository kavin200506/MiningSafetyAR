using UnityEngine;
using TMPro;

namespace MiningSafetyAR.Modules
{
    /// <summary>
    /// Controls the realistic industrial LCD screen display mounted on the multi-gas detector.
    /// Replaces simple floating text with a authentic backlit LCD panel matching real MSA / Dräger
    /// 4-gas detectors (EX/CH4 %LEL, O2 %, H2S PPM, CO PPM).
    /// </summary>
    public class MultiGasDetectorScreenDisplay : MonoBehaviour
    {
        [System.Serializable]
        public struct GasReadingPreset
        {
            public float exCh4Lel;
            public float o2Percent;
            public float h2sPpm;
            public float coPpm;
            public bool isAlarming;
            public string statusMessage;
        }

        [Header("UI Text References")]
        [SerializeField] private TextMeshPro screenText;

        [Header("Looping Readings (shown one per grab, wraps forever)")]
        [SerializeField]
        private GasReadingPreset[] presetReadings = new[]
        {
            new GasReadingPreset { exCh4Lel = 10f, o2Percent = 20.9f, h2sPpm = 12f, coPpm = 75f, isAlarming = true, statusMessage = "DANGER: TOXIC GAS CO/H2S HIGH" },
            new GasReadingPreset { exCh4Lel = 12f, o2Percent = 20.8f, h2sPpm = 14f, coPpm = 85f, isAlarming = true, statusMessage = "DANGER: TOXIC GAS CO/H2S HIGH" },
        };

        private int nextPresetIndex;

        [Header("Default Reading Values")]
        [SerializeField] private float o2Percent = 20.9f;
        [SerializeField] private float exCh4Lel = 0.0f;
        [SerializeField] private float h2sPpm = 0.0f;
        [SerializeField] private float coPpm = 0.0f;
        [SerializeField] private bool isAlarming = false;
        [SerializeField] private string statusMessage = "";

        [Header("Backlight Settings")]
        [SerializeField] private Color normalBacklightColor = new Color(0.72f, 0.84f, 0.88f, 1f); // Backlit LCD Blue-Gray
        [SerializeField] private Color alarmBacklightColor = new Color(1f, 0.25f, 0.2f, 1f);       // Red Alarm Flash
        [SerializeField] private Color textColor = new Color(0.1f, 0.12f, 0.15f, 1f);              // High-Contrast LCD Dark Text

        private float alarmFlashTimer;

        private void Awake()
        {
            if (screenText == null) screenText = GetComponent<TextMeshPro>();
            RefreshDisplay();
        }

        private void Update()
        {
            if (isAlarming && screenText != null)
            {
                alarmFlashTimer += Time.deltaTime * 6f;
                float t = (Mathf.Sin(alarmFlashTimer) + 1f) * 0.5f;
                screenText.color = Color.Lerp(textColor, Color.red, t);
            }
        }

        /// <summary>
        /// Public API to dynamically update gas detector readings and alarm state.
        /// </summary>
        public void UpdateReadings(float o2, float exCh4, float co, float h2s, bool alarm = false, string status = "")
        {
            o2Percent = o2;
            exCh4Lel = exCh4;
            coPpm = co;
            h2sPpm = h2s;
            isAlarming = alarm;
            statusMessage = status;
            RefreshDisplay();
        }

        /// <summary>
        /// Advances to the next preset in <see cref="presetReadings"/> and displays it, wrapping back
        /// to the first once the list is exhausted — called once per detector grab so the reading
        /// cycles 1, 2, 1, 2, ... forever, in sync with BeltItemGrabController's grab animation.
        /// </summary>
        public void ShowNextPreset()
        {
            if (presetReadings == null || presetReadings.Length == 0) return;

            GasReadingPreset preset = presetReadings[nextPresetIndex];
            nextPresetIndex = (nextPresetIndex + 1) % presetReadings.Length;

            UpdateReadings(preset.o2Percent, preset.exCh4Lel, preset.coPpm, preset.h2sPpm, preset.isAlarming, preset.statusMessage);
        }

        public void SetAlarmState(bool alarm)
        {
            isAlarming = alarm;
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (screenText == null) return;

            string formattedText =
                $"<size=80%><style=\"Bold\">EX         O2</style></size>\n" +
                $"<size=110%>{exCh4Lel:F1}%LEL   {o2Percent:F1}%</size>\n\n" +
                $"<size=80%><style=\"Bold\">H2S        CO</style></size>\n" +
                $"<size=110%>{h2sPpm:F0}PPM      {coPpm:F0}PPM</size>\n\n" +
                (string.IsNullOrEmpty(statusMessage)
                    ? $"<size=65%><color=#333333>Gas Detector</color></size>"
                    : $"<size=60%><style=\"Bold\"><color=#B00000>{statusMessage}</color></style></size>");

            screenText.text = formattedText;
            screenText.color = isAlarming ? Color.red : textColor;
        }
    }
}
