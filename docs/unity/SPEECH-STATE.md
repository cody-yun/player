# 음성 출력 상태 알림 — 2026-11-09

`LivLiv.Runtime.SpeechPlayer.onState`는 원본 `src/runtime/speech.ts`의 오디오 context 상태 알림에
대응한다. 콜백 안에서 `Running`을 읽는다. 문장 재생 종료를 알리는 `onIdle` 및 대기열의 `Busy`와 구분한다.
`Stop()`은 음성 큐를 비우지만 오디오 context를 닫지 않으므로 상태 알림을 만들지 않는다.

```csharp
runtime.onState = () => ShowAudioState(runtime.Running);
```

Unity factory에서는 플랫폼 편의 옵션으로 같은 값을 받을 수 있다. 원본의 공개 `Speech` wrapper는
`onState`를 직접 노출하지 않으므로 이 옵션은 Unity 어댑터 확장으로 구분한다.

```csharp
var speech = LivLiv.Unity.UnitySpeech.Create(player, options:
    new LivLiv.Unity.UnitySpeechOptions {
        onState = running => ShowAudioState(running)
    });
```

## 플랫폼 경계와 호출 시점

- WebGL은 실제 `AudioContext.onstatechange` 횟수를 받아 다음 main-thread `Tick()`에서 전달한다.
  `suspended → interrupted`처럼 `Running=false`가 유지되는 서로 다른 상태 이벤트도 누락하지 않는다.
  한 번의 pump에서 최대 4096개를 소비하고 나머지는 다음 pump에 남긴다.
  콜백의 `Running`은 관찰 당시 현재 값이며 중간 상태의 이력을 재구성하는 API는 아니다.
- native 및 기존 사용자 `ISpeechOutput`은 `Running`의 변화를 Tick에서 관찰한다.
  Unity에서는 `AudioListener.pause`와 출력 sample rate를 사용한다. 한 프레임 사이에 변경 후 원복된 상태나
  OS 오디오 중단의 모든 종류를 보장하지 않는다. native `Unlock`은 전역 AudioListener pause를 해제하지 않는다.
- 더 자세한 상태 이벤트가 있는 출력만 선택적으로 `ISpeechStateOutput`을 구현한다.
  기존 `ISpeechOutput` 구현은 수정하지 않아도 된다. 생성 당시 이미 실행 중인 native 출력에 가짜 시작 알림을 만들지 않는다.
- 종료할 때는 소유 리소스를 해제하고 `Running=false`의 마지막 알림을 **Dispose 안에서 동기적으로** 전달한다.
  원본 브라우저의 비동기 close 이벤트와 호출 시점이 다르다. Unity pump가 같이 파괴되므로 종료한 owner를
  잡고 나중에 호출하지 않는다. WebGL에서 context를 만들지 않았거나 이미 닫았다면 추가 종료 알림은 없다.
- callback의 Stop/Dispose/Tick 재진입과 예외를 처리한다. 예외는 기존 `onError` 경로로 보고하며
  출력 정리를 계속한다. Dispose 반복과 이후 Tick은 중복 알림을 만들지 않고 callback 참조를 해제한다.

## 현재 검증

원본 코드를 바꾸지 않은 `generate-speech-state.mjs`로 3개 시나리오/16개 관찰 값을 생성했다:
autoplay 중단 → 실행 → 중단 → interrupted → 실행 → Stop → Dispose,
처음부터 실행 중인 context, unlock하지 않은 상태의 Dispose다.
AudioContext double은 이벤트 경계만 제어하며 원본의 `SpeechPlayer`를 실제 실행한다.

- `.NET` SpeechState/Speech/SpeechWav **73 passed**.
- Unity CLI EditMode 같은 범위 **73 passed**.
- 이후 DriverParityTests까지 포함해 다시 확인한 .NET/Unity CLI 결과는 각각 **81 passed**다.
- Unity CLI PlayMode SpeechOutputTests **7 passed**. 실제 Unity audio engine에서
  `timeScale=0` 상태의 listener pause/resume/최종 종료 `[false,true,false]`와 반복 종료를 검증했다.
  기존 PCM 출력·decoder·큐/정리 회귀도 포함한다.
- JavaScript speech bridge **16 passed**. 추가된 두 검사는 이벤트 소비, 여러 non-running 상태,
  종료 뒤 늦은 이벤트, 이벤트 상한 초과분 보존을 검사한다.
- WebGL IL2CPP CLI build: errors/warnings 0, **53,331,383 bytes**, 19.03초.
  새로운 P/Invoke와 `.jslib` 심볼 링크를 확인했다.
