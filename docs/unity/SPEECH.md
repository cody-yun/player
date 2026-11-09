# Speech 큐 포팅 — 공통 로직

`src/runtime/speech.ts`의 계산·재생 순서 제어를 `Runtime/runtime/speech.cs`에 대응시켰다.
공통 코어, **native Unity와 WebGL 오디오 출력·URL/바이트 로더·공개 Speech driver**를 연결했다.
WAV PCM16은 수신 중 처리하고 MP3/기타 WAV는 전체 수신 후 디코딩한다.
WebGL은 Chrome 156에서 사용자 클릭으로 활성화한 실제 Web Audio 출력·브라우저 디코더를 검증했다.
Safari/모바일 브라우저와 물리 기기의 검증은 남아 있다.
샘플에는 재생 버튼이나 추가 패널을 넣지 않았다.

## 원본과의 대응

| 원본 | C# | 계약 |
|---|---|---|
| `envelope` | `SpeechPlayer.Envelope` | 150–1500 Hz 음성 대역, clip별 정규화, local contrast, 30 ms 선행 |
| `Grow`, `update` | `Grow`, `Update` | 수신 PCM 누적, 확정된 envelope/vowel 유지, 마지막 15 frame 재계산 |
| `play`, `tail` | `Play`, `tail` | 현재 출력 clock + 40 ms와 이전 piece 끝 중 늦은 시각에 예약 |
| `at`, `heardTime` | `At`, `ISpeechOutput.HeardTime` | 실제 들리는 시각 기준으로 mouth/vowel 선형 보간 |
| `Incoming`, `order` | `ISpeechLoader`, `ISpeechIncoming`, `Consume` | 다운로드는 함께 시작하고 수신 샘플은 문장 순서대로 예약 |
| `ClipHooks`, `owed` | 동일 역할 | 각 enqueue마다 hooks 복사; 시작 후 종료 또는 중단 실패를 한 번 알림 |
| `tick`, `retire`, watchdog | `Tick`, `Retire` | 출력 종료 후 지연만큼 유지(최대 400 ms), 정지된 출력 clock 감시 |
| `stop`, `dispose` | `Stop`, `Dispose` | 세대 변경, 다운로드 취소, 예약 음성 해제, 미완료 hooks 실패 |

`Platform/Audio/SpeechContracts.cs`는 Web Audio/fetch 경계다. 모든 호출과 비동기 continuation은
호스트 main thread에서 처리해야 한다. 출력 어댑터는 `CurrentTime`, 출력 지연을 반영한 `HeardTime`,
출력 정지 중에도 흐르는 `WallTime`을 제공한다. 렌더링이 멈춰도 `Tick()`은 계속 호출해야 한다.
`ISpeechPiece.Dispose`는 예약 중이거나 재생 중인 source를 즉시 중지하고 자신이 소유한 buffer를 해제한다.
다운로드 어댑터의 `Open`은 즉시 수신을 시작한다. `NextAsync`는 첫 채널 PCM을 반환하며 종료 시 null,
취소 시 대기를 풀어야 한다. 부분 다운로드가 실패하면 이미 수신해 예약한 소리까지 재생하고 종료한다.

## 의도적인 차이

- 브라우저의 25 ms interval/onended/setTimeout 대신 호스트의 `Tick()`에서 관찰한다. mouth 보간은 출력
  clock을 사용하지만, hooks 관찰 지연은 호스트 pump 간격의 영향을 받는다. 실제 장치의 end-to-end 지연은 아직 측정하지 않았다.
- `Stop`은 새 문장이 취소된 이전 다운로드를 기다리지 않도록 순서 gate도 초기화한다. 늦게 돌아온 이전
  결과는 generation으로 버린다. 원본은 AbortController가 이전 다운로드를 해제하는 동작에 의존한다.
- 사용자 hook 예외는 `onError`로 보고하고 다른 hooks/리소스를 마저 정리한다. Dispose 중 새 enqueue는 거절한다.
- PCM sample rate는 3000–384000 Hz로 제한한다. 원본 WAV 수신 경로의 범위를 공통 경계에서 검사한다.
- 원본 입력을 수정하지 않았다. `onState` 출력 상태 알림과 Unity factory 옵션을 연결했다.
  main-thread 관찰 및 Dispose의 동기 종료 알림 차이와 검증 범위는 [SPEECH-STATE.md](SPEECH-STATE.md)에 기록했다.

## Native Unity 출력과 공개 API

