# LivLiv Unity port — in development

조사/아키텍처/단계별 계획은 [PORTING.md](../docs/unity/PORTING.md), 실제 상태는
[compatibility.json](Packages/ai.livliv.player/compatibility.json)을 참고한다.

이 디렉터리는 CLI 검증용 Unity 프로젝트이고 `Packages/ai.livliv.player`는 재사용할 embedded UPM 패키지다.
다른 프로젝트에 설치할 로컬 UPM tarball 생성·설치와 독립 소비자 검증은 [INSTALL.md](../docs/unity/INSTALL.md)를 따른다.
현재 모델 로더·동적 합성·변형/물리·키포즈, Idle/Player 및 얼굴·음성 driver와 native microphone 입력 어댑터를 연결했다.
WebGL GPU/native CPU 얼굴 추론과 공통 비동기 모델 로드를 연결했다. Linux IL2CPP는 Docker/Mesa 생성 입력 실행을 확인했다. **Windows 실행, 물리 PC 장치와 모바일 실기기 검증은 남아 있다.**
`LivLiv.Runtime`은 UnityEngine 의존성이 없고 JSON.NET을 사용한다. GPU/이미지 I/O는 별도 `LivLiv.Unity` 어셈블리다.
LivLiv 로고 워터마크는 중앙/모서리·깜박임까지 합성한다. 소유자 텍스트는 기본 NanumGothic 또는 호스트 폰트 어댑터로 표시한다([TEXT-MARK.md](../docs/unity/TEXT-MARK.md)).

샘플은 얼굴 거리가 달라져도 캐릭터 크기가 고정된다. 거리 확대·축소 기능과 on/off 옵션을 제공하지 않는다.
좌우·상하 위치, 고개와 표정·립싱크는 유지한다. 이 규칙은 샘플의 `SamplePresentation`에서 적용하며,
원본 업데이트 대응을 위한 패키지의 tracking 1:1 매핑은 유지한다. PC/Mobile/WebGL이 같은 샘플 코드를 사용한다.

샘플 UI는 UGUI이며 [화면 구성과 검증](../docs/unity/UI.md)에 기록했다. CLI가 생성하는 Canvas/버튼 계층은 `Assets/Verification/Verification.unity`에 저장된다.

## 재현

저장소 루트(`player/`)에서 실행한다. Node ≥22.18, .NET 9 SDK, Unity CLI와 Unity 6000.3.14f1이 필요하다.
기본 shell Node가 구버전이면 PATH를 맞춘다. 설치된 버전 예:

```sh
export PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH"
npm --prefix tools/UpstreamAudit ci --ignore-scripts
node scripts/unity/check-upstream.mjs
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-parity.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-format.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-brand.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-puppet.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-dynamics.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-idle.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-player.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-loading.mjs --check
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-drivers.mjs --check
npm test
dotnet restore tools/Parity/Parity.csproj --locked-mode
dotnet test tools/Parity/Parity.csproj --no-restore
mkdir -p unity/TestResults
unity test ./unity --mode EditMode --output ./unity/TestResults/editmode.xml --timeout 240
```

Unity 라이선스가 필요하다. `unity license list`로 상태를 확인할 수 있다.
`.NET` 검증은 동일한 C# 소스/동일 NUnit 테스트를 실행하는 보조 경로이며 Unity 컴파일/렌더링 검증을 대체하지 않는다.
원본 변경 영향 보고서·fixture 연결·실제 Git 이력 리허설은 [UPSTREAM.md](../docs/unity/UPSTREAM.md)를 따른다.

원본 변경을 검토하고 의도적으로 fixture를 갱신할 때만:

```sh
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-parity.mjs
```

그래픽 테스트는 실제 GPU가 필요하므로 전체 suite에는 `-nographics`를 넣지 않는다. 수치/모델 테스트만 실행하려면
`--filter "LivLiv.Tests.ParityTests;LivLiv.Tests.FormatParityTests" -- -nographics`를 쓸 수 있다.

fixture는 원본 TypeScript 함수들을 직접 실행하여 만든다. 각 source SHA-256도 기록하므로
upstream 코드 변경 후 오래된 기대값으로 통과하는 것을 검출한다. 카메라·AudioContext는 입력 경계만 stub 처리한다.
JS private 메서드를 별도로 복제하지 않고 원본 tracker.poll/read/apply 경로를 사용한다.

