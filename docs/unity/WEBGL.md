# WebGL 구현과 실행 증거

로고·텍스트 경계 후속 빌드도 오류/경고 0으로 통과했다(53,393,932 bytes, 20.559477초).
Chrome 실제 WASM에서 텍스트 8개 상태와 생성 입력 종료/재시작 6개 상태를 재검증했다.
텍스트의 픽셀 변화는 오른쪽 아래 지정 영역에만 있었고 종료 후 소유 리소스는 0이었다.
최종 reload Ready/Aoi/입력 Off/배율 1/스크롤 없음 확인 후 Ego 공간 28을 닫았다.
증거: `unity/TestResults/mark-boundary-final/{browser-text,browser-inputs.json,browser-recovery.json}`.
텍스트 검사는 실행별 폴더에 저장해 이전 결과를 덮어쓰지 않는다. 작은 캔버스 경계 비교는 Unity Editor 범위이며,
물리 장치·모바일 WebGL·Safari·장시간 검증은 미완료다. 아래 수치는 이전 빌드 이력이다.

원본 기준 d999dea/Player.Expressions를 반영한 최신 WebGL 빌드도 갱신했다.
CLI errors/warnings 0, 53,393,491 bytes, 17.120초. 샘플 증거 경로는 typed getter에서 표정 개수·이름·번역을 읽는다.
이 산출물도 Chrome 실제 WASM에서 생성 portrait/650Hz 음성 입력으로 종료·재시작 6개 상태를 통과했다.
모든 상태에서 표정 개수 4 및 대표 smile의 이름/영어 번역, scale=1을 확인했다.
최종 reload 후 Ready/Aoi/입력 Off/스크롤 없음으로 복구한 뒤 Ego 공간 25를 닫았다.
증거는 `unity/TestResults/upstream-latest/browser.{json,log}`, `browser-audit.json`이다.
생성 입력의 ended 이벤트 주입이며 물리 권한 창·장치 분리·다른 브라우저 실행 검증은 아니다.
최신 빌드 요약/로그 및 파일 SHA-256은 `unity/TestResults/upstream-latest/players/`에 있다.
아래 Chrome 실행 기록은 각 이전 빌드에 대한 이력이다.

컨텍스트 유실 후속 검증: WebGL canvas listener → 직접 AOT 콜백 → C# Player의 유실 상태와
Dispose/리스너 해제를 Chrome 실제 WASM에서 확인했다. 트리거는 `WEBGL_lose_context`이며
물리 GPU 드라이버 리셋을 재현한 것은 아니다. Player 두 개는 각각 1회, 해제한 Player는 0회 알림을 받았다.
같은 QA 페이지 reload 후 Ready·scale=1·입력 Off도 확인했다.
증거는 `unity/TestResults/context-loss/webgl/browser.json`, `browser.log`, `browser-build.log`다.
유실 처리/예외/로딩 검증의 경계는 [LOADING.md](LOADING.md)를 따른다.

2026-11-09 후속 빌드: 프레임 단위 변형 설정 읽기 최적화와 UGUI 카메라 해상도 수정을 포함해
Unity CLI WASM 빌드를 완료했다. errors/warnings 0, 53,336,894 bytes, 18.70초.
소스/산출물 해시는 `unity/TestResults/post-optimization/webgl-evidence.json`에 있다.
이번 산출물은 Ego 공간 18/p1의 Chrome에서 Surface 8개 상태, 음성 상태 9개,
음성 재생 회귀 8개 기록, 눈 마스크 4개 자세 검사를 통과했다.
최종 888×990 화면에서 scale=1, 카메라/마이크 Off, overflow=false와 실제 캐릭터 표시를 확인했다.
증거와 이전 결과 보존본은 `unity/TestResults/post-optimization/browser/`에 있다.
`evidence.json`은 각 결과 JSON의 SHA-256과 빌드 산출물 해시를 포함한다.
`final-sample.png`는 실제 브라우저 화면이다. 완료 후 공간 18을 닫았다.
카메라 Canvas의 8개 원근 상태는 native/Editor에서만 검사했으며 이번 브라우저 Surface는 overlay 경로다.
이 실행은 물리 카메라/마이크·Safari·모바일 브라우저 검증을 포함하지 않는다.

