# Snapshot — 현재 프레임 PNG

`src/base.ts`의 `snapshot()`은 `draw(0)` 뒤 canvas의 PNG data URL을 반환한다.
Unity에서는 `Runtime/Platform/Unity/Player.Snapshot.cs`가 이 경계를 맡는다. 원본 코드는 변경하지 않았다.

```csharp
// Unity main thread. player.Target is a caller-owned, created RenderTexture.
string dataUrl = player.Snapshot();    // data:image/png;base64,...
byte[] png = player.SnapshotPng();      // same image without the base64/string allocation
```

현재 render target 크기로 현재 자세를 다시 그린다. pause/visible과 무관하게 dt=0으로 driver·renderer를
실행하므로, pause 후 바꾼 params도 반영되며 시간은 진행되지 않는다. `Playing`과 `visible`은 바꾸지 않는다.
원본처럼 package watermark가 포함된다. UI/페이지 배경·카메라 영상·CSS poster는 캔버스 픽셀이 아니므로
포함하지 않는다. 로딩 포스터는 별도 [UGUI 어댑터](POSTER.md)에서 표시한다.

## 픽셀과 소유권

- 렌더러의 premultiplied RGBA를 straight RGBA로 변환한 뒤 PNG로 인코딩한다. alpha=0의 RGB는 0으로
  정규화한다. 이 변환을 생략하면 일반 PNG viewer에서 투명 가장자리 색이 한 번 더 어두워진다.
- `ReadPixels`와 PNG encoder의 좌하단 좌표계를 사용한다. 불필요한 Y 반전이나 gamma 변환을 넣지 않는다.
  기존 renderer 계약에 맞는 linear ARGB32 render target을 사용한다. PNG에는 gamma/profile metadata가 없다.
- caller 소유 target은 해제하지 않는다. 기존 `RenderTexture.active`를 finally에서 복원하고 임시
  readable Texture2D를 해제한다. player Dispose 후에도 caller가 보존한 target의 마지막 픽셀은 캡처 가능하다.
- 생성되지 않은/null target은 명시적으로 거절한다. GPU readback·CPU 복사 전 **16M pixels** 한도를 검사한다.
  이는 Unity 어댑터의 추가 메모리 경계다. 원본 canvas의 `data:,`/브라우저별 최대 크기와 다른 오류 계약이다.
- 동기 GPU readback과 PNG 인코딩이므로 매 프레임 호출용이 아니다. 메모리와 지연은 크기에 따라 증가한다.
  파일 저장·공유·브라우저 다운로드 UI는 host가 반환 바이트/URL로 처리한다.

Unity API 근거: [ReadPixels](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/texture2d/readpixels),
[RenderTexture.active](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendertexture/active),
[EncodeToPNG](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/imageconversion/encodetopng).
실제 실행 버전은 Unity 6000.3.14f1이다.

## 실제 검증 — 2026-11-09

Unity CLI EditMode/Metal **4개 통과**, 실패/skip 0, 06:18:00Z–06:18:05Z (4.812초).

- 색/반투명/완전 투명 2×2 입력의 PNG decode 결과, 방향, data URL, Dispose 후 caller target 보존.
- paused/playing 각각 hidden 상태에서 driver 1회·dt=0·clock 불변·현재 params 재그리기·mark 포함.
- 같은 PNG를 premultiplied 공간으로 되돌려 GPU raw RGBA와 비교(최대 1 LSB 이내).
- 반복 캡처 후 임시 Texture2D 수 증가 없음, active target 복원.
- null/uncreated/예산 초과 target 거절; 큰 target을 실제 할당하기 전에 실패.

WebGL IL2CPP build errors/warnings 0, **52,272,501 bytes / 18.847256초**.
Ego 검증 공간 9에서 macOS Chrome 156으로 실행했다. 원본 `PlayerBase.snapshot()`에 실제 WebGL
2×2 canvas를 넣어 straight-alpha/방향 기준을 확인했다. 같은 값은 Unity 색상 테스트에서도 확인한다.
최신 Unity 빌드에서 공개 Snapshot API로 Aoi 기본/고개/눈 감김/말하기 **512×768 PNG 4장**을 생성했다.

4장 전체 픽셀 검사: alpha 차이 **0**, premultiplied 왕복 오차 최대 **0.498039/255**.
이 수치는 같은 Unity 프레임의 GPU readback과 PNG 사이 변환 오차이며 원본 renderer와의 전체 그림 오차가 아니다.
브라우저 Canvas로 PNG를 다시 읽으면 내부 alpha 반올림으로 최대 1.4902/255가 관측된다.
시간 불변, paused/hidden 유지, active target 복원, scale=1도 모두 통과했다.
고개 자세 PNG를 직접 열어 캐릭터·워터마크·투명 배경을 확인했다. QA 후 샘플은 기본 미리보기로 복귀했다.

증거 파일(로컬 결과 디렉터리는 Git 제외):

- `unity/TestResults/snapshot.xml`, `snapshot-editor.log`
- `unity/TestResults/snapshot-webgl-build.log`, `webgl-build.json`
- `unity/TestResults/webgl-snapshot.json`: 브라우저 상태와 사용 소스 SHA-256
- `unity/TestResults/snapshot-pixel-check.json`: 전체 픽셀 오차 및 PNG SHA-256
- `unity/TestResults/webgl-snapshot-{neutral,turn,blink,talk}.png`와 동일 프레임 `-raw.png`
- `unity/TestResults/snapshot-upstream-alpha.png`: 원본 snapshot API의 alpha/orientation 기준

```sh
unity test ./unity --mode EditMode --filter LivLiv.Tests.SnapshotTests --output ./unity/TestResults/snapshot.xml --timeout 240
unity build ./unity --target WebGL --execute-method VerificationBuild.WebGL --output-path "$PWD/unity/TestResults/WebGL"
python3 scripts/unity/check-snapshot-pixels.py
```

브라우저 재현은 `reference-server.mjs`(8769)와 `serve-speech-tests.py`(8772, 최신 build도 제공)를 켜고
`verify-webgl-snapshot.mjs`에 절대경로 `root`와 허용된 `snapshotSpaceId`를 주입해 ego-browser로 실행한다.
스크립트는 새 검증 공간을 만들지 않는다. 소스 비교 PNG와 제품 snapshot의 straight-alpha 계약은 유지하며,
기존 QA의 raw-premultiplied `CaptureFrame()` 경로는 별개로 보존했다.

추가로 macOS ARM64/Metal 및 BlueStacks ARM64/OpenGLES3 IL2CPP에서 크기 변경별 PNG 전체 픽셀을 비교했다.
alpha 차이 0, premultiplied 왕복 최대 오차 0.498039 LSB 및 time=0/scale=1을 확인했다.
[NATIVE-PRESENTATION.md](NATIVE-PRESENTATION.md)에 실행 증거와 재현 방법을 기록했다.
물리 Android/iOS, Windows/Linux native player, Safari/mobile browser와 큰 이미지의 성능/최대 메모리는 아직 검증하지 않았다.
