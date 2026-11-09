"""Run the native ARM64/x64 IL2CPP speech probe. Generated PCM only; no hardware capture."""
from pathlib import Path
import hashlib
import json
import subprocess
import time
import argparse
from macos_player import resolve_app,launch,audit

parser=argparse.ArgumentParser();parser.add_argument('--architecture',choices=['arm64','x86_64'],default='arm64');args=parser.parse_args()

root = Path(__file__).resolve().parents[2]
app,binary,plist = resolve_app(args.architecture);bundle=audit(app,args.architecture)
output = root / 'unity/TestResults' / ('macos-intel-speech' if args.architecture=='x86_64' else 'macos-speech') / str(time.time_ns())
output.mkdir(parents=True)
result = subprocess.run([*launch(binary,args.architecture), '-batchmode', '-logFile', str(output / 'player.log'), '-livlivSpeechSmoke', str(output)], timeout=60)
evidence = json.loads((output / 'speech-player.json').read_text())
files = [
    'unity/Assets/Verification/PortVerification.cs',
    'unity/Assets/Verification/PortVerification.Speech.cs',
    'unity/Packages/ai.livliv.player/Runtime/voice.cs',
    'unity/Packages/ai.livliv.player/Runtime/runtime/speech.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnitySpeechOutput.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnitySpeechPump.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnitySpeechLoader.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Audio/SpeechWavReader.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Audio/SpeechIncomingBuffer.cs',
    'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/speech-stereo.mp3.bytes',
    'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/speech-float.wav.bytes',
]
evidence['sourceHashes'] = {f: hashlib.sha256((root / f).read_bytes()).hexdigest() for f in files}
evidence['executableSha256'] = hashlib.sha256(binary.read_bytes()).hexdigest()
evidence.update(bundle)
evidence['output'] = str(output)
(output / 'evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')
assert result.returncode == 0, evidence
assert evidence['result'] == 'Passed' and evidence['platform'] == 'OSXPlayer' and evidence['graphics'] == 'Metal', evidence
assert evidence['inputs'] == ['PCM16 WAV', 'MP3', 'float WAV']
assert evidence['hooks'] == [f'{i}:{event}' for i in range(1, 4) for event in ('start', 'end')]
assert evidence['idle'] == 1 and evidence['stopped'] == 1
assert evidence['peak'] > 1e-5 and evidence['mouth'] > .1 and evidence['scale'] == 1
assert evidence['remainingPieces'] == 0 and evidence['remainingClips'] == 0
assert evidence['stateEvents'] == [False, True, False]
print(json.dumps(evidence, indent=2))