2026-11-09, Unity 6000.3.14f1 / Emscripten 3.1.39-git / IL2CPP.
`unity install-modules`로 해당 Editor의 Web Build Support를 설치하고 같은 toolchain으로 libwebp 1.6.0 decoder를 빌드했다.
source archive SHA-256은 설치된 Homebrew 1.6.0 formula와 대조했으며 artifact/hash/toolchain은 `ThirdParty/libwebp-webgl.json`에 있다.
`prepare-webp-webgl.sh`는 source와 재빌드 artifact hash를 검사한다. SIMD는 끈 portable WASM archive다.

## 실제 확인한 범위

- `VerificationBuild.WebGL`: 원본 Aoi(char06) package ZIP을 Resources에 포함하는 development build.
- 최신 PlayerSurface 포함 build: 오류/경고 0, 52,341,506 bytes, 21.065163초(증분 빌드). `TestResults/webgl-build.json`. 얼굴 입력의 loose 배포 파일 30,748,485 bytes는 이 수치와 별도다.
- desktop Chrome 156에서 첫 프레임 도달. JSON/ZIP/PNG/WebP/mesh/shader/mark와 Player frame loop 실행.
- 중립·회전·눈 감김·말하기의 실제 RenderTexture PNG를 WASM에서 읽어 원본 브라우저 WebGL과 비교.
- 실제 AudioContext/MediaStream과 WASM P/Invoke 경계를 통과하는 생성 오디오: mouth ≈0.99968, vowel ≈−0.99945.
- 정지 후 생성한 capture track은 ended, capture AudioContext는 closed.
- getUserMedia를 거부하는 테스트 Promise를 주입했을 때 오류가 반환되고 캐릭터 frame loop는 계속 실행.
- 권한 대기 중 취소한 뒤 늦게 도착한 생성 stream도 종료. 물리 마이크/OS 권한을 열지는 않았다.
- 실제 Tasks Vision GPU 추론 → WASM → C# FaceTracking: 생성 영상으로 회전/유실·재검출/어두움/재시작/거부/late cancel 확인. [FACE.md](FACE.md)에 별도 기록.

