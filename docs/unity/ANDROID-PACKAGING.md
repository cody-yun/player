# Android 개발 APK 패키징 크기

마감 APK: **53,311,574 bytes**,
SHA-256 `bd663421814835fabdda2d17f6c88aaaf3b2594a9a646bd6472d8e9f21bc18f7`.
세로 방향 지정과 UGUI 실제 버튼 위치 진단을 포함한다. Android CLI build/서명·ARM64·16KB·자산 감사,
실시간 입력 Home/복귀/재시작 3회 및 일반 실행 Off 복구 통과. [HANDOFF.md](HANDOFF.md).
아래는 이전 빌드 기록이다.

최신 native 방향·안정화 캡처 진단 APK: **53,311,102 bytes**,
SHA-256 `3cd58af6a590c170ea72867b50e51b8dbc70de4797d0b245832ef3610a3fe2f8`.
Android CLI errors/warnings 0, APK 감사·일반 실행 복구 통과. 네 방향 전체는 Failed이며
요청 전달 4/4, display rotation 일치 2/4를 분리했다. [ORIENTATION.md](ORIENTATION.md).

이전 샘플 QA APK: **53,307,622 bytes**,
SHA-256 `f458c75a0b65511eee5a25923cb1fed18ddcbbf7c4f7fac87186aa140171c591`.
UGUI 방향/영역 진단과 Screen Capture module을 포함한다. CLI errors/warnings 0,
서명/ARM64/16KB/자산·미참조 ZIP 영역 0 감사 및 일반 실행 Ready/12초 생존 통과.
네 방향 검사는 2개 일치/2개 미적용으로 Failed다. [ORIENTATION.md](ORIENTATION.md).
아래 APK 수치는 이전 빌드 기록이다.

최신 파일 목록 ZIP 처리 통합 APK: **53,297,358 bytes**,
SHA-256 `7063cca85970dc5d163af8510453328650c5a4f3a70dd5e01ce2038499a2a898`.
Unity errors/warnings 0. 서명/ARM64/16KB/자산 및 미참조 ZIP 영역 0 감사 통과.
BlueStacks 기본 MAIN/LAUNCHER 실행 Ready/12초 생존을 확인했다. 이번 선택 ZIP 경로 자체의
GPU 검증은 Unity/Metal PlayMode와 설치 소비자에서 수행했으며 Android 실행 증거로 간주하지 않는다.
증거: `unity/TestResults/android-startup/1794232739099032000/`, `package-source/evidence.json`.
이는 실제 카메라/마이크 재검사나 물리 Android 실행 증거가 아니다. 이하 APK 수치는 이전 빌드 이력이다.

## 로고 경계 수정 후 최신 앱

로고 off-canvas 그림자 수정과 CLI 전용 추론 진단을 포함한 APK는 **53,269,418 bytes**,
SHA-256 `8415c0174d20d0ae2b8c9a74034766d0f90083da4ff0dfc4b6cb91de1660fefd`.
대상을 명시한 Unity CLI Android IL2CPP build errors/warnings 0, 27.419488초.
서명·ARM64·16KB 정렬·자산 감사 통과, 미참조 ZIP 영역 0 bytes.
BlueStacks 기본 GLES3의 생성 영상 10개 상태에서 회전/유실/재검출/오류 격리/재시작·배율 1·해제를 확인했다.
`unity/TestResults/bluestacks/fixed-size-smoke/1794227015900605000/`.
기본 MAIN/LAUNCHER 실행도 Ready/12초 생존·GLES3를 통과했고 UGUI 전체 화면을 직접 확인했다.
증거: `unity/TestResults/android-startup/1794227035758335000/`. 최종 입력은 Off 상태다.

이번 최초 실행은 `unity run -- -executeMethod VerificationBuild.Android`로 대상을 지정하지 않아
Android 전용 `#if UNITY_ANDROID` 패키징 callback이 에디터에 포함되지 않았다. 빌드 자체는 성공했지만
감사가 미참조 영역 12,021,011 bytes를 검출해 거절했다. 해당 APK는 설치하지 않았다.
실패의 `brand-clip/apk-packaging.json`, `first-apk-rejection.json`, `android-build.log`를 보존했다.
샘플 builder는 이제 active editor target과 요청 target이 다르면 생성물을 변경하기 전에 거절한다.
잘못된 호출의 실제 실패는 `brand-clip/wrong-target.log`와 `wrong-target-evidence.json`에 있다.
대상 지정 재실행에서 callback 로그와 0 bytes 감사를 확인했다. 라이브러리 패키지는 바꾸지 않았다.

