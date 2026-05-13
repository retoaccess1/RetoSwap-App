#!/usr/bin/env bash
# run-tests.sh — Run the Appium smoke test suite against the running emulator.
#
# Requires:
#   - Emulator running (scripts/start-emulator.sh)
#   - APK installed (adb install -r <apk>)
#   - Appium installed ($PWD/.appium, done by shellHook)
#
# Usage (from nix develop shell, not inside android-env FHS):
#   APP_PACKAGE=com.example.myapp APP_ACTIVITY=.MainActivity scripts/run-tests.sh
#
# Environment variables:
#   APP_PACKAGE   — Android application package name (required)
#   APP_ACTIVITY  — Main activity class (default: .MainActivity)
#   APPIUM_HOST   — Appium server host (default: 127.0.0.1)
#   APPIUM_PORT   — Appium server port (default: 4723)

set -euo pipefail

APP_PACKAGE="${APP_PACKAGE:?Set APP_PACKAGE to the app package name}"
APP_ACTIVITY="${APP_ACTIVITY:-.MainActivity}"
APPIUM_HOST="${APPIUM_HOST:-127.0.0.1}"
APPIUM_PORT="${APPIUM_PORT:-4723}"
TEST_DIR="tests/appium"

echo "Running Appium smoke tests"
echo "  Package  : $APP_PACKAGE"
echo "  Activity : $APP_ACTIVITY"
echo "  Appium   : http://$APPIUM_HOST:$APPIUM_PORT"

# ── Start Appium server ───────────────────────────────────────────────────────
echo ""
echo "Starting Appium server..."
appium \
  --port "$APPIUM_PORT" \
  --log /tmp/appium.log \
  &
APPIUM_PID=$!
echo "  Appium PID: $APPIUM_PID"

# Wait for Appium to be ready
echo "Waiting for Appium to be ready..."
for i in $(seq 1 20); do
  if curl -s "http://$APPIUM_HOST:$APPIUM_PORT/status" | grep -qE '"ready"\s*:\s*true'; then
    echo "  Appium ready."
    break
  fi
  if [ "$i" -eq 20 ]; then
    echo "ERROR: Appium did not become ready in time." >&2
    kill "$APPIUM_PID" 2>/dev/null || true
    exit 1
  fi
  sleep 1
done

# ── Run tests ─────────────────────────────────────────────────────────────────
echo ""
echo "Running dotnet test..."
TEST_EXIT=0
APP_PACKAGE="$APP_PACKAGE" APP_ACTIVITY="$APP_ACTIVITY" \
  APPIUM_HOST="$APPIUM_HOST" APPIUM_PORT="$APPIUM_PORT" \
  dotnet test "$TEST_DIR" --logger "console;verbosity=normal" || TEST_EXIT=$?

# ── Stop Appium ───────────────────────────────────────────────────────────────
echo ""
echo "Stopping Appium server..."
kill "$APPIUM_PID" 2>/dev/null || true

if [ "$TEST_EXIT" -ne 0 ]; then
  echo ""
  echo "Tests FAILED (exit code: $TEST_EXIT). Appium log at /tmp/appium.log"
  exit "$TEST_EXIT"
fi

echo ""
echo "All tests PASSED."
