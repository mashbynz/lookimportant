#!/usr/bin/env bash
# Benchmark the Linux build on this machine's real GPU, without a monitor:
# headless Weston (GL renderer) + Xwayland, vsync off, frame times to the log.
#   tools/perf.sh [width] [height] [seconds] [state] [extra player args...]
# Extra args are AutoCapture's -perf-* switches, e.g. -perf-msaa 1 -perf-nossao.
# Needs read/write on /dev/dri/renderD128 (render group, or a seat ACL).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
W="${1:-1440}"; H="${2:-900}"; SECS="${3:-10}"; STATE="${4:-live}"
shift $(( $# < 4 ? $# : 4 ))
PLAYER="$ROOT/Builds/Linux/LookImportant.x86_64"
WORK="$(mktemp -d)"
trap 'kill ${WESTON:-} 2>/dev/null || true; rm -rf "$WORK"' EXIT
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
SOCK="lookimportant-perf-$$"

weston --backend=headless --renderer=gl --width="$W" --height="$H" --xwayland \
  --socket="$SOCK" --idle-time=0 --log="$WORK/weston.log" &
WESTON=$!
for _ in $(seq 1 50); do grep -q "xserver listening on display\|Loading module.*xwayland" "$WORK/weston.log" 2>/dev/null && break; sleep 0.2; done
sleep 1
DISP="$(grep -o 'xserver listening on display :[0-9]*' "$WORK/weston.log" | tail -1 | awk '{print $NF}')"
DISP="${DISP:-:0}"
grep -iE 'renderer|EGL vendor|GL renderer|xwayland|xserver' "$WORK/weston.log" | head -5 || true

DISPLAY="$DISP" WAYLAND_DISPLAY="$SOCK" "$PLAYER" -capture "$WORK/perf.png" -capture-state "$STATE" \
  -capture-seconds "$SECS" -screen-width "$W" -screen-height "$H" -screen-fullscreen 1 -logFile "$WORK/player.log" "$@" || true
grep -E 'AutoCapture|Exception' "$WORK/player.log" | head -8
