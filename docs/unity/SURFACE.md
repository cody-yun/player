# UGUI PlayerSurface — 표시 영역과 렌더 타깃 소유권

선택 UPM `ai.livliv.ugui`의 `PlayerSurface`는 UGUI RawImage에서 `LivLiv.Unity.Player`를 구동한다.
원본 `src/index.ts`의 Player host와 `src/runtime/puppet.ts render()`의 화면 크기 계산을 연결한다.
원본 코드와 계산 코어는 수정하지 않았다. 기존 수동 host도 계속 사용할 수 있다.

```csharp
// A RectTransform under a Canvas, already sized by the host layout.
var surface = characterRect.gameObject.AddComponent<LivLiv.Unity.UI.PlayerSurface>();
var player = surface.Initialize(new LivLiv.PlayerOptions {
    idle = true, autoplay = true, maxPixelRatio = 2
});
await player.LoadAsync("https://example.com/character.livliv");
// FaceTracking / LipSync / Speech attach to this normal Player instance.
```

`Initialize(options, markText)`는 첫 LateUpdate 전에 호출한다. 선택 인수 `markText`에는
`new LivLiv.Unity.UnityMarkText(includedDynamicFont)` 또는 사용자 정의 `IMarkTextRasterizer`를 전달한다.
생략하면 패키지의 기본 NanumGothic을 사용한다([TEXT-MARK.md](TEXT-MARK.md)).
 생략하면 첫 LateUpdate 또는 `surface.Player` 접근 시
기본 옵션으로 생성한다. 이미 생성한 뒤 다른 옵션으로 Initialize하면 거절한다. 초기 설정 이후에는
Player의 기존 params/view/driver/Load/Snapshot API를 그대로 쓴다.

## 원본 크기 계산과 Unity 정책

`Runtime/Platform/SurfaceSize.cs`는 다음 원본 식을 유지한다.

```text
ratio = min(maxPixelRatio, density || 1)
width = round(logicalWidth * ratio)
height = round(logicalHeight * ratio)
```

양수 `.5`를 올리는 JS Math.round를 사용한다. 기본 maxPixelRatio는 원본처럼 2다.
`generate-surface-size.mjs`는 원본 render 본문의 두 문장을 직접 실행해 96개 fixture를 만든다.

UGUI의 logical 크기는 RectTransform.rect, density는 world corners를 화면에 투영한 가로/세로 길이를
logical 크기로 나눈 값 중 큰 값이다. 원근 카메라에서는 네 변을 모두 비교해 더 가까운 반대쪽 변의 해상도도 반영한다. Overlay는 camera 없이 계산하며 Screen Space Camera/World Space는
canvas.worldCamera 또는 Camera.main을 사용한다. Unity 화면은 이미 device pixel을 사용하므로
Screen.dpi나 브라우저 devicePixelRatio를 추가로 곱하지 않는다. transform의 비균등 확대에서는 더 큰 밀도를
선택해 한쪽 해상도가 부족해지지 않도록 한다.

할당 전 SystemInfo.maxTextureSize와 **기본 4,194,304 pixels** 예산을 적용한다.
비율을 유지해 축소하고 마지막 정수 반올림 뒤에도 한도를 검사한다. `MaxPixels`, `MaxPixelRatio`로
조절하며 `ResolutionLimited`로 예산 적용 여부를 확인한다. 이는 원본에는 없는 Unity GPU 메모리 경계다.
NaN/Infinity/음수 크기, 유효하지 않은 상한 및 int 범위를 넘는 요청은 할당 전에 거절한다.

## 생명주기와 렌더링

- 같은 픽셀 크기의 타깃은 재사용한다. 크기가 달라지면 새 linear ARGB32 + depth/stencil 24-bit 타깃을
  생성·clear한 뒤 바인딩을 교체하고 이전 타깃을 해제한다. 생성 실패 시 기존 타깃을 먼저 지우지 않는다.
- width/height 중 하나가 0으로 반올림되면 타깃을 해제한다. 다시 크기가 생기면 생성한다.
  외부/장치 이유로 target storage가 Release된 경우도 다음 갱신에서 복구한다.
- LateUpdate에서 layout과 타깃을 동기화하고 unscaledDeltaTime으로 Player.Tick을 호출한다.
  pause 상태에서는 크기가 바뀔 때 dt=0으로 다시 그린다. `Refresh()`로 이를 즉시 요청할 수도 있다.
- 컴포넌트 Disable은 frame pump를 멈추고 모델/타깃/Playing 상태를 유지한다. Enable 후 재개한다.
  원본의 pause/visibility와 같은 렌더 제어 경계이며, 별도 입력·Speech pump의 background 정책을 대체하지 않는다.
