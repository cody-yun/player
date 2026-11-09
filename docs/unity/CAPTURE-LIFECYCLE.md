# 입력 권한과 앱 수명주기

## 네이티브 캡처 중단 상태

원본 수식 밖의 장치 경계에 선택적 `LivLiv.Platform.ICaptureStatus.Error`를 추가했다.
기존 IFaceInput/IVoiceInput과 외부 INativeFaceSource 구현에 새 멤버를 강제하지 않는다.
AudioCaptureInput은 선택적 isRecording callback이 false가 되면 입력을 한 번 해제하고 terminal 오류를 보존한다.
이 callback을 주지 않은 외부 AudioClip reader는 기존 negative-position 동작을 유지한다.
WebCamFaceSource는 시작 완료 후 WebCamTexture.isPlaying이 false가 되면 장치를 해제하고 오류를 알린다.
NativeFaceInput은 optional source 오류를 받으면 추론 task와 source를 해제한다. 무음·새 프레임 없음·일시적인
source.Ready=false는 terminal 오류로 취급하지 않는다. WebGL 어댑터도 같은 상태 인터페이스를 구현한다.
샘플은 두 플랫폼 모두 개별 driver만 Stop하고 UGUI에 재시작 안내를 표시하며 렌더러를 유지한다.
사용자는 main-thread loop에서 Error를 읽어 보관한 후 driver를 Stop한다. background/teardown Stop은 별도로 필요하다.

Unity EditMode `AudioCaptureTests|NativeFaceTests` 17 passed, failed/skipped 0.
Android IL2CPP CLI build errors/warnings 0. BlueStacks에서 실제 캡처를 시작하고 WebCamTexture.Stop과
Microphone.End를 각각 호출해 해당 입력의 종료/오류, 다른 입력 보존, 각 재시작, 렌더 프레임 계속 진행을 확인했다.
최종 Stop 뒤 WebCamTexture/AudioClip 각각 0개 및 두 Android AppOps 실행 종료를 확인했다.
이는 실제 물리 장치 분리나 OS 장치 변경 검증이 아니다. isPlaying/IsRecording을 계속 true로 보고하는 드라이버의
무응답과 native OS 장치 교체는 아직 검증하지 않았다.
증거: `unity/TestResults/native-capture-end/`, `bluestacks/capture-end/1794221502076499000/`.
같은 APK의 일반 MAIN/LAUNCHER 실행 생존도 통과했다: `android-startup/1794221519955848000/`.
APK 53,261,774 bytes, SHA-256 `501db0c9be08e798d8bf44db0cfaf2834a2ec0cf2e6338c2bf1ab609422818a2`,
서명·ARM64·16KB ELF/ZIP 정렬/포함 자산 감사 통과. 실행은 `verify-android-capture-loss.py --end`로 재현한다.
공통 상태 인터페이스 반영 후 WebGL CLI build도 errors/warnings 0으로 통과했다.
최신 core/native tarball의 새 native 포함 소비자는 PlayMode 1개 및 자체 설치 파일 264개 감사를 통과했다.
원본 25개 module 검사 issues 0이며 tarball 재패키징 manifest도 일치했다.

PC 후속: macOS ARM64/Metal과 Linux x64 IL2CPP Docker/Mesa에서도 생성 source 오류를 주입했다.
10개 상태에서 오류 입력만 종료·renderer 유지·새 source 재검출, 두 source 각각 한 번 해제를 확인했다.
원래의 회전/유실/재검출/거리 변화 scale=1 검사도 유지했다. 실제 장치나 마이크 입력 검증은 아니다.
Windows x64 Mono도 최신 코드 빌드/파일 감사를 통과했지만 실행은 미검증이다.
증거: `unity/TestResults/capture-status-desktop/`, `macos-smoke/1794221966161144000/`,
`linux-face/1794222020306310000/`. 실행 스크립트 `verify-macos-player.py`, `verify-linux-face.py`는
새 10개 상태를 요구한다. `verify-bluestacks-player.py`도 다음 APK 재빌드 후 같은 확장 검사를 사용한다.

## macOS 실제 입력 후속

ARM64 IL2CPP/Metal 일반 창 모드에서 실제 카메라·마이크를 시작한 뒤 Unity Stop/End로 각각
종료했다. 다른 입력 보존, 재시작, 렌더 프레임 진행과 최종 WebCamTexture/AudioClip 0/0을 확인했다.
관측 62개 중 두 입력 활성 13개, 그중 얼굴 검출 11개였으며 scale은 모두 1이었다.
이는 입력/추론 동작 확인이며 추적 정확도 평가나 물리 장치 분리는 아니다.
별도 실행에서 두 입력 활성 중 명시적 NotifyContextLost를 호출해 입력 종료·Player 해제·재시작 거절과
콜백 1회, 캡처 객체 0 복귀를 확인했다. 실제 Metal 장치 유실 검사는 아니다.