## 현재 API 예

```csharp
var tracker = new LivLiv.Runtime.FaceTracker();
var pose = new LivLiv.Runtime.Params();
// frame: trace.ts 형식; nowMs는 단조 증가하는 ms clock.
if (tracker.Poll(frame, nowMs)) tracker.Apply(pose, deltaSeconds);
// false이면 host에서 idle을 적용. stale target을 계속 적용하지 않는다.
```

`VoiceMeter.Read` 입력은 band-pass(650Hz/Q .55) 처리된 time-domain sample이다.
선택적 spectrum은 band-pass 전의 **linear power**이며 Hz/bin을 함께 준다.
native MicrophoneInput/AudioCaptureInput과 WebGLVoiceInput이 PCM/FFT acquisition을 담당한다. raw mic sample을 VoiceMeter에 곧바로 넣는 것은 원본 동작과 다르다.
`Vowel.Track`는 AudioClip의 mono PCM에 대응하는 float array로 clip cue를 계산한다.

## 수치와 향후 그래픽 허용 오차

- double 기반 params/actions/filters/tracking/voice: 절대 오차 `1e-9`.
- Float32Array FFT 저장 경계를 갖는 vowelTrack: `2e-6`.
- static renderer: synthetic layer/order/alpha/stencil의 RGBA8 픽셀을 채널별 ±1로 확인한다.
- puppet build: 원본과 정점/삼각형 순서는 정확히, Float32 가중치는 `1e-6 × max(1, |reference|)`로 확인한다.
- alphaGrid: Chrome 156 Canvas의 21 모델 514 레이어 occupancy 배열을 정확히 비교한다. PNG 채널 정규화와 Mono의 Float32 경계를 포함한다.
- dynamics: 원본 21 모델 × 120프레임. double 상태/변환 `1e-9 × max(1, |ref|)`, 체인 상태/정점 `1e-6 × max(1, |ref|)`.
- animated renderer: 실제 이미지의 coverage/lash/nasal 및 keyform 파일을 연결해 3 캐릭터 × 4 자세의 원본 정점/경계 값과 비교한다.
- 21 모델의 수치 변형 fixture는 coverage/lash/nasal/keyform 필드를 주입한다. 위 실제 이미지 검증과 범위가 다르다.
- 장치 추론 출력의 동등성: 아직 검증하지 않았다. 동일 입력 trace에서의 계산 동등성과 구분한다.

## 모델 입력과 이미지

```csharp
using LivLiv.Platform;
using LivLiv.Unity;

var package = ModelPackage.FromDirectory("/absolute/path/to/model");
// 또는 ModelPackage.FromZip(File.ReadAllBytes("model.livliv"));
using (var renderer = new PuppetRenderer())
{
    renderer.Load(package); // 모든 이미지 로드 성공 후 기존 GPU 모델과 교체
    // 중립 자세 + 패키지의 로고 mark; target은 Linear ARGB32 + depth/stencil 24비트
    renderer.RenderNeutral(target, now: 0, dpr: 1); // now는 ms, dpr은 CSS pixel당 출력 pixel
    renderer.Core.@params.angleX = 20;
    renderer.Core.@params.mouthOpen = .7;
    renderer.Update(1d / 60); // dt는 초; 입력 → 물리/변형 → Render 순서
    renderer.Render(target, now: 1000, dpr: 1);
}
```

v3/v4, ZIP stored/deflate, wrapper folder와 picked-file basename fallback을 지원한다.
JSON 크기/메시 예산/압축 해제 예산과 경로 제한을 먼저 검사한다. 선택적 keyforms가 없거나 손상됐으면
원본처럼 warning을 남기고 rig-only fallback을 준비한다. 유효한 파일은 grid/mask를 정점에 보간하고 rig의 key pose를 빼서 오프셋을 준비한다. 잘못된 schema/base64는 warning과 함께 rig-only로 진행한다.

