# 샘플 UGUI — 2026-11-09

후속 Android 네 방향 진단은 [ORIENTATION.md](ORIENTATION.md)를 따른다. 세로/가로 두 레이아웃은
실제 native Unity 크기로 확인했지만 반대 방향 두 요청이 적용되지 않아 전체 방향 검사는 실패로 유지한다.

샘플은 Unity UGUI 2.0.0으로 구현한다. 원본 포팅 패키지는 UI를 참조하지 않는다.
기존 `OnGUI`/`GUILayout` 조작 화면은 제거했다. 별도 UI Toolkit 또는 웹 DOM 조작 UI는 사용하지 않는다.

## 구성

- `VerificationHud.cs`: Canvas/CanvasScaler, SafeArea, 캐릭터 RawImage, 입력 버튼·상태·오류 안내. 스크롤 컴포넌트 없이 고정된 세 영역을 배치한다.
- `HudFocus.cs`: UGUI 키보드/컨트롤러 선택 포커스 표시.
- `PortVerification.cs`: 입력 lifecycle과 샘플 상태 제공; UI는 ToggleCamera/ToggleMicrophone/CalibrateFace/SetPose를 호출한다.
- `Editor/VerificationBuild.cs`: CLI 빌드 때 editable GameObject 계층을 생성하고 Verification.unity에 직렬화한다. 생성 UI 변경의 기준은 builder 코드이며 재생성 시 수동 scene 편집을 덮어쓴다.
- `Avatar.shader`: 이미 premultiplied alpha인 캐릭터 RenderTexture를 UGUI에 합성한다. 모델 렌더링을 UI 코드로 옮기지 않는다.

## 사용 흐름

카메라·마이크는 사용자가 각각 시작한다. 실행 중에는 같은 버튼이 중지로 바뀌고, 시작 요청 중에는 취소가 가능하다.
권한/장치 오류는 해당 입력 옆에 재시도 안내를 표시하며 stack trace는 로그에만 남긴다.
정면 맞추기는 얼굴이 검출된 상태에서 활성화한다. 표정 미리보기는 라이브 입력 중 비활성화하고 이유를 표시한다.
입력을 시작하면 이전 미리보기 자세를 초기화해 입 벌림 값이 립싱크를 방해하지 않도록 한다.

샘플은 얼굴 거리에 따른 확대·축소 기능을 제공하지 않으며 켜고 끄는 옵션도 없다.
좌우/상하 위치, 고개, 표정과 립싱크를 유지한다.

## 화면 대응

넓은 화면은 미리보기 + 오른쪽 입력 패널, 좁은 세로 화면은 미리보기 + 아래 패널이다.
화면 방향/크기와 Screen.safeArea 변경을 감지한다. 컨트롤은 48–50 논리 단위 높이이며
native DPI/데스크톱 크기에 따라 CanvasScaler로 스케일한다.
사용자 요구에 따라 ScrollRect/Scrollbar와 viewport clipping을 제거했다. 카메라·마이크·표정 버튼 6개는 항상 한 화면에 배치한다.
짧은 화면에서는 중복된 보조 설명을 줄이고, 매우 작은 창은 UI 배율 상한을 낮춰 조작부가 화면 밖으로 밀리지 않게 한다.
세로 패널은 340/380 논리 단위 높이이며 가로는 오른쪽에 배치한다. 오류 안내 공간은 두 입력 모두 유지한다.
WebGL 호스트는 전체 뷰포트를 사용한다. 생성된 Unity 기본 footer는 숨긴다.
한국어 텍스트는 번들 NanumGothic regular 글꼴을 사용한다. 출처와 SIL OFL은 Fonts 디렉터리에 보관한다.

UGUI 접근성(스크린리더/OS 글꼴 확대), iOS safe area 실기기, foldable/multiwindow 검증은 완료하지 않았다.
에뮬레이터 성공으로 모바일 실기기 완료를 주장하지 않는다.

Unity 공식 API 근거: [Screen.safeArea](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/screen/safearea),
[Canvas.scaleFactor](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/canvas/scalefactor).
실제 컴파일/실행 버전은 6000.3.14f1이다.

