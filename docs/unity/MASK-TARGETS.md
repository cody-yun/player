# 눈 마스크 타깃 — 원본 bbox와 64px 확장 포팅

2026-11-09, 원본 `src/runtime/puppet.ts drawMasks()`의 영역 계산과 저장 공간 정책을 연결했다.
기존 Unity 렌더러는 전체 출력 크기의 RGBA texture를 할당했다. 이제 변형된 눈 영역만 저장한다.
원본 TS와 모델·표정·추적 계산은 변경하지 않았다.

## 매핑

- `Runtime/Platform/MaskBounds.cs`: 선택한 눈의 변형 정점을 원본 view로 화면 픽셀에 투영한다.
  최소 좌표 floor/최대 ceil, 양쪽 2px 여백, 화면 경계 clamp, 빈 영역 생략을 유지한다.
- `PuppetRenderer.Draw.cs`: soft iris가 있는 보이는 눈을 최대 4개 RGBA channel에 배정한다.
  너비와 높이는 각각 64px 배수로 확장하며 기존 storage보다 작아지지 않는다.
  같은 크기는 재사용하고, texture storage 유실은 복구하며 모델 교체/Dispose에서 해제한다.
- 원본의 `viewport(-x0,-y0,w,h)`는 동일 픽셀 배치를 만드는 clip 좌표 변환으로 대응한다.
  Metal/D3D에 음수 viewport를 요구하지 않는다. 기존 GPU projection의 Y 보정은 유지한다.
- `LivLivLayer.shader`: fragment pixel에서 bbox 원점을 빼고 해당 texel의 중심을 point sampling한다.
  저장 영역 밖은 coverage 0이다. stencil fallback, RGBA channel, fractional coverage와 soft 혼합은 유지한다.
- 할당은 GPU texture 최대 크기를 검사하고 새 타깃 생성 성공 후 기존 타깃을 해제한다.

원본처럼 큰 자세/출력에서 한 번 커진 마스크는 같은 모델에서 작아지지 않는다.
offscreen/softIris off 상태에서도 기존 storage는 보존하며 불필요한 draw만 생략한다.
샘플의 얼굴 거리 확대·축소 제거, UGUI 배치 및 스크롤 없는 구성은 유지한다.

## 검증

- 원본 bounds/growth 문장을 직접 실행한 **160개 fixture**: 다양한 출력 크기, float 정점,
  복수 눈, 빈 영역, 화면 밖, 기존 storage 재사용. source SHA-256을 함께 검사한다.
  .NET 1개 테스트 통과. Unity 검사에도 같은 fixture를 사용한다.
- Unity CLI EditMode/Metal **55개 통과**, 06:50:50Z–06:51:40Z, 50.062초.
  기존 GraphicsTests와 bounds 계약, fractional coverage, grow/reuse/no-shrink,
  storage 복구, hidden/offscreen/disabled 생략, 교체/Dispose 해제를 포함한다.
- 변경 전 full-size renderer에서 같은 실행 환경으로 생성한 실제 모델 3종 × 4 자세,
  **12개 512×768 프레임의 전체 RGBA가 변경 후와 완전히 일치**했다.
- WebGL IL2CPP build errors/warnings 0, **52,358,429 bytes / 18.169415초**.
- 승인된 Ego 공간 9 / Chrome 156에서 실제 WASM의 직접 renderer probe를 실행했다.
  Aoi 시선 이동·반쯤 감은 눈·고개/시선 조합은 모두 2개 mask channel을 사용했고,
  화면 밖 상태에서는 channel 0으로 생략했다. 모든 상태에서 sample scale=1이었다.
  조합 자세의 PNG를 직접 열어 출력된 눈·캐릭터·mark를 확인했다.
- 512×768 출력에서 마스크는 **192×128 RGBA8 = 98,304 bytes(96KiB)**였다.
  기존 1,572,864 bytes(1.5MiB)보다 **93.75% 감소**했다. 전체 GPU 메모리나 프레임 시간의 감소율이 아니다.
- 기존 WebGL Snapshot 4개 상태도 재실행했다. 이 경로는 paused host의 dt=0 회귀이며,
  별도의 직접 renderer probe가 30회 update 후 실제 gaze/blink 변형과 mask 할당을 검사한다.
  full-size 기준 이미지와 비교한 4개 프레임 중 3개는 전체 픽셀이 일치했고, 나머지 1개는
  393,216 pixels 중 1 pixel의 1 channel에서 1 LSB 차이였다. `mask-pixel-comparison.json`에 오차·hash를 기록한다.

첫 Metal 구현은 bbox offset에 Y 방향 변환을 중복 적용해 fractional coverage 테스트가 실패했다.
기존 GPU projection이 이미 방향을 맞추므로 원본 픽셀 offset을 그대로 사용하도록 수정했다.
실패 결과 `mask-cropped.xml`과 통과 결과 `mask-targeted.xml`, `mask-final.xml`을 보존한다.
첫 WebGL 직접 probe도 host Params와 renderer Params를 혼동해 mask를 활성화하지 못했다.
probe를 renderer Params에 입력하도록 수정했으며 제품 params/driver 로직은 변경하지 않았다.

## 재현과 범위

```sh
node scripts/unity/generate-mask-bounds.mjs --check
dotnet test tools/Parity/Parity.csproj --no-restore --filter FullyQualifiedName~MaskBoundsTests
unity test ./unity --mode EditMode --filter 'LivLiv.Tests.GraphicsTests;LivLiv.Tests.MaskBoundsTests' --output ./unity/TestResults/mask-final.xml --timeout 240
python3 scripts/unity/check-mask-pixels.py
```

