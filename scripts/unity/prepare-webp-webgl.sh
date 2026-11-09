#!/bin/sh
# Build the pinned decoder with the exact Unity Editor's Emscripten toolchain.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
editor_root=${1:-/Applications/Unity/Hub/Editor/6000.3.14f1}
toolchain="$editor_root/PlaybackEngines/WebGLSupport/BuildTools/Emscripten"
cache="$repo/unity/TestResults/webp-webgl"
mkdir -p "$cache"
archive="$cache/libwebp-1.6.0.tar.gz"
if [ ! -f "$archive" ]; then curl --fail --location --retry 2 'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz' --output "$archive"; fi
python3 - "$archive" <<'PY'
import hashlib,sys
from pathlib import Path
if hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest()!='e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564':raise SystemExit('libwebp source archive hash mismatch')
PY
if [ ! -d "$cache/libwebp-1.6.0" ]; then tar -xzf "$archive" -C "$cache"; fi
export EM_CONFIG="$toolchain/.emscripten"
"$toolchain/emscripten/emcc" --version > "$cache/toolchain-version.txt"
"$toolchain/emscripten/emcmake" cmake -S "$cache/libwebp-1.6.0" -B "$cache/build" -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_FLAGS='-O2' -DBUILD_SHARED_LIBS=OFF -DWEBP_ENABLE_SIMD=OFF -DWEBP_BUILD_LIBWEBPDECODER=ON -DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEBP=OFF -DWEBP_BUILD_DWEBP=OFF -DWEBP_BUILD_GIF2WEBP=OFF -DWEBP_BUILD_IMG2WEBP=OFF -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF -DWEBP_BUILD_LIBWEBPMUX=OFF -DWEBP_BUILD_WEBPMUX=OFF -DWEBP_BUILD_EXTRAS=OFF
cmake --build "$cache/build" --target webpdecoder --parallel 4
mkdir -p "$repo/unity/Packages/ai.livliv.player/Plugins/WebGL"
python3 - "$cache/build/libwebpdecoder.a" "$repo/unity/Packages/ai.livliv.player/ThirdParty/libwebp-webgl.json" <<'PYVERIFY'
import hashlib,json,sys
from pathlib import Path
artifact,manifest=map(Path,sys.argv[1:]);actual=hashlib.sha256(artifact.read_bytes()).hexdigest()
if manifest.exists() and actual!=json.loads(manifest.read_text())['artifactSha256']:raise SystemExit('Rebuilt WASM decoder differs from pinned artifact; review toolchain before updating provenance')
PYVERIFY
cp "$cache/build/libwebpdecoder.a" "$repo/unity/Packages/ai.livliv.player/Plugins/WebGL/liblivlivwebp.a"
python3 - "$repo" <<'PY'
import hashlib,json,sys
from pathlib import Path
r=Path(sys.argv[1]);lib=r/'unity/Packages/ai.livliv.player/Plugins/WebGL/liblivlivwebp.a';version=(r/'unity/TestResults/webp-webgl/toolchain-version.txt').read_text().splitlines()[0]
print(json.dumps({'artifact':str(lib),'sha256':hashlib.sha256(lib.read_bytes()).hexdigest(),'toolchain':version},indent=2))
PY
