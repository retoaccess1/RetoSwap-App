#!/usr/bin/env bash
# create-avd.sh — Create the dev35 Android Virtual Device (one-time setup).
#
# Must run inside `android-env` (the FHS shell) so avdmanager is on PATH.
# Usage: android-env -c 'scripts/create-avd.sh'
#
# Creates AVD: dev35
#   API level : 35 (Android 15)
#   ABI       : x86_64 (KVM-accelerated on NixOS with /dev/kvm accessible)
#   Image type: google_apis
#
# KVM prerequisite: /dev/kvm must be readable.
# On NixOS with virtualisation.libvirtd.enable = true, /dev/kvm is world-accessible.
# Verify: ls -la /dev/kvm

set -euo pipefail

AVD_NAME="${AVD_NAME:-dev35}"
SYSTEM_IMAGE="system-images;android-35;google_apis;x86_64"
# Fallback if google_apis unavailable: "system-images;android-35;default;x86_64"

echo "Creating AVD: $AVD_NAME"
echo "  System image : $SYSTEM_IMAGE"
echo "  Device       : pixel_5"

# Check KVM access (warning, not failure — emulator falls back to software)
if [ ! -r /dev/kvm ]; then
  echo "WARNING: /dev/kvm not accessible. Emulator will use software rendering (slow)."
  echo "  On NixOS: ensure virtualisation.libvirtd.enable = true and user is in kvm group."
fi

# Delete existing AVD with same name to ensure clean state
avdmanager delete avd --name "$AVD_NAME" 2>/dev/null || true

avdmanager create avd \
  --name "$AVD_NAME" \
  --package "$SYSTEM_IMAGE" \
  --device "pixel_5" \
  --force

echo ""
echo "AVD '$AVD_NAME' created successfully."
echo "Start it: scripts/start-emulator.sh"
