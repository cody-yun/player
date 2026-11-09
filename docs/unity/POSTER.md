# 로딩 포스터 — 원본 생명주기와 UGUI 표시

`src/base.ts`의 `LoadOptions.poster` / `start` / `setPoster` / `draw` / `stop`을 대응시켰다.
공통 `PlayerBase`는 URL 상태와 변경 알림만 제공하고, 별도 UPM `ai.livliv.ugui`의
`PosterPresenter`가 이미지 다운로드·UGUI 표시를 담당한다. 원본 파일은 변경하지 않았다.

## 사용

```csharp
// Place this RawImage behind the character RawImage, under the same display-bounds RectTransform.
// Set the poster RawImage disabled initially if there is no existing background image.
var presenter = posterRect.gameObject.AddComponent<LivLiv.Unity.UI.PosterPresenter>();
presenter.Bind(player, new Uri("https://example.com/characters/"));
await player.LoadAsync("https://example.com/characters/aoi.livliv",
    new LivLiv.LoadOptions { poster = "cover.webp" });
```

`RawImage`와 `AspectRatioFitter`는 RequireComponent로 추가한다. 표준 UGUI material을 사용한다.
이미지를 중앙에 비율 유지/contain으로 맞추며 반복하지 않는다. 렌더 타깃·모델 프레임·snapshot에는
합성하지 않는다. 원래 RawImage texture/enabled와 aspect/layout 값을 저장했다가 포스터가 사라지면 복원한다.
외부 소유 texture와 Player를 해제하지 않는다. component Disable 시 요청을 취소하고 원래 상태를 복원하며,
Enable 시 현재 `Player.Poster`에 다시 연결한다. `Unbind()`/Destroy는 구독과 자신이 만든 texture를 해제한다.

샘플의 `VerificationHud.LoadingPoster`는 기존 Character RawImage 뒤에 배치된다.
UI 크기·조작 버튼·스크롤 정책은 그대로이고 포스터용 컨트롤은 추가하지 않았다.
기본 샘플은 내장 모델을 바로 로드하므로 별도 포스터가 나타나지 않는다.

## 원본과 같은 상태 전환

- 빈 player에만 표시한다. 모델이 이미 있으면 교체 중에도 그 모델을 유지하며 새 포스터를 다운로드하지 않는다.
- 새 로드는 이전 URL을 교체한다. poster가 없는 새 로드는 포스터를 지운다.
- 이미 취소된 token은 현재 로드/포스터를 건드리지 않는다.
- 모델 설치 성공 시점과 첫 프레임 시점을 구분한다. `Draw`의 `Show`가 성공한 뒤 포스터를 지운다.
  paused player의 Load는 원래부터 Resize하므로 첫 프레임이 바로 출력된다.
- 현재 로드 실패/취소는 포스터를 지우지만 이전 로드의 늦은 실패는 새 로드의 포스터를 지우지 않는다.
- Dispose는 상태·구독·다운로드·소유 이미지 리소스를 정리한다.
- Unity 편의 API인 동기 `Load(ModelPackage)`로 대체해도 이전 포스터를 지운다.

`Player.Poster`와 `PosterChanged`가 CSS 경계의 대응 API다. observer의 예외는 로드를 깨뜨리지 않으며
진단 경로로 보고한다. observer에서 재진입해 새 로드를 시작하면 이전 로드 작업을 시작하지 않고 취소한다.
재진입 후 오래된 URL 알림을 나머지 observer에 전달하지 않는다. 이는 C#의 공개 알림 경계에 필요한 보강이다.

## 이미지와 플랫폼 경계

기본 presenter는 모델과 같은 **PNG/WebP/JPEG** decoder를 재사용한다. HTTP(S)/file URL과 base64 PNG/WebP/JPEG
image data URL을 받는다. WebGL 상대 URL의 기본 기준은 `Application.absoluteURL`이며 native 상대 URL은
`Bind`의 base URI가 필요하다. `IModelTransport`를 주입할 수 있다. encoded 32 MiB, 요청 timeout 60초,
기존 decoder의 side/texel 한도를 적용한다. 브라우저 CORS와 각 OS의 네트워크 정책을 따른다.

