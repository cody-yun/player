#!/bin/sh
# xvfb-run's SIGUSR1 readiness wait can hang under Docker Desktop Rosetta.
# This isolated, network-disabled container uses the X socket readiness instead.
set -eu
export DISPLAY=:99
Xvfb "$DISPLAY" -screen 0 1280x720x24 -ac -nolisten tcp >/out/xvfb.log 2>&1 &
xvfb_pid=$!
trap 'kill "$xvfb_pid" 2>/dev/null || true' EXIT HUP INT TERM
attempt=0
while [ ! -S /tmp/.X11-unix/X99 ]; do
    kill -0 "$xvfb_pid"
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 100 ]; then cat /out/xvfb.log; exit 1; fi
    sleep .1
done
"$@"