재현 명령:

```sh
unity build ./unity --target Android --execute-method VerificationBuild.Android
python3 scripts/unity/check-android-apk.py
python3 scripts/unity/verify-bluestacks-player.py --require-expression-labels
python3 scripts/unity/verify-android-startup.py
```

아래 APK 수치와 기록은 이전 산출물 이력이다.

최신 원본 기준 d999dea와 Player.Expressions를 포함해 APK를 갱신했다.
IL2CPP CLI build errors/warnings 0, 42.851초, APK 53,260,542 bytes,
SHA-256 `384e1ceea913f23a512a3c39c9f4ccbecbebaea6fcbbb9139c26c27dc3b7a9f5`.
BlueStacks/OpenGLES3 생성 영상 검사 10개 상태에서 표정 개수 4와 대표 smile의 이름·영어 번역을 직접 읽었다.
회전/유실/재검출/입력 오류 격리/재시작·scale=1 및 두 source 각각 한 번 해제를 통과했다.
일반 MAIN/LAUNCHER 실행의 Ready/12초 생존, 서명·ARM64·16KB 정렬·포함 자산 감사도 통과했다.
실행 전 종료돼 있던 BlueStacks를 재실행했고 최종에는 일반 샘플 입력 Off 상태로 복구했다.
이는 생성 영상 검사이며 물리 Android 장치나 새 권한 창 검증은 아니다.
증거: `unity/TestResults/upstream-latest/players/`,
`bluestacks/fixed-size-smoke/1794222825975832000/`, `android-startup/1794222851061587000/`.
재현: 새 APK 빌드 후 `verify-bluestacks-player.py --require-expression-labels`, `verify-android-startup.py`.
아래 수치는 이전 빌드 각각의 이력이다.

기본 실행 수정 후 Android probe 스크립트 8개에서 `-force-gles30`을 제거했다.
새 APK의 기본 API로 Surface/카메라/마스크/텍스트/PNG·WebP·JPEG 포스터와 지원·유실 처리 회귀를 다시 통과했다.
증거: `unity/TestResults/bluestacks/presentation/1794220303832482000/`와
`unity/TestResults/android-startup-fix/default-presentation.json`.

## 기본 실행 그래픽 API 수정

BlueStacks 일반 실행이 Vulkan의 vk_common_SetDebugUtilsObjectNameEXT에서 SIGSEGV로 종료됐다.
CLI probe의 -force-gles30만으로 성공을 판단해 기본 실행 설정을 누락했던 문제다.
VerificationBuild.Android는 이제 기본 API 자동 선택을 끄고 OpenGLES3만 지정한다.
일반 MAIN/LAUNCHER intent에 Unity extras 없이 새 APK를 실행해 OpenGL ES·Ready·12초 생존과 실제 화면을 확인했다.
`python3 scripts/unity/verify-android-startup.py`로 같은 기본 실행 검사를 재현한다(BlueStacks 연결 필요).

수정 APK: 53,249,430 bytes, SHA-256 `c39682276a3d13d0851c1390fc0c1b58b098eaf454dfacbc0b03dba701251864`.
CLI build errors/warnings 0, 28.36초. 서명/ARM64/16KB/자산 감사도 통과했다.
증거: `unity/TestResults/android-startup-fix/`와 `android-startup/1794220207354946000/`.
후자의 evidence.json은 실제 APK 해시·인수 없는 실행 명령·빌드 소스 해시를 포함하고 screen.png는 실제 기기 화면이다.
이하 크기와 강제 API 실행 기록은 이전 산출물 이력이다.

2026-11-09, Unity 6000.3.14f1 / Android ARM64 / IL2CPP / Development.

## 원인과 수정

기존 APK는 85,540,877 bytes였지만 유효 ZIP member의 압축 크기 합계는 53,355,742 bytes였다.
local header, central directory, APK signing block을 분리해 조사한 결과,
central directory에서 참조하지 않는 영역이 **32,106,334 bytes** 남아 있었다.
큰 빈 영역에는 zipflinger의 빈 local-file-header 패턴이 반복됐다. 런타임 리소스 추가가 아니라
증분 APK 업데이트가 이전 entry의 빈 영역을 재사용하지 못하면서 생긴 패키징 증가로 판단했다.

