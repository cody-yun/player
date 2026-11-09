# 플랫폼 계약 및 검증 행렬

2026-11-09 사용자 요구: **PC, Mobile, WebGL 모두 기대한 동작**을 전체 목표로 한다.
초기 macOS 검증은 개발 환경일 뿐 최종 지원 범위가 아니다. 에디터 테스트로 다른 플랫폼 지원을 선언하지 않는다.

## 지원 목표

| 대상 | 렌더/코어 | WebP | 얼굴 추적/장치 입력 | 현재 증거 |
|---|---|---|---|---|
| Windows x64 | 동일 C#/Unity shader; D3D11 우선 | MinGW x64 decoder DLL 빌드 | 네이티브 Face Landmarker + WebCamTexture/Microphone | Mono CLI build·PE x64·DLL/심볼/모델 포함 확인; 실제 실행 미검증. [DESKTOP.md](DESKTOP.md) |
| macOS ARM64 / x64 | 동일 코어; Metal | libwebp universal dylib; 샘플 macOS 13+ | 네이티브 Face Landmarker + WebCamTexture/Microphone | ARM64 Editor 수치/모델/동적 GPU/Player/디코더 검증; 네이티브 CPU 사진 추론/유실 검증; ARM64 IL2CPP standalone/Metal 생성 영상 추론·렌더·유실/재검출 및 Surface/Snapshot/Poster 및 cropped eye mask 실행 확인; x64 IL2CPP Rosetta/Metal에서 생성 영상·표시·음성 실행 확인; 물리 Intel/입력 장치 미검증 ([MACOS.md](MACOS.md)) |
| Linux x64 | 동일 코어; Vulkan 또는 GLCore | GCC x64 decoder SO 빌드 | 네이티브 Face Landmarker + WebCamTexture/Microphone | IL2CPP CLI build; Docker Ubuntu 24.04/Mesa GLCore 생성 영상 추론·회전/유실/재검출·크기 고정·Surface/Snapshot/Poster/눈 마스크/텍스트 실행 통과. 물리 GPU·장치 미검증. [DESKTOP.md](DESKTOP.md) |
| Android ARM64 | 동일 코어; GLES3/Vulkan | NDK r27c decoder so 빌드 | 네이티브 Face Landmarker + runtime camera/mic permission | IL2CPP APK 빌드·ARM64 라이브러리/심볼·권한 선언·16KB ELF/ZIP 정렬 통과; BlueStacks GLES3 Surface/Snapshot/Poster·cropped eye mask 실행 통과; 실기기 미검증 |
| iOS ARM64 | 동일 코어; Metal/IL2CPP | ARM64 libwebp static archive 빌드, `__Internal` | 네이티브 Face Landmarker + usage descriptions/permission | 최신 Unity export + unsigned Xcode iPhoneOS build/link, WebP/MediaPipe 심볼·embedding·ARM64·Resources·IL2CPP metadata 확인. iPhone 13 mini는 연결 가능하나 서명/기기 DDI 준비가 막혀 실제 실행 미검증 ([NATIVE-BUILDS.md](NATIVE-BUILDS.md)) |
| WebGL/WASM | 동일 코어; WebGL2 | Unity Emscripten libwebp archive 빌드/링크 확인 | Web Audio + Tasks Vision 1.1.1 GPU bridge 구현 | desktop Chrome 156: IL2CPP build, Aoi 4 poses, 생성 오디오/영상·얼굴 추론·회전/유실/어두움·재시작/거부/취소/해제 확인; 실제 권한·장치/다른 브라우저 미검증 |

PC는 Windows/macOS/Linux를 포함한다. WebGL은 desktop browser뿐 아니라 지원 가능한 mobile browser에서도
화면 회전·권한·pause/resume·장치 제약을 확인한다. 브라우저/OS 최소 버전과 성능 기준은 실제 build/device 결과로 확정한다.

## 구현 경계

- **원본 계산 코어 공유**: Params/filters/tracking/voice/rig/physics/format/ZIP은 C#으로 공유한다.
  `Runtime/`에 OS 파일 경로나 WebGL JavaScript 호출을 섞지 않는다.
- **WebP**: `IWebPDecoder`의 header probe + RGBA decode. 먼저 dimensions/budget을 검사한다.
  디폴트 구현은 동일 libwebp C API를 DLL/dylib/so 또는 statically linked WASM/iOS에서 호출한다.
  WebGL archive는 해당 Unity Editor가 제공하는 Emscripten toolchain과 맞춰 빌드한다. iOS/Android는
  Unity 프로젝트의 아키텍처·SDK/NDK·배포 타깃과 linker 설정을 맞춘다.
  현재 바이너리는 macOS ARM64/x64 universal, Unity 6000.3.14f1 Emscripten WebGL, Android ARM64 shared, iOS ARM64 static, Windows/Linux x64 decoder다. 바이너리/링크 코드가 있다는 이유로 지원 완료라고 하지 않는다.
