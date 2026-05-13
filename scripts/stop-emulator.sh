#!/usr/bin/env bash
# stop-emulator.sh — Gracefully stop the running Android emulator.
#
# Must run inside `android-env` or with adb on PATH.
# Usage: android-env -c 'scripts/stop-emulator.sh'

set -euo pipefail

echo "Stopping Android emulator..."

if adb devices | grep -q "emulator"; then
  adb emu kill
  echo "Emulator stopped."
else
  echo "No running emulator found."
fi
