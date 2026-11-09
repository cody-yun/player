#!/bin/sh
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cache="$repo/unity/TestResults/webp-desktop"
mkdir -p "$cache"
archive="$repo/unity/TestResults/webp-webgl/libwebp-1.6.0.tar.gz"
if [ ! -f "$archive" ]; then
    mkdir -p "$(dirname "$archive")"
    curl --fail --location 'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz' --output "$archive"
fi
docker build --platform linux/amd64 -t livliv-desktop-native:24.04 -f "$repo/scripts/unity/docker/desktop-native.Dockerfile" "$repo/scripts/unity/docker"
docker image inspect livliv-desktop-native:24.04 --format '{{.Id}}' > "$cache/toolchain-image-id.txt"
docker run --rm --platform linux/amd64 --network none -v "$repo:/repo:ro" -v "$cache:/out" livliv-desktop-native:24.04 python3 /repo/scripts/unity/build-webp-desktop.py
python3 - "$repo" <<'PY'
from pathlib import Path
import json,sys,shutil
repo=Path(sys.argv[1]);cache=repo/'unity/TestResults/webp-desktop';package=repo/'unity/Packages/ai.livliv.player'
for key,folder,name in [('linux-x86_64','Linux/x86_64','liblivlivwebp.so'),('windows-x86_64','Windows/x86_64','livlivwebp.dll')]:
    report=json.loads((cache/(key+'.json')).read_text());report['toolchainImageId']=(cache/'toolchain-image-id.txt').read_text().strip()
    manifest=package/'ThirdParty'/('libwebp-'+key+'.json')
    if manifest.exists() and json.loads(manifest.read_text())['artifactSha256']!=report['artifactSha256']:
        raise SystemExit(key+' decoder differs from pinned artifact; review compiler/container provenance')
    dest=package/'Plugins'/folder/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(cache/(key+'-'+name),dest)
    if not manifest.exists():manifest.write_text(json.dumps(report,indent=2)+'\n')
    print(key+': '+report['artifactSha256'])
PY