`UnitySpeech.Create(player, loader, options)`는 플랫폼별 출력 / `UnitySpeechPump` / `Speech.Attach`를
묶는다. native에서는 `UnitySpeechOutput`과 기본 `UnitySpeechLoader`를, WebGL player에서는
`WebGLSpeechOutput`과 기본 `WebGLSpeechLoader`를 사용한다. `Speech.Say(string)`, `Say(byte[])`, `Stop`,
`Unlock`, `Busy`, `onIdle`, `Dispose`는 원본 공개 인터페이스에 대응한다.
native 호스트 씬에는 활성 AudioListener가 필요하다. 샘플의 배경 카메라에는 이미 포함되어 있다.

- PCM 조각마다 mono `AudioClip`과 2D `AudioSource`를 소유하고 `PlayScheduled`로 DSP 시각에 예약한다.
  pitch=1, doppler=0, loop=false로 시간축을 유지한다. 선택적 AudioMixerGroup을 지정할 수 있다.
- `AudioListener.pause`를 존중하며 `Unlock`이 전역 일시정지를 해제하지 않는다. volume은 Unity 범위 0..1로 제한한다.
- `HeardTime`은 DSP clock에서 지연을 뺀다. 기본 추정은 `GetDSPBufferSize`의 length×count/sampleRate다.
  Bluetooth/OS/device의 추가 지연까지 측정한 값이 아니며, `outputLatencySeconds`로 실제 측정치를 주입할 수 있다.
- pump는 Player의 렌더링·visibility와 `Time.timeScale`에 독립적으로 매 프레임 실행한다.
  native background와 audio configuration 변경 시 큐를 중단하고, 복귀 시 자동 재생하지 않는다.
- Stop은 source와 PCM을 해제하고 mouth driver는 유지한다. Speech/Player Dispose는 pump까지 해제한다.
- native 전용 `UnitySpeechOutput`을 직접 생성하면 WebGL에서 거절한다. 공용 factory는 WebGL 어댑터를 자동 선택한다.

공식 API 근거: [PlayScheduled](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiosource/playscheduled),
[DSP clock](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiosettings/dsptime),
[listener pause](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiolistener/pause),
[output PCM sampling](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiosource/getoutputdata).

## URL·바이트·WAV 스트리밍

`UnitySpeechLoader`는 HTTP(S)/file URI를 받는다. 상대 URL은 `UnitySpeechLoaderOptions.baseUri`가 필요하다.
각 Open은 즉시 비동기 수신을 시작하며, 공통 큐가 앞 문장의 완료를 기다리는 동안에도 다음 다운로드는 진행된다.
바이트 입력은 Open 시 복사하므로 `Say(bytes)` 이후 호출자가 원본 배열을 변경해도 큐의 음성은 변하지 않는다.

- Content-Type에 소문자 `wav`가 있으면 원본처럼 스트리밍 파서를 사용한다. PCM16 RIFF의 분할 헤더,
  홀수 길이 부가 chunk/padding, stereo/multichannel의 첫 채널, 파일 고유 rate와 불완전 마지막 frame을 처리한다.
- `SpeechWavReader`의 streaming data 길이는 원본처럼 무시한다(~2GB sentinel 지원). 잘못된 0-channel과
  짧은 fmt chunk는 명시적으로 거절한다. 지원하지 않는 형식은 원본 바이트를 보존해 전체 decoder로 넘긴다.
- byte 입력 또는 비-WAV MIME은 전체 수신 후 디코딩한다. 전체 WAV는 native decoder를 사용하므로
  RIFF data 뒤 metadata를 PCM으로 오인하지 않는다. 전체 decode 결과의 50 ms 이하 clip은 원본처럼 재생하지 않는다.
- native decoder는 UnityWebRequestMultimedia/DownloadHandlerAudioClip을 사용한다. WAV/MP3/Ogg magic을 구분하며,
  임시 cache 파일로 바이트를 전달하고 완료/실패/취소 정리 시 자신이 만든 파일·AudioClip을 해제한다.
  PCM은 첫 채널만 추출한다. native codec과 Web Audio codec의 출력 sample rate/MP3 delay는 bit-exact 계약이 아니다.
- 기본 encoded 한도 32 MiB, decoded 한도 16M float elements, HTTP timeout 60초다. Native decoded 한도는
  clip metadata를 얻은 뒤 managed PCM 배열 할당 전에 검사한다. 다운로드 중 초과는 중단한다.
- 취소는 대기 중인 `NextAsync`를 해제하고 늦게 온 PCM을 버린다. 이미 도착한 조각 이후 연결이 끊기면
  공통 큐가 수신한 prefix까지 재생하고 종료한다. 사용자 hook 예외와 다운로드 오류는 `onError`로 관찰할 수 있다.

