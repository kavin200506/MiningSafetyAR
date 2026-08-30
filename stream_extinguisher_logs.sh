#!/bin/bash
# Stream live Fire Extinguisher AR device logs into extinguisher_logs.txt, keeping only the last 1500 lines.

MAX_LINES=1500
LOG_FILE="extinguisher_logs.txt"

echo "Streaming Fire Extinguisher AR logs to $LOG_FILE (keeping last $MAX_LINES lines)..."
echo "Press Ctrl+C to stop."

# Ensure output log file exists
touch "$LOG_FILE"

# Filter Unity logcat output for Fire Extinguisher, GLTFast, Submesh, Shader, and Pink status lines
adb logcat -s Unity | grep --line-buffered -i -E "extinguisher|gltfast|submesh|pink|shader|fire" | while read -r line; do
    echo "$line" >> "$LOG_FILE"
    if [ $(wc -l < "$LOG_FILE") -gt $MAX_LINES ]; then
        tail -n $MAX_LINES "$LOG_FILE" > "${LOG_FILE}.tmp" && mv "${LOG_FILE}.tmp" "$LOG_FILE"
    fi
done