증거: `unity/TestResults/macos-capture-end/1794225145641333000/`,
`macos-capture-loss/1794225331526928000/`. 재현은 `verify-macos-capture-end.py --interactive`,
여기에 `--context-loss`를 추가한다. 30회 반복과 메모리 한계는 [CAPTURE-SOAK.md](CAPTURE-SOAK.md).

## WebGL 장치 트랙 종료 처리

브라우저 경계는 이제 MediaStreamTrack의 `ended` 이벤트를 등록한다. 마이크는 분석 graph·AudioContext·gesture
listener와 stream을 즉시 정리하고, 카메라는 video 분리·stream 종료·대기 다운로드 취소를 수행한다.
정리 전에 이벤트 listener를 제거해 중복 종료를 막으며 일반적인 `muted` 상태를 영구 종료로 취급하지 않는다.
권한 응답 때 이미 종료된 stream은 시작 실패가 되고, 늦은 video.play 완료도 실패 상태를 되살리지 않는다.
샘플은 다음 Update에서 입력 Error를 확인해 해당 driver만 중지하고 UGUI의 연결 확인/재시작 안내를 표시한다.
렌더/추적 수식과 원본 TypeScript는 변경하지 않았다.

종료 이벤트/이미 종료된 stream 네 가지 검사는 수정 전 실패했다. 수정 후 늦은 video.play 완료와 기존
권한·숨김·컨텍스트 검사를 합쳐 Node 29 passed, failed/skipped 0.
WebGL CLI build도 errors/warnings 0으로 통과했다. `verify-webgl-device-ended.mjs`가 생성 입력용 실행 harness다.
증거: `unity/TestResults/device-ended/{before,node,upstream}.log`, `webgl-device-ended-build.log`.

후속 Chrome 실제 WASM 검사에서 마이크 종료/재시작은 통과했지만 카메라 종료가 실패했다.
로컬 파일과 HTTP 응답은 새 모듈인데 실제 실행 함수에는 종료 listener 코드가 없어 캐시된 이전 모듈 실행을 확인했다.
기본 moduleUrl에 Unity Application.buildGUID query를 추가해 같은 브라우저에서 새 모듈을 읽도록 수정했다.
사용자가 지정한 BrowserFaceOptions.moduleUrl은 그대로 사용하므로 해당 URL 버전 관리는 host 책임이다.
고정된 Tasks Vision 1.1.1 vendor 버전은 바꾸지 않았다.

최종 검사 6개 상태(active, microphone-ended, microphone-restarted, camera-ended, camera-restarted, stopped)를 통과했다.
생성 portrait canvas/650Hz oscillator의 실제 MediaStreams에서 track.stop 후 ended 이벤트를 주입했다.
각 실패 입력은 Off/오류 안내 상태가 되고 다른 입력은 유지됐다. 재시작 후 얼굴 추론과 립싱크가 다시 활성화됐고,
최종 종료 후 모든 트랙 ended/샘플 AudioContext closed, scale=1을 확인했다.
reload 후 Ready/Aoi/입력 Off/스크롤 없음으로 복구한 뒤 승인받아 생성한 Ego 공간 24를 닫았다.
이는 물리 장치 분리/실제 권한 창이나 다른 브라우저의 검증이 아니다.
증거: `unity/TestResults/device-ended/browser.json`, `browser.log`, `cache-build.log`.
실패 상태/캐시된 모듈의 실행 여부는 `browser-cached-module-failure.{json,log}`에 보존했다.

후속 d999dea 실행은 `unity/TestResults/upstream-latest/browser.json`에 별도로 보관했다.
6개 상태에서 표정 개수 4/대표 smile 이름·영어 번역과 기존 입력 종료/재시작/scale=1을 확인했다.
최종 Ready/입력 Off/스크롤 없음 확인 후 Ego 공간 25를 닫았다. 사용자는 이 목표의 후속 공간 자동 생성을 허용했다.
기록 보존 한계: 당시 스크립트의 고정 경로 `device-ended/browser.json`이 이 실행으로 갱신돼 이전 공간 24의
통과 JSON은 더 이상 보존되지 않는다. 실패 JSON/로그는 남아 있다. 이 파일을 이전 빌드의 원본 결과로 해석하지 않는다.
검사기는 이후 실행별 파일 경로를 사용하고 기존 결과 파일에는 쓰기를 거절하도록 수정했다.
기존 바이트를 유지하며 browser handle 획득 전 거절하는 로컬 검사를 통과했다(`upstream-latest/browser-output-guard.log`).

아래 기존 검증 기록은 각각 당시 소스를 대상으로 한다.

