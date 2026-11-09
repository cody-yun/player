#!/bin/sh
# Reproduce the shipped ARM64/x86_64 universal decoder from pinned libwebp source.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
python3 "$repo/scripts/unity/build-webp-macos.py"
