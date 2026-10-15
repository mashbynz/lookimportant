#!/usr/bin/env bash
# Regenerate the Unity scene from the Blender export and build players.
#   tools/build.sh                     linux,windows,webgl
#   tools/build.sh linux               just Linux
#   tools/build.sh --scene-only        rebuild Assets/Scenes/Workbench.unity, no players
# Players go to Builds/<Platform>/, the log to Builds/build.log.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
mkdir -p "$ROOT/Builds"
LOG="$ROOT/Builds/build.log"

if [[ "${1:-}" == "--scene-only" ]]; then
  METHOD=LookImportant.EditorTools.BuildScript.RunSceneOnly
  TARGETS=none
else
  METHOD=LookImportant.EditorTools.BuildScript.Run
  TARGETS="${1:-linux,windows,webgl}"
fi

set +e
"$UNITY" -batchmode -nographics -projectPath "$ROOT/game" -logFile "$LOG" \
  -executeMethod "$METHOD" -lookimportant-targets "$TARGETS"
code=$?
set -e
grep -E '\[(BuildScript|SceneBuilder)\]|error CS|Exception|Build (succeeded|failed)' "$LOG" | grep -v '^UnityEngine' | tail -40 || true
echo "Unity exited with $code (full log: $LOG)"
exit $code
