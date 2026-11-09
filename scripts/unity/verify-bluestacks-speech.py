"""Verify generated speech PCM -> Android audio mix + rendered character mouth in BlueStacks.

This is an explicit CLI probe, not hardware camera/microphone capture or physical Android QA.
It uses a new remote directory for each run and restores the normal sample after collecting evidence.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import time

root = Path(__file__).resolve().parents[2]
adb = Path('/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb')
serial = '127.0.0.1:5555'
component = 'ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity'
apk = root / 'unity/TestResults/Android/LivLivVerification.apk'
output = root / 'unity/TestResults/bluestacks/speech' / str(time.time_ns())
output.mkdir(parents=True, exist_ok=True)


def run(*args):
    return subprocess.check_output([str(adb), '-s', serial, *map(str, args)], text=True, stderr=subprocess.STDOUT)


subprocess.run([str(adb), 'connect', serial], check=True)
assert run('get-state').strip() == 'device'
run('install', '-r', apk)
remote = '/sdcard/Android/data/ai.livliv.verification/files/speech-' + str(time.time_ns())
run('shell', f'am start -S -W -n {component} --es unity "-livlivSpeechSmoke {remote}"')
try:
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        try:
            evidence = json.loads(run('shell', 'cat', remote + '/speech-player.json'))
            break
        except (subprocess.CalledProcessError, json.JSONDecodeError):
            time.sleep(1)
    else:
        raise TimeoutError('Android speech probe did not write a result')
    run('pull', remote + '/.', output)
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
    evidence['apkSha256'] = hashlib.sha256(apk.read_bytes()).hexdigest()
    evidence['apkBytes'] = apk.stat().st_size
    evidence['output'] = str(output)
    evidence['android'] = run('shell', 'getprop', 'ro.build.version.release').strip()
    evidence['abi'] = run('shell', 'getprop', 'ro.product.cpu.abi').strip()
    (output / 'evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')
    assert evidence['result'] == 'Passed', evidence
    assert evidence['platform'] == 'Android' and evidence['graphics'] == 'OpenGLES3'
    assert evidence['inputs'] == ['PCM16 WAV', 'MP3', 'float WAV']
    assert evidence['hooks'] == [f'{i}:{event}' for i in range(1, 4) for event in ('start', 'end')]
    assert evidence['idle'] == 1 and evidence['stopped'] == 1
    assert evidence['peak'] > 1e-5 and evidence['mouth'] > .1 and evidence['scale'] == 1
    assert evidence['remainingPieces'] == 0 and evidence['remainingClips'] == 0
    assert evidence['stateEvents'] == [False, True, False]
    print(json.dumps(evidence, indent=2))
finally:
    run('shell', f'am start -S -W -n {component} ')
