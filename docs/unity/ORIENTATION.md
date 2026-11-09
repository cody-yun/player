# Android UGUI 방향 전환 검증

## 후속 native 경계 및 캡처 검증

현재 기록: `android-orientation/1794235569502136000/` (TestResults 기준).
Activity.getRequestedOrientation와 Display.getRotation을 함께 읽었다. Android SDK 36의 실제
`android.jar`를 javap로 읽은 상수/해시와 분석은 `native-analysis.json`, `sdk-constants.txt`에 보존했다.

| Unity 요청 | Activity가 받은 값 | 기대 display rotation | 실제 display rotation |
| --- | --- | --- | --- |
| Portrait | 1 (PORTRAIT) | 0 | 0 |
| LandscapeLeft | 0 (LANDSCAPE) | 1 | 1 |
| PortraitUpsideDown | 9 (REVERSE_PORTRAIT) | 2 | 0 |
| LandscapeRight | 8 (REVERSE_LANDSCAPE) | 3 | 1 |

**4개 요청은 모두 Android Activity에 전달됐지만 실제 display rotation은 2개만 일치했다.**
요청 누락은 반증했으며 미적용 지점은 Android/가상 디스플레이 처리 경계로 좁혀졌다.
구체적인 BlueStacks/OS 정책 원인은 아직 확정하지 않는다. 네 방향 전체 결과는 계속 Failed다.

전환 직후의 Unity PNG가 깨지는 실행도 있었다(`1794235318414341000/`). 단순한 geometry 수치만으로
시각 합격을 선언하지 않았다. 후속 진단은 크기 변경 뒤 1.5초 더 기다리고, 2초 동안 Android 시스템
캡처를 수집한 다음 Unity GPU readback을 수행한다. 현재 실행의 Android 캡처 4장과 Unity 캡처 2장을
직접 확인했으며 이미지 깨짐·버튼 잘림·겹침·스크롤이 없었다. 반대 방향 단계의 Android 캡처는
요청한 반대 방향이 아니라 **관측된 기본 방향**의 화면이다. 두 캡처는 시간 차이가 있어 픽셀 동일성을
주장하지 않는다. 이 한 번의 정상 실행으로 전환 중 모든 프레임이나 GPU race의 영구 해결을 입증하지 않는다.

재현: `verify-android-orientation.py`는 네 방향 실패 시 exit 1을 유지한다.
`analyze-android-orientation.py <run-directory>`는 SDK 상수와 raw report를 비교하며 성공으로 덮어쓰지 않는다.
현재 Android build: `build-refresh/1794235499577576000/evidence.json`, errors/warnings 0.
APK: 53,311,102 bytes, SHA-256 `3cd58af6a590c170ea72867b50e51b8dbc70de4797d0b245832ef3610a3fe2f8`.
서명/ARM64/16KB·자산 감사와 `android-startup/1794235606867551000/`의 일반 실행 Ready/12초 생존 통과.
배포 Player 패키지는 변경하지 않았다. 아래는 최초 진단 구현과 이전 실행 기록이다.

샘플에 화면 픽셀 크기·현재 Unity 방향·표시 버튼 수·safe area 밖 버튼 수·버튼 겹침·스크롤 컴포넌트 수를
기록하는 진단을 추가했다. 렌더러/컨트롤러 패키지와 UI 배치는 변경하지 않았다.
Android 로그 메시지 제한 때문에 좌표 배열은 기록하지 않고 판정 수치만 남긴다.

`PortVerification.Orientation.cs`는 CLI 인자 `-livlivOrientationProbe <directory>`로만 시작한다.
실제 Unity 방향을 순서대로 요청하고 프레임 진행·화면 크기·입력 Off·배율 1·UGUI 영역을 검사한다.
네 방향이 전부 일치해야 전체 성공이다. 실패 방향에서도 관측 상태를 기록하고 다음 방향을 시험한다.
끝나면 샘플의 AutoRotation 정책을 요청하고 종료한다. host runner는 옵션 없는 일반 샘플을 재실행한다.
전체 UI PNG 캡처를 위해 검증 프로젝트에만 Unity Screen Capture module을 추가했다.

```sh
python3 scripts/unity/verify-android-orientation.py
```

## 최종 관측 결과

BlueStacks/OpenGLES3, `android-orientation/1794235092860377000/` (TestResults 기준).
**네 방향 전체 검사는 Failed다.** 두 방향만 검증됐으며 반대 방향 두 개의 원인은 미확정이다.

| 요청 | 실제 Unity 방향/크기 | 결과 |
| --- | --- | --- |
| Portrait | Portrait, 1080×1920 | 방향 일치, 버튼 6개, 잘림/겹침/스크롤 0; PNG 직접 확인 |
| LandscapeLeft | LandscapeLeft, 1920×1080 | 방향 일치, 버튼 6개, 잘림/겹침/스크롤 0; PNG 직접 확인 |
| PortraitUpsideDown | Portrait, 1080×1920 | 8초 내 요청 방향 미적용. 버튼 영역은 정상 |
| LandscapeRight | LandscapeLeft, 1920×1080 | 8초 내 요청 방향 미적용. 버튼 영역은 정상 |

BlueStacks 제한인지 Unity/앱의 방향 처리 문제인지는 이 결과만으로 확정하지 않는다.
센서 자동 회전, 네 방향 전체, 물리 Android/iOS의 safe area·장치 입력 중 회전은 미검증이다.
일반 실행 복구는 `android-startup/1794235115060208000/`에서 Ready/입력 Off/12초 생존을 통과했다.

최종 Android CLI 빌드: `build-refresh/1794235041966282000/evidence.json`, errors/warnings 0.
이번 QA 코드는 Android에서 컴파일/실행했다. 배포 라이브러리는 직전 package-source 단계와 동일하다.
APK: 53,307,622 bytes, SHA-256 `f458c75a0b65511eee5a25923cb1fed18ddcbbf7c4f7fac87186aa140171c591`.
서명/ARM64/16KB 정렬·자산·미참조 ZIP 영역 0 감사도 통과했다.

## OS 설정과 Unity 요청 구분

처음 `wm user-rotation`/`wm size`로 시험했을 때 Android 회전 메타데이터와 실제 캡처/Unity 방향이
일치하지 않았다. 그 결과를 Unity 방향 전환 성공으로 취급하지 않는다.
해당 실험은 `verify-android-display-settings.py`와 이전 `android-orientation/` 실패 기록에 남겨 뒀다.
변경한 user rotation/accelerometer/fixed-to-user/화면 크기는 원래 값으로 복원했다.
현재 검사는 OS 설정을 수정하지 않고 Unity 자체 요청을 사용한다.
