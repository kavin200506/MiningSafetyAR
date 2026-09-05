#pragma warning disable 0414
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiningSafetyAR.AR
{
    /// <summary>
    /// Centralized session event logger and debug tracker for the AR Fire Safety simulation.
    /// Captures all button clicks, keyboard presses (I, J, K, L, W, Space, etc.), P.A.S.S. state transitions,
    /// proximity warnings, score changes, and errors into a live rolling log buffer and disk file.
    /// File Path: persistentDataPath/ar_simulation_debug_log.txt
    /// </summary>
    public class ARSimulationLogger : MonoBehaviour
    {
        public static ARSimulationLogger Instance { get; private set; }

        [Header("Log Storage Settings")]
        [SerializeField] private int maxLogHistory = 100;
        [SerializeField] private bool logToFile = true;
        [SerializeField] private bool showOnScreenGUI = false;

        private List<string> logEntries = new List<string>();
        private string logFilePath;
        private Vector2 scrollPosition;
        private bool isLogPanelExpanded = false;

        public event Action<string> OnLogAdded;
        public IReadOnlyList<string> LogEntries => logEntries;
        public string LogFilePath => logFilePath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            logFilePath = Path.Combine(Application.persistentDataPath, "ar_simulation_debug_log.txt");
            InitLogFile();
            LogEvent("SYSTEM", "=== AR Simulation Debug Logger Started ===");
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleUnityLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleUnityLog;
        }

        private void InitLogFile()
        {
            try
            {
                string header = $"AR FIRE SAFETY SIMULATION DEBUG LOG\nSession Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nPlatform: {Application.platform}\nPath: {logFilePath}\n=======================================================\n";
                File.WriteAllText(logFilePath, header, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ARSimulationLogger] Failed to write log header to file: {ex.Message}");
            }
        }

        public static void LogButton(string buttonName, string actionDescription)
        {
            if (Instance != null)
            {
                Instance.LogEvent("BUTTON", $"Clicked '{buttonName}' -> {actionDescription}");
            }
            else
            {
                Debug.Log($"[BUTTON] Clicked '{buttonName}' -> {actionDescription}");
            }
        }

        public static void LogKey(string keyName, string actionDescription)
        {
            if (Instance != null)
            {
                Instance.LogEvent("KEYBOARD", $"Pressed '{keyName}' -> {actionDescription}");
            }
            else
            {
                Debug.Log($"[KEYBOARD] Pressed '{keyName}' -> {actionDescription}");
            }
        }

        public static void LogState(string subsystem, string message)
        {
            if (Instance != null)
            {
                Instance.LogEvent("STATE", $"[{subsystem}] {message}");
            }
            else
            {
                Debug.Log($"[STATE] [{subsystem}] {message}");
            }
        }

        public void LogEvent(string category, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.ff");
            string entry = $"[{timestamp}] [{category}] {message}";

            logEntries.Add(entry);
            if (logEntries.Count > maxLogHistory)
            {
                logEntries.RemoveAt(0);
            }

            OnLogAdded?.Invoke(entry);

            if (logToFile)
            {
                AppendToFile(entry);
            }
        }

        private void AppendToFile(string entry)
        {
            try
            {
                File.AppendAllText(logFilePath, entry + "\n", Encoding.UTF8);
            }
            catch { }
        }

        private void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            if (logString.StartsWith("[") && (logString.Contains("ARSimulationLogger") || logString.Contains("EDITOR_CONTROL") || logString.Contains("BUTTON")))
            {
                return; // Avoid duplicate logging
            }

            if (type == LogType.Error || type == LogType.Exception)
            {
                LogEvent("ERROR", $"{logString}\n{stackTrace}");
            }
            else if (type == LogType.Warning)
            {
                LogEvent("WARN", logString);
            }
            else if (logString.Contains("[FireSafety") || logString.Contains("[FireExtinguisher") || logString.Contains("[ARPlacement"))
            {
                LogEvent("UNITY", logString);
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (Keyboard.current != null)
            {
                if (Keyboard.current.backquoteKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    isLogPanelExpanded = !isLogPanelExpanded;
                }
            }
#endif
        }

        private void OnGUI()
        {
            // Debug UI overlay log console completely disabled per user request
        }
    }
}
