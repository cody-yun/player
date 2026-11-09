# 구현 상태 — 2026-11-09

마감 범위와 보류 항목은 [HANDOFF.md](HANDOFF.md)에 정리했다. 최종 세로 입력 수명주기 시험은
Portrait 1080×1920의 10개 상태/3회 Home·복귀·재시작과 입력 Off 복구를 통과했다.
UGUI 실제 버튼 위치를 사용하며 고정 클릭 좌표를 제거했다. 최종 Android build/감사 통과,
APK 53,311,574 bytes. 전체 플랫폼/실기기 인증 완료를 뜻하지 않는다. 아래는 기능별 진행 이력이다.

방향 문제의 후속 진단에서 요청 4개가 Android Activity에 정확히 전달됐고 display rotation만
2개 일치함을 확인했다. 요청 누락이 아니라 Android/가상 디스플레이 경계의 미적용으로 범위를 좁혔다.
전환 후 안정화 대기와 Android 시스템 캡처를 추가한 실행에서 화면 깨짐·조작부 잘림은 없었다.
네 방향 전체는 여전히 Failed다. APK 53,311,102 bytes, 새 Android build/감사/일반 실행 복구 통과.
증거: `android-orientation/1794235569502136000/`와 [ORIENTATION.md](ORIENTATION.md). 라이브러리는 동일하다.

후속 Android UGUI 진단을 추가했다. Portrait 1080×1920/ LandscapeLeft 1920×1080에서 버튼 6개,
잘림·겹침·스크롤 0과 PNG를 확인했다. PortraitUpsideDown/LandscapeRight는 각각 반대 기본 방향으로
남아 네 방향 전체 검사는 Failed다. 원인은 미확정이며 센서/실기기 회전 완료로 취급하지 않는다.
Android 새 CLI build errors/warnings 0, APK 53,307,622 bytes 감사·일반 실행 Ready/12초 복구 통과.
이는 샘플 QA 변경이다. 배포 패키지는 아래 package-source 단계 그대로다. [ORIENTATION.md](ORIENTATION.md).

최신 갱신은 파일 목록의 ZIP 선택 경로다. 원본 private `fromFiles`를 직접 실행한 22개 사례와
취소 2개에 맞춰 직접 model.json 우선, 첫 ZIP 우선, 1회 해제, GLB/VRM 거절을 보완했다.
기존 `FromFiles(files, lang)` 서명을 유지했다. [LOADING.md](LOADING.md).
전체 .NET 1,317/EditMode 1,416/PlayMode 30, 25모듈/24fixture 감사 issues 0,
동일 105개 소스의 7개 CLI 빌드 errors/warnings 0. 실제 Aoi의 직접 ZIP/파일 목록 PNG 일치와
실패·취소 시 이전 모델 유지도 PlayMode에서 통과했다. 두 새 tarball 소비자(243/268 파일)도 통과했다.
최신 APK 53,297,358 bytes는 BlueStacks 기본 MAIN/LAUNCHER Ready/12초 생존을 통과했다.
iOS 새 export의 unsigned Xcode link/ARM64·포함 자산 감사도 통과했다(error 0, warning 11줄).
이번 파일 목록 경로의 GPU 실행 증거는 Unity/Metal 소비자까지이며 다른 플랫폼 실행까지 확대 해석하지 않는다.
통합 증거: `unity/TestResults/package-source/evidence.json`. 아래 수치는 이전 기능별 실행 이력이다.

원본 release 모듈의 지연 반환/즉시 claim 취소/terminal 오류를 이식했다. Unity의 명시적
`Player.Dispose(new PlayerDisposeOptions { release = true })`는 대상 GPU 저장소만 반환하고 공유 엔진
컨텍스트를 유지한다. 같은 대상의 기존 소유자에게 유실을 한 번 알리며 retarget된 소유자는 분리한다.
전체 .NET 1,293/EditMode 1,392/PlayMode 29와 원본 작업 순서 10개를 통과했다.
25모듈/23fixture 감사 issues 0, 7개 대상 CLI build errors/warnings 0. 세부 ownership은 [RELEASE.md](RELEASE.md).
이번 API는 Chrome WASM/macOS ARM64/BlueStacks GLES3/Linux Docker에서 10개 상태 조건과
별도 대상의 실제 PNG alpha 1,862 pixels 및 메인 프레임 진행을 확인했다. 두 새 설치 소비자도 통과했다.
APK는 53,296,434 bytes이며 일반 실행 Ready/12초 생존도 통과했다. iOS unsigned link/감사 통과,
Windows는 빌드/파일 검사까지이며 실행 환경 없음, iOS 실기기는 요청대로 보류한다.
최신 통합 증거: `unity/TestResults/release-port/evidence.json`.

단계별 계측을 추가한 후속 10분 실행도 입력 유지·해제와 프레임 대응 검사를 통과했다.
이번 최악 간격은 44.03 ms였고 50 ms 이상 기록은 없었다. 따라서 이전 약 1초 지연의 원인은 미확정이다.
이번 실행의 독립 최대 Update/Tick/얼굴 입력/계측 저장은 18.45/18.25/12.58/2.82 ms였다.
서로 다른 프레임의 최대값을 더하거나 빼서 원인을 설명하지 않는다. [CAPTURE-SOAK.md](CAPTURE-SOAK.md).
QA 계측만 추가했으며 제품 최적화로 성능이 개선됐다고 판단하지 않는다.

생성 얼굴 CPU 추론·PCM 립싱크·렌더러 10분 동시 실행은 600.0046초/55,750 Update와 입력 해제 후
camera/clip/Texture2D 기준값 복귀를 통과했다. 배율은 1, 활성 객체 수는 일정했다.
평균 Update 간격 10.76 ms, 구간별 p95 17.91~18.79 ms지만 최악 1,013.94 ms 지연이 있어 성능 합격으로
판정하지 않는다. RSS는 초반보다 감소했고 마지막 5분은 331.27~336.95 MiB였다. 단일 모델·생성 입력 범위다.
Mac QA 앱의 새 CLI 진단이며 라이브러리 패키지는 변경하지 않았다. [CAPTURE-SOAK.md](CAPTURE-SOAK.md).

이전 통합 기준은 로고·소유자 텍스트의 화면 경계 그림자 수정을 모두 포함한다.
전체 회귀 .NET 1,283 · Unity EditMode 1,382 · PlayMode 23 passed, failed/skipped 0.
WebGL/macOS ARM64/macOS x64/Android/Windows/Linux/iOS 7개 대상의 Unity CLI build는 모두 errors/warnings 0.
빌드 중 C#/shader/jslib 100개 소스 해시가 동일함을 검사했다. iOS는 별도 export의 unsigned Xcode link도
성공했다(error 0, warning 11줄, 앱 174,407,432 bytes). 물리 iOS 실행은 요청대로 보류한다.
macOS 두 아키텍처·BlueStacks·Linux Docker/Mesa에서 표시/카메라 surface/마스크/텍스트/포스터 회귀,
Chrome 실제 WASM에서 텍스트 8개 상태·입력 종료/재시작 6개 상태와 Ready/입력 Off/scale=1/스크롤 없음 복구를 확인했다.
macOS x64는 Rosetta이며 물리 Intel 검증이 아니다. Windows는 파일/전달 ZIP 감사까지다.
새 APK는 53,269,950 bytes, 서명/ARM64/16KB/자산/미참조 ZIP 영역 0과 BlueStacks 기본 실행 12초 생존 통과.
전체 근거: `unity/TestResults/mark-boundary-final/evidence.json`,
7개 빌드 기록: `unity/TestResults/build-refresh/1794227437302436000/evidence.json`.
Windows 실행, 물리 모바일/Safari, 장시간 메모리·성능 등 [PLATFORMS.md](PLATFORMS.md)의 잔여 게이트는 유지한다.

이하 항목별 수치와 이전 빌드 경계는 당시의 진행 기록이다. 문서 맨 위 release 모듈 갱신이 최신 상태다.

소유자 텍스트의 화면 경계 그림자도 수정했다. 기본 provider가 제한된 halo 안의 화면 밖 glyph를
보존하고 최종 합성에서 자른다. 수정 전 Unity GPU 네 경계 실패 → 수정 후 관련 154개 및
.NET 전체 1,283개 통과. 기존 provider와 인터페이스는 유지한다. [TEXT-MARK.md](TEXT-MARK.md).
아래 앱들은 이 텍스트 후속 이전의 로고 수정까지 반영한 빌드이며, 텍스트 후속의 player 실행 증거는 아니다.

