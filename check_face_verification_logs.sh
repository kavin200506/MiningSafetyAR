#!/bin/bash
# ==============================================================================
# MiningSafetyAR - Mobile Face Verification Log Diagnostic Tool
# ==============================================================================
# Usage:
#   ./check_face_verification_logs.sh          (Live stream mode)
#   ./check_face_verification_logs.sh --dump   (Dump recent logs & exit)
#   ./check_face_verification_logs.sh --help   (Show help message)
# ==============================================================================

LOG_FILE="face_verification_logs.txt"
MAX_LINES=1500

# ANSI Color formatting
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}======================================================${NC}"
echo -e "${CYAN}  MiningSafetyAR - Face Verification Log Diagnostic  ${NC}"
echo -e "${CYAN}======================================================${NC}"

# Check ADB connection
if ! command -v adb &> /dev/null; then
    echo -e "${RED}[ERROR] 'adb' command not found. Please ensure Android SDK platform-tools are in your PATH.${NC}"
    exit 1
fi

DEVICE_COUNT=$(adb devices | grep -v "List" | grep "device" | wc -l | tr -d ' ')

if [ "$DEVICE_COUNT" -eq 0 ]; then
    echo -e "${YELLOW}[WARNING] No Android device connected via ADB.${NC}"
    echo "Please connect your mobile device via USB, enable USB Debugging, and try again."
    echo "Run 'adb devices' to confirm connection."
    exit 1
fi

echo -e "${GREEN}[OK] ADB Connected device(s) found: $DEVICE_COUNT${NC}"

# Pattern filter for Face Verification & Camera/Sentis related keywords, plus Adaptive Quiz keywords
FILTER_PATTERN="FaceVerificationService|WebCamTexture|Camera|Sentis|BlazeFace|MobileFaceNet|Permission|InferenceEngine|AndroidCameraPermissionHelper|EnrollFace|VerifyFace|QuizSelectionService|AdaptiveQuiz|QuestionBank|MistakeTag|GetAdaptiveQuiz|SubmitQuizResult"

if [ "$1" == "--dump" ]; then
    echo -e "\n${CYAN}Dumping recent Face Verification logs from device into ${LOG_FILE} (max ${MAX_LINES} lines)...${NC}\n"
    echo "=== Face Verification Log Dump at $(date) ===" > "$LOG_FILE"
    adb logcat -d -s Unity *:E *:W | grep -E -i "$FILTER_PATTERN" | tail -n $MAX_LINES | while read -r line; do
        echo "$line" >> "$LOG_FILE"
        if echo "$line" | grep -E -i "WARN|ERROR|FAILED|Exception|Timeout|Denied" > /dev/null; then
            echo -e "${RED}$line${NC}"
        else
            echo -e "${GREEN}$line${NC}"
        fi
    done
    exit 0
fi

echo -e "${CYAN}Streaming Unity Face Verification logs into ${LOG_FILE}...${NC}"
echo -e "${YELLOW}Trigger Face Verification on your mobile app now to capture errors.${NC}"
echo -e "Press ${RED}Ctrl+C${NC} to stop streaming.\n"

# Clear or initialize log file
echo "=== Face Verification Log Session Started at $(date) ===" > "$LOG_FILE"

# Live stream with highlighted error output
adb logcat -v time -s Unity *:E *:W | while read -r line; do
    # Check if the line matches our target face verification patterns or Unity errors
    if echo "$line" | grep -E -i "$FILTER_PATTERN|Exception|Error" > /dev/null; then
        # Append to log file
        echo "$line" >> "$LOG_FILE"
        
        # Color code terminal output
        if echo "$line" | grep -E -i "WARN|ERROR|FAILED|Exception|Timeout|Denied" > /dev/null; then
            echo -e "${RED}$line${NC}"
        elif echo "$line" | grep -E -i "DIAG|INFO|SUCCESS|Ready|Created" > /dev/null; then
            echo -e "${CYAN}$line${NC}"
        else
            echo "$line"
        fi

        # Maintain log size cap
        if [ $(wc -l < "$LOG_FILE") -gt $MAX_LINES ]; then
            tail -n $MAX_LINES "$LOG_FILE" > "${LOG_FILE}.tmp" && mv "${LOG_FILE}.tmp" "$LOG_FILE"
        fi
    fi
done