`PortVerification` 샘플은 장치 시작을 **권한 요청 → foreground 복귀 → capture 시작**으로 나눈다.
Android 권한 창은 앱을 일시정지시킬 수 있다. 이전의 `OnApplicationPause → Stop` 처리는 첫 요청까지 취소해서, 허용 후 시작 버튼을 다시 눌러야 했다.

## 구현 경계

- `CapturePermissions.RequestAsync`: Unity 플랫폼 경계의 공통 권한 요청. Android granted/denied/dismissed와 취소, iOS 권한 요청, native WebCam 권한을 처리한다. 카메라나 마이크를 열지 않는다.
- `CaptureActivationGate`: main-thread host 정책. 권한 요청 중 pause는 요청을 유지하고, 허용되어도 background에서는 capture로 넘어가지 않는다. foreground 복귀와 취소를 기다린다.
- `MicrophoneInput` / `WebCamFaceSource`: 독립 사용 시에도 권한을 확인한다. 샘플의 preflight 뒤에는 이미 허용된 권한을 확인하므로 두 번째 dialog를 요청하지 않는다.
- `PortVerification`: 카메라·마이크별 gate를 사용한다. 권한 요청 밖의 pause는 시작 중인 작업을 취소한다. 이미 활성화된 입력은 모든 pause에서 닫는다. 일반 background 복귀 후 자동으로 다시 열지 않으며 시작 버튼을 사용한다.
- 한 입력의 첫 권한 창 때문에 다른 활성 입력도 닫힐 수 있다. 그 입력은 사용자가 다시 시작한다. 권한을 모두 받은 뒤 동시 입력에는 이 전환이 필요 없다.
- WebGL은 기존 browser bridge의 user-gesture 시작 경로를 유지한다. 이 native preflight를 거치지 않는다.
  샘플 전용 `PortVerification.Visibility.cs` / `CaptureVisibility.jslib`가 `visibilitychange`의
  `document.hidden`과 `pagehide`를 받아 두 입력 및 진행 중인 요청을 즉시 종료한다.
  `blur`만으로는 종료하지 않는다(권한 창의 포커스 전환과 구별). 숨겨진 문서에서 시작 요청은 무시하고,
  보이게 되거나 `pageshow`가 와도 자동 재시작하지 않는다. 샘플 파괴 시 두 event listener를 해제한다.
- `WebGLFaceInput` / `WebGLVoiceInput`의 시작 취소는 token registration으로 JS Stop을 즉시 호출한다.
  숨겨진 문서가 다음 Unity frame의 `Task.Yield`를 실행하지 않아도 늦게 허용된 stream이 JS에서 닫힌다.
  registration은 시작 작업이 끝나면 해제되며, 시작 성공 후 입력 수명은 기존 driver/host가 소유한다.
- sample destruction은 pending token을 취소하고 열린 입력을 해제한다.

렌더러, 추적 수식, upstream 파라미터는 변경하지 않는다. 권한 허용과 앱 수명주기는 Unity host 책임이다.

## 검증

`CaptureActivationTests`: 7개 시나리오. 권한 허용/복귀 순서 양방향, 거부, foreground 대기 취소와 재시도, 사전 취소, 취소 후 늦은 허용, 카메라와 마이크 간 분리.
.NET 9 및 Unity 6000.3.14f1 EditMode에서 모두 7 passed, 0 failed.
Unity 결과: `unity/TestResults/capture-activation.xml` (2026-11-09).

이 단위 테스트는 실제 OS dialog나 장치 동작의 증거가 아니다. Android 실행 증거는 검증 후 `BLUESTACKS.md`에 기록한다. iOS 실기기 수명주기는 미검증이다.

공식 API 근거: [RequestUserPermission](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/android/permission/requestuserpermission), [PermissionRequestDismissed](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/android/permissioncallbacks/permissionrequestdismissed). 실행 검증의 실제 Editor 버전은 위 6000.3.14f1이다.

## WebGL 숨김 경계 검증 — 2026-11-09

수정 전 실제 WASM에서 생성 영상/음성 입력을 실행한 후 숨김 이벤트를 주입했을 때,
tracking/microphone이 모두 켜져 있고 video/audio track이 live, AudioContext가 running인 상태를 재현했다.
`unity/TestResults/webgl-lifecycle-before.json`에 기존 WASM 및 소스 hash를 보존했다.

수정 후 `verify-webgl-lifecycle.mjs`는 실제 Chrome 156 WASM에서 다음 12개 상태를 확인한다.

1. 생성 영상의 얼굴 검출과 생성 음성의 립싱크 동시 활성.
2. hidden 이벤트 후 두 입력·MediaStream tracks·AudioContext 종료.
3. hidden 상태에서 시작 요청을 해도 추가 capture가 생성되지 않음.
4. visible 복귀 후 Off 유지.
5. 사용자 동작으로 두 입력 재시작.
6. 권한 응답을 보류한 상태에서 hidden → 취소 → 늦은 응답. 같은 JS turn의 microtask 단계에서
   모든 늦은 track이 ended이며 Unity의 다음 frame에 의존하지 않음.