로고 가장자리의 그림자 누락을 수정했다. 화면 밖 도형도 blur에 포함한 뒤 최종 합성에서 자른다.
수정 전 9개 실패 재현, 수정 후 .NET 전체 1,278개와 Unity 관련 145개 통과, upstream 감사 issues 0.
새 core+UGUI tarball 소비자 PlayMode/설치 파일 감사도 통과했다. macOS ARM64/Android ARM64 앱도
수정 후 빌드와 생성 입력 10개 상태를 통과했다. 새 APK 53,269,418 bytes, 서명/정렬/자산/미참조 영역 0 감사 통과.
BlueStacks 기본 아이콘 실행도 Ready/12초 생존·GLES3 및 스크롤 없는 전체 UGUI 화면을 확인했다.
CLI sample builder는 잘못된 active target에서 실행하면 Android 전용 callback이 빠지지 않도록 시작 전 거절한다.
아래 전체 Unity 회귀와 다른 플랫폼 앱은 이 작은 로고 경계 수정 이전의 증거다.
[TEXT-MARK.md](TEXT-MARK.md), `unity/TestResults/brand-clip/`.

표정 getter 반영 직후 전체 회귀: .NET 1,269 · Unity EditMode 1,364 · PlayMode 23 passed, failed/skipped 0.
Unity는 macOS ARM64/Metal 실행이며 다른 플랫폼 완료 선언은 아니다.
XML/TRX 및 현재 런타임 C#/jslib/shader 71개 파일 해시는 `unity/TestResults/upstream-latest/current-full/evidence.json`.

전체 목표는 PC(Windows/macOS/Linux), Mobile(Android/iOS), WebGL이다. [플랫폼 계약](PLATFORMS.md)의 각 검증 행렬이 완료 기준이다.

실제 upstream main `d999dea`(표정 label 타입 보존)를 fetch·검토하고 로컬 main/포팅 기준에 반영했다.
Unity Player.Expressions는 label/labels를 보존하는 ModelExpression[]를 반환한다. 원본 build/type 검사 3개 설정,
.NET 전체 1,269개, Unity 영향 범위 41개, 감사 도구 12개와 새 UPM 소비자 1개/239파일 감사를 통과했다.
영향받은 fixture 3개의 동작 결과는 같고 sourceHashes만 갱신했다. 최신 작업 파일/ref 감사 issues 0.
getter 추가 후 Android/macOS x64 IL2CPP 앱을 갱신했고 각각 10개 상태에서 표정 개수·이름·번역의 직접 읽기,
source 오류 격리/재시작/해제·scale=1을 통과했다. macOS x64는 ARM64 호스트의 Rosetta 실행이다.
Android 일반 실행 및 APK 감사도 통과했다. Windows/Linux/macOS ARM64도 최신 기준으로 다시 빌드했다(errors/warnings 0).
Linux Docker/Mesa·macOS ARM64 Metal은 표정 정보 읽기/입력 수명주기 10개 상태를 통과했고 Windows는 파일 감사까지다.
iOS도 별도 경로로 최신 export/unsigned Xcode link/ARM64·심볼·자산·metadata 감사를 통과했다.
Unity errors/warnings 0, Xcode error 0/warning 11줄, unsigned app 174,348,756 bytes.
iOS 실기기 실행 보류는 유지한다. `unity/TestResults/upstream-latest/{desktop,ios}/`.
WebGL build/link도 errors/warnings 0으로 갱신했고 Chrome 실제 WASM 생성 입력의 종료/재시작 6개 상태에서
표정 개수 4·대표 smile 이름/번역·scale=1을 확인했다. reload 후 입력 Off/스크롤 없음으로 복구했다.
현재 Windows 환경은 없다는 사용자 응답을 받았다. 실제 Windows 실행은 미검증으로 유지하고
무결성/3개 probe 실행/결과 수집용 전달 ZIP을 준비했다(계약 검사 13개, 전달 파일 282개 확인; 실행 증거 아님).
Ego 검증 공간은 이 목표에서 필요할 때 자동 생성하도록 사용자가 허용했다. 최신 공간 25는 검사 후 닫았다.
BodyPlayer는 여전히 미구현이다. `unity/TestResults/upstream-latest/`, [UPSTREAM.md](UPSTREAM.md).

macOS ARM64/Metal의 실제 카메라·마이크 개별 종료/재시작, 다른 입력 보존과 명시적 컨텍스트 유실 정리를 확인했다.
실제 입력 30회 반복에서도 캡처 객체는 종료 후 0으로 복귀했다. RSS 증가는 생성 영상 추론 반복에서도
관측되어 원인/장시간 상한은 미해결이다. vmmap 두 시점의 실제 malloc 할당/footprint는 증가하지 않아
RSS만으로 누수라고 단정하지 않는다. [CAPTURE-SOAK.md](CAPTURE-SOAK.md), [CAPTURE-LIFECYCLE.md](CAPTURE-LIFECYCLE.md).

Chrome 156 실제 WASM에서 생성 영상/음성 입력의 빠른 시작·종료 30회(25.50초, 61개 상태)를 확인했다.
활성 시 얼굴 검출/립싱크, 매 종료 후 모든 트랙 ended/캡처 AudioContext closed와 scale=1/스크롤 없음 통과.
최종 생성기도 닫고 reload Ready/입력 Off 복구 후 Ego 공간 26을 닫았다. JS heap만으로 WASM/GPU 메모리
안정성이나 장시간 누수 없음은 판단하지 않는다. [CAPTURE-SOAK.md](CAPTURE-SOAK.md).

입력 상태 API/샘플 정책을 macOS ARM64·Linux x64 IL2CPP와 Windows x64 Mono 빌드에도 갱신했다.
세 빌드 errors/warnings 0. macOS Metal 및 Linux Docker/Mesa에서 생성 source 오류→입력만 종료→새 source
재검출을 포함한 10개 상태가 통과했고, 두 source 각각 한 번 해제·모델/렌더 유지·scale=1을 확인했다.
Windows는 파일 감사까지, 실제 실행/물리 장치는 미검증이다. `unity/TestResults/capture-status-desktop/`.

네이티브 장치 중단 시 선택적 ICaptureStatus를 통해 해당 입력만 정리하고 렌더러를 유지한다.
Unity 17개와 Android CLI build를 통과했고 BlueStacks의 실제 캡처를 Unity Stop/End로 중단해
개별 종료·다른 입력 보존·재시작·프레임 진행·최종 카메라/클립 0 복귀와 AppOps 종료를 확인했다.
일반 실행 및 APK 감사도 통과했다. 실제 장치 분리/OS 장치 교체는 미검증이다.
장치 상태 추가 시점 tarball도 새 native 포함 소비자에서 PlayMode 1개·자체 파일 264개 감사를 통과했다.
증거: `unity/TestResults/native-capture-end/`, [CAPTURE-LIFECYCLE.md](CAPTURE-LIFECYCLE.md).

WebGL 카메라/마이크 track ended 처리와 샘플의 개별 입력 실패/재시작 안내를 추가했다.
브라우저 이벤트에서 즉시 자원을 정리하고, 종료된 권한 stream과 늦은 video.play 완료를 거절한다.
수정 전 네 실패를 재현했고 수정 후 관련 Node 29개 및 WebGL build(errors/warnings 0)를 통과했다.
Chrome 실제 WASM의 생성 입력에서 ended 이벤트를 주입해 마이크/카메라 각각 종료·다른 입력 보존·재시작과
전체 종료의 6개 상태를 통과했다. 고정 URL의 캐시된 얼굴 모듈이 실행되는 실패도 재현했고 기본 moduleUrl에
Unity build GUID를 붙여 해결했다. 최종 reload 후 Ready/입력 Off/scale=1/스크롤 없음 확인. 물리 장치 분리는 미검증이다.
캐시 수정을 포함한 최신 core+UGUI tarball의 새 소비자 PlayMode 1개/239파일 감사도 통과했다.
[CAPTURE-LIFECYCLE.md](CAPTURE-LIFECYCLE.md).

