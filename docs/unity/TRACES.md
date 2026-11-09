# 얼굴 입력 기록·재생·통계 — 2026-11-09

`src/runtime/trace.ts` → `Runtime/runtime/trace.cs`의 Frame/BS/LM/TraceSource에 이어
TraceRecorder, synthTrace와 SynthStats를 포팅했다. 장치 입력은 `Platform/TraceInput.cs`에서
기존 IFaceInput으로 연결한다. 원본 TS와 카메라/추론 어댑터는 변경하지 않았다.

## 기록과 재생

```csharp
// liveInput is an already-started WebGLFaceInput or native FaceLandmarker input.
var recording = new LivLiv.Platform.RecordingFaceInput(liveInput);
var face = LivLiv.FaceTracking.Attach(player, recording, nowMilliseconds, options);
// ... keep ticking the existing Player ...
face.Stop(); // also disposes the wrapped input, exactly once
string json = recording.Recorder.Save();
string filename = LivLiv.Runtime.TraceRecorder.FileName(System.DateTime.UtcNow);
// Host chooses a native file/share destination or a browser download flow.
```

Save는 원본 save()가 다운로드하던 JSON 문자열을 반환한다. 파일 선택·공유·브라우저 다운로드 UI는
host 책임이며 이 API가 임의로 파일을 쓰거나 다운로드 창을 띄우지 않는다. 입력 해제 후에도 Recorder를
내보낼 수 있다. 원본처럼 배열은 shallow copy이므로 사용자 IFaceInput은 이미 전달한 m/b/l 배열을 재사용해
덮어쓰지 않아야 한다. 기본 native/WebGL 입력은 결과마다 배열을 만든다.

```csharp
var replay = LivLiv.Platform.TraceFaceInput.FromJson(json);
// Or: TraceFaceInput.Synthetic(narrow: true)
// Or: await TraceFaceInput.LoadAsync(absoluteUri, new UnityModelTransport(), cancellationToken)
var replayFace = LivLiv.FaceTracking.Attach(player, replay, nowMilliseconds, options);
```

재생은 카메라/MediaPipe를 시작하지 않는다. IFaceInputMode.IsReplay를 통해 저장 보정값을 읽거나
덮어쓰지 않으며, 재생 중 자동/수동 보정은 그 인스턴스에서만 동작한다. RecordingFaceInput으로 다시 감싼
재생 입력도 같은 규칙을 유지한다. 재생을 다시 기록하는 확장 경로에서는 loop 안의 timestamp 대신
host의 현재 clock을 기록해 여러 loop가 시간순 JSON으로 이어지게 한다. 원본 recorder 계산 자체는 바꾸지 않는다.

## 원본과 유지한 계약

- 상대 timestamp는 첫 입력 기준 `Math.round`, m/b/l 값은 소수 4자리 JS 반올림이다.
  음수 .5 경계도 C#의 기본 banker rounding을 사용하지 않는다. undefined 배열은 JSON에서 생략한다.
- 기록 상한은 **9,000 frames**다. 30fps에서 약 5분이며 시간 기준으로 자르는 것은 아니다.
  null(새 입력 없음)은 기록하지 않고, m/b가 없는 완성된 검출 결과(얼굴 없음)는 기록한다.
- TraceSource는 마지막 timestamp +33ms로 반복한다. 늦게 poll하면 현재 시간까지 진행한 마지막 프레임을
  반환하며 이미 반환한 index를 중복 반환하지 않는다. 원본의 한 프레임 trace 경계 동작도 유지한다.
- 합성 trace는 seed=7, 24초·30fps·720프레임이다. 회전/아래 보기/10–12초 얼굴 유실/깜박임/먼쪽 눈의
  오검출/발화/좌우·앞쪽 기울임을 포함하며 narrow는 눈 기본 blink score가 .38이다.
- FPS는 원본처럼 얼굴이 있는 검출만 세며 첫 집계는 0이다. replay의 Dark는 false다.

외부 JSON 입력은 원본의 단순 fetch().json()보다 엄격하게 검사한다. 최대 16M UTF-16 문자,
9,000프레임, 비감소·음수가 아닌 유한 timestamp, 배열 크기 m=16/b=18/l=8과 유한 숫자를 요구한다.
선택 배열은 생략할 수 있다. malformed는 `invalid`, 예산 초과는 `too-large`다.
URL 로드는 IModelTransport의 취소/오류 경계를 재사용하며 16MiB bytes와 엄격한 UTF-8을 검사한다.
이는 별도 Unity 방어 경계이며 원본 TraceSource의 계산을 바꾼 것은 아니다.

## 통계 관찰

```csharp
var stats = new LivLiv.Runtime.SynthStats(() => replayFace.Mirror);
// After the host has finished composing the frame:
stats.Observe(replay.Source.At(nowMilliseconds()) / 1000, finalParams);
var result = stats.Latest; // null until a loop reaching >=23s wraps
```

원본처럼 프레임의 최종 Params를 복사하고 loop가 되감기면 이전 loop를 집계한다.
정지 떨림·평균 pitch/눈 열림, yaw 지연 탐색(0..0.4초, 8ms 간격), 회전 RMS, 얼굴 유실 중 움직임,
깜박임 최소값과 먼쪽 눈 wink 비율을 계산한다. 비어 있는 구간은 원본처럼 NaN/Infinity가 될 수 있다.
입력이 없는 구간의 통계를 0으로 꾸미지 않는다.

원본의 `window.__traceAt/__trackStats`는 `At/Latest`로 대체했다. Unity에서는 각 SynthStats가 자신의
loop 수를 관리하며 전역 window 상태를 공유하지 않는다. host는 실제 final Params가 확정된 후 Observe를
호출한다. 임의로 일반 driver 순서에 관찰자를 끼워 넣어 미완성 pose를 통계로 간주하지 않는다.