- Destroy는 Player/driver·자신의 타깃·머티리얼을 해제하고 RawImage의 기존 texture/material을 복원한다.
  기존 외부 texture/material은 해제하지 않는다. `surface.Player.Target`과 RawImage.texture/material은
  컴포넌트가 소유하므로 실행 중 외부에서 덮어쓰지 않는다.
- `LivLivSurface.shader`는 premultiplied RGBA를 그대로 합성한다. UGUI tint/alpha, stencil mask와
  직사각 clip을 지원한다. RectMask2D의 soft-edge softness 및 XR/stereo는 아직 검증·구현 범위에 포함하지 않는다.

얼굴/음성 입력은 자동으로 켜지 않는다. pointer 입력은 host가 기존 Player.PointerMove/PointerLeave로
전달한다. 별도의 입력 시스템이나 GameObject 계층을 강제하지 않는다. 포스터를 사용할 때는 같은 bounds
아래 별도 `PosterPresenter`를 뒤에 배치하고 `surface.Player`에 Bind한다.

기존 샘플은 고정 512×768 검증 타깃과 sample driver를 유지한다. 이번에 샘플의 얼굴 거리 zoom이나 UI layout을
변경하지 않았다. browser QA는 잠시 별도 영역에 새 component를 만들고 끝난 뒤 제거한다.

## 검증 — 2026-11-09

- .NET **6개**, Unity CLI EditMode **6개 통과**. 96개 원본 크기 사례, 극단 종횡비의 side/pixel 예산,
  잘못된 값의 조기 거절. Unity 06:35:40Z, 0.072초.
- Unity PlayMode **11개 통과**(PlayerSurface 6 + 기존 PosterPresenter 5), 06:33:43Z–06:33:44Z, 1.144초.
  재사용/교체·소유권, transform density/상한, 0 크기 복구, paused 모델 재그리기와 Disable/Enable,
  target storage 유실, 실제 GPU premultiplied alpha·clip·stencil 동작을 검사했다.
- WebGL IL2CPP build errors/warnings 0, **52,341,506 bytes / 21.065163초**.
- macOS Chrome 156의 실제 WASM/UGUI에서 8개 상태 기록. logical 320×480 → 444×666,
  480×320 → 666×444, transform scale 3과 cap 2 → 640×960, 10,000 pixels 예산 → 81×122.
  같은 크기에서는 target ID가 유지되고, 0 크기에서 해제·복구 후 새 ID가 생성된다.
  각 PNG에서 실제 모델/mark가 출력됐고 paused time=0과 sample scale=1을 유지했다.
  종료 후 소유 타깃 0/머티리얼 0을 확인했다.
- 별도 320×480 logical 표시 영역의 실제 UGUI 화면을 캡처·직접 확인했다. 확인 후 원래 샘플로 복귀했다.

증거: `surface-size.xml`, `surface-playmode.xml`, `webgl-surface.json`, `webgl-surface-*.png`,
`webgl-surface-visible.png`, `surface-webgl-build.log` (모두 unity/TestResults).
원본/어댑터/셰이더 SHA-256을 browser JSON에 기록했다.
macOS ARM64/Metal 및 BlueStacks ARM64/OpenGLES3 IL2CPP에서도 7개 상태와 snapshot/poster 정리를 통과했다.
실행 산출물·소스 hash·초기 layout 안정화 조건은 [NATIVE-PRESENTATION.md](NATIVE-PRESENTATION.md)에 기록했다.
전체 suite를 재실행한 결과가 아니라 위 대상 검증이다. World Space/perspective camera의 후속 검증은 아래에 기록했다.
물리 Android/iOS, 카메라 모드의 Windows/Linux player와 Safari/mobile browser, 큰 타깃의 장기 성능은 아직 검증하지 않았다.

```sh
node scripts/unity/generate-surface-size.mjs --check
unity test ./unity --mode EditMode --filter LivLiv.Tests.SurfaceSizeTests --output ./unity/TestResults/surface-size.xml --timeout 240
unity test ./unity --mode PlayMode --filter 'LivLiv.Tests.PlayerSurfaceTests;LivLiv.Tests.PosterPresenterTests' --output ./unity/TestResults/surface-playmode.xml --timeout 240
```

`verify-webgl-surface.mjs`는 `serve-poster-tests.py`의 127.0.0.1:8773 최신 빌드에서 실행한다.
ego-browser에 절대 `root`와 허용된 `surfaceSpaceId`를 전달한다. 이번 공간은 기존 9번이다.
Unity 좌표 근거: [GetWorldCorners](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/recttransform/getworldcorners).

## 원근 카메라의 반대쪽 변 해상도 수정 — 2026-11-09