샘플의 컨텍스트 유실 콜백에 카메라/마이크 Stop·시작 요청 취소·Player Dispose를 연결했다.
Chrome의 실제 컨텍스트 유실과 생성 입력으로 활성 추적/립싱크 종료 및 늦은 권한 스트림 즉시 종료를 확인했다.
유실 뒤 재시작은 차단하고 reload 후 Ready/입력 Off로 복구했다. 물리 입력은 사용하지 않았다.
Android APK도 갱신해 BlueStacks의 실제 카메라/마이크 활성 상태에서 명시적 유실 알림을 보냈다.
두 입력 종료·Player 해제·알림 한 번·재시작 차단·카메라/클립 객체 0 복귀와 AppOps 사용 종료를 확인했다.
새 APK의 일반 실행·서명/정렬 감사도 통과했다. iOS unsigned 빌드도 갱신했으며 실제 GPU 장애 검증은 남아 있다.
증거: `unity/TestResults/bluestacks/capture-loss/1794221064882548000/`,
`android-startup/1794221046722379000/`, `android-capture-loss-build.log`. [LOADING.md](LOADING.md).

직전 공통 코드 전체 회귀: .NET **1,269**, Unity EditMode **1,359**, PlayMode **23** passed,
각각 failed/skipped 0. macOS ARM64/Metal의 검증이며 다른 OS·실제 장치 지원 선언은 아니다.
XML/TRX·CLI 로그·결과/런타임 소스 SHA-256은 `unity/TestResults/current-full/`에 기록했다.
아래 기능별 검사와 과거 전체 수치는 각 당시 소스의 이력으로 보존한다.

사용자가 BlueStacks 일반 실행 즉시 종료를 보고했다. SIGSEGV는 vulkan.default.so의
vk_common_SetDebugUtilsObjectNameEXT → Unity Vulkan 그래픽 스레드 경로였다.
기존 검증은 -force-gles30으로 실행해 APK 기본 Vulkan 선택 문제를 놓쳤다.
Android 샘플 빌드의 기본 API를 OpenGLES3로 명시하고, Unity 인수 없는 MAIN/LAUNCHER 실행 검사를 추가했다.
새 APK의 Ready·실제 캐릭터/UGUI 표시·12초 생존을 확인했다.
증거: `unity/TestResults/android-startup-fix/`, `android-startup/1794220207354946000/`.
Android probe 스크립트 8개에서도 -force-gles30을 제거했다. 새 APK의 기본 설정으로 표시 회귀를 다시 통과했다.

컨텍스트 유실의 공통 NotifyContextLost/OnContextLost 상태·로드 취소 처리를 추가했다.
WebGL canvas 이벤트 자동 연결과 직접 AOT 콜백도 추가했다. JS 등록/해제 검사 4개와 WASM 빌드를 통과했지만
Chrome WASM에서도 실제 Unity canvas의 WEBGL_lose_context로 유실시켜 C# 콜백 전달을 확인했다.
살아 있는 Player 2개에 각각 한 번, 해제한 Player에는 0번 알렸고 마지막 리스너 해제·페이지 재로드도 확인했다.
대기 HTTP 모델 로드와 유실을 겹친 검사에서 프레임 정지 후 loading에 남는 문제를 재현·수정했다.
Chrome에서 fetch AbortSignal, context-lost와 await 완료, 정상 HTTP 로드 9개 기록을 확인했다.
.NET 18개/Unity 23개 회귀도 통과했다. 실제 하드웨어 리셋/모바일 OS 회수와 입력 동시 부하는 남아 있다.
구현 범위는 [LOADING.md](LOADING.md)를 따른다. 최신 tarball의 두 새 소비자 검사도 통과했다.
core+UGUI/native 포함 각각 237/262파일 대조, 유실 후 동기·비동기 로드 거절과 해제를 확인했다.
현재 수정의 macOS/BlueStacks IL2CPP 빌드와 IsSupported/유실 후 로드 거절/표시 회귀도 통과했다.
native 유실 검사는 명시적 알림 호출이며 실제 드라이버 리셋과 구분한다.
Windows/Linux 빌드·파일 감사와 Linux Docker/Mesa의 지원/유실 알림/로드 거절·표시 회귀도 통과했다.
iOS의 해당 변경 unsigned export/link/파일 검사도 통과했다. Windows 실제 실행·물리 장치/드라이버 리셋은 아직 미검증이다.

원본 `Player.isSupported()`에 대응하는 Unity `Player.IsSupported()`를 추가했다.
메인 스레드에서 그래픽 장치·LivLiv layer shader·임시 24-bit 이상 depth 타깃 생성 여부를 검사한다.
Metal PlayMode에서 10회 반복 호출 후 probe 해제와 호출자 타깃 보존을 확인했다.
증거는 `unity/TestResults/player-support/`다. 실제 `unity run -nographics`에서도 Null device를 확인했고
10회 모두 false, 생성 probe 0개였다. 다른 그래픽 환경과 player 빌드 검증은 남아 있다.
이 API를 포함한 tarball의 새 core+UGUI/native 포함 소비자도 IsSupported → 로드 → 렌더 → 해제와
각 233/258개 파일 감사를 통과했다. 두 PNG와 재패키징 해시가 일치했다. [INSTALL.md](INSTALL.md).

JPEG 포스터/텍스처 디코딩을 추가했다. 디코딩 전 크기 예산을 검사하며 baseline/progressive RGB의
색상·행 방향·알파, UGUI contain/첫 프레임 해제와 기존 회귀를 확인했다.
.NET 3개, Unity EditMode 6개, PlayMode 6개 통과. macOS/BlueStacks IL2CPP에서 JPEG data/file 표시와
첫 프레임 해제 및 기존 표시 회귀도 통과했다. Chrome WASM 포스터 13개 상태와 갱신한 tarball의
새 core+UGUI 및 native 포함 소비자의 PlayMode/설치 감사도 각각 233/258파일로 통과했다.
두 PNG와 재패키징 해시가 일치했다. Linux Docker/Mesa의 JPEG/표시 실행과 Windows 최신 빌드·파일 감사도 통과했다.
Windows/iOS JPEG 실행 및 물리 입력·장기 성능 검증은 남아 있다.
EXIF 방향·ICC/CMYK·특수 인코딩과 SVG/애니메이션의 범위는 [POSTER.md](POSTER.md)를 따른다.

21개 모델의 3회 순회 검증을 macOS/BlueStacks에서 완료했다. 각각 63회 교체·해제,
126회 로드·1,890개 animation Tick 후 신규 LivLiv 렌더 객체 잔존은 매회 0개였다.
최초 마지막 순회 Tick 평균 23.89/25.43 ms에서 정점 변형 병목을 발견했다.
프레임 단위 설정 읽기로 최적화한 macOS의 Tick은 1.47 ms, 별도 정점 변형 평균은
22.38 → 0.93 ms다. .NET/Unity 수치 23개씩·그래픽 60개·고정 시각 21개 PNG 완전 일치를 확인했다.
BlueStacks의 후속 63회 검사도 잔존 객체 0으로 통과했고 Tick 평균은 25.43 → 3.94 ms다.
물리 모바일 성능이나 60 fps 보장은 아니다. 재배포 tarball과 두 새 소비자 검사를 통과했다.
최적화/UGUI 카메라 해상도 수정을 포함한 WebGL CLI 빌드도 errors/warnings 0으로 통과했다.
이 빌드의 Chrome Surface/음성/눈 마스크 재검증과 스크롤 없는 최종 화면 확인도 통과했다.
Windows/Linux 산출물 갱신도 errors/warnings 0으로 완료했다. Linux Docker/Mesa 얼굴·표시·카메라 상태 재검증도 통과했다.
Windows 실행과 다른 브라우저·실기기 검증은 남아 있다. [DESKTOP.md](DESKTOP.md).
측정 경계와 기록은 [PERFORMANCE.md](PERFORMANCE.md), 최초 반복 결과는 [MODEL-SOAK.md](MODEL-SOAK.md)를 따른다.

iOS export/unsigned link도 최신 컨텍스트 유실/취소·IsSupported·JPEG를 포함해 별도 출력 경로에 갱신했다.
Unity errors/warnings 0, Xcode errors 0 / warnings 11줄, 앱 174,281,364 bytes.
ARM64·최소 OS·WebP/MediaPipe 심볼·JPEG 등 입력 자산·IL2CPP metadata 검사를 통과했다.
연결된 iPhone 13 mini는 발견했지만 서명 인증서 0개와 DDI services 불가로 실제 실행은 미검증이다.
사용자가 iOS 실기기 검증을 나중에 진행하도록 지정했다. 해당 검증은 보류하며 다른 플랫폼 작업을 계속한다. [네이티브 빌드](NATIVE-BUILDS.md).

