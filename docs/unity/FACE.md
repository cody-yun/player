# 얼굴 입력: 원본 매핑과 WebGL 검증

2026-11-09. 공통 C# tracking 계산은 PC/Mobile/WebGL에서 공유한다. WebGL GPU 추론과 macOS ARM64 Editor의 native CPU 사진 추론을 확인했다. 네이티브 상세는 [NATIVE-FACE.md](NATIVE-FACE.md).

## 매핑

| 원본 경계 | Unity 구현 |
|---|---|
| `src/runtime/tracking.ts` read/apply/calibration/smoothing | `Runtime/runtime/tracking.cs` |
| 원본 loader, FaceTracker.poll/toFrame/checkLight | `Browser~/face-input.mjs` |
| browser ↔ WASM ABI | `Runtime/Plugins/WebGL/LivLivFace.jslib` |
| IFaceInput, 시작/진행률/취소/해제 | `Runtime/Platform/Unity/WebGLFaceInput.cs` |
| `src/tracking.ts` attach/driver/controls | `Runtime/tracking.cs` |
| self-hosted 브라우저 파일 배치 | `Editor/BrowserAssets.cs` |

원본과 동일한 Tasks Vision 1.1.1, GPU delegate, VIDEO, numFaces=1, blendshape/matrix 출력을 사용한다.
MediaPipe 결과 중 column-major matrix 16, 정해진 blendshape 18, landmark [10,13,14,152]의 xy 8개를
42개 double로 전달한다. 추론 없음과 새 결과의 얼굴 없음은 별도 flag다. heap 증가 후 현재 HEAPF64를 사용한다.
원본 매핑/hash는 `compatibility.json`에 기록한다. 원본 TS 파일은 수정하지 않았다.

공식 API 설명: [Face Landmarker Web](https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker/web_js).
모델·npm 배포본의 출처, 정확한 버전/integrity와 6개 파일 SHA-256은 `ThirdParty/mediapipe-webgl.json`에 있다.
라이선스/NOTICE를 패키지에 포함하며, `prepare-mediapipe-webgl.mjs`로 복원하고 `check-native-artifacts.mjs`로 검증한다.
사용하지 않는 별도 WASM module pair는 포함하지 않는다. Browser~ 배포 파일은 약 30.75 MB이며 Unity build summary 용량과 별도다.

## 실제 수행한 검증

- 원본 `FaceTracker.poll()` 직접 실행 fixture 720회: maxFps auto/10/0, 중복 프레임/readyState,
  추론 시간, no-face/선택 필드, FPS, 어두움 hysteresis(40/55). 어댑터 결과 모두 일치.
- Node 얼굴 계약 12개 통과: 위 수치 비교, 마지막 대기자 취소/공유 다운로드,
  권한 거부/늦은 카메라 stream, preload 재사용, WASM heap 교체/packed fields,
  module 로드 중 취소, preload 상태 JSON 크기. 음성 계약 6개는 별도다.
- 실제 Unity WebGL IL2CPP build, desktop Chrome 156에서 실제 MediaPipe GPU 추론을 실행했다.
  물리 카메라 대신 [공식 테스트 사진](https://storage.googleapis.com/mediapipe-assets/portrait.jpg)을
  640×480 Canvas에 그린 `captureStream(30)`을 getUserMedia 결과로 주입했다.
- C# FaceTracking driver로 전달되어 Aoi 입 값 약 0.417, 회전 입력 후 angleZ 약 −14.349°를 확인했다.
  calibration, 얼굴 유실/재검출, 어두움 진입/해제, 정지·재시작을 확인했다.
- 재시작 전후 모델/wasm resource 요청 수는 3으로 같았다. 정지한 stream은 ended였다.
- NotAllowedError 주입 후 렌더 프레임은 계속 증가했다. 취소 뒤 늦게 도착한 stream도 ended였다.
- `TestResults/webgl-face.json`에 9개 상태, 코드/hash, 사진 hash, 실행 시각을 저장했다.
  `webgl-face-turn.png`는 실제 추론으로 머리를 기울인 Unity 화면이며 직접 열어 확인했다.

생성 입력 검증은 OS 권한이나 물리 웹캠의 품질·지연·호환성 증거가 아니다. 관측 FPS는 성능 벤치마크가 아니다.

## 재현

1. `node scripts/unity/prepare-mediapipe-webgl.mjs`와 `node scripts/unity/check-native-artifacts.mjs`.
2. `node --test scripts/unity/test-face-input.mjs scripts/unity/test-browser-input.mjs`.
3. [README](../../unity/README.md)의 WebGL CLI build/server 실행.
4. 공식 사진을 ego-browser로 내려받아 `unity/TestResults/mediapipe/portrait.jpg`와
   빌드 출력 `unity/TestResults/WebGL/portrait.jpg`에 저장한다. 사진은 결과 디렉터리에만 두고 패키지에 배포하지 않는다.
5. 기존 TaskSpace 3의 p1에서 `scripts/unity/verify-webgl-face.mjs`를 ego-browser nodejs로 실행한다.
   스크립트 앞에 `const root="<repo absolute path>/";`를 주입한다. 종료/오류 때 원래 getUserMedia를 복구한다.

## 남은 범위

네이티브 Face Landmarker/WebCamTexture의 실제 장치·target build 검증, calibration 저장, recorder/diagnostics, 실제 camera/mic 동시 사용,
OS 권한/장치 제거·background/resume·회전, 다른 브라우저와 모바일 WebGL, 장기 메모리/성능 검증이 남아 있다.
현재 모델 cache는 원본처럼 페이지 수명 동안 공유한다. multi-player 동시 inference 및 실제 장치 강제 종료는 아직 검증하지 않았다.
브라우저 입력은 Unity main thread에서 시작/읽기/해제한다. Android/iOS용 native adapter로 사용할 수 없다.
