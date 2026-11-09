# BlueStacks Android 실행 검증 — 2026-11-09

BlueStacks Air 5.21.750.7536, Android 13, ARM64, Unity 6000.3.14f1 IL2CPP development APK에서 확인했다.
물리 Android 휴대폰 검증은 아니다. 로컬 ADB `127.0.0.1:5555`를 사용한다.

## 샘플 크기 고정

샘플에서 얼굴 거리 확대·축소 기능을 제거했다. on/off 옵션은 없다.
`PortVerification.SamplePresentation`이 모든 driver 뒤에서 `frame.move.s=1`을 적용한다.
좌우·상하 위치와 고개·표정·립싱크는 유지한다. 패키지 `tracking.cs`는 원본 계산을 그대로 유지하므로 upstream 매핑에 예외를 추가하지 않는다.
PC/Mobile/WebGL이 이 샘플 코드를 공유한다.

`python3 scripts/unity/verify-bluestacks-player.py`는 APK 설치 후 생성 영상으로 실행한다.
중립 → 얼굴 검출 → 회전 → 유실 → 재검출 → 작은 얼굴 영상(0.8배) → 큰 얼굴 영상(1.2배)을 확인한다.
7개 상태 모두 캐릭터 배율 1, Z 회전 약 -14.2167도, 입력 source 해제까지 통과했다.
원거리/근거리 이미지를 직접 열어 크기 고정도 확인했다. 실제 사람의 거리 이동을 통제한 측정은 아니다.

- APK: `unity/TestResults/Android/LivLivVerification.apk`, 63,264,350 bytes.
- CLI build: errors/warnings 0, 48.525초. APK native libraries 9개, ARM64/16KB 정렬/권한/모델 payload 검사 통과.
- 결과·APK/소스 SHA-256: `unity/TestResults/bluestacks/fixed-size-smoke/evidence.json`.
- 스크린샷: 같은 폴더의 `neutral/detected/rotated/farther/nearer.png`.
- 기존 driver 원본 비교: .NET `DriverParityTests` 8 passed. upstream 25 modules, issues 0.

## 카메라와 마이크

이전 APK(63,263,340 bytes)에서 BlueStacks가 연결한 Mac 카메라·마이크를 동시 실행했다.
`live-inputs.log`에 `microphone=true`, `tracking=true`, `faceSeen=true`, 변하는 고개 각도와 mouth/vowel 값이 기록됐다.
`live-inputs.png`에서 활성 상태와 렌더링을 확인했다. 앱의 Stop 버튼으로 두 입력이 중단된 것도 확인했다.
BlueStacks 볼륨 팝업의 microphone 토글을 켜야 실제 PCM이 들어왔다. 그 설정은 현재 켜져 있다.
이 증거를 물리 Android 기기 또는 통제된 음성 정확도 검증으로 해석하지 않는다.

## 실행 제약과 남은 검증

- 기본 Vulkan은 emulator `vulkan.default.so`의 `vk_common_SetDebugUtilsObjectNameEXT`에서 SIGSEGV가 났다. `vulkan-crash.log`에 기록했다.
  이 환경에서는 Android intent의 Unity 인수 `-force-gles30`으로 OpenGLES3를 사용한다. 앱의 전체 그래픽 기본값은 바꾸지 않았다.
- 투명 OnGUI 합성의 이전 프레임 잔상을 없애기 위해 샘플 scene에 배경을 지우는 Camera를 추가했다. GPU 모델 RT의 clear 문제와 구분한다.
- 첫 권한 요청의 pause 취소 문제는 아래 권한 검증 단계에서 수정했다. iOS/물리 Android 검증은 남아 있다.
- 에뮬레이터 viewport 회전과 Android 화면 방향이 어긋나는 경우가 있었다. 세로/가로·실기기 회전 검증은 미완료다.
- 최신 크기 고정 APK에서는 생성 영상 smoke를 재실행했다. 실제 카메라·마이크 동시 실행 증거는 위의 이전 APK에서 수집한 것이다.

UGUI 전환 후 최신 APK(63,259,353 bytes)에서 같은 7단계 smoke를 다시 통과했다. 실제 UGUI와 카메라 추적은 [UI.md](UI.md)에 별도 기록했다. 위 63,264,350-byte 결과는 UGUI 전환 직전 단계다.

## 권한 dialog 복귀 수정

63,264,674-byte APK에서 공통 권한 preflight와 장치별 foreground gate를 적용했다.
BlueStacks에서 테스트 앱의 권한과 재요청 플래그를 초기화하고 UGUI로 요청했다.
카메라/마이크 각각 native 허용 창을 닫은 뒤 추가 Start 없이 Active로 전환했다. 거부 시 오류 안내와 Off 상태도 확인했다.
마이크 실행 중 Home으로 나가면 입력이 해제되고, 같은 프로세스로 복귀해도 Off를 유지했다.
증거: `unity/TestResults/bluestacks/permissions/`의 단계별 JSON/log와 `evidence.json`의 APK/소스 hash.
실행 중인 다른 입력은 권한 창 pause에서도 닫으며 자동 복구하지 않는다. [수명주기 정책](CAPTURE-LIFECYCLE.md) 참고.

## 음성 재생 추가 검증 — 2026-11-09

`verify-bluestacks-speech.py`로 별도 CLI 생성 PCM probe를 실행했다. ARM64 IL2CPP/GLES3에서
음성 3개 순차 재생의 start/end hooks, 실제 Unity audio mix 출력, 캐릭터 mouthOpen,
stop/리소스 해제와 scale=1을 확인했다. 카메라·마이크를 열지 않았으며, 완료 후 일반 샘플로 복귀했다.
APK 73,499,549 bytes, build errors/warnings 0. 세부 수치·범위는 [SPEECH.md](SPEECH.md),
실행 증거는 `unity/TestResults/bluestacks/speech/evidence.json`이다. 이전 권한/UGUI 검증 APK와 구분한다.

후속 decoder 검증에서는 PCM16 WAV·MP3·float WAV 바이트를 실제 native decoder로 재생했다.
세 입력의 순서·audio mix·renderer mouth·stop·출력 리소스 해제·scale=1을 통과했다.
현재 APK는 63,316,194 bytes이고 build errors/warnings 0이다. 위 `speech/evidence.json`은
이 후속 실행과 fixture/decoder 소스 hash로 갱신됐다. 상세 수치는 [SPEECH.md](SPEECH.md)에 있다.

## 카메라·마이크 동시 입력의 반복 background/return

후속 APK 53,436,206 bytes에서 동시 입력 → Home → 동일 프로세스 복귀 → 명시적 재시작을
3회 통과했다. 최초 상태 포함 10개 상태 모두 scale=1이며, Android AppOps로 카메라/마이크의
실제 running/종료도 확인했다. 단순히 Unity의 활성 표시만 검사한 결과가 아니다.
증거: `unity/TestResults/bluestacks/lifecycle/1794211150325729000/`.
이 검증은 BlueStacks가 전달하는 실제 입력을 사용했지만, 물리 모바일이나 장기 메모리 안정성
검증은 아니다. 실행 방법과 WebGL의 별도 검증 범위는 [CAPTURE-LIFECYCLE.md](CAPTURE-LIFECYCLE.md).