```csharp
var speech = UnitySpeech.Create(player);
speech.Say("https://example.com/audio/line-1.wav");
speech.Say(encodedAudioBytes); // WAV/MP3 bytes
speech.Stop();                // keeps the mouth driver for the next reply
speech.Dispose();             // removes driver, output and pump
```

근거: [ReceiveData는 main thread에서 호출](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/networking/downloadhandler/receivedata),
[GetAudioClip](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/networking/unitywebrequestmultimedia/getaudioclip).

## 검증과 재현

`scripts/unity/generate-speech.mjs`는 고정된 원본 TS를 직접 실행한다. Web Audio는 수동 clock/scheduler로
대체했다. 실제 스피커 또는 브라우저 재생 검증으로 해석하면 안 된다.

- 8/16/22.05/24/44.1/48 kHz × 빈 입력/침묵/짧은 입력/음성 합성파: envelope 24개.
- 16/24/44.1 kHz의 분할 수신과 늦게 도착한 piece: 예약 시각과 mouth/vowel 36개 시각 비교.
- 수치 허용 오차: envelope/mouth 2e-7, vowel 2e-6, 예약 시각 1e-12초.
- 결정적 lifecycle 테스트: 병렬 수신·순서 유지, 출력 지연, 중단/늦은 결과, 빈 입력/실패/부분 실패,
  suspended watchdog, callback 재진입·예외, 중복 해제. fixture의 원본 SHA-256도 검사한다.

2026-11-09 검증: .NET 38 passed / Unity CLI EditMode 38 passed, 실패·skip 0.
Unity 실행 시각 05:39:35Z, 0.152초. `unity/TestResults/speech.xml` 및 `speech-editor.log`,
`tools/Parity/TestResults/speech.trx`에 결과를 기록했다. 이 변경에서는 전체 suite를 재실행하지 않았다.
fixture 재생성 `--check` 일치, 원본 25개 모듈 hash 검사 issues 0.

```sh
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-speech.mjs --check
dotnet test tools/Parity/Parity.csproj --no-restore --filter 'FullyQualifiedName~SpeechTests'
unity test ./unity --mode EditMode --filter LivLiv.Tests.SpeechTests --output ./unity/TestResults/speech.xml --timeout 240
```

추가 검증: Speech 공개 driver와 기존 Face/Voice driver를 포함한 **.NET 47개 / Unity EditMode 47개 통과**.
Unity PlayMode **5개 통과**: 실제 AudioClip/AudioSource 출력의 nonzero PCM 관측, 큐 자동 진행,
timeScale=0, listener pause, 중단·retry·해제, 잘못된 PCM/constructor 실패의 리소스 정리.
background/configuration 테스트는 Unity notification을 주입한 것이며 실제 OS background 전환 검증은 아니다.
`speech-driver.xml` (05:44:53Z–05:44:54Z), `speech-playmode.xml` (05:43:53Z–05:43:55Z)과 각 로그에 기록했다.

```sh
unity test ./unity --mode PlayMode --filter LivLiv.Tests.SpeechOutputTests --output ./unity/TestResults/speech-playmode.xml --timeout 240
```

Android ARM64 IL2CPP build errors/warnings 0, APK 73,499,549 bytes. BlueStacks Android 13/OpenGLES3의
`-livlivSpeechSmoke` probe에서 생성 PCM 3개를 재생하고 renderer의 mouthOpen을 관측했다.
오디오 mix peak 0.003536, mouth 최대 0.9761, 시작/종료 hooks 6개 순서 일치, idle 1회,
stop 실패 hook 1회, 남은 source/clip 0, 캐릭터 scale=1을 확인했다.
이는 에뮬레이터의 실제 Unity 오디오 믹스·렌더 경로 검증이며, 마이크 캡처·물리 Android·스피커 loopback 측정이 아니다.
`unity/TestResults/bluestacks/speech/evidence.json`에 APK와 소스 SHA-256 및 실제 수치를 저장했다.
APK의 ARM64 라이브러리 9개·16KB 정렬·리소스 검사도 통과했다.

```sh
unity build ./unity --target Android --execute-method VerificationBuild.Android --output-path "$PWD/unity/TestResults/Android/LivLivVerification.apk"
python3 scripts/unity/verify-bluestacks-speech.py
```

macOS ARM64 IL2CPP/Metal에서도 같은 probe를 통과했다. build errors/warnings 0,
실제 audio mix peak 0.006707, mouth 최대 0.9884, hooks/idle/stop/해제 및 scale=1 조건이 일치했다.
`unity/TestResults/macos-speech/1794203267784813000/evidence.json`에 실행파일/소스 SHA-256을 기록했다.
생성 PCM을 대상으로 한 런타임 검증이며, 원본 음성 파일 디코딩·Bluetooth 지연·음성 청취 품질 검증은 아니다.