브라우저 실행은 `serve-poster-tests.py`의 최신 빌드(127.0.0.1:8773)와
`verify-webgl-masks.mjs`를 사용한다. ego-browser에 절대경로 `root`와 승인된 `maskSpaceId`를 전달한다.
snapshot 회귀는 `verify-webgl-snapshot.mjs`의 build URL을 같은 서버에 맞춰 실행했다.
`check-mask-pixels.py`는 보존된 변경 전 PNG와 현재 PNG를 비교하므로 baseline 없이 단독 실행할 수 없다.

로컬 `unity/TestResults` 증거: `mask-baseline.xml`, `mask-baseline-frames/`, `mask-final.xml`,
`mask-webgl-baseline/`, `webgl-mask.json`, `webgl-mask-*.png`, `mask-pixel-comparison.json`,
`mask-webgl-final-build.log`. WebGL mask 결과에 source/adapter/shader/QA 소스 hash를 기록한다.

## 추가 네이티브 검증 — 2026-11-09

`PortVerification.MasksNative.cs`를 기존 native presentation CLI probe에 연결했다.
내장 Aoi 모델을 별도 renderer로 읽고 실제 IL2CPP/GPU에서 시선 이동·반쯤 감은 눈·조합 자세·재사용·
큰 출력·작은 출력·화면 밖·storage 유실 복구의 **8개 상태**를 검사한다. 카메라/마이크/HTTP는 사용하지 않는다.

macOS ARM64/Metal과 BlueStacks Android 13 ARM64/OpenGLES3 모두 통과했다.
두 실행 모두 512×768에서 192×128(96KiB), 1024×1536에서 320×128(160KiB) 마스크를 사용했다.
다시 작은 출력으로 돌아와도 원본 정책대로 160KiB storage를 재사용한다. 첫 96KiB 사례의 절감률을
모든 자세/해상도에 일반화하지 않는다. 화면 밖에서 channel 0, 나머지 상태에서 channel 2,
모든 상태에서 scale=1을 확인했고 Dispose 후 남은 mask target은 0개였다.

각 native backend 내에서 재사용·작은 출력 복귀·storage 복구 결과는 기준 조합 자세와 전체 픽셀이 일치했다.
기존 Surface/Snapshot/Poster 7개 상태와 오류·늦은 이미지 정리도 같은 실행에서 통과했다.
두 플랫폼의 조합 자세 PNG를 직접 열어 눈·캐릭터·mark 출력을 확인했다.

- macOS: `unity/TestResults/macos-presentation/1794207373071798000/evidence.json`
- BlueStacks: `unity/TestResults/bluestacks/presentation/1794207457813357000/evidence.json`
- 관련 소스 16개, 실제 APK 및 macOS GameAssembly/metadata/resources hash를 기록하고 현재 파일과 대조했다.
- 최종 Android APK **63,351,483 bytes**, SHA-256 `8c6d62ecff310f21e2135e8cb68344e935cbb63b353b49752ca04923a2b94076`.
  ARM64 native library 9개, 16KB ELF/ZIP 정렬·권한·리소스 검사를 재통과했다.
- iOS Unity export + Xcode unsigned build/link 및 산출물 검사 통과. app **171,405,578 bytes**,
  ARM64/MediaPipe embedding/WebP 심볼/Resources 확인. `mask-ios-xcode.log`, `ios-app.json`.
  Unity build reports는 errors/warnings 0이나 Xcode의 Unity 생성 코드/API·아이콘 경고는 남아 있다.
  iPhone에서 renderer를 실행한 증거는 아니다.

### 같은 WebGL backend의 전체 크기 비교

플랫폼 간 눈 가장자리의 차이를 확인하기 위해 동일 shader/renderer에서 **타깃 크기·view·offset만**
전체 화면으로 바꾼 비교용 WebGL 빌드를 추가 실행했다. 시선 이동·반쯤 감은 눈·조합 자세·화면 밖
4개 출력은 cropped 빌드와 **전체 RGBA가 완전히 일치**했다.
비교용 소스는 `mask-full-reference-Draw.cs.txt`, 결과는 `webgl-mask-full.json`과 PNG로 보존했다.
이 실험은 crop으로 생기는 차이를 분리한 것이며 원본 웹 renderer 전체와 동일하다는 주장은 아니다.

비교 후 임시 소스를 복원하고 최종 cropped WebGL 빌드·4개 probe를 재실행했다.
최종 build errors/warnings 0, **52,358,426 bytes / 17.66373초**, `mask-webgl-restored-build.log`.
샘플은 기본 자세로 복귀했다. 최종 소스 hash는 native 실행 및 WebGL 결과와 일치한다.

`native-mask-pixels.json`의 backend 간 측정에서 Android GLES3와 Chrome WebGL의 최대 채널 오차는 1 LSB였다.
macOS Metal과 Chrome은 시선/눈 감김/조합 자세에서 최대 28/63/64 LSB,
전체 RGBA 채널 평균 0.006866/0.022153/0.042810 LSB 차이가 있었다.
플랫폼 간 byte 단위 동등성은 주장하지 않는다. 같은 WebGL 내 전체 크기/cropped 비교에는 차이가 없었다.

```sh
python3 scripts/unity/verify-native-presentation.py macos
python3 scripts/unity/verify-native-presentation.py android
python3 scripts/unity/check-native-mask-pixels.py
```

새 cropped 경로는 Editor Metal, macOS ARM64 standalone, BlueStacks GLES3, desktop Chrome WebGL에서 검증했다.
물리 Android/iOS, Windows/Linux player, macOS x64, Safari/mobile browser, 장시간 성능·메모리 실측은 남아 있다.
전체 suite를 재실행하거나 모든 플랫폼 지원을 완료한 결과가 아니다.