PNG는 Unity가 읽고 WebP는 패키지의 libwebp 1.6.0 디코더가 읽는다.
현재 디코더 artifact는 **macOS ARM64/x64, Windows/Linux x64, Android/iOS ARM64, Unity 6000.3.14f1 WebGL/WASM**용이다. 실행에는 Homebrew가 필요 없다.
macOS standalone/Metal, BlueStacks/GLES3, Linux Docker/Mesa 및 desktop Chrome 실행을 확인했다.
iOS는 unsigned app build/link, Windows는 Mono build/포함 파일 검사까지 확인했다. 출처·해시는
[libwebp-macos-universal.json](Packages/ai.livliv.player/ThirdParty/libwebp-macos-universal.json), 라이선스는 그 옆 파일에 있다.
macOS는 `scripts/unity/prepare-webp-macos.sh`로 고정 소스의 두 아키텍처를 빌드하며 원본·결과 SHA-256을 확인한다.
MediaPipe를 포함한 샘플은 macOS 13.0 이상이 필요하다. x64 Rosetta 실행 범위와 재현은 [MACOS.md](../docs/unity/MACOS.md)에 있다.
Windows/Linux는 `scripts/unity/prepare-webp-desktop.sh`로 빌드하며 Docker/GCC/MinGW provenance와
아티팩트 해시를 기록한다. 필수 런타임과 검증 범위는 [DESKTOP.md](../docs/unity/DESKTOP.md)에 있다.

GPU 증거 이미지는 `unity/TestResults/neutral-compositor.png`, `neutral-d01.png`, `neutral-char06.png`, `mark-*.png`에 생성된다.
실제 원본과의 중립 프레임 비교 방법·수치·한계는 [GRAPHICS.md](../docs/unity/GRAPHICS.md)를 참고한다.
로고의 공용 CPU rasterizer는 외부 그래픽 라이브러리가 없지만 Canvas AA/그림자의 근사이며 작은 로고의 가장자리에 차이가 있다.
현재 CPU raster의 Mobile 성능과 각 타깃 GPU의 출력은 아직 검증하지 않았다.

renderer는 현재 원본 `puppet.ts`와 같은 공용 격자와 alpha culling을 사용한다.
`LivLiv.Runtime.Puppet`은 build/weight 및 update/deform 수치 코어를 제공한다. `LoadDefinition` 후
호스트가 각 `BuildLayer` 결과를 `layers`에 추가하고 `params`를 설정해 `Update(dt)`를 호출하면,
각 레이어의 `output`에 변형 정점이 기록된다. `Update(dt, mesh: false)`는 보이지 않는 view의 물리만 진행한다.
Unity 렌더러는 이 코어에 동적 업로드/입·표정 순서/눈 마스크를 연결한다. 웹캠을 연결한 Player API는 아직 남아 있다.
`SoftIris`로 soft/stencil 눈 clipping을 선택하고, `Masks`/`Only`로 원본의 layer 선택 경계를 설정한다.
현재 눈 mask RT는 출력 크기 전체를 사용한다. 원본의 cropped 64px 성장 전략과 Mobile 메모리/CPU 프로파일링은 남아 있다.

전체 지원 목표와 플랫폼별 완료 기준은 [PLATFORMS.md](../docs/unity/PLATFORMS.md)를 따른다.
`new PuppetRenderer(customWebPDecoder)`로 다른 플랫폼 디코더를 공급할 수 있다.


## Unity Player host

`src/base.ts`는 `Runtime/base.cs`, `src/follow.ts`는 `Runtime/follow.cs`에 대응한다.
`src/index.ts`의 Unity surface는 `Runtime/Platform/Unity/Player.cs`다. DOM/RAF 대신 host가 프레임과 화면 크기를 전달한다.

```csharp
// target: caller-owned RenderTexture with a 24-bit depth/stencil buffer.
var player = new LivLiv.Unity.Player(target);
player.Width = 768; player.Height = 1024; // logical display coordinates
player.Load(package);                   // ModelPackage.FromFiles/FromZip/FromFolder
player.view.zoom = 1.2;
player.SetExpression("smile", 0.7);
// Each Unity Update, on the main thread:
player.Tick(Time.unscaledDeltaTime);
// Pointer positions use the player's top-left as origin (Unity UI requires conversion):
player.PointerMove(localX, localY);
// On host teardown:
player.Dispose(); // target remains owned by caller
```