새 소비자 프로젝트 설치 검증: 로컬 UPM tarball 3종과 고정 homuler 의존성을 묶었다.
저장소 Verification Assets/Library 없이 native 포함 및 core+UGUI만 설치한 두 새 프로젝트에서
Unity CLI PlayMode 로드·320×480 렌더·해제와 설치 캐시 원본 대조가 통과했다.
배포 파일에 캐릭터/테스트 fixture는 넣지 않았다. [설치 안내](INSTALL.md).

최근 UGUI 수정: 원근 Canvas의 반대쪽 가까운 변을 누락해 해상도가 부족해지는 문제를 재현·수정했다.
Unity PlayMode 14개, macOS/BlueStacks IL2CPP의 카메라 8개 상태와 기존 표시 회귀가 통과했다.
원본 크기 계산/샘플 scale=1은 유지한다. 픽셀 비교의 작은 Android 텍스트 차이와 범위는 [SURFACE.md](SURFACE.md)에 기록했다.

최근 macOS 검증: WebP를 ARM64/x64 universal로 전환하고 샘플 최소 OS를 MediaPipe 요구사항인 13.0으로 맞췄다.
x64 IL2CPP/Rosetta·Metal 및 재빌드한 ARM64의 생성 영상 추론·표시·음성 probe, ARM64 Editor 그래픽 62개가 통과했다.
이전 ARM64 표시 PNG 21장과 새 ARM64/x64 각각 21장의 픽셀이 모두 일치했다.
소스/바이너리 재현과 물리 Intel 검증의 구분은 [MACOS.md](MACOS.md)에 있다.

최근 플랫폼 검증: Windows x64 Mono build/포함 파일 검사, Linux x64 IL2CPP build 및
Docker/Mesa에서 생성 영상 얼굴 추론·Surface/Snapshot/Poster/눈 마스크/텍스트 표시 실행을 통과했다.
물리 PC 입력·Windows 실행은 남아 있다. [DESKTOP.md](DESKTOP.md).

원본 갱신 검증: 원본 변경의 직접/간접 영향·C#/어댑터·fixture 참조 보고서와 알려진 WAV 수정 이력의
실패 감지 리허설을 구현했다. 기준 커밋은 유지했다. [UPSTREAM.md](UPSTREAM.md).

최신 추가: SpeechPlayer의 `onState`와 Unity factory 알림을 연결했다. .NET/Unity CLI 81개,
PlayMode 7개, JS 16개 통과. macOS/BlueStacks IL2CPP 및 Chrome 실제 WASM에서 상태 알림·재생 회귀를 확인했다.
Dispose 알림의 동기화 차이와 검증 범위는 [SPEECH-STATE.md](SPEECH-STATE.md)에 있다.

전체 포팅은 진행 중이다. **P0(조사·추적 구조) / P1(수치 코어) 완료**, **P2(입력·정적 렌더러) 진행 중**.
**P3의 update/deform·physics·keyform과 동적 GPU 합성까지 구현**했다.
view 배치·Idle·공개 Player host·얼굴/음성 driver와 native 마이크 입력 어댑터까지 연결했다.
WebGL 음성 bridge와 IL2CPP build/browser 실행까지 확인했다. WebGL MediaPipe GPU 얼굴 추론도 연결하고 생성 영상으로 검증했다. native CPU 추론과 WebCamTexture 어댑터도 추가하고 macOS ARM64 Editor 사진 추론을 확인했다. 비동기 캐릭터 로드를 구현하고 HTTP/취소/교체를 검증했다. 실제 장치/플랫폼별 검증은 남아 있다.

샘플 요구사항: 얼굴 거리에 따른 확대·축소를 제거했다. on/off 옵션 없이 크기를 고정하고 위치·고개·표정·립싱크를 유지한다.
공통 `PortVerification.SamplePresentation`이 적용하며, 원본 tracking 계산의 1:1 매핑은 유지한다.

샘플 UI는 UGUI Canvas/RawImage/Button으로 전환했다. 스크롤 없이 한국어 안내, 반응형 패널, safe area, 시작/중지/취소·오류 상태와 키보드 포커스를 제공한다. [UI.md](UI.md).

고개 미리보기의 얼굴 왜곡을 원본 Aoi 동일 자세와 비교했다. 샘플 목표 8/-3, 부드러운 전환과 좌우·상하 조합 제한을 적용했다. [POSE-SAFETY.md](POSE-SAFETY.md).

## 구현된 것

- Unity 6000.3.14f1 CLI 프로젝트와 embedded UPM `ai.livliv.player`.
- 원본 25개 모듈의 source → target map, 기준 커밋과 SHA-256, pending/partial/ported 상태.
- Params/OneEuro/actions/vowel 및 voice/tracking/trace의 수치 계산 코어.
- `format.ts`: v3/v4 lowering, label 선택, rig/anchor/layer 검증, resource/mesh 예산, 재호출 identity.
- `zip.ts`/`errors.ts`: stored/deflate, 안전한 경로/중복/ZIP64/암호화/크기 제한, 오류 code/status/cause.
- `ModelPackage`: folder/ZIP/picked files, wrapper 폴더와 basename fallback, keyforms warning/fallback, 엄격한 JSON 문법.
- `TextureDecoder`: PNG + libwebp 1.6.0 macOS ARM64/x64 universal WebP, 디코딩 전 크기 제한, Y 방향/알파 premultiply/mipmap.
- `PuppetRenderer`: neutral 및 animated mesh 업로드, 입/표정/brow draw order, premultiplied alpha, 눈 stencil/soft mask, load rollback, dispose.
- WebP 네이티브 플러그인의 라이선스·출처·SHA-256과 같은 bottle에서 재현하는 스크립트.
- `brand.ts`의 로고 경로·배치·눈 깜박임·Canvas 명령과 `mark.ts`의 선택/cache 계약.
- 공용 CPU 로고 rasterizer + Unity texture 합성. 중앙/모서리 logo, DPR, blink, dispose. Canvas AA/그림자는 근사.
- `puppet.ts`의 `buildLayer` 공용 격자/UV/삼각형, head/hair/jaw/neck/brow 가중치, `skinOf`, rig/tuning/frame-jaw 초기화.
- `alphaGrid` 임계값·3×3 확장과 Canvas 이미지 축소 어댑터. 정적 렌더러의 quad를 이 격자로 교체.
- PNG의 ARGB32/RGBA32 채널 정규화, .NET/Mono 중간 Float32 저장 경계 및 Q15 보간 반올림 대응.
- `headTrig/headXform/bodyOffset/stepBody`, 입 hysteresis/hold/vowel/talk latch, front/back/side spring, Verlet chain와 skin offset.
- `update/deform`: 각도 제한·1/120초 물리 substep, 눈/시선/턱/눈썹/목/rigid/머리카락 변형, keyform 가중치와 Hermite 보간.
- `shown/exprAlpha/moodAlpha`, 물리·입 초기화. 원본 함수의 경계를 유지하며 GPU와 별도로 테스트.
- 실제 이미지의 앞머리 겹침 가중치, closed lash curve, nasal room과 Unity sampling 어댑터.
- keyforms schema/base64 검사, grid/mask 보간, rig의 key pose를 뺀 정점 오프셋 준비.
- `Update → Render` 연결, 입의 표정 위 지연 합성/talk latch, 새 brow texture, soft iris RGBA mask 채널.
- WebP premultiply 버림과 Canvas readback 경계: 잘못 선택되던 darkest lash pixel 수정.
- view/focus/pan/zoom·하단 잘림 보정, CanvasToModel과 poke 체인 속도 입력.
- LivelyIdle/classic IdleDriver 및 follow wrapper: noise/spring/breath/move/attention·pointer/held look/blink.
- PlayerBase의 frame 합성·driver 순서/제거·재생/정지/해제·표정/액션. Unity Player는 렌더러·좌표 API와 연결.
- `LoadAsync`: ZIP/파일 map/ModelPackage/HTTP(S) 폴더·패키지, 취소·교체·진행률·재시도·다운로드 바이트 제한. 원본 진행률 6개와 캐시 해시 21개 비교, 실제 Unity HTTP/GPU 경로 검증.
- 모델 교체 시 params/time/사용자 설정을 유지하고 모델별 physics/expressions를 초기화; 실패하면 기존 렌더러 유지.
- TalkMotion과 FaceTracking/MouthDriver: 얼굴 유실 easing, follow 이동, 음성 기반 nod/brow/blink/hop, tracked 억제와 input ownership.
- PCM 650Hz/Q .55 bandpass + 1024 Blackman FFT: 실제 Chrome WebAudio 4개 sample rate와 비교.
- Unity 순환 AudioClip reader와 취소 가능한 native Microphone 시작/해제 어댑터. BlueStacks가 연결한 Mac 마이크의 실제 PCM 입력 확인; 물리 Android/iOS 기기는 미검증.
- WebGL WebAudio/Float32 heap bridge, generated MediaStream → WASM VoiceMeter → mouth 동작, 거부/취소/stop 해제 검증.
- Unity 6000.3.14f1 Emscripten으로 libwebp 1.6.0 WASM archive 재현; 실제 Aoi WebGL build/4 poses 비교.
- VoiceMeter 첫 read의 1/60초 clock 경계 수정 및 원본 read() 기반 28개 시나리오 검증.
- WebGL Tasks Vision 1.1.1 self-hosted loader/추론·42-double ABI와 IFaceInput 연결. 원본 poll 720개 일치; 실제 GPU 추론의 회전/유실/어두움/재시작/거부/취소를 생성 영상으로 확인.
- 별도 `ai.livliv.mediapipe` 패키지: 고정 homuler 0.16.3, CPU VIDEO 추론·Z축 역변환·WebCamTexture 방향/permission/취소/해제. macOS ARM64 Editor에서 실제 사진 추론/유실/어두움/재생성 검증.

