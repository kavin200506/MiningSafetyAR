#!/usr/bin/env bash
# ==============================================================================
# Mining Safety AR - Location & Navigation Log Monitor Script
# ==============================================================================
# Monitors live Android device logs via 'adb logcat' or parses log files to
# inspect Location Capture, GPS Hardware, Reverse Geocoding, and UI Scene Transitions.
# Automatically saves detailed output to 'location_monitor_logs.txt' (capped at 1500 lines).
#
# Usage:
#   ./monitor_location_logs.sh                   # Live stream from connected Android device (ADB)
#   ./monitor_location_logs.sh device_logs.txt   # Parse existing log file
# ==============================================================================

# Output file & line limit configuration
OUTPUT_LOG_FILE="location_monitor_logs.txt"
MAX_LINES=1500

# Touch log file to guarantee existence
touch "$OUTPUT_LOG_FILE"

# ANSI Color Definitions
CLR_RESET="\033[0m"
CLR_BOLD="\033[1m"
CLR_RED="\033[1;31m"
CLR_GREEN="\033[1;32m"
CLR_YELLOW="\033[1;33m"
CLR_BLUE="\033[1;34m"
CLR_MAGENTA="\033[1;35m"
CLR_CYAN="\033[1;36m"
CLR_WHITE="\033[1;37m"

echo -e "${CLR_CYAN}${CLR_BOLD}========================================================================${CLR_RESET}"
echo -e "${CLR_CYAN}${CLR_BOLD}   Mining Safety AR — Location & Navigation Log Monitor Tool            ${CLR_RESET}"
echo -e "${CLR_CYAN}${CLR_BOLD}========================================================================${CLR_RESET}"
echo -e "${CLR_YELLOW}Logs auto-saved to: ${CLR_BOLD}${OUTPUT_LOG_FILE}${CLR_YELLOW} (Max limit: ${MAX_LINES} lines)${CLR_RESET}\n"

# Log filter terms for grep
FILTER_REGEX="TrainingLocationCapture|LocationCapturePage|ModuleDetail|NavigationManager|\[Nav\]|\[PageController\]|ReverseGeocode|LocationServiceStatus|UI_LocationCapture|AR Plane Detection Placement|UI_ARSimulation|UI_Assessment|UI_Results|UI_Certificate|ARPlacementManager|ACCESS_FINE_LOCATION|InvalidOperationException|DllNotFoundException"

# Helper function to append line to output log file and maintain 1500 lines limit
append_to_file() {
    local tag="$1"
    local raw_line="$2"
    echo "[$tag] $raw_line" >> "$OUTPUT_LOG_FILE"

    # Enforce 1500 line limit on file
    local current_lines
    current_lines=$(wc -l < "$OUTPUT_LOG_FILE" 2>/dev/null || echo 0)
    if [ "$current_lines" -gt "$MAX_LINES" ]; then
        local tmp_file="${OUTPUT_LOG_FILE}.tmp"
        tail -n "$MAX_LINES" "$OUTPUT_LOG_FILE" > "$tmp_file" && mv "$tmp_file" "$OUTPUT_LOG_FILE"
    fi
}

format_and_save_log() {
    while IFS= read -r line; do
        if echo "$line" | grep -qE "Location resolved|Verified Location|ReverseGeocode OK|NavigateTo.*UI_LocationCapture|NavigateTo.*UI_ARSimulation"; then
            append_to_file "LOCATION OK" "$line"
            echo -e "${CLR_GREEN}${CLR_BOLD}[LOCATION OK] ${line}${CLR_RESET}"
        elif echo "$line" | grep -qE "ReverseGeocode|Reverse geocoding|Requesting ACCESS_FINE_LOCATION|Location service started|Starting location capture"; then
            append_to_file "GPS PENDING" "$line"
            echo -e "${CLR_YELLOW}${CLR_BOLD}[GPS PENDING] ${line}${CLR_RESET}"
        elif echo "$line" | grep -qE "NavigateTo|Loading scene|LoadSceneAsync|OnPageEnter"; then
            append_to_file "SCENE NAV" "$line"
            echo -e "${CLR_BLUE}${CLR_BOLD}[SCENE NAV]   ${line}${CLR_RESET}"
        elif echo "$line" | grep -qE "User location consent|HasUserConsented"; then
            append_to_file "CONSENT" "$line"
            echo -e "${CLR_MAGENTA}${CLR_BOLD}[CONSENT]     ${line}${CLR_RESET}"
        elif echo "$line" | grep -qE "InvalidOperationException|DllNotFoundException|Exception|Error|WARN|FAILED"; then
            append_to_file "ERROR/WARN" "$line"
            echo -e "${CLR_RED}${CLR_BOLD}[ERROR/WARN]  ${line}${CLR_RESET}"
        else
            append_to_file "INFO" "$line"
            echo -e "${CLR_WHITE}${line}${CLR_RESET}"
        fi
    done
}

if [ -n "$1" ]; then
    if [ -f "$1" ]; then
        echo -e "${CLR_BLUE}--> Parsing offline log file: ${CLR_BOLD}$1${CLR_RESET}\n"
        grep -E "$FILTER_REGEX" "$1" | format_and_save_log
    else
        echo -e "${CLR_RED}Error: Log file '$1' not found!${CLR_RESET}"
        exit 1
    fi
else
    echo -e "${CLR_YELLOW}--> Connecting to ADB live device logcat...${CLR_RESET}"
    if ! command -v adb &> /dev/null; then
        echo -e "${CLR_RED}Error: 'adb' command not found in PATH!${CLR_RESET}"
        echo -e "Tip: You can also pass a log file as an argument: ${CLR_BOLD}./monitor_location_logs.sh device_logs.txt${CLR_RESET}"
        exit 1
    fi

    # Clear old logs and start streaming filtered logcat
    adb logcat -c 2>/dev/null
    echo -e "${CLR_GREEN}Streaming live logs from device (Press Ctrl+C to stop)...${CLR_RESET}\n"
    adb logcat -v time | grep --line-buffered -E "$FILTER_REGEX" | format_and_save_log
fi