`Use(IPlayerDriver)`는 우선순위 순으로 실행하고 반환한 `Action`으로 제거/해제한다.
`visible=false`는 시간 진행을 멈추며, `Pause/Play`는 자동 host tick을 제어한다. `Resize()`는 정지 중에도 dt=0으로 그린다.
`Load`와 `LoadAsync`는 Unity main thread에서 호출한다. `LoadAsync`는 절대 HTTP(S) URL(ZIP/폴더/model.json), ZIP 바이트, 파일 map, ModelPackage를 받는다. 취소·교체·진행률·재시도·플랫폼 제한은 [LOADING.md](../docs/unity/LOADING.md)를 참고한다.
RenderTexture의 해상도/DPR·회전·생성/해제와 실제 카메라/마이크 권한은 host가 관리한다. `PlayerOptions.maxPixelRatio`는 아직 target 크기를 자동 변경하지 않는다.


## 입력 드라이버

`FaceTracking.Attach(player, faceInput, monotonicMilliseconds, options)`는 준비된 `IFaceInput`을 받아
원본 `tracking.ts`의 driver를 등록한다. `Next`의 null은 새 추론이 없다는 뜻이고 빈 `FaceFrame`은 얼굴을 찾지 못한 새 결과다.
WebGL에서는 `WebGLFaceInput`의 MediaPipe GPU 추론이 연결됐다. 네이티브 CPU 추론과 WebCamTexture 어댑터는 선택 패키지 `ai.livliv.mediapipe`에 구현했다. 얼굴이 사라져도 세션이 준비된 동안 `frame.tracked`는 원본처럼 true다.

```csharp
// Call from a user-initiated main-thread action. Cancellation cleans up a partially started capture.
var input = await LivLiv.Unity.MicrophoneInput.StartAsync(cancellationToken: token);
var lip = LivLiv.LipSync.Attach(player, input, new LivLiv.LipSyncOptions { motion = 1, bounce = false });
// Later: lip.Stop(); Player.Dispose() also removes and disposes its owned drivers.
```

네이티브 마이크 입력은 Unity Microphone의 순환 AudioClip을 읽고 공용 `PcmVoiceInput`에 전달한다.
iOS usage description과 Android microphone permission 설정이 필요하다. 권한/실제 장치 실행은 아직 검증하지 않았다.
WebGL에서는 같은 API가 Web Audio bridge를 사용한다. `WebGLVoiceInput.NeedsGesture`가 true이면 페이지의 pointer/key 입력으로 AudioContext를 활성화할 수 있다. 실제 권한·장치는 미검증이며 localhost의 생성 MediaStream으로 WASM 연결을 검증했다.
외부 오디오 스트림은 mono/stereo interleaved PCM을 `PcmVoiceInput.Push`로 전달하고 같은 LipSync에 연결할 수 있다.
PCM 입력은 lock으로 audio producer와 main-thread consumer를 동기화한다. Unity `AudioCaptureInput` 자체는 main thread 전용이다.
음성 분석의 기준/브라우저 fixture는 [AUDIO.md](../docs/unity/AUDIO.md)에 기록했다.


## WebGL CLI 검증 씬

```sh
unity install-modules --editor-version 6000.3.14f1 --architecture arm64 --module webgl --yes
scripts/unity/prepare-webp-webgl.sh
node scripts/unity/prepare-mediapipe-native.mjs
node scripts/unity/check-native-artifacts.mjs
node scripts/unity/prepare-mediapipe-webgl.mjs
node --test scripts/unity/test-browser-input.mjs scripts/unity/test-face-input.mjs
unity build ./unity --target WebGL --execute-method VerificationBuild.WebGL --output-path "$PWD/unity/TestResults/WebGL"
python3 -m http.server 8770 --bind 127.0.0.1 --directory unity/TestResults/WebGL
```

`VerificationBuild`는 원본 char06 폴더를 다시 ZIP으로 묶고 검증 씬을 만든다. 입력은 페이지 버튼을 눌러야 시작한다.
빌드 출력과 생성 ZIP은 Git에서 제외된다. `Assets/Verification`은 개발용 검증 코드이며 UPM package의 제품 API가 아니다.
브라우저 검사 스크립트/결과와 범위는 [WEBGL.md](../docs/unity/WEBGL.md)에 기록했다.


## WebGL 얼굴 입력