샘플 전용 `VerificationAndroidPackaging`은 `VerificationBuild.Android()` 실행 중에만
`IPostGenerateGradleAndroidProject`에서 다음 두 생성물을 제거한다.

- `launcher/build/outputs/apk/debug/launcher-debug.apk`
- `launcher/build/intermediates/incremental/packageDebug`

이후 Gradle이 APK를 다시 패키징하고 서명한다. Unity/IL2CPP 및 Gradle 컴파일 캐시와
다른 플랫폼 빌드, 라이브러리 패키지 코드는 그대로 유지한다. 빌드가 실패해도 적용 플래그는
`finally`에서 해제된다. APK를 사후에 ZIP 재작성하거나 서명을 제거하지 않는다.
현재 샘플의 debug launcher 구조에 한정되며 AAB/release/export 프로젝트에 대한 기능은 아니다.
Unity 업그레이드로 경로가 달라지면 launcher 확인 또는 아래 감사에서 검출한다.

공식 callback은 Unity library 경로를 받고 Gradle 실행 전에 호출된다.
[Unity callback 문서](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/android/ipostgenerategradleandroidproject/onpostgenerategradleandroidproject),
[Android 빌드 순서](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/how-unity-builds-android-applications).
문서는 6000.6이며 실제 아래 검증은 6000.3.14f1이다.

## 검증

| 산출물 | 실제 APK bytes | 미참조 영역 bytes | 결과 |
|---|---:|---:|---|
| 수정 전 | 85,540,877 | 32,106,334 | 새 감사에서 의도대로 거절 |
| 수정 빌드 1 | 53,434,562 | 0 | 성공, errors/warnings 0 |
| 동일 경로 반복 빌드 2 | 53,434,594 | 0 | 성공, errors/warnings 0 |

약 32.11 MB(37.5%) 감소했다. 두 번의 빌드 사이 크기 차이는 32 bytes로,
비트 단위 재현 빌드를 보장한다는 의미는 아니다. 각각 423개 member 중 420개 payload가 이전과 동일하며,
변경된 member는 재생성되는 `boot.config`, `level0`, `unity_app_guid` 세 개다.
네이티브 라이브러리 9개와 모델/폰트/MediaPipe 리소스는 이전 payload를 유지했다.

최종 APK SHA-256:
`5774d9e14ac743dca1e1ac688f6d242bbd2bf76e8638a5fd49cfb1a89dcde204`.
APK v2 서명, ARM64 ELF 16KB alignment, ZIP 16KB alignment, 카메라/마이크 권한과
모델/MediaPipe 자산 바이트 감사 통과. 두 CLI 빌드 소요는 29.12초 / 28.69초였다.
BuildReport의 bytes는 심볼 등 다른 빌드 산출물도 포함하므로 위 APK 파일 크기와 구분한다.

BlueStacks GLES3에 최종 APK를 실제 재설치하고 일반/좁은 눈 각각 1,441프레임을 실행했다.
통계뿐 아니라 export JSON 전체 값이 원본 fixture와 일치했다. 이후 일반 UGUI 샘플로 복귀했다.
이는 합성 입력의 실행 회귀이며 실제 카메라/마이크나 물리 Android 장치의 재검증은 아니다.

증거: `unity/TestResults/apk-packaging/{before-audit,build-1,build-2,latest}.json`,
`{build-1,build-2}.log`, `unity/TestResults/android-apk.json`,
`unity/TestResults/bluestacks/trace/1794210681867211000/{evidence,trace-record-parity}.json`.

## 재현 및 회귀 방지

```sh
/Users/cody/.unity/bin/unity build ./unity --target Android \
  --execute-method VerificationBuild.Android \
  --output-path "$PWD/unity/TestResults/Android/LivLivVerification.apk" \
  --log-file "$PWD/unity/TestResults/android-build.log" --no-tail
python3 scripts/unity/check-android-apk.py
python3 scripts/unity/verify-native-trace.py android
```

`check-android-apk.py`는 서명 검사와 함께 `check-apk-packaging.py`를 호출한다.
후자는 central directory가 참조하는 local entries와 signing block을 제외한 미사용 공간이
256 KiB를 넘으면 실패한다. 이 한도는 해당 검증 APK용이며 모든 APK 포맷의 일반 규칙은 아니다.
`--compare 이전보고서.json`으로 각 entry의 SHA-256과 추가/삭제/변경 항목을 비교할 수 있다.
