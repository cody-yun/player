# 네이티브 얼굴 입력

2026-11-09. `ai.livliv.mediapipe`는 공통 `ai.livliv.player`와 별도인 선택적 UPM 연동 패키지다.
이 검증 프로젝트에는 두 패키지를 설치한다. WebGL에서는 네이티브 어댑터 assembly를 제외하고 기존 Tasks Vision bridge를 사용한다.

## 선택과 고정

[MediaPipe Unity Plugin v0.16.3 릴리스](https://github.com/homuler/MediaPipeUnityPlugin/releases/tag/v0.16.3)의
`com.github.homuler.mediapipe-0.16.3.tgz`를 사용한다. 실제 다운로드 크기는 290,414,365 bytes이며
공개 release digest `cc3e77a219e0b99618ae3be64c31a566197deedc69c1e136acf52d65d7cf2e79`와 일치한다.
`unity/ThirdParty/mediapipe-native.json`에 출처/버전을 기록한다. 바이너리는 Git에서 제외하고
`node scripts/unity/prepare-mediapipe-native.mjs`로 SHA-256 검사 후 복원한다.
프로젝트 manifest는 `file:../ThirdParty/com.github.homuler.mediapipe-0.16.3.tgz`를 참조한다.

[프로젝트 README](https://github.com/homuler/MediaPipeUnityPlugin)는 Windows/macOS/Linux/Android/iOS의 Face Landmarker를 제공한다.
macOS/Windows 사전 빌드 라이브러리는 CPU 전용이며 Windows 지원은 experimental로 표기되어 있다.
다운로드한 macOS dylib는 ARM64/x64 universal이며 `otool -L`에서 시스템 framework/libc++ 의존성을 확인했다.
다른 대상의 실제 링크·실행 증거는 아직 없다. Android는 upstream에서 요구하는 NDK C++ shared runtime을 추가로 패키징해야 한다.
릴리스 archive의 LICENSE와 Third Party Notices를 배포 시 보존한다.

모델은 WebGL과 같은 `face_landmarker.task` SHA-256 `64184e229b263107bc2b804c6625db1341ff2bb731874b0bcc2fe6544e0bc9ff`다.
단, 네이티브 내부 MediaPipe 0.10.22와 웹 Tasks Vision 1.1.1은 같은 런타임이 아니므로 추론 수치의 완전 동등성을 주장하지 않는다.

## 원본과 연결하는 경계

- `NativeFaceInput`: CPU, VIDEO, 한 얼굴, blendshape/matrix 활성화. `IFaceInput`으로 원본 tracking driver에 연결한다.
- `ToFrame`: 명칭 기반 18 blendshape와 4 landmark를 선택한다. 플러그인이 수행한 Z축 반전 `S*M*S`를 역변환하고 column-major 16개 값을 전달한다. Euler/Quaternion 중간 변환은 없다.
- `WebCamFaceSource`: Android permission / Unity camera authorization, 카메라 선택, 취소·15초 초기화 timeout, 소유한 WebCamTexture와 CPU texture 해제.
- 영상은 Unity의 bottom-up 메모리에서 upright top-down으로 정규화한다. vertical mirror와 clockwise 0/90/180/270 회전을 처리한다. 사용자 좌우 mirror는 기존 tracking 계산에 남긴다.
- 새 프레임 없음은 null, 얼굴 없는 추론은 빈 FaceFrame이다. native VIDEO의 정수 timestamp는 반드시 증가시킨 입력만 사용한다.
- auto/0/양수 FPS 정책은 원본의 간격 계산을 따른다. 초기 CPU 경로의 어두움은 32×24 nearest 샘플로 같은 40/55 hysteresis를 적용한다. 브라우저 Canvas의 축소 필터와 픽셀 단위 동일한 분석은 아니다.
- `INativeFaceSource`로 host 영상/녹화 프레임도 공급할 수 있다. source의 ownership과 top-down RGBA32 계약은 패키지 README에 있다.

초기 구현은 Unity main thread에서 동기 CPU 추론한다. 모바일 성능·GPU/worker 경로와 frame budget은 검증 후 보완한다.
입력 오류는 Error에 보존하고 입력을 해제한다. host는 background/teardown에서 Stop하고 foreground 복귀 때 명시적으로 재시작한다.

## 실제 증거

Unity 6000.3.14f1 CLI, macOS ARM64 Editor에서 다음을 실행했다.

- 방향 검사 8개: 4개 회전 × vertical mirror on/off, 비정사각형 입력의 정확한 픽셀 순서.
- synthetic matrix/선택 category/landmark: Z축 역변환, column-major 순서, 빠진 category=0, 얼굴 없음.
- 실제 네이티브 라이브러리와 task 모델로 공식 portrait 사진의 얼굴 감지 → 검은 이미지에서 얼굴 유실/어두움 → dispose → 새 task 생성 후 사진 재검출.
- 위 실행의 matrix16/blendshape18/landmark8을 `unity/TestResults/native-face-frame.json`으로 저장했다.
- 외부 source 소유 texture를 입력이 임의 파괴하지 않고, source Dispose는 한 번만 호출되는지 확인했다.

물리 카메라와 OS 권한은 열지 않았다. Editor 결과를 실제 장치·다른 플랫폼 실행 증거로 확대 해석하지 않는다. 후속 macOS IL2CPP player 실행과 Android/iOS 빌드 증거는 [NATIVE-BUILDS.md](NATIVE-BUILDS.md)에 있다.

전체 Unity 회귀 1,134개 통과 후, 취소 시작 테스트 추가와 시작 취소 보강을 포함한 focused native suite **11개 통과**를 확인했다.
`native-face-evidence.json`은 실행 환경·XML 결과·코드/모델/사진 hash와 추론 frame을 묶는다.

## 실행

```sh
node scripts/unity/prepare-mediapipe-native.mjs
node scripts/unity/check-native-artifacts.mjs
unity test ./unity --mode EditMode --filter LivLiv.MediaPipe.Tests \
  --output ./unity/TestResults/native-face.xml --timeout 240
```

테스트는 `unity/TestResults/mediapipe/portrait.jpg`를 사용한다. [FACE.md](FACE.md)의 공식 사진 다운로드 절차로 준비한다.
실제 추론 fixture가 없으면 해당 테스트는 실패하며 skip으로 감추지 않는다. model bytes는 Browser~의 고정 모델에서 읽는다.

실제 앱은 `WebCamFaceSource.StartAsync(modelBytes, options, token)` →
`FaceTracking.Attach(player, input, NativeFaceInput.Now, options)` 순서로 연결한다.
macOS/iOS usage description, Android manifest/runtime permission 및 C++ runtime은 target build에서 확인해야 한다.
