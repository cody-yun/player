"""Measure separate renderer phases in the actual ARM64 macOS IL2CPP player."""
from pathlib import Path
import hashlib
import json
import subprocess
import time
from macos_player import resolve_app, launch, audit

root = Path(__file__).resolve().parents[2]
models = root / 'unity/TestResults/model-soak-models'
assets = json.loads((models / 'manifest.json').read_text())
assert len(assets) == 21
for name, item in assets.items():
    assert hashlib.sha256((models / (name + '.livliv')).read_bytes()).hexdigest() == item['sha256']
output = root / 'unity/TestResults/macos-model-profile' / str(time.time_ns())
output.mkdir(parents=True)
app, binary, _ = resolve_app('arm64')
bundle = audit(app, 'arm64')
with (output / 'launcher.log').open('w') as log:
    process = subprocess.Popen([*launch(binary, 'arm64'), '-batchmode', '-logFile', str(output / 'player.log'),
        '-livlivModelProfile', str(output), '-livlivSoakModels', str(models)], stdout=log, stderr=subprocess.STDOUT)
    print('RUNNING ' + str(output), flush=True)
    try:
        code = process.wait(timeout=300)
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)
data = json.loads((output / 'model-profile.json').read_text())
data.update(bundle)
data['models'] = assets
data['sourceHashes'] = {}
for file in [root / 'unity/Assets/Verification/PortVerification.ModelProfile.cs',
             *sorted((root / 'unity/Packages/ai.livliv.player/Runtime').rglob('*.cs'))]:
    data['sourceHashes'][str(file.relative_to(root))] = hashlib.sha256(file.read_bytes()).hexdigest()
data['scope'] = 'CPU phase timings, not GPU completion. 60 samples per model after 10 warmup frames; no input/driver timing or physical mobile claim.'
(output / 'evidence.json').write_text(json.dumps(data, indent=2) + '\n')
assert code == 0 and data['result'] == 'Passed' and len(data['rows']) == 21, data.get('error')
for name in assets:
    assert (output / (name + '.png')).stat().st_size > 0, name
for row in sorted(data['rows'], key=lambda row: row['deformMeanMs'], reverse=True):
    print(json.dumps(row), flush=True)
print('PASSED ' + str(output), flush=True)
