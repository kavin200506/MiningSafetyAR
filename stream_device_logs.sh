#!/bin/bash
# Stream live Unity device logs into device_logs.txt, keeping only the last MAX_LINES lines.

MAX_LINES=1500
LOG_FILE="device_logs.txt"

echo "Streaming Unity device logs to $LOG_FILE (keeping last $MAX_LINES lines)..."
echo "Press Ctrl+C to stop."

adb logcat -s Unity | while read -r line; do
    echo "$line" >> "$LOG_FILE"
    if [ $(wc -l < "$LOG_FILE") -gt $MAX_LINES ]; then
        tail -n $MAX_LINES "$LOG_FILE" > "${LOG_FILE}.tmp" && mv "${LOG_FILE}.tmp" "$LOG_FILE"
    fi
done