7. 그 요청의 visible 복귀 후 Off 유지.
8. 취소 후 두 입력 재시작.
9. `pagehide` 후 종료.
10. `pageshow` 후 Off 유지.
11. 페이지 복귀 후 두 입력 재시작.
12. 명시적 Stop 후 모든 capture track ended / capture context closed.

결과는 `unity/TestResults/webgl-lifecycle.json`이며 source hashes와 WASM SHA-256을 포함한다.
WebGL CLI build errors/warnings 0, 53,325,245 bytes, 18.80초.
최종 QA 버튼과 숨김 속성 override를 제거했고 viewport/document 888×990, scale=1,
카메라·마이크 Off 및 스크롤 없는 UGUI를 확인했다(`webgl-lifecycle-final.png`).

**범위:** Ego 브라우저는 비활성 탭도 `document.hidden=false`로 보고했다.
따라서 이 검증은 실제 WASM/MediaPipe/WebAudio에 숨김 값과 이벤트를 주입한 통합 검증이며,
OS가 브라우저를 실제 중단하거나 Safari가 탭을 freeze/bfcache 복원한 증거가 아니다.
입력은 생성 canvas와 oscillator이고 실제 브라우저 카메라/마이크 권한 창을 열지 않았다.
물리 모바일 및 다른 브라우저의 실제 background/restore는 남아 있다.

Node: visibility listener 해제/복수 host/focus 정책 2개 + 기존 음성 bridge 6개 + 얼굴 bridge 12개 통과.
Unity CLI `CaptureActivationTests` 7개 재통과: `unity/TestResults/lifecycle-activation.xml`.
원본 25개 module hash 검사 issues 0. 원본 TS 및 렌더러/추적 수식은 변경하지 않았다.

## Android 반복 실행 방법

최종 도구는 UGUI가 보고하는 실제 버튼 중심을 사용하며 고정된 1920×1080 클릭 좌표를 사용하지 않는다.
`python3 scripts/unity/verify-bluestacks-lifecycle.py --orientation Portrait`는 CLI 전용 방향 지정으로 시작해
모든 상태에서 실제 방향을 검사한다. `1794236042644738000/` 실행은 10개 상태 모두
Portrait 1080×1920, 입력 동시 실행/Home/복귀/명시적 재시작 3회, 배율 1,
UI 잘림·겹침·스크롤 0 및 AppOps running/종료를 통과했다. 일반 실행 Off 복구도 별도 기록한다.
증거는 `unity/TestResults/bluestacks/lifecycle/1794236042644738000/`다.
앞선 `1794235851826797000/` 실행은 실제 가로 화면이었으며 세로 검증으로 계산하지 않는다.
아래는 기존 가로 고정 좌표 버전의 실행 이력이다.

`python3 scripts/unity/verify-bluestacks-lifecycle.py`는 최신 APK를 설치하고 UGUI 버튼으로
BlueStacks가 전달하는 실제 카메라·마이크를 동시에 연다. 같은 PID를 유지한 채
Home → 2초 대기 → 복귀 → 명시적 재시작을 3회 수행한다. 각 단계의 Unity 상태,
Android CAMERA/RECORD_AUDIO AppOps의 running/종료, 메모리 정보와 UI 스크린샷을 기록한다.
기존 권한 허용 상태와 1920×1080 가로 UGUI를 전제로 하며 권한/앱 데이터를 초기화하지 않는다.
성공/실패와 관계없이 종료 시 일반 샘플을 입력 Off 상태로 다시 연다.

이것은 에뮬레이터의 짧은 반복 실행 검증이다. 메모리 정보 수집만으로 장시간 누수 없음,
물리 모바일 장치 수명주기 또는 음성/얼굴 인식 정확도를 입증하지 않는다.

최종 소스의 Android CLI 빌드 후 3회/10개 상태를 통과했다.
증거: `unity/TestResults/bluestacks/lifecycle/1794211150325729000/evidence.json` 및 같은 폴더의
`player.log`, 단계별 `*-meminfo.txt` / PNG. 실행 중에는 두 AppOps가 running이고,
각 background/resume에서는 두 AppOps에 running 표시가 없음을 확인했다. 동일 PID 검사를 통과했다.
APK 53,436,206 bytes, build errors/warnings 0, 46.37초. 서명·ARM64/16KB·자산 감사 통과,
미참조 ZIP 영역 0으로 이전 패키징 수정도 유지했다. Android 실기기 검증으로 해석하지 않는다.

후속 30회 입력 반복·동기화 메모리 측정은 [CAPTURE-SOAK.md](CAPTURE-SOAK.md)에 기록했다.