- macOS ARM64 IL2CPP/Metal player 실행: listener pause/resume/종료 알림 `[false,true,false]`,
  PCM16 WAV/MP3/float WAV의 실제 mixer 출력과 입 동작, 순차 hooks/idle/Stop,
  scale=1 및 종료 후 source/clip 0개를 확인했다. build errors/warnings 0.
  `unity/TestResults/macos-speech/1794213632755661000/evidence.json`에 실행 코드·metadata·자산 해시를 기록했다.
- Android ARM64 IL2CPP/BlueStacks GLES3에서도 같은 `[false,true,false]`, 세 가지 인코딩의 mixer/입 동작,
  hooks/idle/Stop 및 리소스 0개를 통과했다. 종료 후 일반 샘플을 다시 실행했다.
  `unity/TestResults/bluestacks/speech/1794213772150169000/evidence.json`.
  APK **53,444,606 bytes**, SHA-256 `9ad0811018c6263d6bb7ecceeb3141942f8d568e28da63f9c551887e9dc2f2a9`.
  build errors/warnings 0, 서명·16KB 정렬·모델 포함·빈 ZIP 영역 0 검사 통과.

사용자가 새 Ego 공간 생성을 허용한 뒤 공간 11/p1에서 **Chrome 실제 WASM**도 확인했다.
`verify-webgl-speech-state.mjs`의 9개 관찰에서 AudioContext 생성/중단/gesture 재개,
Stop의 상태 알림 없음, Dispose의 종료 알림 한 번, 반복 Dispose 및 이미 닫힌 context의 중복 알림 없음이 통과했다.
context의 `suspend/resume/close`를 실제 호출했으며 statechange 이벤트를 흉내낸 검사가 아니다.
`interrupted` 상태는 Node 경계 검사에만 포함되며 실제 OS interruption을 재현한 것은 아니다.
기존 URL/bytes/분할 WAV/취소/오류/재시작 speech 브라우저 회귀 8개 기록도 통과했다.
최종 context/piece/load/decode 0개, 입력 Off, scale=1, 888×990에서 overflow 없음.
Safari/모바일 OS interruption 및 물리 장치 전환은 미검증이다.

2026-11-09 후속 승인으로 새 Ego 공간 15/p1을 만들고 같은 WebGL 빌드의 상태 검사
9개를 다시 실행해 통과했다. 기존 상태 증거는 `webgl-speech-state-before-space15.json`으로
보존했다. 최종 scale=1, 입력 Off, 888×990에서 overflow 없음도 재확인했다.
이어 음성 재생 회귀 8개 기록(formats/bytes/streamingPrefix/stream/cancel/missing/invalid/restart)도
통과했다. 마지막 context/piece/load/decode는 모두 0개였다. 기존 재생 결과는
`webgl-speech-before-space15.json`에 보존했으며 완료 후 검증 공간 15를 닫았다.
이 재실행에서 오디오 소스 해시와 WebGL 빌드 해시는 상태 증거와 모두 일치했다.
새 UGUI 카메라 해상도 수정의 WebGL 재빌드 검증으로 확대 해석하지 않는다.

## 재현과 산출물

```sh
node --import ./test/ts.mjs scripts/unity/generate-speech-state.mjs --check
node --test scripts/unity/test-speech-browser.mjs
dotnet test tools/Parity/Parity.csproj --no-restore --filter 'FullyQualifiedName~Speech'
unity test ./unity --mode EditMode --filter 'SpeechStateTests;SpeechTests;SpeechWavTests' --output ./unity/TestResults/speech-state/unity-editmode.xml --timeout 240
unity test ./unity --mode PlayMode --filter SpeechOutputTests --output ./unity/TestResults/speech-state/unity-playmode.xml --timeout 240
```

로컬 증거는 `unity/TestResults/speech-state/`의 TRX/XML 및 Unity 로그,
`unity/TestResults/speech-state-node.log`, `Tests/Editor/Fixtures/speech-state.json`이다.
브라우저 증거는 `unity/TestResults/webgl-speech-state.json`(소스와 실제 WASM/data/framework 해시 포함),
`unity/TestResults/webgl-speech.json`과 `speech-state/browser-run.log`, `browser-regression.log`다.
WebGL 브라우저 검사는 기존 승인된 Ego TaskSpace ID와 절대 root를 스크립트에 전달한다.
새 공간을 자동 생성하지 않으며 카메라·마이크나 오디오 clip을 사용하지 않는다.
원본 25개 모듈 hash는 유지했고 새 fixture를 포함한 22개 출처 감사의 issues는 0이다.
