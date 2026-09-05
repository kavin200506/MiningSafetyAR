#!/usr/bin/env bash
# ==============================================================================
# FIRE ALARM DIAGNOSTICS & TAP MONITOR
# Monitors real-time Fire Alarm Button spawning, screen tap locations, raycast hits,
# 3D emergency light state, and screen edge alert overlay toggles.
# ==============================================================================

LOG_FILE="fire_alarm_diagnostics.log"

echo "======================================================================"
echo "🚨 STARTING FIRE ALARM MONITOR & DIAGNOSTIC LOG TRACKER"
echo "======================================================================"
echo "Writing live logs to: ${LOG_FILE}"
echo "Filter targets: [SPAWN_DIAG], [ALARM_DIAG], AlarmButton, ScreenEdgeAlertUI"
echo "Press Ctrl+C to stop logging at any time."
echo "======================================================================"

# Ensure ADB is available
if ! command -v adb &> /dev/null; then
    echo "⚠️ ADB tool not found in PATH! Make sure Android SDK platform-tools are installed."
    echo "Logs will still appear inside the Unity Editor Console."
    exit 1
fi

# Check connected devices
DEVICES=$(adb devices | grep -v "List" | grep "device")
if [ -z "$DEVICES" ]; then
    echo "⚠️ No USB Android device found connected via adb."
    echo "Logs will still print in the Unity Editor Console during Play Mode."
    echo "To view device logs: Connect your phone via USB & enable USB Debugging."
    exit 1
fi

echo "✅ Connected Android Device detected!"
echo "Starting adb logcat live stream..."
echo ""

# Clear logcat buffer first for clean session
adb logcat -c

# Monitor ADB logcat filtering specifically for Fire Alarm tags
adb logcat -v time Unity:V CRASH:E *:S | grep --line-buffered -E "ALARM_DIAG|SPAWN_DIAG|AlarmButton|ScreenEdgeAlertUI|NotifyAlarmActivated" | tee -a "${LOG_FILE}"