## 검증

원본 함수를 직접 실행하는 `generate-traces.mjs`가 다음 기대값을 만든다:

- 두 합성 trace의 **1,440프레임 전체**.
- 기록 JSON 3사례와 9,000프레임 cap, timed replay 4시퀀스.
- 양쪽 미러/빈 구간을 포함한 stats 4시퀀스와 Params 복사 동작.
- 일반/좁은 눈 각각 **2 loop + 다음 첫 프레임**, 1,441프레임의 전체 tracker→record→stats 경로.

.NET **88개**, Unity CLI EditMode **108개 통과**(07:41:48Z–07:41:51Z, 2.830초).
기존 calibration/driver/수치 회귀와 replay 보정 저장 억제, 기록→JSON→재생, malformed/예산,
UTF-8 및 early/late 취소를 포함한다. 취소는 Mono가 TaskCanceledException으로 감쌀 수 있어
OperationCanceledException의 파생형까지 동일한 취소 계약으로 검사한다.

```sh
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-traces.mjs --check
unity test ./unity --mode EditMode \
  --filter 'LivLiv.Tests.TraceTests;LivLiv.Tests.CalibrationTests;LivLiv.Tests.UnityCalibrationStoreTests;LivLiv.Tests.DriverParityTests;LivLiv.Tests.ParityTests' \
  --output ./unity/TestResults/traces.xml --timeout 240
```

실제 player 검증은 `verify-webgl-trace.mjs`(기존 ego-browser 공간)와 `verify-native-trace.py macos|android`로
수행한다. 원본 pipeline 기대값과 실제 runtime의 모든 통계값을 비교하고 기록 JSON을 파일로 보존한다.
이 검증은 생성 입력의 trace 경로이며 실제 사람의 녹화나 새로운 추론 정확도 검증을 뜻하지 않는다.
URL trace의 transport 계약은 대체 전송기로 검사했으며 실제 HTTP trace 다운로드는 별도 검증 범위다.


## 실제 runtime 검증

Chrome 156 WebGL, macOS ARM64 IL2CPP와 BlueStacks Android ARM64 IL2CPP에서 일반/좁은 눈 각각 1,441프레임을
생성→재생→FaceTracker.Poll/Apply→기록→JSON 재파싱→통계 집계했다.
두 경우 모두 얼굴 프레임 1,321개, 유실 120개, 0..48,000ms, 2 loop이며 원본 pipeline의
모든 통계값과 일치한다. `check-trace-records.py`로 기록 JSON의 **모든 숫자/배열 값**을 원본 recorder 출력과
비교해 통과했다. 원본과 문자열 숫자 표기까지 같다는 주장은 아니다.

- WebGL: `unity/TestResults/webgl-trace.json`, `webgl-trace-{normal,narrow}.json`, `trace-record-parity.json`.
- macOS: `unity/TestResults/macos-trace/1794210229690441000/evidence.json`과 같은 폴더의 기록/비교 JSON.
- 실제 실행 JSON마다 source/adapter/QA 7개 hash를 기록한다. macOS는 launcher와 GameAssembly hash도 기록한다.
- WebGL 최종 샘플 Ready/scale=1/카메라·마이크 Off, viewport/document 모두 888×990으로 overflow 없음.

이 pipeline은 tracker의 Poll/Apply 계산을 직접 비교한다. 얼굴 유실 시 public driver의 idle 혼합이나
실제 렌더 완료 시점의 사용자 체감 지연을 측정하는 테스트는 아니다. 예를 들어 이 설정의 turnLagMs=32는
동일한 합성 입력과 호출 간격에서 얻은 통계이며 실제 카메라의 전체 지연으로 해석하지 않는다.
기존 public driver의 loss/reacquisition 혼합은 별도 DriverParityTests 회귀로 확인한다.


BlueStacks 증거: `unity/TestResults/bluestacks/trace/1794210343383109000/evidence.json`과
같은 폴더의 기록/`trace-record-parity.json`. 세 플랫폼의 C# export 파일 SHA-256도 서로 같다:
normal `d749c64a89cf136f36174c74c6ad0ff3b994389dba93d32e6195b4e5de6c62ec`,
narrow `32bc59329e46027996612ae8e761f004deb08678e035bcf07ab62b0a38621f46`.
원본과의 비교는 JSON 의미 값 전체 비교이며, 세 Unity 플랫폼 사이에서는 파일 bytes까지 같다.

최종 Unity WebGL/macOS/Android/iOS build errors/warnings 0.
WebGL **53,319,272 bytes / 24.889285초**, Android 개발 APK **85,540,877 bytes**,
SHA-256 `d9d135207d083a44b1ef557bc86e3d7482bc7c9aa46b1303d30505dc1b55e7dd`. ARM64 라이브러리 9개와 16KB ELF/ZIP 정렬 감사 통과.
iOS Xcode unsigned build/link **BUILD SUCCEEDED**, iPhoneOS app **173,828,608 bytes**,
Resources/폰트/MediaPipe embedding/WebP 심볼 감사 통과. Xcode 생성 코드·app icon 경고는 남는다.
로그는 `traces-{webgl,macos,android,ios}-build.log`, `traces-ios-xcode.log`다.

물리 Android/iOS 및 Windows/Linux/Safari/mobile browser 실행, 실제 사용자 영상의 기록·재생,
장시간 성능과 trace URL의 실제 네트워크 검증은 아직 남아 있다.
개발 APK의 압축 member 합계는 53,355,742 bytes, archive 전체와의 차이는 32,185,135 bytes다.
기능 추가량과 구분하여 패키징 잔여 영역/메타데이터 원인을 다음 P5 점검에서 확인한다.