`Runtime/runtime/puppet.cs`는 부분 포팅이다. 변형/물리/이미지 경계/keyform 계산과 동적 합성은 검증했지만
cropped mask RT를 원본 bbox/64px 확장 규칙으로 연결하고 Chrome/macOS/BlueStacks에서 검증했다. 추가 플랫폼의 GPU 검증이 남아 있다. Player host는 ModelPackage/ZIP/파일 map과 HTTP(S) URL의 LoadAsync·취소·교체·진행률을 지원한다.
snapshot은 PNG data URL/byte API로 연결했다. PNG/WebP poster 상태와 선택 UGUI 표시 어댑터도 연결했으며 선택 UGUI PlayerSurface의 component/해상도 정책도 연결했다. 자세한 계약과 차이는 [LOADING.md](LOADING.md)를 참고한다.
21 모델 dynamics fixture는 샘플 필드를 주입한다. 별도 3 모델의 실제 이미지/키포즈 파일에서 12개 프레임을
원본 브라우저와 비교했다. 눈 mask RT는 원본의 cropped bbox/64px 성장 전략을 적용했다. [MASK-TARGETS.md](MASK-TARGETS.md).
LivLiv 로고와 소유자 텍스트 mark를 합성한다. 기본 NanumGothic/사용자 Font를 지원하며 미지원 glyph는 명시적 오류로 거절한다. [TEXT-MARK.md](TEXT-MARK.md).
기존 21개 캐릭터는 모든 이미지 로드/해제를 검증했다. Yui/Aoi 중립 PNG를 원본 WebGL과 비교했고,
WebP 버림 반영 후 활성 영역 채널 평균 오차는 255 기준 0.3008/0.1900이다. 일부 로고 가장자리 오차가 크므로 완전 동등성을 주장하지 않는다.
방법과 최대 오차 등은 [GRAPHICS.md](GRAPHICS.md)에 기록했다.

## 기준 전체 검증 및 기능별 증거

아래 전체 suite 수치는 각 기능 추가 전의 기준 실행이다. 이후 변경별 최신 검증은 아래 추가 기록과
각 기능 문서에 날짜·산출물별로 구분한다.

| 검증 | 결과 |
|---|---|
| 원본 Node 테스트 (P1 기준; 이번에 원본 변경 없음) | 91 passed, 0 failed |
| `.NET 9.0.200` 공용 수치/모델/ZIP/brand/puppet/sampling 테스트 | 1,082 passed, 0 failed, 0 skipped |
| `unity test ./unity --mode EditMode …` (GPU 포함) | 1,149 passed, 0 failed, 0 skipped; Editor 6000.3.14f1 / Metal |
| capture 검증 (전체 suite 포함) | `AudioCaptureTests` 3 passed; mono/stereo 순환·버퍼 초과·장치 중단·외부 clip ownership (실제 microphone 권한/장치 아님) |
| WebGL CLI build | IL2CPP, errors/warnings 0, development 52,358,426 bytes (추가 loose 얼굴 모델/JS/WASM 30,748,485 bytes); Emscripten libwebp 링크 포함 |
| WebGL browser | Chrome 156: Aoi 첫 프레임/4 poses, 생성 오디오 활성·stop·권한 거부 주입·취소 후 late stream 종료 |
| 비동기 모델 로드 | 공통 13 tests + 실제 Unity HTTP/GPU 1 test; WebGL Chrome에서 ZIP/폴더/unknown-length/retry/404/invalid/cancel/replaced/replacement 9개 성공. 모바일 실기기 네트워크 미검증 |
| Cropped eye mask | 원본 bounds 160개, Unity GPU/계약 55 tests; Metal 3모델×4프레임 기존 출력 일치. Chrome 실제 2개 눈 channel/화면 밖 생략; macOS·BlueStacks IL2CPP 8개 상태/해제 통과. Aoi 마스크 1.5MiB→96KiB. [MASK-TARGETS.md](MASK-TARGETS.md) |
| UGUI PlayerSurface | .NET/Unity 크기 6개씩, PlayMode 11개(포스터 회귀 포함), Chrome WASM 8개 상태; source 크기 계산 96개 일치. [SURFACE.md](SURFACE.md) |
| Poster | .NET 25, Unity EditMode 31, PlayMode 5 passed; Chrome WASM 실제 포스터/첫 프레임/교체/취소/이미지 오류 9개 상태. [POSTER.md](POSTER.md) |
| Snapshot | Unity GPU 4 passed; Chrome WASM 4 poses, straight alpha·PNG 방향·mark·dt=0·target 소유권. [SNAPSHOT.md](SNAPSHOT.md) |
| 네이티브 표시 어댑터 | macOS ARM64/Metal·BlueStacks ARM64/GLES3 각각 7개 상태, PNG alpha/전체 픽셀·file/data 포스터·소유 리소스 정리 통과. [NATIVE-PRESENTATION.md](NATIVE-PRESENTATION.md) |
| Speech WebGL | Node 14 passed; Chrome 실제 URL/byte/stream/cancel/error/restart 8개 기록과 context 해제; native PlayMode 회귀 6 passed |
| 입력 JS bridge 계약 | Node 18 passed (음성 6 + 얼굴 12); 원본 poll 720개, heap grow/packed fields/dispose/error/cancel/progress |
| macOS standalone | ARM64 및 x64(Rosetta) IL2CPP/Metal 실행: Aoi, CPU 추론/회전/유실/재검출/해제·표시·음성. ARM64 실제 카메라·마이크 개별 중단/재시작 및 30회 회수 확인; 물리 Intel/장치 분리는 미검증. [MACOS.md](MACOS.md) |
| Android APK | ARM64 IL2CPP build, 표시 어댑터 검증 포함 63,351,483-byte APK, native libs/심볼·권한·16KB 정렬·Resources 확인; 실기기 미검증 |
| BlueStacks Air | Android 13/OpenGLES3: 생성 영상 얼굴 추론·회전·유실/재검출·해제, 크기 고정 7 상태 통과. 이전 APK에서 Mac 카메라·마이크 동시 입력 확인. 권한 dialog 복귀 수정 확인. Vulkan driver crash 제약은 [BLUESTACKS.md](BLUESTACKS.md) 참고 |
| UGUI 샘플 | Android/WebGL build 오류 0; Chrome 데스크톱·세로 화면, 실제 버튼/취소/오류/생성 오디오·표정·키보드 확인. BlueStacks 새 UI와 카메라 추적, scale=1 확인. [UI.md](UI.md) |
| iOS app | Unity export + Xcode unsigned iPhoneOS build/link, ARM64·MediaPipe embedding·WebP 심볼·Resources 확인; 서명/실기기 미검증 |
| 네이티브 얼굴 추론 | macOS ARM64 Editor CPU task 사진 추론/방향 8개 및 standalone 실제 카메라 추론/재시작 확인. 장치 분리/정확도 평가는 미검증 |
| WebGL 얼굴 추론 | Chrome 156, 실제 MediaPipe GPU + 생성 canvas stream: 회전/유실·재검출/어두움/재시작/권한 거부·late cancel; 물리 카메라 아님 |
| format fixture | 원본 직접 실행: 모델 748개, ZIP 21개; 현재 source와 일치 |
| brand fixture | 원본 직접 실행: Canvas 64개 사례, Mark 35개 상태 전환; 현재 source와 일치 |
| puppet fixture | 원본 직접 실행: 21 모델의 139 레이어, 동일 alpha 입력에서 정점/삼각형/가중치/스키닝 비교 |
| dynamics fixture | 원본 직접 실행: 21 모델 × 120프레임 = 2,520프레임. 물리·입·변환/가시성·poke·view/좌표 및 189회 전체 레이어 변형 snapshot 비교 |
| Idle/follow fixture | 원본 직접 실행: 5개 시나리오 × 1,800 = 9,000프레임, move 정의·RNG 호출 수·상태/params 비교 |
| PlayerBase fixture | 원본 직접 실행: 460프레임, driver 순서/제거·표정/액션·pause/visibility·dispose 비교 |
| Talk/입력 driver fixture | 원본 Talk 5,400 + Face/Voice 조합 6,400프레임; RNG·face loss/mirror/calibration·motion 억제·해제 횟수 비교 |
| PCM 분석 fixture | 실제 Chrome OfflineAudioContext: 16/44.1/48/96kHz, 필터/FFT/time window 비교; stereo/chunk 처리도 검증 |
| 실제 동적 프레임 | Chrome 원본 d01/char06/d03ft × turn/blink/talk/neutral 12 프레임의 표시 정점·fol/folB/lash/nasal 비교; 6개 lash RGBA 수치 fixture 별도 |
| 실제 Canvas alpha fixture | 원본 함수 + Chrome 156: 21 모델 514 레이어의 occupancy가 Unity와 모두 정확히 일치 |
| 현재 source hash 검사 | 25 modules, issues 0 |
| upstream 차이 감지 | P1에서 `--ref HEAD~1`의 speech.ts 변경을 exit 1로 검출 |
| WebP plugin 재현 | macOS universal 소스 pin·두 아키텍처 clean build 결과 SHA-256 일치; 외부 Homebrew dylib 의존성 없음. 타 플랫폼 포함 6 artifact 감사 통과 |

