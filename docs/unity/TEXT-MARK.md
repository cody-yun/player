# 소유자 텍스트 워터마크 — 2026-11-09

## 텍스트 경계 그림자 후속

기본 `UnityMarkText`도 blur에 필요한 화면 밖 glyph coverage를 보존하도록 수정했다.
coverage bounds는 그림자가 있을 때만 `ceil(1.5 × blur)` 픽셀까지 화면 밖으로 확장할 수 있다.
최종 출력은 캔버스 안으로 자르며, portable 경계가 확장 범위·16M 픽셀 예산과 alpha를 검증한다.
메서드/타입은 그대로이고 기존의 화면 안 coverage를 반환하는 외부 provider도 계속 지원한다.

수정 전 Unity GPU 경계 4개는 큰 캔버스의 crop과 최대 28/41/40/39 채널 단계가 달라 실패했다.
수정 후 네 경계 모두 기존 허용치 2/255 이내이며, 관련 Unity 154개와 .NET 전체 1,283개가 통과했다.
2단계 허용치는 GPU projection의 coverage 반올림 차이를 위한 것으로 수정 전후에 변경하지 않았다.
앞선 로고 경계 9개도 이 회귀에 포함된다. 원본 상수·폰트 선택·텍스트 배치 수식은 변경하지 않았다.
새 core+UGUI tarball 소비자도 PlayMode 1개/설치 파일 감사를 통과했다:
`unity/TestResults/consumers/1794227253161886000/`.

증거: `unity/TestResults/text-clip/`의 `unity-before.xml`, `unity-final.xml`, `final.trx`, 소스 해시와
UPM 설치 검증. 초기 portable 테스트는 그림자용 투명 여백을 빠뜨린 1×1 coverage를 사용해 수정 후에도
4개 실패했다. 이를 계약에 맞는 padding fixture로 바로잡았으며 해당 실패 결과도 `all-after.trx`와
`unity-after.xml`에 남겼다. GPU 경계 테스트는 처음부터 같은 입력과 허용치로 실행했다.
이 결과로 Canvas와 폰트/AA/blur 픽셀 동등성까지 주장하지 않는다.
후속 통합 갱신에서 7개 대상 빌드 및 macOS ARM64/x64(Rosetta)·BlueStacks·Linux Docker·Chrome의
텍스트 8개 상태/종료 후 소유 자원 0을 확인했다. Chrome 한글 PNG도 직접 확인했고 픽셀 변화는
오른쪽 아래 지정 영역에만 있었다. 작은 캔버스의 경계 픽셀 비교 자체는 위 Unity Editor 테스트 범위다.
증거: `unity/TestResults/mark-boundary-final/evidence.json`.

아래는 텍스트 후속 이전의 로고 수정과 초기 구현 기록이다.

후속 로고 경계 수정: `BrandRasterizer`가 캔버스 밖 도형을 잘라낸 뒤 blur하던 순서를 수정했다.
blur kernel 반경만큼 화면 밖 coverage를 유지하고 최종 합성에서 자른다. 원본의 로고 경로·상수·
레이아웃은 변경하지 않았다. .NET 수정 전 경계 9개가 모두 실패했고 수정 후 관련 78개 및
전체 1,278개가 통과했다. Unity CLI의 BrandClip/BrandParity/MarkText/Graphics 145개도 통과했다.
Chrome Canvas에서도 네 변/두 모서리 6개 조건에서 직접 그린 결과와 큰 캔버스 crop이 동일했다.
이는 clipping 순서의 근거이며 Unity의 AA/blur가 Canvas와 픽셀 단위로 같다는 주장은 아니다.
당시 남아 있던 텍스트 provider의 화면 밖 glyph clipping은 위 후속에서 수정했다.
증거: `unity/TestResults/brand-clip/`의 before/after TRX, 전체 TRX, Unity XML, canvas-reference.json.
수정 core tarball의 새 소비자 PlayMode 1개와 설치 파일 감사도 통과했다.
후속 macOS ARM64 Metal/Android ARM64 GLES3 빌드와 생성 추론 10개 상태도 통과했다.
Android APK 감사의 미참조 ZIP 영역은 0이다. `unity/TestResults/brand-clip/players-evidence.json`.
해당 smoke는 일반 캐릭터 동작 회귀이며 작은 캔버스의 그림자 픽셀 검사는 위 .NET/Unity 테스트 범위다.
다른 플랫폼 앱은 이 로고 수정 이전 결과로 유지한다.

원본 `src/mark.ts` → `Runtime/mark.cs`, `src/runtime/brand.ts` → `Runtime/runtime/brand.cs`의
선택·레이아웃·DPR·색상·캐시 계산은 그대로 유지한다. `UnityMarkText`는 원본 Canvas `fillText`의
문자 coverage 경계만 맡으며 `BrandRasterizer`가 기존 opacity/shadow/premultiplied RGBA 합성을 적용한다.
모델이 지정한 소유자 텍스트는 화면과 `SnapshotPng()` 모두에 포함된다. UGUI나 포스터와는 독립적이다.

