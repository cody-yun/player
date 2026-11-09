#!/bin/sh
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
editor_root=${1:-/Applications/Unity/Hub/Editor/6000.3.14f1}
ndk="$editor_root/PlaybackEngines/AndroidPlayer/NDK"
cache="$repo/unity/TestResults/webp-android"
mkdir -p "$cache"
archive="$repo/unity/TestResults/webp-webgl/libwebp-1.6.0.tar.gz"
if [ ! -f "$archive" ]; then mkdir -p "$(dirname "$archive")"; curl --fail --location 'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz' --output "$archive"; fi
python3 - "$archive" <<'PY'
import hashlib,sys
from pathlib import Path
assert hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest()=='e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564','Source archive hash mismatch'
PY
if [ ! -d "$cache/libwebp-1.6.0" ]; then tar -xzf "$archive" -C "$cache"; fi
cmake -S "$cache/libwebp-1.6.0" -B "$cache/build" -DCMAKE_TOOLCHAIN_FILE="$ndk/build/cmake/android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON -DWEBP_BUILD_LIBWEBPDECODER=ON -DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEBP=OFF -DWEBP_BUILD_DWEBP=OFF -DWEBP_BUILD_GIF2WEBP=OFF -DWEBP_BUILD_IMG2WEBP=OFF -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF -DWEBP_BUILD_LIBWEBPMUX=OFF -DWEBP_BUILD_WEBPMUX=OFF -DWEBP_BUILD_EXTRAS=OFF
cmake --build "$cache/build" --target webpdecoder --parallel 4
python3 - "$repo" "$ndk" <<'PY'
import hashlib,json,sys,shutil
from pathlib import Path
repo,ndk=map(Path,sys.argv[1:]);src=repo/'unity/TestResults/webp-android/build/libwebpdecoder.so';sha=hashlib.sha256(src.read_bytes()).hexdigest()
manifest=repo/'unity/Packages/ai.livliv.player/ThirdParty/libwebp-android.json'
if manifest.exists() and json.loads(manifest.read_text())['artifactSha256']!=sha:raise SystemExit('Android decoder differs from pinned artifact; review toolchain')
dest=repo/'unity/Packages/ai.livliv.player/Plugins/Android/arm64-v8a/liblivlivwebp.so';dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
data={'version':'1.6.0','source':'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz','sourceSha256':'e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564','artifactSha256':sha,'ndk':(ndk/'source.properties').read_text(),'abi':'arm64-v8a','minApi':24,'flexiblePageSizes':True}
if not manifest.exists():manifest.write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps(data,indent=2))
PY