```sh
unity build ./unity --target StandaloneOSX --execute-method VerificationBuild.MacOS --output-path "$PWD/unity/TestResults/MacOS/LivLivVerification.app"
python3 scripts/unity/verify-macos-speech.py
```

아래 WebGL bridge 구현 이후에도 PC/Mobile/WebGL에서
중단·background·autoplay·출력 지연과 실제 음성/입의
동기화 검증을 마쳐야 모듈을 `ported`로 올릴 수 있다.

native 어댑터 추가 후 WebGL CLI build도 errors/warnings 0으로 통과했다(52,147,120 bytes).
이는 WebGL bridge 추가 전 플랫폼 분기와 기존 샘플의 빌드 검증이다. 최신 음성 실행 검증은 아래에 기록했다.

## 로더 추가 검증 — 2026-11-09

- `generate-speech-wav.mjs`는 원본의 private Incoming와 helper 본문을 그대로 실행한다. 5 sample rates,
  1/2/3채널, byte 분할, odd chunk, 불완전 마지막 frame, decoder fallback, 잘못된 rate의 원본 default를 포함한 21개.
  float PCM 출력은 정확히 일치하며 fallback 바이트도 동일하다. 원본 SHA-256을 fixture에 기록한다.
- .NET Speech/WAV 65개 통과. Unity EditMode 공통+HTTP/decoder 69개 통과.
  HTTP origin이 본문 뒤쪽을 보내지 않고 대기할 때 먼저 21개의 PCM frame을 받았으므로 전체 다운로드 후 재생이 아니다.
  상대 URI/404/취소/encoded·decoded 예산, MP3·float WAV·첫 채널·입력 배열 복사·50 ms gate를 검증했다.
- whole WAV의 trailing metadata 처리를 추가해 실제 HTTP/decoder 4개를 다시 통과했다.
- Unity PlayMode 6개 통과. 기본 factory의 `Say(byte[])` → native decode → 실제 audio mix → idle·해제를 추가했다.
- `generate-speech-audio.py --check`로 합성 WAV/MP3 fixture 재현을 확인했다. 좌330/우770 Hz, 24 kHz stereo,
  1/3초이며 실제 녹음이 아니다. MP3 생성 도구는 ffmpeg 8.0/libmp3lame다.

결과: `speech-loading.xml` (69개), `speech-http.xml` (4개), `speech-playmode.xml` (6개),
`tools/Parity/TestResults/speech-wav.trx` (65개). 이번 변경은 대상 suite를 실행했고 전체 suite는 재실행하지 않았다.
첫 HTTP run의 2개 실패 후 상대 URI 해석/오류 종료를 수정했고, 후속 실행에서 위 결과를 확인했다.

이후 native probe를 **encoded PCM16 WAV → MP3 → float WAV** 세 입력으로 바꿔 실제 decoder를 경유했다.
Android ARM64 IL2CPP/BlueStacks와 macOS ARM64 IL2CPP/Metal 모두 start/end 순서·idle 1회·stop 1회,
nonzero audio mix·renderer mouthOpen·출력 조각/clip 해제·scale=1을 통과했다.

| 경로 | audio peak | mouth 최대 | 증거 |
|---|---:|---:|---|
| Android 13 / GLES3 | 0.003536 | 0.9886 | `unity/TestResults/bluestacks/speech/evidence.json` |
| macOS ARM64 / Metal | 0.003532 | 0.9947 | `unity/TestResults/macos-speech/1794204128672675000/evidence.json` |

두 build 모두 errors/warnings 0, 현재 APK 63,316,194 bytes. 앞의 직접 PCM probe와 다른 산출물이며
각 evidence에 실행파일/APK, decoder·WAV parser 소스, 합성 음성 fixture SHA-256을 기록했다.
실제 HTTP 분할 전송 검증은 Editor loopback이고, 두 native player probe의 입력은 패키지에 넣은 바이트다.
iOS/Windows/Linux decoder 실행·모바일 실제 네트워크·물리 기기 품질/지연은 남아 있다.

decoder 추가 후 WebGL CLI build도 errors/warnings 0, 52,201,881 bytes로 통과했다.
이 수치는 WebGL bridge 추가 전 빌드이며, 최신 결과는 다음 절에 있다.

## WebGL 출력·로더와 실제 브라우저 검증 — 2026-11-09