## 사용과 소유권

기본 Player는 패키지 Resources의 NanumGothic Regular를 사용한다. 별도 설정 없이 한국어/영문 이름을
그릴 수 있다. 다른 폰트를 사용하려면 Dynamic + Include Font Data로 가져온 Font를 전달한다.

```csharp
var text = new LivLiv.Unity.UnityMarkText(includedFont, UnityEngine.FontStyle.Bold);
var player = new LivLiv.Unity.Player(target, options, markText: text);
// Optional UGUI host, before first LateUpdate / first access to surface.Player:
var uiPlayer = surface.Initialize(options, markText: text);
```

Font와 provider는 빌려 사용한다. Player가 해제하지 않으므로 host가 사용 기간 동안 유지해야 한다.
실제 semibold 폰트 파일을 전달할 때는 FontStyle.Normal을 선택할 수 있다.
Player core는 UGUI 패키지를 참조하지 않는다. glyph mesh의 UIVertex 때문에 engine 모듈
`com.unity.modules.ui`를 명시하며 TextRendering/TextCore는 Unity engine API를 사용한다.

`IMarkTextRasterizer`는 캔버스 안의 top-down coverage와 shadow padding을 반환한다. 다른 조판 엔진도
이 경계로 연결할 수 있다. 선택 인터페이스 `IMarkTextValidator`를 함께 구현하면 모델 교체 전 검증한다.
validator가 없는 사용자 provider의 오류는 그리기 시점에 발생할 수 있다.

## 동작과 제한

- 기본 provider는 실제 FontEngine face의 글리프를 검사한다. 미지원 문자는 `missing-feature`로
  거절하고 이미 로드한 모델을 유지한다. OS 폰트로 묵시적으로 fallback하지 않는다.
- 문자열은 rich text로 해석하지 않는다. TAB/LF/CR/FF는 Canvas처럼 공백으로 바꾸고 줄바꿈하지 않는다.
  긴 문자열은 오른쪽 기준으로 배치하며 캔버스 밖을 자른다.
- 최대 8,192 UTF-16 units, font size 2,048, glyph 요청 `length × ceil(size)²` 16,777,216,
  coverage 16,777,216 pixels 및 GPU 최대 변 길이를 검사한다. 초과는 `too-large`다.
- owner text는 시간만 바뀔 때 다시 굽지 않는다. text/size/DPR 변경은 원본 캐시 정책에 따라 갱신한다.
  cache miss에서 작은 coverage RT에 glyph를 그린 뒤 동기 GPU readback한다. 큰 문자열의 지연은
  아직 모바일 장기 성능 검증 범위에 포함하지 않는다.
- 임시 target/mesh/material/readback texture는 해제하고 기존 RenderTexture.active/GL.sRGBWrite를 복원한다.
  FontEngine/폰트 atlas는 공유 엔진 리소스이며 이 어댑터가 소유하지 않는다.
- 원본은 `600 ... system-ui,sans-serif`다. 기본 NanumGothic Regular의 synthetic Bold는 근사다.
  폰트 메트릭·AA·그림자의 Canvas 픽셀 동등성은 주장하지 않는다. 복잡한 조판, RTL, emoji/color font는
  미검증이며 필요하면 전용 provider를 주입한다. `mark.ts`의 compatibility 상태는 partial을 유지한다.

## 검증

- .NET MarkTextTests/BrandParityTests **69 통과**.
- Unity CLI EditMode GraphicsTests/MarkTextTests/BrandParityTests **129 통과**,
  07:15:31Z–07:16:20Z. 영문/한국어, 긴 문자열, top baseline, 리터럴, 미지원 글리프,
  캐시·DPR·그림자·상태 복원·교체 실패 rollback과 기존 그래픽 회귀를 검사했다.
- UGUI PlayMode PlayerSurface/PosterPresenter **11 통과**, 07:17:02Z–07:17:03Z.
- WebGL IL2CPP build **errors/warnings 0**, **53,209,406 bytes / 18.765219초**.
- macOS Chrome 156 실제 WASM에서 empty/latin/korean/literal/default/retina-empty/retina/budget
  **8개 상태 통과**. 7장 PNG를 저장하고 5개 text 이미지가 같은 해상도의 빈 텍스트 이미지와
  오른쪽 아래 워터마크 영역에서만 달라짐을 확인했다. 한국어·기본 폰트 PNG를 직접 열어 확인했다.
  512×768 한국어 변화 영역 `[425,732,499,754)`, 1024×1536에서는 `[850,1465,996,1508)`이다.
- 각 WebGL 상태 종료 후 probe 소유 watermark texture와 text coverage RT/mesh/material은 모두 0이다.
  정상 샘플 로고의 blink 재할당과 혼동하지 않도록 생성 시점의 texture ID로 소유권을 추적한다.

