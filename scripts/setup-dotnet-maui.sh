#!/usr/bin/env bash
# setup-dotnet-maui.sh — Install .NET MAUI Android workloads into a mutable copy.
#
# Run once after `nix develop`. Re-run when the flake's dotnet-sdk_9 version
# bumps (the shellHook will warn about a mismatch).
#
# Why this script exists:
#   dotnet-sdk_9 from nixpkgs lives in the read-only Nix store. The `dotnet
#   workload install` command requires a writable SDK root. This script copies
#   the Nix SDK to $HOME/.dotnet-maui (writable) and installs the MAUI Android
#   workload there. The shellHook in flake.nix detects this copy and sets
#   DOTNET_ROOT accordingly.
#
# NixOS-specific steps this script performs (in addition to the copy):
#   1. patchelf: The MAUI SDK bundles pre-compiled Linux binaries (llc, ld,
#      mono-aot-cross, …) that hard-code /lib64/ld-linux-x86-64.so.2 as their
#      ELF interpreter. On NixOS that path is a stub. This script patches all
#      such executables to use the real glibc interpreter. GLIBC_INTERP is
#      injected by flake.nix so the path is always in sync.
#   2. shebang-fix: Several binutils wrapper scripts use #!/bin/bash which does
#      not exist on NixOS. This script rewrites those shebangs to the real bash.

set -euo pipefail

MUTABLE_ROOT="$HOME/.dotnet-maui"
MARKER="$MUTABLE_ROOT/.nix-sdk-source"

# DOTNET_SDK_NIX_ROOT is exported by the flake.nix shellHook.
# Fall back to the env var set by dotnet-sdk_9 if running standalone.
NIX_SDK_ROOT="${DOTNET_SDK_NIX_ROOT:-}"

if [ -z "$NIX_SDK_ROOT" ]; then
  echo "ERROR: DOTNET_SDK_NIX_ROOT is not set. Run this script inside 'nix develop'." >&2
  exit 1
fi

if [ ! -d "$NIX_SDK_ROOT" ]; then
  echo "ERROR: Nix SDK root does not exist: $NIX_SDK_ROOT" >&2
  exit 1
fi

# ── Check if rebuild is needed ────────────────────────────────────────────────
if [ -f "$MARKER" ] && [ "$(cat "$MARKER")" = "$NIX_SDK_ROOT" ]; then
  echo "MAUI workloads already installed and up to date."
  echo "  SDK root: $MUTABLE_ROOT"
  dotnet workload list
  exit 0
fi

# ── Copy SDK to mutable location ─────────────────────────────────────────────
echo "Copying Nix dotnet SDK to mutable location..."
echo "  From: $NIX_SDK_ROOT"
echo "  To:   $MUTABLE_ROOT"

if [ -d "$MUTABLE_ROOT" ]; then
  rm -rf "$MUTABLE_ROOT"
fi

# -aL: archive mode + dereference all symlinks.
# Required because dotnet-sdk-wrapped has symlinks into the read-only Nix store;
# cp -a would preserve those symlinks and dotnet workload install would then fail
# when it tries to write to their Nix store targets.
cp -aL "$NIX_SDK_ROOT" "$MUTABLE_ROOT"
chmod -R u+w "$MUTABLE_ROOT"

# ── Override DOTNET_ROOT for this session ─────────────────────────────────────
export DOTNET_ROOT="$MUTABLE_ROOT"
export PATH="$MUTABLE_ROOT:$PATH"

echo ""
echo "Installing MAUI Android workload..."
dotnet workload install maui-android

# ── NixOS: patchelf MAUI native binaries ─────────────────────────────────────
# The workload installs pre-compiled ELF binaries (llc, ld, mono-aot-cross,
# as, …) that hard-code /lib64/ld-linux-x86-64.so.2 as the ELF interpreter.
# On NixOS that path is a stub. Some binaries are UPX-packed (statically-linked
# outer shell that decompresses a dynamically-linked inner binary) — these must
# be UPX-decompressed first, then patchelf'd.
# GLIBC_INTERP is injected by flake.nix so the path is always correct.
if [ -n "${GLIBC_INTERP:-}" ] && command -v patchelf &>/dev/null; then
  echo ""
  echo "Patching MAUI native ELF binaries for NixOS (patchelf)..."

  # Step 1: UPX-decompress any packed binaries (they show as "statically linked,
  # no section header" in file(1) output — a hallmark of UPX packing).
  if command -v upx &>/dev/null; then
    find "$MUTABLE_ROOT/packs" -type f -executable 2>/dev/null | \
    while IFS= read -r f; do
      if file "$f" 2>/dev/null | grep -q "statically linked.*no section header"; then
        upx -dq "$f" 2>/dev/null && echo "  upx-unpacked: $(basename "$f")"
      fi
    done
  fi

  # Step 2: patchelf all dynamically-linked executables.
  find "$MUTABLE_ROOT/packs" -type f -executable 2>/dev/null | \
  while IFS= read -r f; do
    if file "$f" 2>/dev/null | grep -q "ELF 64-bit.*dynamically linked"; then
      patchelf --set-interpreter "$GLIBC_INTERP" "$f" 2>/dev/null && \
        echo "  patched: $(basename "$f")"
    fi
  done
  echo "  patchelf done."
elif [ -z "${GLIBC_INTERP:-}" ]; then
  echo "WARNING: GLIBC_INTERP not set — skipping patchelf (run inside 'nix develop')."
elif ! command -v patchelf &>/dev/null; then
  echo "WARNING: patchelf not found — skipping NixOS binary patch."
  echo "  Add patchelf to flake.nix buildInputs and re-run."
fi

# ── NixOS: fix shell script shebangs ─────────────────────────────────────────
# Several MAUI binutils wrapper scripts use #!/bin/bash or #!/bin/sh which do
# not exist on NixOS. Rewrite to the real bash path from the current session.
BASH_BIN="${BASH:-$(command -v bash 2>/dev/null)}"
if [ -n "$BASH_BIN" ]; then
  BASH_REAL=$(readlink -f "$BASH_BIN")
  echo ""
  echo "Patching shell script shebangs for NixOS..."
  n=0
  while IFS= read -r f; do
    first=$(head -c 20 "$f" 2>/dev/null | tr -d '\0')
    if echo "$first" | grep -qE '^#!/bin/bash|^#!/bin/sh'; then
      sed -i "1s|#!/bin/bash|#!${BASH_REAL}|; 1s|#!/bin/sh|#!${BASH_REAL}|" "$f" 2>/dev/null && n=$((n+1))
    fi
  done < <(find "$MUTABLE_ROOT/packs" -type f 2>/dev/null)
  echo "  shebang patch done ($n files patched)."
fi

# ── Write the marker ─────────────────────────────────────────────────────────
printf '%s' "$NIX_SDK_ROOT" > "$MARKER"

echo ""
echo "Done. MAUI Android workloads installed."
echo "  DOTNET_ROOT: $MUTABLE_ROOT"
echo ""
echo "Installed workloads:"
dotnet workload list
echo ""
echo "Re-open 'nix develop' for the shellHook to pick up DOTNET_ROOT automatically."