`Runtime/Plugins/WebGL/LivLivBrowser.jslib`는 Unity가 지원하는 ES5 문법이다. `WebGLVoiceInput`은
샘플/linear-power Float32 arrays를 받아 기존 VoiceMeter와 MouthDriver에 전달한다. WASM memory grow 후에도
현재 HEAPF32를 다시 참조한다. 장치/노드/pointer·key listener를 정리하며 suspended context는 다음 사용자 입력에서 resume한다.
공식 플러그인 경계: [Unity JavaScript plug-in](https://docs.unity.com/en-us/engine/6000.3/manual/web-interacting-browser-js).

## 그림 비교

512×768, 원본 straight alpha를 premultiply한 뒤 활성 영역 합집합의 RGBA 채널을 비교한다.
`CaptureFrame`과 원본 모두 time/physics/mouth를 초기화하고 dt=0으로 고정 pose를 그린다.
이 표의 pose는 이전 30-frame animation fixture와 다르다.

| Aoi pose | 채널 MAE (255 기준) | 최대 채널 오차 | 오차 >16인 활성 픽셀 비율 |
|---|---:|---:|---:|
| neutral | 0.190002 | 105.894 | 0.07579% |
| turn | 0.198919 | 105.894 | 0.07570% |
| blink | 0.192411 | 105.894 | 0.07684% |
| talk | 0.190617 | 105.894 | 0.07579% |

로고 raster/AA 등의 큰 국소 차이가 남아 있으므로 픽셀 완전 일치는 아니다. 4개 WASM PNG와 생성 오디오 화면은 직접 열어 확인했다.
다른 모델·DPR·GPU/브라우저와 성능 기준은 별도 검증해야 한다.

## 재현 자료

- `scripts/unity/test-browser-input.mjs`: graph 설정, heap 교체, 취소/거부/구성 실패, suspended/closed context의 6개 Node 계약 테스트.
- `scripts/unity/verify-webgl-browser.mjs`: 실제 빌드 페이지(127.0.0.1:8770)를 ego-browser로 실행. 생성 오디오만 사용.
- `scripts/unity/capture-webgl-reference.mjs`: 127.0.0.1:8769의 원본 TS renderer를 직접 실행.
- 두 초기 브라우저 스크립트는 당시 TaskSpace 3을 재사용했으며 `const root="<repo absolute path>/";`를 주입해서 실행한다.
- `TestResults/webgl-browser.json`: 상태/소유권 결과와 실행 코드 hash. `webgl-reference.json`: 원본 코드 hash.
- `TestResults/webgl-char06-*.png`, `browser-webgl-char06-*.png`, `webgl-generated-audio.png`, `render-comparison.json`.

전체 플랫폼 지원 완료를 뜻하지 않는다. 실제 microphone/camera 권한·장치와 동시 입력, mobile browser,
background/resume, HTTPS 배포·외부 오디오, 다른 graphics API, 장기 메모리/CPU/GPU 검증은 남아 있다.

네이티브 연동 패키지 설치 후에도 WebGL CLI build(오류/경고 0)와 얼굴 입력의 9개 browser 시나리오를 재실행해 통과했다. 네이티브 어댑터 assembly는 WebGL에서 제외된다.


## Speech 출력·스트리밍 추가

`LivLivSpeech.jslib` / `WebGLSpeech.cs`를 공용 `UnitySpeech.Create`에 연결했다. 2026-11-09 Chrome 156에서
실제 클릭 → Web Audio 활성화 → URL/byte decode → C# envelope/vowel → mouth 렌더를 확인했다.
PCM16 WAV는 HTTP 본문 완료 전 소리와 입 움직임을 관측했고, MP3/float WAV·취소·404·decode 오류·
재시작과 Dispose 후 모든 context closed/잔존 리소스 0도 통과했다. Node 계약 14개 통과.
자세한 제한과 재현은 [SPEECH.md](SPEECH.md)의 WebGL 절에 있다.

`scripts/unity/serve-speech-tests.py`(8772)와 `verify-webgl-speech.mjs`를 사용했다.
기존 공간 3이 사라진 뒤 사용자 승인으로 새 Ego 검증 공간 **9**를 만들었고 이 공간에서 실행했다.
재현 스크립트는 공간을 생성하지 않으며 허용된 `speechSpaceId`와 절대경로 `root`를 전달받는다.
결과는 `unity/TestResults/webgl-speech.json`, 종료 화면은 `webgl-speech-finished.png`다.
실제 스피커 loopback·물리 기기 지연, Safari/WebKit 실제 실행과 모바일 브라우저는 아직 검증하지 않았다.


## Snapshot 추가

공개 `Player.Snapshot()` / `SnapshotPng()`를 실제 WASM에서 실행했다. 512×768의 기본/고개/눈 감김/
말하기 PNG 4장, paused/hidden 상태의 dt=0 재그리기, 워터마크와 투명 alpha, scale=1 및 active target 복원
조건을 통과했다. 원본 `PlayerBase.snapshot()`을 실제 WebGL canvas에 실행해 작은 alpha/orientation
기준도 확인했다. PNG 파일 전체 픽셀 검사에서 alpha 차이 0, premultiplied 왕복 오차 최대 0.4981/255다.
`scripts/unity/verify-webgl-snapshot.mjs`는 허용된 공간 ID `snapshotSpaceId`를 주입받으며 이번에는 9번을
재사용했다. `webgl-snapshot.json`, `snapshot-pixel-check.json`, `webgl-snapshot-*.png`에 기록했다.
추가 sample UI는 없으며 QA 뒤 기본 미리보기로 복귀했다. 세부 계약은 [SNAPSHOT.md](SNAPSHOT.md).


## 로딩 포스터 추가

`LoadOptions.poster`의 원본 lifecycle과 선택 UGUI 패키지 `ai.livliv.ugui`를 연결했다.
Chrome 156에서 실제 UnityWebRequest/WebP decoder/UGUI를 경유해 포스터 표시, 첫 모델 프레임으로 전환,
기존 모델 보존, 늦은 요청 교체·취소, 이미지 오류 후 모델 성공 및 샘플 복귀를 확인했다.
로드 후 포스터 texture 0과 scale=1을 확인했고 포스터 표시 스크린샷을 직접 검토했다.
`webgl-poster.json`, `webgl-poster-visible.png` 및 [POSTER.md](POSTER.md)에 검증 범위가 있다.
기본 PNG/WebP 이외의 형식과 Safari/mobile browser는 미검증/미구현 범위를 구분해 문서화했다.


## 재사용 가능한 UGUI PlayerSurface

Player/RT/material 소유권과 자동 크기·density/pixel budget을 가진 `PlayerSurface`를 실제 WASM에서 실행했다.
8개 상태에서 재사용/방향 변경/밀도 상한/예산/0 크기 해제·복구/Dispose를 확인했고, 소유 target/material은
최종 0개다. 실제 UI와 PNG에 모델/mark가 표시된다. `webgl-surface.json`과 [SURFACE.md](SURFACE.md)에 기록했다.
검증용 표시 영역은 끝난 뒤 제거했고 기존 샘플과 입력 Off 상태로 복귀했다.
