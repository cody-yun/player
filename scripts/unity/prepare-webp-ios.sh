#!/bin/sh
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cache="$repo/unity/TestResults/webp-ios"
mkdir -p "$cache"
archive="$repo/unity/TestResults/webp-webgl/libwebp-1.6.0.tar.gz"
if [ ! -f "$archive" ]; then mkdir -p "$(dirname "$archive")"; curl --fail --location 'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz' --output "$archive"; fi
python3 - "$archive" <<'PY'
import hashlib,sys
from pathlib import Path
assert hashlib.sha256(Path(sys.argv[1]).read_bytes()).hexdigest()=='e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564','Source archive hash mismatch'
PY
if [ ! -d "$cache/libwebp-1.6.0" ]; then tar -xzf "$archive" -C "$cache"; fi
cmake -S "$cache/libwebp-1.6.0" -B "$cache/build" -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_SYSROOT=iphoneos -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0 -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=OFF -DWEBP_BUILD_LIBWEBPDECODER=ON -DWEBP_BUILD_ANIM_UTILS=OFF -DWEBP_BUILD_CWEBP=OFF -DWEBP_BUILD_DWEBP=OFF -DWEBP_BUILD_GIF2WEBP=OFF -DWEBP_BUILD_IMG2WEBP=OFF -DWEBP_BUILD_VWEBP=OFF -DWEBP_BUILD_WEBPINFO=OFF -DWEBP_BUILD_LIBWEBPMUX=OFF -DWEBP_BUILD_WEBPMUX=OFF -DWEBP_BUILD_EXTRAS=OFF
cmake --build "$cache/build" --target webpdecoder --parallel 4
python3 - "$repo" <<'PY'
import hashlib,json,sys,shutil,subprocess
from pathlib import Path
repo=Path(sys.argv[1]);src=repo/'unity/TestResults/webp-ios/build/libwebpdecoder.a';sha=hashlib.sha256(src.read_bytes()).hexdigest();manifest=repo/'unity/Packages/ai.livliv.player/ThirdParty/libwebp-ios.json'
if manifest.exists() and json.loads(manifest.read_text())['artifactSha256']!=sha:raise SystemExit('iOS decoder differs from pinned artifact; review toolchain')
dest=repo/'unity/Packages/ai.livliv.player/Plugins/iOS/liblivlivwebp.a';dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
data={'version':'1.6.0','source':'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz','sourceSha256':'e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564','artifactSha256':sha,'xcode':subprocess.check_output(['xcodebuild','-version'],text=True).strip(),'sdk':subprocess.check_output(['xcrun','--sdk','iphoneos','--show-sdk-version'],text=True).strip(),'architecture':'arm64','deploymentTarget':'15.0'}
if not manifest.exists():manifest.write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps(data,indent=2))
PY