Unity 결과: `unity/TestResults/editmode.xml`, 로그: `unity/TestResults/editor.log`.
최신 전체 실행: 2026-11-09 04:50:14Z–2026-11-09 04:51:44Z, 89.608초, 실패/skip 0.
Android/iOS permission 분기는 해당 player build/device에서 추가 검증해야 한다.
GPU 이미지: `unity/TestResults/neutral-compositor.png`, `neutral-{d01,char06}.png`, `mark-*.png`, `animated-*.png`.
원본 비교: `unity/TestResults/browser-*.png`, `render-comparison.json`. 로고/Yui 및 Nana 말하기 원본/Unity, Aoi blink는 직접 열어 확인.
.NET 결과: `tools/Parity/TestResults/parity.trx`.
결과/캐시 디렉터리는 Git에서 제외한다. 재현 명령은 [unity/README.md](../../unity/README.md).

그래픽 테스트 52개는 PNG/WebP 채널·상하 방향·premultiply, 사전 크기 제한, 합성 순서/alpha/stencil,
Player의 reload/settings/coords/pause/target ownership, 실패한 load의 rollback, 21개 캐릭터의 모든 텍스처 로드·Dispose 후 잔존 texture 수,
로고 합성/깜박임/GPU 픽셀, 텍스트 mark를 조용히 생략하지 않는 동작, 두 캐릭터 중립 출력과 12개 동적 프레임, fractional soft mask와 talking/shut/latch 픽셀,
호스트 제공 디코더 주입과 크기 예산, 21 모델의 Canvas alpha 격자 및 source hash를 확인한다. GPU suite에는 `-nographics`를 쓰지 않는다.

모델 비교에는 21개 실제 모델, legacy/ancient 변형, 정상 선택 필드, null/누락/잘못된 타입,
경로 이탈, vertex/skinned/readback 예산이 포함된다. ZIP은 정상·손상·중복·경로 이탈·압축 폭탄을 포함한다.
원본 JS가 uncoded TypeError/RangeError로 실패하는 malformed 입력은 C#에서 `invalid`로 정규화한다.
디스크 폴더 어댑터는 root 아래 symlink도 거부한다. 웹 브라우저에 없는 로컬 파일 경계 규칙이다.

Dynamics 비교는 가변 dt(0~0.1초), 각도 초과, rig/tuning 변경, 물리 강도 변경, 중간 reset과
눈·입·표정 전환을 포함한다. head/body/spring double은 `1e-9 × max(1, |ref|)`,
체인 상태(Float32 버퍼 포함)/정점 필드는 `1e-6 × max(1, |ref|)` 허용 오차로 비교한다.
2개 체인 모델(d03ft/k02) 및 keyform 대칭 Hermite·명시 weight·kfOff도 포함한다.
렌더링/추론 성능과 Mobile IL2CPP/WebGL 실행 검증은 별도 단계다.

첫 Unity 실행의 라이선스 문제는 사용자의 활성화 후 해결됐다. 현재 에디터 테스트를 막는 조건은 없다.

## 남은 순서

1. P2 마무리: 소유자 텍스트 워터마크의 추가 플랫폼·문자권 검증, 로고 AA/그림자 차이 개선. 더 넓은 캐릭터/해상도 비교와 Mobile CPU 예산.
2. P3: cropped mask target의 추가 플랫폼 검증, 더 넓은 keyform malformed/직접 field 검증과 렌더링 회귀. view/좌표/poke·동적 기본 경로는 연결됨.
3. P3: speech의 남은 플랫폼/기기 검증, UGUI PlayerSurface의 추가 플랫폼/camera 모드 검증 및 poster의 추가 이미지 형식/플랫폼 검증. Speech 공통 큐·native/WebGL 출력/URL/byte/decoder·공개 driver는 연결됨. 비동기 URL load job·취소/진행률·재시도 및 Idle/talk·driver lifecycle은 연결됨.
4. P4: native MediaPipe + WebCamTexture의 target build/device 검증, native/browser 실제 camera/microphone 권한·동시 실행·background/resume. WebGL Tasks Vision과 WebAudio bridge의 생성 입력 검증은 완료.
5. P5: sample scene, PC/Mobile/WebGL build/실행·실제 장치, 장기 리소스/성능, upstream 갱신 rehearsal.

Windows build 및 Linux Docker runtime의 후속 검증은 [DESKTOP.md](DESKTOP.md)에 기록했다.
Windows 실제 실행, 물리 모바일 기기, 실제 장치 추론, 전체 캐릭터/입력 범위의 움직임 동등성은 아직 검증하지 않았다.
WebGL은 desktop Chrome 156의 Aoi와 생성 오디오/영상 및 실제 GPU 얼굴 추론 범위까지 실행 확인했다. 다른 브라우저/모바일 WebGL/실제 장치는 미검증이다.
세부 계획과 판단 근거: [PORTING.md](PORTING.md).

플랫폼 확장 준비: `IWebPDecoder`를 주입 가능하게 분리했고 iOS/WebGL 정적 링크의 `__Internal` 경계를 추가했다.
WebGL build/browser와 macOS ARM64 IL2CPP 실행을 확인했다. Android ARM64 APK·iOS ARM64 unsigned app의 링크/포함 자산 검증도 통과했다. 현재 Windows x64 build, Linux x64 Docker 실행, macOS x64 Rosetta 실행은 추가 검증했다([DESKTOP.md](DESKTOP.md), [MACOS.md](MACOS.md)). Windows 실행과 물리 PC/모바일 실기기 검증은 남아 있다.

음성 분석과 입력 어댑터의 검증 경계는 [AUDIO.md](AUDIO.md)에 기록했다. 권한 관련 플랫폼 분기는 Editor 테스트로 검증했다고 주장하지 않는다.