로컬 증거는 `unity/TestResults/text-mark-final.xml`, `text-mark-playmode.xml`,
`webgl-text-mark.json`, `text-mark-pixels.json`, `webgl-text-*.png`, `text-mark-webgl-final-build.log`다.
이 결과는 대상 검증이며 전체 suite나 모든 플랫폼 검증은 아니다. Native IL2CPP 실행 결과는 아래에 기록한다.

재현: Unity CLI로 빌드하고 `serve-poster-tests.py`의 localhost:8773에서 최신 WebGL을 연다.
`verify-webgl-text-mark.mjs`에 절대 `root`와 기존 허용 공간 `textMarkSpaceId`를 주입해 ego-browser로 실행한 후
`python3 scripts/unity/check-text-mark-pixels.py`로 PNG를 검사한다. 새 브라우저 공간이나 장치 권한은 필요하지 않다.

## 폰트 배포

`Runtime/Platform/Unity/Resources/LivLivMarkFont.ttf`는 기존 검증 UI의 NanumGothic Regular를
수정 없이 복사한 **2,054,744 bytes** 파일이다. SHA-256:
`76f45ef4a6bcff344c837c95a7dcc26e017e38b5846d5ae0cdcb5b86be2e2d31`.
Resources 파일명만 달라졌으며 폰트의 내부 이름과 바이너리는 변경하지 않았다.
NHN Corporation의 SIL OFL 1.1 원문은 패키지 `ThirdParty/NanumGothic-OFL.txt`, 출처는
`ThirdParty/mark-font.json`, 고지는 `NOTICE.md`에 포함한다.


## Native IL2CPP 실행

macOS ARM64/Metal과 BlueStacks Android 13 ARM64/OpenGLES3에서 위와 같은 **8개 텍스트 상태**를
각각 통과했다. 각 플랫폼 PNG는 동일 플랫폼의 빈 문자열 PNG와 비교했으며, 5개 텍스트 사례의
변경 영역과 변경 pixel 수가 WebGL과 일치한다. 이것은 모델 그림 전체의 pixel parity 주장이 아니다.
두 플랫폼의 한국어 PNG를 직접 확인했다. text 소유 texture/coverage RT/mesh/material은 모두 해제됐다.
기존 PlayerSurface/Snapshot/Poster 7개 상태와 cropped eye mask 8개 상태도 함께 통과했다.

- macOS: `unity/TestResults/macos-presentation/1794209012166567000/evidence.json`.
- BlueStacks: `unity/TestResults/bluestacks/presentation/1794209077179387000/evidence.json`.
- 각 폴더: `text-*.png`, `text-mark-pixels.json`, `presentation-player.json`.
- 실행 증거에는 관련 소스/폰트/셰이더 **24개 SHA-256**과 실행 바이너리 hash가 있다.
  macOS는 launcher 외에 GameAssembly/metadata/resources/sharedassets도 기록한다.
- 최종 macOS/Android Unity build errors/warnings 0. Android APK **63,361,452 bytes**,
  SHA-256 `916633529d5d353dd75da3fa23d7e3913962a4084fd4083cb78d41b7e72432ee`.
  ARM64 라이브러리 9개와 16KB ELF/ZIP 정렬 감사 통과.

`verify-native-presentation.py macos|android`와 `check-text-mark-pixels.py --directory ABS_OUTPUT --prefix text-`
로 재현한다. 검증은 카메라·마이크를 활성화하지 않으며 BlueStacks는 종료 후 일반 샘플로 복귀한다.
Native 빌드의 `--output-path`에는 폴더가 아닌 `.app`/`.apk`까지 지정한다. 최초 경로 착오로 실행된
이전 macOS 산출물의 증거 `1794208964740017000/evidence.json`은 InvalidEvidence로 표시했다.
Android 최초 폴더 경로의 build 실패 로그도 보존한다. 최종 증거는 위 경로 수정 후 새 빌드 실행 결과다.

물리 Android/iOS의 실행, Windows/Linux/macOS x64, Safari/mobile browser는 아직 검증하지 않았다.


iOS Unity export errors/warnings 0, Xcode `CODE_SIGNING_ALLOWED=NO` **BUILD SUCCEEDED**.
unsigned iPhoneOS app **173,580,152 bytes**의 ARM64/MediaPipe embedding/WebP 심볼과
Resources에 포함된 원본 모델·기본 폰트의 정확한 바이트를 확인했다.
증거: `text-mark-ios-build.log`, `text-mark-ios-xcode.log`, `ios-app.json`.
Xcode의 Unity 생성 코드 deprecated API 및 app icon 경고는 남으며 iPhone 실행 결과는 아니다.

최종 WebGL 재빌드 후 같은 8개 상태를 다시 통과하고 10개 source/font hash를 재기록했다.
검증 종료 시 샘플 Ready, scale=1, 카메라/마이크 Off, viewport와 document 모두 888×990으로
페이지 overflow가 없음을 확인했다. UGUI의 기존 스크롤 없는 레이아웃을 유지한다.
