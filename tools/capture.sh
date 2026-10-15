#!/usr/bin/env bash
# Screenshot the Linux build headlessly (Xvfb + Mesa software GL) for review.
#   tools/capture.sh out.png [rest|live|dark] [yaw,pitch]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(realpath -m "${1:?output png}")"
STATE="${2:-rest}"
PLAYER="$ROOT/Builds/Linux/LookImportant.x86_64"
ARGS=(-capture "$OUT" -capture-state "$STATE" -screen-width 1280 -screen-height 720 -screen-fullscreen 0
      -force-glcore -logFile "${OUT%.png}.log")
[[ -n "${3:-}" ]] && ARGS+=(-capture-look "$3")
xvfb-run -a -s "-screen 0 1280x720x24" "$PLAYER" "${ARGS[@]}"
grep -E 'AutoCapture|Exception|rror' "${OUT%.png}.log" | head -20 || true
ls -la "$OUT"
