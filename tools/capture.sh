#!/usr/bin/env bash
# Screenshot the Linux build headlessly (Xvfb + Mesa software GL) for review.
#   tools/capture.sh out.png [rest|live|dark] [yaw,pitch]
# Software rendering is slow; the first frames also compile shaders, so allow a few minutes.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$(realpath -m "${1:?output png}")"
STATE="${2:-rest}"
PLAYER="$ROOT/Builds/Linux/LookImportant.x86_64"
LOG="${OUT%.png}.log"
ARGS=(-capture "$OUT" -capture-state "$STATE" -screen-width 1280 -screen-height 720 -screen-fullscreen 0
      -force-glcore -logFile "$LOG")
[[ -n "${3:-}" ]] && ARGS+=(-capture-look "$3")

DISP=":$((90 + RANDOM % 9))"
Xvfb "$DISP" -screen 0 1280x720x24 -nolisten tcp >/dev/null 2>&1 &
XVFB=$!
trap 'kill $XVFB 2>/dev/null || true' EXIT
export DISPLAY="$DISP"
sleep 1
"$PLAYER" "${ARGS[@]}" &
GAME=$!
# No window manager under Xvfb, so give the window input focus ourselves.
for _ in $(seq 1 60); do
  W="$(xdotool search --name 'Look Important' 2>/dev/null | head -1 || true)"
  if [[ -n "$W" ]]; then xdotool windowfocus "$W" 2>/dev/null || true; break; fi
  sleep 1
done
wait "$GAME" || true
grep -E 'AutoCapture|Exception|rror' "$LOG" | head -20 || true
ls -la "$OUT"