`Platform/Unity/WebGLSpeech.cs`와 `Plugins/WebGL/LivLivSpeech.jslib`는 원본 Web Audio/fetch 경계를
담당한다. 큐·envelope·vowel·PCM16 WAV 파서는 native와 같은 C# 코어를 사용한다.

- AudioContext는 `Unlock` 시 지연 생성한다. 사용자 클릭 처리에서 `speech.Unlock()` 또는 `Say(...)`를
  호출해야 한다. resume와 짧은 무음 buffer로 활성화하며 전역 입력 listener는 추가하지 않는다.
- 기본 24 kHz를 요청하고 지원하지 않으면 장치 기본 rate를 사용한다. WebKit context도 장치 rate를 사용한다.
  `getOutputTimestamp`가 있으면 원본 heard-time 식을 사용하고, 없으면 output/base latency를 뺀다.
- GainNode로 volume을 적용하고 AudioBufferSourceNode로 조각을 예약한다. Unity AudioMixerGroup 경유는
  지원하지 않으며 지정하면 거절한다. `outputLatencySeconds` 주입은 지원한다.
- 상대 URL은 `document.baseURI` 기준이다. fetch/ReadableStream과 AbortController로 수신·취소하며
  WAV MIME이면 공통 parser에 조각을 넘긴다. 다른 MIME/형식 또는 byte 입력은 `decodeAudioData`로
  전체 디코딩하고 첫 채널을 사용한다. 서버의 CORS 정책과 브라우저 codec 지원을 따른다.
- encoded 32 MiB, decoded 16M float elements, fetch timeout 60초를 적용한다. decoded 한도는 브라우저
  decode 결과가 나온 뒤 확인하므로 브라우저 내부 디코더의 최대 메모리를 제한하지는 않는다.
- WASM 입력 바이트를 비동기 작업 전에 복사하고 readback마다 현재 heap을 사용한다. 중단/해제 시
  다운로드·decode 결과를 취소/폐기하고 source를 disconnect하며, Dispose는 context를 닫는다.

검증은 macOS Chrome 156, Unity WebGL IL2CPP에서 실제 사용자 클릭으로 수행했다.
WebGL CLI build errors/warnings 0, **52,263,742 bytes / 22.276222초**. Node bridge 계약 **14개 통과**,
기본 factory 분기 변경 후 native Unity PlayMode **6개 재통과**(06:12:05Z–06:12:07Z).

| 브라우저 기록 | 결과 |
|---|---|
| WAV → MP3 → float WAV URL 순차 입력 | audio peak 0.004000, mouth 최대 0.9821, idle 1회 |
| MP3 바이트 입력 | audio peak 0.003916, mouth 최대 0.9853 |
| WAV prefix / tail 완료 | 서버 completed=false 상태에서 audio peak 0.004000와 mouth 0.7268 관측; tail 해제 후 정상 종료 |
| 수신 중 Stop | AbortSignal 취소, busy=false, 출력/다운로드/decode 수 0 |
| HTTP 404 / invalid audio | 각 오류 1회, 큐 정상 종료 |
| 오류 후 MP3 재시작 | audio peak 0.003916, mouth 최대 0.9785, idle 1회 |
| 최종 Dispose | 모든 context closed, 출력/다운로드/decode 수 0, scale=1 |

8개 기록 상태(정상 URL, byte, streaming prefix, streaming 완료, 취소, 404, decode 오류, 재시작)를
`unity/TestResults/webgl-speech.json`에 소스 SHA-256과 함께 저장했다. audio peak는 실제 Web Audio
출력 graph의 analyser 관측이며 스피커 loopback 측정은 아니다. 입력은 합성 음성 fixture다.
`webgl-speech-finished.png`를 직접 확인했고 임시 QA 버튼 제거 후 스크롤 없는 UGUI를 유지했다.
카메라/마이크는 열지 않았다.

재현: `test-speech-browser.mjs`는 Node 계약 테스트, `serve-speech-tests.py`는 loopback 8772에서
최신 빌드/합성 fixture/분할 전송을 제공한다. `verify-webgl-speech.mjs`는 ego-browser에서 절대경로
`root`와 사용이 허용된 `speechSpaceId`를 주입해 실행한다. 이번 실행 공간은 사용자 승인으로 만든 9번이다.

```sh
node --test scripts/unity/test-speech-browser.mjs
python3 scripts/unity/serve-speech-tests.py
```

WebKit 분기는 Node double로 검사했으며 실제 Safari 실행 증거는 아니다. Safari/iOS/Android browser,
백그라운드 복귀·autoplay 제한의 플랫폼별 차이, 물리 기기 지연/청취 품질과 장기 메모리는 남아 있다.