WebGL의 재현 명령·WASM/browser 증거·픽셀 오차와 남은 범위는 [WEBGL.md](WEBGL.md)에 기록했다.

얼굴 추론의 1:1 매핑·배포 자산·검증 및 남은 범위는 [FACE.md](FACE.md)에 기록했다.

네이티브 입력의 dependency 고정·설치·실행 근거는 [NATIVE-FACE.md](NATIVE-FACE.md)에 기록했다.

시작 취소 보강을 포함한 native 11개가 최신 전체 1,135개 통과에 포함된다.

macOS ARM64 IL2CPP standalone/Metal 생성 영상 추론·렌더·회전/유실/재검출 smoke와
Android ARM64 APK build/권한·native libs·16KB 정렬 검사를 추가로 통과했다.
실제 장치는 미검증이다. [NATIVE-BUILDS.md](NATIVE-BUILDS.md)에 산출물·재현 명령·제약을 기록했다.

iOS Unity export와 실제 Xcode unsigned iPhoneOS 앱 build/link·framework/심볼/Resources 검사도 통과했다. 서명과 실기기 실행은 남아 있다.

2026-11-09 추가: [입력 수명주기](CAPTURE-LIFECYCLE.md)의 권한 gate 7개 테스트(.NET/Unity) 통과. BlueStacks에서 native 권한 허용 후 시작 지속, 거부, background 입력 해제와 복귀 시 Off를 확인했다.
샘플 스크롤을 제거하고 4개 WebGL 화면 크기와 새 Android APK 화면을 확인했다. 세부 증거는 [UI.md](UI.md)의 스크롤 제거 후 검증에 있다.

2026-11-09 추가: [Speech](SPEECH.md)의 공통 큐와 native Unity 출력·공개 driver를 연결했다.
.NET/Unity EditMode 47개(기존 Face/Voice driver 회귀 포함), Unity PlayMode 5개 통과.
Android IL2CPP/BlueStacks와 macOS ARM64 IL2CPP/Metal에서 실제 믹스 PCM·렌더된 mouth·순서·중단·해제·scale=1을 확인했다.
Speech 반영 APK는 73,499,549 bytes이며 UI는 기존 스크롤 없는 구성을 유지한다.
위 수치는 URL/byte decoder 추가 전 native PCM 출력 검증이다. 기존 전체 suite 수치는 speech 변경 전 결과이며,
이번에는 위 대상 테스트와 native speech probe를 실행했다.
native speech 코드 추가 후 WebGL CLI build도 errors/warnings 0 (52,147,120 bytes)으로 통과했다.
이 빌드는 WebGL speech bridge 추가 전 결과다. 최신 실행 검증은 아래에 기록했다.

2026-11-09 로더 추가: native 기본 URL/byte/WAV/MP3 경로를 연결했다. 원본 Incoming의 WAV fixture 21개,
.NET 음성 65개, Unity EditMode 공통/HTTP/decoder 69개, PlayMode 6개 통과. whole WAV metadata 검증 추가 후
HTTP/decoder 4개도 다시 통과했다. 실제 streaming HTTP 첫 PCM을 본문 완료 전에 받았으며,
MP3/float WAV 디코딩·첫 채널·취소·예산·배열 복사·50 ms gate를 확인했다. [SPEECH.md](SPEECH.md).
native byte decoder를 실제로 경유한 WAV/MP3/float WAV probe도 Android IL2CPP/BlueStacks와
macOS ARM64 IL2CPP/Metal에서 통과했다. 두 build errors/warnings 0, 현재 APK 63,316,194 bytes.
실제 HTTP 분할 수신은 Editor에서 검증했으며, native player는 내장된 합성 바이트로 검증했다.
decoder 추가 후 WebGL build errors/warnings 0, 52,201,881 bytes(bridge 추가 전).


2026-11-09 WebGL Speech 추가: 기본 factory에 Web Audio 출력과 fetch/ReadableStream/decodeAudioData를
연결했다. Node 계약 14개, native Unity PlayMode 회귀 6개 통과. WebGL build errors/warnings 0,
52,263,742 bytes. 실제 Chrome 156에서 WAV/MP3/float WAV URL·MP3 byte, 본문 완료 전 WAV 재생,
중단·404/decode 오류·재시작을 검증해 8개 상태를 기록했다. 최종 context closed와 잔존 리소스 0,
scale=1, 임시 QA 버튼 제거 후 스크롤 없는 UGUI 화면을 확인했다. [SPEECH.md](SPEECH.md).
Safari/모바일 브라우저·물리 장치 지연은 미검증이며, WebKit Node double을 실제 기기 검증으로 보지 않는다.


2026-11-09 Snapshot 추가: 원본 `base.ts snapshot()`을 `Player.Snapshot()`(PNG data URL)과
`SnapshotPng()`(byte[])로 연결했다. 현재 pose를 dt=0으로 다시 그리며 package mark를 포함한다.
Unity GPU 테스트 4개와 실제 Chrome WebGL 4개 pose를 통과했다. 4장 PNG의 모든 픽셀에서 alpha가
동일하고 premultiplied 왕복 오차 최대 0.4981/255, 임시 readback texture 해제와 target 상태 복원을 확인했다.
WebGL CLI build errors/warnings 0, 52,272,501 bytes. 포스터 표시·Unity component/해상도 정책과
다른 플랫폼의 snapshot player 실행은 남아 있다. [SNAPSHOT.md](SNAPSHOT.md).


2026-11-09 Poster 추가: `LoadOptions.poster`와 source-mapped lifecycle을 공통 PlayerBase에 연결했다.
선택 패키지 `ai.livliv.ugui`의 `PosterPresenter`는 PNG/WebP를 UGUI contain으로 표시하고 기존 배경을 복원한다.
원본 8개 시나리오를 포함한 .NET 회귀 25개, Unity EditMode 31개, UGUI PlayMode 5개 통과.
실제 Chrome에서 모델 다운로드 대기 중 표시, 첫 프레임 해제, 기존 모델 보존, 늦은 교체/취소, 잘못된 이미지 후
모델 성공과 샘플 복귀를 검증했다. 포스터 texture 0, scale=1, 스크롤 없는 UI 유지. [POSTER.md](POSTER.md).
일반 Player MonoBehaviour/자동 해상도 정책, 다른 poster 형식, Android/iOS/Safari 실행은 남아 있다.

포스터 포함 최종 WebGL build: errors/warnings 0, 52,315,491 bytes.


2026-11-09 PlayerSurface 추가: optional UGUI 컴포넌트에 Player/타깃/머티리얼 소유권과 자동 frame/size 갱신을
연결했다. 원본 DPR/round 계산 96개, .NET/Unity EditMode 6개씩과 PlayMode 11개(포스터 회귀 포함) 통과.
실제 Chrome WASM에서 세로/가로/density cap/pixel budget/0 크기/복구/해제 8개 상태와 UI 출력을 확인했다.
최종 WebGL build errors/warnings 0, 52,341,506 bytes. 고정 해상도 기존 샘플은 유지하며 검증 후 복귀했다.
다른 platform 및 world-space camera/soft clipping/장기 성능은 남아 있다. [SURFACE.md](SURFACE.md).


2026-11-09 소유자 텍스트 워터마크 추가: 원본 Mark/Brand의 선택·배치·DPR·캐시를 유지하고
Unity Font coverage 어댑터를 연결했다. 기본 NanumGothic 폰트와 라이선스를 포함하며, host의 Font 또는
IMarkTextRasterizer를 주입할 수 있다. 미지원 글리프는 모델 교체 전에 명시적 오류로 거절한다.
.NET 69개, Unity EditMode 129개, UGUI PlayMode 11개 대상 검증 통과.
실제 Chrome WebGL 8개 상태에서 한국어/영문/리터럴/기본 폰트/2배 해상도/예산 거절과 리소스 해제를 확인했다.
WebGL build errors/warnings 0, 53,209,406 bytes. Canvas system-ui와 글꼴 모양까지 같은 것은 아니며,
macOS ARM64 Metal 및 BlueStacks ARM64 GLES3에서도 같은 8개 상태와 기존 표시/마스크 회귀가 통과했다.
복잡한 조판·RTL·emoji, 물리 모바일 및 Windows/Linux 검증은 남아 있다. [TEXT-MARK.md](TEXT-MARK.md).

