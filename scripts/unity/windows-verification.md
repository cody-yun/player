# LivLiv Windows verification

Extract the entire ZIP into an empty directory on Windows x64. Keep `Player/` and its DLL/data folders together.
Python 3.10 or later is needed for the portable runner; the Unity Editor is not needed.

From PowerShell or Command Prompt in the extracted directory:

```text
py -3 verify-windows-player.py --check-only
py -3 verify-windows-player.py
```

`--check-only` verifies the transferred payload; it does not execute Unity and is not a Windows runtime pass.
The second command checks the files, launches the sample with its default graphics selection, and runs ten
generated-face states: detect/rotate/lose/reacquire/distance, terminal source failure, restart, release and fixed scale.
It also reads the expression count and the representative smile label/English translation through `Player.Expressions`.
It then runs the Surface/camera-layout/mask/text/poster checks and the PCM/WAV/MP3 speech/lip-sync checks.
Speech may produce quiet fixture audio. Camera and microphone devices are not opened.
No graphics override or `-nographics` flag is supplied.

The runner prints the evidence directory and a ZIP containing each probe's JSON, player/launcher logs and PNGs.
Both passing and failing runtime results are preserved. Evidence defaults to a unique directory under the system
temporary directory, outside this fingerprinted bundle. A custom directory can be given with `--output C:\LivLivResults\run1`.
Use a new output path for every run. No data is uploaded automatically.

Without Python, the native probe can still be run from PowerShell (the portable file/result checks are then omitted):

```powershell
$result = Join-Path $env:TEMP ('livliv-manual-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $result | Out-Null
& .\Player\LivLivVerification.exe -batchmode -logFile "$result\player.log" -livlivSmoke "$result"
```

Inspect `native-player.json` and `player.log` after the app closes. Preserve the full directory for review;
manual execution alone does not verify the bundle hashes or automatically classify the result.

The native MediaPipe DLL requires the Microsoft Visual C++ x64 runtime, Media Foundation and the Windows
graphics DLLs listed in `Proof/windows-player-audit.json`. If startup fails, preserve the logs/exit code.
These generated-input probes do not establish physical camera/microphone permission behavior, device removal,
GPU portability, audio latency or long-duration stability. Those require separate target-machine testing.

The included Aoi character is for verification under its own terms in `Licenses/characters/`.
Code/font/WebP/MediaPipe notices are retained under `Licenses/`. This bundle is a development build.