texture를 디코딩한 뒤 표준 UGUI가 요구하는 straight alpha로 변환한다.
JPEG는 frame header에서 크기를 먼저 읽어 side/texel 예산을 검사한 뒤 Unity ImageConversion으로 디코딩한다.
baseline/progressive의 RGB JPEG를 검사했다. SVG/애니메이션 및 JPEG EXIF 방향·ICC/CMYK·특수 인코딩은
추가 대응/검증 범위이며 대응 source 상태는 partial을 유지한다.

2026-11-09 JPEG 추가 검증: .NET 헤더 3개, Unity EditMode 헤더/실제 디코딩 6개,
UGUI PlayMode 포스터 6개 통과. 합성 32×48 JPEG 두 종류에서 위쪽 빨강/아래쪽 파랑과 불투명 알파,
과대 크기 선행 거절, data URL의 contain 크기와 첫 프레임 후 texture 해제를 확인했다.
기존 PNG/WebP의 취소·복원·재시도 회귀도 포함한다.
증거: `unity/TestResults/jpeg/`의 TRX/XML과 CLI 로그.
fixture는 `scripts/unity/generate-jpeg-fixtures.py`로 만든 합성 이미지다.
macOS ARM64/Metal IL2CPP 앱에서도 JPEG data URL(baseline)과 file URL(progressive)을 확인했다.
32×48 크기·빨강/파랑 방향·불투명 알파·첫 프레임 후 해제를 검사하고 디코딩 PNG를 저장했다.
기존 Surface 7개/카메라 8개/마스크 8개/텍스트 8개와 PNG/WebP 포스터 회귀도 통과했다.
macOS 증거: `unity/TestResults/macos-presentation/1794217581057501000/`.
빌드는 errors/warnings 0, 14.85초이며 자세한 로그는 `unity/TestResults/jpeg/native/`에 있다.
BlueStacks ARM64/GLES3 IL2CPP에서도 같은 JPEG data/file, 색상/행 방향/알파·첫 프레임 해제 및
기존 표시 회귀가 통과했다. 종료 후 target/material/poster/mask/text coverage 조건도 통과했다.
증거: `unity/TestResults/bluestacks/presentation/1794217655031412000/`.
Android 빌드는 errors/warnings 0, 41.33초이며 APK 서명·ARM64/16KB·포함 자산 감사도 통과했다.
APK 53,483,134 bytes, SHA-256 `7e84af1f7a81f66faa88f9f88d2652a1ea26e1d62dd2f602267f0559950c4e70`.
실행 증거에는 JPEG fixture·decoder/presenter·probe 소스 및 실제 앱 해시를 보존했다.
WebGL도 errors/warnings 0으로 빌드(53,341,441 bytes, 19.29초)하고 Chrome 실제 WASM에서
baseline/progressive data URL의 크기/색상/행 방향/contain과 모델 첫 프레임 후 해제를 확인했다.
기존 포스터의 교체·취소·잘못된 이미지·기존 모델 유지까지 총 13개 상태 기록을 통과했다.
Ego 공간 20은 완료 후 닫았다. 증거는 `unity/TestResults/jpeg/webgl/`에 보존했다.
JPEG를 포함한 tarball과 새 core+UGUI 소비자 PlayMode/233파일 감사도 통과했다.
소비자 증거: `unity/TestResults/consumers/1794217775046760000/Evidence/`, CLI 로그는 `unity/TestResults/jpeg/`.
갱신 패키지의 native 포함 새 소비자도 PlayMode와 자체 258파일 감사를 통과했다.
증거: `unity/TestResults/consumers/1794217842727784000/Evidence/`, `unity/TestResults/jpeg/native-consumer-audit.json`.
core+UGUI 결과와 PNG 해시가 같고 재패키징 해시도 동일했다. [INSTALL.md](INSTALL.md).
Linux IL2CPP도 Docker/Mesa에서 같은 JPEG data/file·색상/행 방향·해제와 기존 표시 회귀를 통과했다.
증거: `unity/TestResults/linux-presentation/1794217978207208000/`.
Windows는 JPEG fixture/필수 DLL을 포함한 최신 빌드·파일 감사까지 통과했다. [DESKTOP.md](DESKTOP.md).
iOS unsigned export/link와 JPEG fixture 포함 바이트 검사도 통과했다. [NATIVE-BUILDS.md](NATIVE-BUILDS.md).
물리 Android 및 Windows/iOS의 JPEG 실행은 남아 있으며 iOS 실기기는 사용자의 요청으로 보류한다.

