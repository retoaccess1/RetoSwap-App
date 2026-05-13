#!/usr/bin/env bash
# start-emulator.sh — Boot the dev35 AVD headlessly and wait until fully booted.
#
# Must run inside `android-env` (the FHS shell) so `emulator` and `adb` are on PATH.
# Usage: android-env -c 'scripts/start-emulator.sh'
#
# Exits with code 0 only when sys.boot_completed=1.
# Exits with code 1 if the emulator process dies during boot.
# Boot timeout: 300 seconds (swiftshader_indirect can take 3-5 min on cold cache).

set -euo pipefail

AVD_NAME="${AVD_NAME:-dev35}"
BOOT_TIMEOUT="${BOOT_TIMEOUT:-300}"  # seconds

echo "Starting emulator: $AVD_NAME (headless)"

# Launch emulator in background
emulator \
  -avd "$AVD_NAME" \
  -no-window \
  -no-audio \
  -no-boot-anim \
  -gpu swiftshader_indirect \
  -wipe-data \
  &
EMU_PID=$!
echo "  Emulator PID: $EMU_PID"

# Wait for ADB to detect the device (bounded by BOOT_TIMEOUT to prevent indefinite hang)
echo "Waiting for ADB device..."
timeout "$BOOT_TIMEOUT" adb wait-for-device || {
  echo "ERROR: ADB device did not appear within ${BOOT_TIMEOUT}s." >&2
  kill "$EMU_PID" 2>/dev/null || true
  exit 1
}

echo "Waiting for boot to complete (timeout: ${BOOT_TIMEOUT}s)..."
ELAPSED=0
INTERVAL=3

while true; do
  # Check that the emulator process is still alive
  if ! kill -0 "$EMU_PID" 2>/dev/null; then
    echo "ERROR: Emulator process ($EMU_PID) died unexpectedly." >&2
    exit 1
  fi

  BOOT=$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r\n')
  if [ "$BOOT" = "1" ]; then
    echo "Emulator booted successfully (${ELAPSED}s)."
    # Allow the launcher to finish settling
    sleep 2
    exit 0
  fi

  if [ "$ELAPSED" -ge "$BOOT_TIMEOUT" ]; then
    echo "ERROR: Emulator boot timed out after ${BOOT_TIMEOUT}s." >&2
    kill "$EMU_PID" 2>/dev/null || true
    exit 1
  fi

  sleep "$INTERVAL"
  ELAPSED=$((ELAPSED + INTERVAL))
done