기존 어댑터는 왼쪽·아래쪽 변만 투영했다. World Space Canvas가 기울어져 오른쪽 또는 위쪽 변이
카메라에 가까워지면 그 변에 필요한 해상도가 반영되지 않았다. 640×480, FOV 60°, 거리 3,
160×240 rect/scale .01, Y축 50°의 재현 사례에서 필요한 너비 279 대신 184를 할당했다.
`PlayerSurface`에서 네 변의 투영 길이를 모두 검사하도록 수정했다. 원본 크기 공식/메모리 예산은 유지한다.
이 변경은 Unity 표시 영역의 렌더 타깃 해상도에만 적용된다. 샘플 얼굴 거리 크기 고정 정책은 계속 scale=1이다.

- 수정 전 Unity PlayMode: 새 카메라 테스트 3개 중 2 passed / 1 failed. 실패가 위 184→279 사례다.
- 수정 후 Unity PlayMode: 카메라 3개 + 기존 Surface 6개 + Poster 5개 = 14 passed, failed/skipped 0.
  X/Y 각각 ±50°의 가까운 변, Screen Space Camera와 중첩 Canvas, 실제 원근 UGUI 합성 PNG를 검사했다.
- macOS ARM64 IL2CPP/Metal: 빌드 errors/warnings 0. 새 카메라 8개 상태와 기존 표시·마스크·텍스트·포스터 회귀 통과.
  정면 222×333, 좌/우 279×418, 위/아래 320×479, 직교 192×288, Screen Space Camera/중첩 Canvas 160×240.
  기대값은 고정 카메라의 FOV/거리/near-edge 깊이에서 독립 계산하며 실제 할당과 1픽셀 이내인지 검사한다.
  각 카메라의 실제 640×480 렌더에서 캐릭터 픽셀을 확인하고 PNG로 저장했다.
  paused time=0, sample scale=1, 기존 표시 리소스 종료 카운터 0도 확인했다.
- BlueStacks Android 13 / ARM64 / GLES3 IL2CPP에서도 같은 8개 상태·해상도 및 기존 표시 회귀 통과.
  APK 53,458,126 bytes, SHA-256 `7b93825d70049d04efc834e32fd5df9512f3becc5bf00c1852e3c1ab2434b407`.
  빌드 errors/warnings 0, APK 서명·16KB 정렬·포함 자산 검사 통과. 실행 후 일반 샘플로 복귀했다.
- Overlay 회귀 PNG: macOS 21/21 완전 일치, Android 20/21 완전 일치.
  Android의 `text-retina.png`만 우하단 글자 영역 23픽셀에서 최대 3/255 차이
  (전체 채널 평균 0.00000922/255, bounds 861,1491–883,1496)가 있었다.
  텍스트 위치/범위 독립 검사도 통과했다. 이 이미지를 완전 일치로 기록하지 않는다.
- Metal/GLES3 카메라 출력 차이는 평균 0.00114–0.02723/255, 최대 1–120/255였다.
  각 backend에서 실제 출력과 예상 해상도는 확인했지만 backend 간 픽셀 완전 동등성을 주장하지 않는다.

로컬 증거(Git 제외): `surface-camera-before.xml`, `surface-camera-after.xml`, `surface-world-camera.png`,
`surface-camera-macos-build.log`, `macos-presentation/1794214811622979000/evidence.json` 및 `camera-*.png`.
Android 증거는 `bluestacks/presentation/1794214882499151000/evidence.json`, PNG와 `text-mark-pixels.json`,
`surface-camera-android-build.log`, `surface-camera-android-audit.log`다. 픽셀별 비교의 입력 해시·차이 범위는
`surface-camera-pixels.json`에 남겼다. `surface-camera-upstream.log`는 원본 25모듈/22fixture, issue 0이다.
카메라 선택은 지정된 `canvas.worldCamera`를 우선하며 없을 때 `Camera.main`을 사용한다.
World Space를 여러 카메라가 서로 다른 거리로 그리는 경우의 최적 해상도, 카메라 near plane을 가로지르는 패널,
XR/stereo 및 pipeline별 동작은 이 검증에 포함하지 않는다.

```sh
unity test ./unity --mode PlayMode --filter 'LivLiv.Tests.PlayerSurfaceCameraTests;LivLiv.Tests.PlayerSurfaceTests;LivLiv.Tests.PosterPresenterTests' --output ./unity/TestResults/surface-camera-after.xml --timeout 240
# Native presentation probe now also requires the eight camera states: rebuild the player first.
python3 scripts/unity/verify-native-presentation.py macos
```

카메라 투영 근거: [Camera.WorldToScreenPoint](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/camera/worldtoscreenpoint),
[Canvas.worldCamera](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/canvas/worldcamera).