포스터 오류는 `LastError`와 `Failed`로 관찰할 수 있지만 모델 로드는 독립적으로 계속된다.
취소된 다운로드가 나중에 완료돼도 새 이미지로 교체하지 않는다. 긴 다운로드/최대 크기의 메모리·디코딩
지연과 모바일 실기기는 추가 검증 대상이다. 일반 Player MonoBehaviour와 자동 해상도/DPR 정책은 [PlayerSurface](SURFACE.md)에 연결했다.

## 검증 — 2026-11-09

`generate-poster.mjs`는 원본 `PlayerBase`를 직접 실행한다. CSS style만 double로 대체했으며
8개 시나리오의 표시 URL·background 복원·draw 수와 원본 SHA-256을 fixture에 저장한다.
첫 프레임, 실패, 교체, 포스터 없는 교체, 선취소, 기존 모델 유지, 취소/Dispose, 늦은 성공을 포함한다.

- .NET 25개 통과: 원본 8개, observer 예외/재진입, 기존 loading/Player parity 회귀.
- Unity EditMode **31개 통과**, 06:28:27Z–06:28:37Z, 9.977초. 같은 계약 + 실제 HTTP/GPU 모델 로드와 snapshot 회귀, 동기 Load로의 교체를 검사했다.
- Unity PlayMode **5개 통과**, 06:25:25Z: PNG/WebP의 contain 크기·alpha·배경 복원, 늦은 다운로드, Disable/Enable,
  포스터 오류 후 재시도, 기본 file transport와 Dispose texture 해제.
- 실제 Chrome 156/WebGL: 별도 loopback 서버가 모델 본문을 대기시킨 동안 포스터를 표시하고 스크린샷을
  확인했다. 첫 프레임, 기존 모델 교체 실패, 느린 이미지/모델의 교체, 취소, 잘못된 이미지 후 모델 성공,
  원래 샘플 복귀를 기록했다. 첫 프레임 이후 소유 포스터 texture 0, scale=1을 확인했다.
- fixture의 avatar.webp는 원본의 작은 thumbnail이므로 큰 창에서는 픽셀이 보인다. 자동 생성/고해상도 보정한 이미지가 아니다.

결과: `unity/TestResults/poster-editor.xml`, `poster-playmode.xml`, `webgl-poster.json`,
`webgl-poster-visible.png`; .NET `tools/Parity/TestResults/poster.trx`.
브라우저 결과에 코드 SHA-256을 저장한다. 카메라/마이크는 켜지 않았다.

```sh
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-poster.mjs --check
unity test ./unity --mode PlayMode --filter LivLiv.Tests.PosterPresenterTests --output ./unity/TestResults/poster-playmode.xml --timeout 240
python3 scripts/unity/serve-poster-tests.py
```

`verify-webgl-poster.mjs`는 절대경로 `root`와 허용된 `posterSpaceId`를 주입해서 ego-browser로 실행한다.
이번에는 기존 공간 9를 재사용했다. 서버는 127.0.0.1:8773에서 최신 빌드와 model/image gate를 제공한다.
최신 대상 suite만 실행했으며 전체 suite를 재실행한 결과로 보지 않는다.
macOS ARM64/Metal 및 BlueStacks ARM64/OpenGLES3 IL2CPP에서 PNG/WebP file/data 표시, 첫 프레임 해제,
오류·늦은 결과·소유 texture 정리를 통과했다. 제어 가능한 PlayerBase host를 사용한 검증으로 HTTP는 포함하지 않는다.
[NATIVE-PRESENTATION.md](NATIVE-PRESENTATION.md). 물리 Android/iOS 및 Safari/mobile browser 실행은 아직 검증하지 않았다.

포스터 검증 당시 WebGL CLI build는 errors/warnings 0, **52,315,491 bytes / 19.793627초**다. 이후 빌드는 [SURFACE.md](SURFACE.md)에 기록했다.