## 초기 UGUI 실행 검증 (스크롤 제거 전)

- WebGL CLI IL2CPP build: errors/warnings 0, 51,885,162 bytes, 21.581초.
- Chrome에서 실제 UGUI 버튼 클릭: 카메라 권한 거부 안내, pending 시작 취소와 늦게 반환된 stream 해제, 생성 오디오 마이크 시작/립싱크/중지 통과.
- 기본/고개/말하기 버튼 및 키보드 방향키 + Enter 동작 통과.
- 1280×800과 390×844 레이아웃, 모바일 패널 스크롤·활성 상태를 캡처하고 직접 확인했다. WebGL 모바일 화면 크기 검증이며 모바일 브라우저 실기기 검증은 아니다.
- 증거: `unity/TestResults/ugui-browser.json`에 상태·소스 해시, `ugui-*.png`에 화면.
- Android CLI build: errors/warnings 0, 48.952초. APK 63,259,353 bytes. native libraries/권한/16KB 정렬 검사 통과.
- BlueStacks 실제 UGUI 화면과 연결된 Mac 카메라의 얼굴 추적을 확인했다. `bluestacks/ugui.png`, `ugui-live.json/log`에 증거. 54개 얼굴 검출 상태 모두 scale=1.
- 새 Android APK의 생성 영상 검증도 7개 상태 모두 scale=1, 회전/유실/재검출/입력 해제 통과. `bluestacks/fixed-size-smoke/evidence.json`.

HTML 정적 디자인 검사기는 Unity wrapper의 기본 Arial/검정 텍스트를 읽으므로 Canvas 안의 실제 NanumGothic/색상 대비를 검사하지 못한다.
UGUI 화면은 실제 렌더 캡처로 확인했다. 스크린리더 검증 통과로 간주하지 않는다.

고개 미리보기의 과도한 변형은 [POSE-SAFETY.md](POSE-SAFETY.md)의 원본 비교 후 완화했다. 8/-3 목표로 부드럽게 전환하고 샘플 추적에도 좌우·상하 조합 범위를 제한한다.

## 스크롤 제거 후 검증

- WebGL/Android CLI build 모두 errors/warnings 0. WebGL 51,899,396 bytes; Android APK 63,264,174 bytes, ARM64/16KB 정렬 검사 통과.
- Chrome WebGL 1280×800, 390×844, 844×390, 320×568을 캡처해 전체 조작부가 한 화면에 표시되는 것을 확인했다. `unity/TestResults/fit-*.png`.
- 390×844에서 실제 UGUI 클릭으로 카메라/마이크 거부 안내, 고개 미리보기 8/-3 전환을 확인했다. 패널에 wheel 700 입력 후에도 전체 조작부가 그대로 표시됐다. `fit-ui.json`, `fit-errors-portrait.png`, `fit-turn-no-scroll.png`.
- 새 APK를 BlueStacks에 설치해 전체 조작부와 스크롤 없는 화면을 확인했다. `bluestacks/no-scroll.png`, `no-scroll.json`.
- 소스와 재생성한 Verification.unity에서 ScrollRect/Scrollbar/ScrollRail/Viewport를 제거했다. 렌더러/추적 계산은 바꾸지 않았다.
- 화면 크기 모의 검증과 BlueStacks 검증이며 iOS/Android 실기기 회전 검증을 대체하지 않는다.


## 로딩 포스터 연결

`VerificationHud`의 `AvatarBounds` 아래, Character 앞 sibling으로 LoadingPoster를 추가했다.
선택 패키지 `ai.livliv.ugui`의 `PosterPresenter`가 모델을 처음 로드할 때만 이미지와 상태를 표시하며,
첫 프레임 뒤 기존 배경을 복원한다. 추가 버튼·스크롤은 없다. RenderTexture에 합성하지 않는다.
실제 Chrome에서 가운데 contain 표시 화면을 직접 확인했고 로드 완료/교체/취소/오류 뒤 리소스 0을 확인했다.
이번 포스터 표시 변경의 Android/iOS 실행 검증은 별도 남아 있다. [POSTER.md](POSTER.md).