iOS도 텍스트 어댑터 포함 unsigned build/link 및 기본 폰트 Resources 바이트 감사까지 통과했다
(173,580,152 bytes). iPhone 실행은 미검증이다. 텍스트 추가 후 최신 native 증거와 재현은 TEXT-MARK.md에 있다.


2026-11-09 얼굴 보정 저장·복원 추가: 원본 `lm_calib:<remember>` JSON, 미러 좌표 정규화와 구형 위치 보완을
ICalibrationStore 경계로 옮겼다. native는 PlayerPrefs, WebGL은 원본과 같은 localStorage를 사용한다.
샘플의 일반 카메라는 캐릭터별 값을 기억하며 확대·축소 제거 정책은 그대로다.
원본 50개 시나리오 포함 .NET 63개, Unity CLI 80개와 bridge Node 2개 통과.
실제 Chrome WASM의 저장·새로고침 복원·구형 값 보완·재복원·잘못된 JSON 5개 상태도 통과했다.
[CALIBRATION.md](CALIBRATION.md)에 API, 저장 시점과 검증 범위를 기록했다.

보정값의 실제 지속성도 macOS/BlueStacks에서 각각 별도 앱 실행 5개 상태로 확인했다.
iOS unsigned build/link 및 포함 자산 감사 통과(173,643,818 bytes), iPhone 실행은 미검증이다.
이 단계의 source/API/검증 경계는 CALIBRATION.md를 따른다. 전체 포팅은 계속 진행 중이다.


2026-11-09 얼굴 입력 trace 확장: 원본의 기록 JSON·cap, 일반/좁은 눈 합성 입력과 loop 통계를 포팅했다.
IFaceInput 기록/재생 및 엄격한 외부 JSON/취소 가능한 transport 로드를 연결했다. 재생에서는 실제 카메라의
저장 보정값을 읽거나 덮어쓰지 않는다. .NET 88개·Unity CLI 108개 대상 검증 통과.
원본 대비 계약과 DOM/window 대체 경계는 [TRACES.md](TRACES.md)에 있다.

Trace runtime은 Chrome WebGL/macOS/BlueStacks에서 일반·좁은 눈 각 1,441프레임을 실행했고,
원본의 모든 통계 및 export JSON 값을 비교해 통과했다. 세 Unity 플랫폼의 export bytes도 일치한다.
iOS unsigned build/link·포함 자산 감사 통과(173,828,608 bytes), iPhone 실행은 미검증이다.
P5 APK 크기 점검 완료: 증분 APK의 미참조 ZIP 영역 32,106,334 bytes를 확인하고 샘플의
debug 패키징만 새로 생성하도록 수정했다. 반복 CLI 빌드에서 53,434,562 / 53,434,594 bytes,
미사용 영역 0을 확인했다. 서명·16KB 정렬·자산 감사와 최종 APK의 BlueStacks trace 실행/전체
원본 JSON 비교를 통과했다. [ANDROID-PACKAGING.md](ANDROID-PACKAGING.md).
다음 P5 범위는 플랫폼별 입력 생명주기/장기 실행과 미검증 Windows/Linux·물리 모바일 지원이다.

2026-11-09 입력 수명주기 후속: WebGL 샘플이 숨김 이벤트에서도 capture를 계속 유지하는 문제를
실제 WASM에서 재현했다. sample document lifecycle adapter와 시작 token의 즉시 JS 취소를 추가했다.
생성 입력 및 주입한 visibility/pagehide 이벤트 12개 상태, Node 20개·Unity CLI gate 7개 통과.
Ego가 비활성 탭도 visible로 보고하므로 실제 OS 탭 중단 증거와 구별한다.
최종 Android APK도 다시 빌드해 BlueStacks 실제 동시 입력의 Home/복귀를 동일 PID에서 3회 통과했다.
장치 해제는 Android AppOps로 확인했다. 양쪽 build errors/warnings 0, scale=1 및 스크롤 없는 UGUI 유지.
[CAPTURE-LIFECYCLE.md](CAPTURE-LIFECYCLE.md)에 재현/증거/남은 범위를 기록했다.
다음은 반복 시작·종료의 장기 메모리/리소스 회수 검증이다. 물리 모바일/Safari와 Windows/Linux는 계속 미검증이다.

2026-11-09 반복 입력 검사 추가: CLI 전용 진단과 Android 메모리 sample acknowledgment를 구현했다.
BlueStacks 실제 카메라/마이크 30회, 152.80초, Unity/Android 각각 63개 sample 통과.
종료마다 카메라·오디오 0개 및 Texture2D 기준값 복귀, 초기 캐시 이후 렌더 객체 수 일정,
Android AppOps 종료와 마지막 10회 PSS 증가 둔화를 확인했다. [CAPTURE-SOAK.md](CAPTURE-SOAK.md).
GC 1초 후 값은 추가 할당을 포함하므로 순수 live heap으로 주장하지 않는다.
최신 APK 53,442,442 bytes, build errors/warnings 0, 서명/정렬/자산/빈 ZIP 영역 검사 통과.
이는 짧은 에뮬레이터 반복 검사이며 장시간·실기기·다른 플랫폼의 자원 회수는 계속 미검증이다.

2026-11-09 Windows/Linux 추가: decoder 전용 DLL/SO와 플랫폼별 PluginImporter 설정,
고정 소스·compiler provenance·SHA-256 검사 및 Unity CLI 진입점을 연결했다.
Windows x64 Mono build는 errors 0 / warnings 1(호스트 에디터 오디오 초기화 경고), Linux x64 IL2CPP는
errors/warnings 0. DLL/SO 아키텍처·심볼·실제 모델/폰트 포함·다른 OS 플러그인 제외 검사 통과.
Linux Docker/Mesa GLCore에서 얼굴 7개, 표시 7개, 눈 마스크 8개, 텍스트 8개 상태 및 소유 리소스 해제 통과.
scale=1과 PNG 왕복 최대 오차 0.49804/255를 확인했다. Linux 물리 GPU/장치·Windows 실제 실행은 미검증이다.
원본 모듈 25개 해시 및 WebP 6개/MediaPipe 자산 해시 검사도 통과했다. 전체 .NET/Unity suite를 다시 실행한 결과는 아니다.
재현과 실패한 Mono/Rosetta 실행 기록, 개발 빌드 크기·런타임 요구사항은 [DESKTOP.md](DESKTOP.md)에 있다.

2026-11-09 upstream 갱신 리허설: TypeScript AST로 이전/후보의 import 그래프를 연결하고,
직접 변경·간접 의존 C#/어댑터와 fixture/테스트 참조, 원본 계약·의존성·자산 변경을 보고한다.
baseline commit의 실제 blob과 manifest/fixture 출처를 비교하며 자동 갱신하지 않는다.
Node 12개 검사 및 실제 수정 전 WAV의 12개 샘플레이트 차이 재현, 수정 후 21개 원본 fixture 일치 통과.
관련 .NET/Unity CLI 테스트 각각 73 passed, 0 failed/skipped. 전체 suite나 새 원격 변경 적용의 증거는 아니다.
현재 모듈 25개/fixture 21개 감사의 issues 0, 원본·baseline·fixture 보존 확인.
P5의 알려진 원본 변경에 대한 리허설을 마쳤으며, 물리 장치/플랫폼 실행·장시간 성능 게이트는 남아 있다.

2026-11-09 음성 상태 알림 추가: 원본 AudioContext 이벤트 3개 시나리오/16개 관찰을 fixture로 만들었다.
WebGL은 이벤트 수를 Tick으로 전달하며 기존 native 출력은 Running 변화로 관찰한다.
main-thread 재진입/예외·Dispose 종료 알림 한 번과 참조 해제를 처리했다.
.NET/Unity EditMode는 Speech/State/WAV/Driver 총 81개, Unity PlayMode 7개, JS bridge 16개 통과.
macOS ARM64/Metal 및 BlueStacks ARM64/GLES3에서 `[false,true,false]`, WAV/MP3/float WAV 출력과 입 동작,
resource 0개 통과. Chrome 실제 WASM 상태 9개 관찰 및 기존 speech 회귀 8개 기록 통과.
WebGL/macOS/Android build errors/warnings 0. 최신 Android APK 53,444,606 bytes, 서명/정렬/포함 자산/패키징 감사 통과.
샘플 크기 고정·입력 Off·스크롤 없음 유지. 다른 브라우저·물리 기기의 오디오 interruption 검증은 남아 있다.