- **모델 소스**: folder path는 native desktop용 편의 API다. WebGL/모바일은 URL/StreamingAssets/사용자 선택 파일을
  `Player.LoadAsync`의 HTTP(S) 입력은 공통 `UnityModelTransport`로 수신한다. ZIP/폴더 URL, 취소·진행률·교체를 지원하며
  WebGL 브라우저 실제 다운로드를 확인했다. StreamingAssets/사용자 선택 파일은 host가 byte[]로 받아 `LoadAsync`/`ModelPackage`로 전달한다. [LOADING.md](LOADING.md).
  APK 안의 StreamingAssets를 File.ReadAllBytes로 읽는다고 가정하지 않는다.
- **얼굴 입력**: native와 browser inference는 별도 구현. 공통 계약은 `FaceFrame`의 column-major 4x4,
  18 blendshape 순서, `[10,13,14,152]` landmarks, monotonic timestamp다. tracking 계산은 동일 코드다.
  WebGL에서는 native MediaPipe 플러그인을 로드하지 않고 원본 Tasks Vision 버전을 고정한 JS bridge를 쓴다.
  네이티브 `ai.livliv.mediapipe`는 homuler 0.16.3 CPU VIDEO 추론과 WebCamTexture 어댑터다. macOS ARM64 Editor/IL2CPP player의 생성 영상 추론을 확인했다. Android APK/iOS unsigned app 빌드를 통과했고 BlueStacks Android/OpenGLES3 실행과 Mac 카메라 전달을 확인했다. 물리 모바일 기기는 미검증이다. [BLUESTACKS.md](BLUESTACKS.md). [NATIVE-FACE.md](NATIVE-FACE.md).
- **음성 입력**: native Microphone/AudioClip와 browser Web Audio를 구분한다. sample rate/channel count,
  bandpass 650Hz/Q .55, unfiltered linear power, clock 단위를 공통으로 맞춘다.
  공용 PCM 분석은 실제 WebAudio 4개 sample rate와 비교했다. Unity native Microphone/순환 AudioClip 어댑터는 구현됐으나
  BlueStacks가 연결한 Mac 마이크 입력을 확인했다. 물리 Android/iOS 장치는 미검증이다. WebGL Web Audio bridge는 desktop Chrome의 생성 MediaStream과 실제 WASM에서 확인했다.
- **렌더링**: premultiplied alpha, render-target Y, sRGB/linear, stencil/depth, UInt32 indices, shader stripping을
  각 graphics API에서 검증한다. 플랫폼별 분기보다 Unity의 GPU projection/texture abstraction을 우선 사용한다.
- **실행 형태**: IL2CPP AOT/stripping에서 Json.NET 및 P/Invoke가 유지되는지 player build로 확인한다.
  WebGL에서 background threads, blocking I/O, OS file picker, arbitrary native library를 사용할 수 있다고 가정하지 않는다.
- **생명주기**: permission denied, device removed, tab hidden/app background, focus resume, orientation change,
  audio context unlock, load cancellation, repeated disposal를 공통 host 계약에 포함한다.

표시 어댑터의 macOS/BlueStacks 실행과 iOS unsigned 빌드 증거는 [NATIVE-PRESENTATION.md](NATIVE-PRESENTATION.md)에 기록했다.
Android 개발 APK 반복 패키징, 서명 및 크기 감사는 [ANDROID-PACKAGING.md](ANDROID-PACKAGING.md)를 따른다.

## 완료 게이트

macOS/BlueStacks의 제한된 21개 모델 반복 교체·해제와 시간/메모리 기준값은
[MODEL-SOAK.md](MODEL-SOAK.md)에 기록했다. 6·7번의 장시간/실기기 게이트는 아직 열려 있다.
후속 CPU 변형 최적화와 macOS/BlueStacks 재검증 범위는 [PERFORMANCE.md](PERFORMANCE.md)를 따른다.

각 플랫폼은 아래 증거가 모두 있어야 완료다. 도구/장치가 없어 실행하지 못한 칸은 미검증으로 유지한다.

1. 고정 Unity 버전으로 CLI player build 성공. Native/WASM linker와 shader compile 오류가 없어야 한다.
2. 설치/브라우저 로드 후 첫 프레임까지 도달. folder/ZIP 중 해당 플랫폼의 실제 전달 경로로 캐릭터 로드.
3. 동일 pose fixture의 geometry/parameter parity와 neutral/turn/blink/talk/mask/mark GPU screenshot 비교.
4. 실제 카메라·마이크 권한 허용/거부, mirror, calibration, face lost/reacquired, lip sync 확인.
5. 오디오와 웹캠 동시 실행, 재시작/foreground 복귀, 세로·가로 회전, WebGL secure context/사용자 gesture 확인.
6. 테스트 장치 사양·출력 해상도·평균/p95 프레임 시간·메모리·추론 주기를 기록하고 합의된 성능 기준 확인.
7. 반복 load/dispose와 장시간 실행 후 이미지·RenderTexture·native buffer·장치 handle 누수 없음.

P2–P3는 이 경계를 유지하며 진행하고, P4에서 native/browser device adapters를 함께 구현한다.
P5는 한 플랫폼의 성공으로 마감하지 않고 위 전체 행렬로 완료 여부를 판단한다.