```csharp
// Unity main thread; call from the user's start action.
var input = await LivLiv.Unity.WebGLFaceInput.StartAsync(
    new LivLiv.Unity.BrowserFaceOptions { maxFps = -1 }, token);
var face = LivLiv.FaceTracking.Attach(player, input, LivLiv.Unity.WebGLFaceInput.Now,
    new LivLiv.FaceTrackingOptions { follow = 1 });
// face.Calibrate(); face.Stop();
```

선택적으로 `WebGLFaceInput.PreloadAsync(options, token)`으로 모델을 먼저 준비할 수 있다.
`maxFps=-1`은 원본 auto, 0은 새 영상 프레임마다, 양수는 상한이다. `onProgress`는 download/prepare/camera 단계와 진행률을 전달한다.
기본 모델·JS·WASM은 패키지 `Browser~`에서 빌드 후 `StreamingAssets/LivLivFace`로 복사한다.
HTTPS/localhost와 브라우저 카메라 권한이 필요하다. 모델은 세션 내 재사용하며, `Stop`은 해당 입력의 스트림을 종료한다.
preload 취소는 기다림을 중단하지만 공유 사전 다운로드는 계속한다(원본 ahead 계약).
네이티브 PC/Mobile에서는 이 브라우저 전용 API가 `missing-feature`를 반환한다.
검증 범위와 재현 절차는 [FACE.md](../docs/unity/FACE.md)를 참조한다.

네이티브 얼굴 입력 설치/API·실행 증거는 [NATIVE-FACE.md](../docs/unity/NATIVE-FACE.md)와 [연동 패키지 README](Packages/ai.livliv.mediapipe/README.md)에 있다.


## PC/Mobile 검증 빌드

[네이티브 빌드 가이드](../docs/unity/NATIVE-BUILDS.md)의 `VerificationBuild.MacOS`와
`VerificationBuild.Android`를 사용한다. Windows/Linux는 [DESKTOP.md](../docs/unity/DESKTOP.md)의
`VerificationBuild.Windows` / `VerificationBuild.Linux`를 사용한다. 같은 씬의 Start face는 네이티브 타깃에서 WebCamTexture/CPU adapter를 연다.
macOS의 `-livlivSmoke <output>`은 물리 장치 없이 생성 영상으로 실제 IL2CPP player를 검증한다.
Android는 APK 내부 검사까지 확인했으며 실제 장치 입력은 별도다.


## 현재 프레임 저장

`player.Snapshot()`은 원본처럼 현재 자세와 package watermark를 포함한 PNG data URL을 반환한다.
`player.SnapshotPng()`은 같은 PNG의 byte[]를 반환한다. Unity main thread에서 호출하며 생성된 caller-owned
RenderTexture가 필요하다. paused/hidden 상태에서도 dt=0으로 다시 그리므로 시간이 진행되지 않는다.
PNG는 straight alpha이며, 16M pixels의 readback 한도가 있다. UI 배경은 포함하지 않는다.
검증과 플랫폼 제한은 [SNAPSHOT.md](../docs/unity/SNAPSHOT.md)를 참고한다.


## 로딩 중 포스터

선택 UPM `ai.livliv.ugui`를 설치하고, 캐릭터 RawImage 뒤의 별도 RawImage에 `PosterPresenter`를 붙여
`presenter.Bind(player, baseUri)`로 연결한다. `LoadAsync`의 `new LoadOptions { poster = "cover.webp" }`는
빈 player에서 첫 프레임까지 포스터를 표시한다. 기존 모델이 있으면 교체 중에도 그 모델을 유지한다.
기본 이미지 형식은 PNG/WebP다. 사용법·소유권·검증은 [POSTER.md](../docs/unity/POSTER.md)를 참고한다.


## 재사용 가능한 UGUI 표시 컴포넌트

`ai.livliv.ugui`의 `PlayerSurface`를 크기가 정해진 RawImage/RectTransform에 붙이면 Player와 render target,
표시 material을 소유하고 frame/해상도 갱신을 처리한다. 첫 LateUpdate 전에 `Initialize(PlayerOptions)`로
설정하고 반환 Player에 모델·driver를 연결한다. 기본 DPR 상한 2와 4M pixel 예산을 적용한다.
`Refresh()`는 paused player도 현재 크기로 다시 그린다. 상세 생명주기·제약은 [SURFACE.md](../docs/unity/SURFACE.md).
기존 샘플의 고정 512×768 타깃과 얼굴 거리 확대·축소 제거 정책은 유지했다.
