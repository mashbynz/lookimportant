#!/usr/bin/env bash
# Reinstall the toolchain on a fresh machine. Everything is user-local except
# two small apt packages. Unity still needs a Personal license afterwards:
# copy Unity_lic.ulf to ~/.local/share/unity3d/Unity/ (see README).
set -euo pipefail
BLENDER_VER=5.2.2
UNITY_VER=6000.3.26f1
UNITY_REV=de8167e7440c
DL="$HOME/Downloads/lookimportant-tools"
mkdir -p "$DL" "$HOME/.local/opt" "$HOME/.local/bin"

sudo apt-get install -y libarchive-tools xvfb   # bsdtar for Unity .pkg modules, virtual display for captures

if [[ ! -x "$HOME/.local/opt/blender-$BLENDER_VER-linux-x64/blender" ]]; then
  curl -fL -o "$DL/blender.tar.xz" "https://download.blender.org/release/Blender${BLENDER_VER%.*}/blender-$BLENDER_VER-linux-x64.tar.xz"
  tar -xf "$DL/blender.tar.xz" -C "$HOME/.local/opt"
fi
ln -sfn "$HOME/.local/opt/blender-$BLENDER_VER-linux-x64/blender" "$HOME/.local/bin/blender"

EDITOR="$HOME/Unity/Hub/Editor/$UNITY_VER"
BASE="https://download.unity3d.com/download_unity/$UNITY_REV"
if [[ ! -x "$EDITOR/Editor/Unity" ]]; then
  mkdir -p "$EDITOR"
  curl -fL "$BASE/LinuxEditorInstaller/Unity-$UNITY_VER.tar.xz" | tar -xJ -C "$EDITOR"
fi
if [[ ! -d "$EDITOR/Editor/Data/PlaybackEngines/WebGLSupport" ]]; then
  curl -fL "$BASE/LinuxEditorTargetInstaller/UnitySetup-WebGL-Support-for-Editor-$UNITY_VER.tar.xz" | tar -xJ -C "$EDITOR"
fi
WIN="$EDITOR/Editor/Data/PlaybackEngines/WindowsStandaloneSupport"
if [[ ! -d "$WIN" ]]; then
  tmp="$(mktemp -d)"
  curl -fL -o "$tmp/win.pkg" "$BASE/MacEditorTargetInstaller/UnitySetup-Windows-Mono-Support-for-Editor-$UNITY_VER.pkg"
  (cd "$tmp" && bsdtar -xf win.pkg)
  mkdir -p "$WIN" && bsdtar -xf "$tmp/TargetSupport.pkg.tmp/Payload" -C "$WIN"
  rm -rf "$tmp"
fi
echo "Blender: $(blender -b --version 2>/dev/null | head -1)"
echo "Unity:   $EDITOR/Editor/Unity"
