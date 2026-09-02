#!/bin/bash
# Stream live Voice Command AR logs (VoiceCommandManager|LanguageManager|ARNarrationController|Bhashini|Vosk|Santali) into voicelogs.txt, keeping only the last 1500 lines.

MAXLINES=1500
LOGFILE="voicelogs.txt"

echo "Streaming Voice Command AR logs to $LOGFILE, keeping last $MAXLINES lines..."
echo "Press Ctrl+C to stop."

# Ensure output log file exists before streaming begins
touch "$LOGFILE"

# Filter Unity logcat output for VoiceCommandManager, LanguageManager, ARNarrationController, Bhashini, Vosk, and Santali
adb logcat -s Unity | grep --line-buffered -i -E "VoiceCommandManager|LanguageManager|ARNarrationController|Bhashini|Vosk|Santali" | while read -r line; do
    echo "$line" >> "$LOGFILE"
    if [ $(wc -l < "$LOGFILE") -gt $MAXLINES ]; then
        tail -n "$MAXLINES" "$LOGFILE" > "$LOGFILE.tmp" && mv "$LOGFILE.tmp" "$LOGFILE"
    fi
done
