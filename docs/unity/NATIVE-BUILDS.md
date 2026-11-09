# 네이티브 CLI 빌드와 실행

로고·텍스트 경계 후속을 포함한 전체 7개 CLI 대상은 errors/warnings 0으로 갱신했다.
iOS는 `unity/TestResults/build-refresh/1794227437302436000/ios-export`로 별도 export했고,
같은 실행의 `ios-derived`에서 unsigned Xcode link/ARM64·심볼·자산·metadata 감사를 통과했다.
앱 174,407,432 bytes, Xcode error 0/warning 11줄이다. 기존 사용자 서명 작업 공간은 보존했고
물리 iOS 실행은 요청대로 보류했다. `unity/TestResults/mark-boundary-final/{evidence,ios-app,xcode-summary}.json`.
이하 크기와 기본 경로는 이전 시점의 검증 기록이다.

최신 컨텍스트 유실/로드 취소 수정과 IsSupported를 포함한 macOS ARM64 및 Android ARM64 빌드를 갱신했다.
둘 다 errors/warnings 0이며 macOS 19.60초, Android 49.85초다.
Metal/BlueStacks GLES3의 지원 검사·명시적 유실 통지·동기/비동기 로드 거절·해제와 기존 표시 회귀가 통과했다.
실제 하드웨어 유실 검증은 아니다. 범위와 증거는 [LOADING.md](LOADING.md)를 따른다.

Windows x64 Mono와 Linux x64 IL2CPP의 후속 빌드·실행 및 필수 런타임은 [DESKTOP.md](DESKTOP.md)에 기록했다.

macOS universal 디코더·x64 Rosetta 실행·최소 OS의 최신 증거는 [MACOS.md](MACOS.md)에 있다.

## macOS ARM64

Unity 6000.3.14f1의 `mac-il2cpp` 모듈을 CLI로 설치했다. `VerificationBuild.MacOS`는
ARM64/IL2CPP development 앱을 만들고, Aoi ZIP·얼굴 모델·검증 사진을 Resources에 포함한다.
모든 입력 버튼은 명시적으로 눌러야 시작한다. 자동 검증은 `-livlivSmoke <output>` 인수가 있을 때만
생성 영상으로 실행한다. 캐릭터/사진은 검증 빌드용이며 UPM 제품 패키지에 배포하지 않는다.

```sh
unity install-modules --editor-version 6000.3.14f1 --architecture arm64 --module mac-il2cpp --yes
unity build ./unity --target StandaloneOSX --execute-method VerificationBuild.MacOS \
  --output-path "$PWD/unity/TestResults/MacOS/LivLivVerification.app"
python3 scripts/unity/verify-macos-player.py
```

실제 `OSXPlayer` / Metal 앱의 exit 0과 다음 상태를 확인했다.

- Aoi 첫 프레임과 WebP 로드, raw RenderTexture PNG 저장.
- 실제 CPU 얼굴 추론 → 공통 C# driver → renderer. 생성 영상 회전이 angleZ 약 −14.22°로 반영됐다.
- 얼굴 유실/어두움, 재검출, 입력 source 해제.
- Info.plist의 camera/microphone usage description과 실행파일 ARM64 아키텍처 확인.
- `TestResults/macos-smoke/native-player.json`, `evidence.json`, `player.log`, neutral/detected/rotated PNG.
  rotated PNG는 직접 열어 확인했다.

최신 Unity 빌드 요약은 errors 0 / warnings 0이다. native에서 사용하지 않는 WebGL 필드는
조건부 컴파일로 옮겼다. 첫 native 링크 로그의 Unity baselib deployment 경고는 기록으로 유지하며
현재 앱의 플랫폼 최소 버전/실행 증거와 구분한다.
앱 Info.plist의 최소 OS는 MediaPipe dylib 요구사항에 맞춰 13.0으로 수정했다. 이전 12.0 선언은 지원 근거가 아니다. 현재 호스트에서 실행했으며 macOS 13.0 자체의 실행 검증은 아니다.
Unity 종료 로그의 allocated-memory 통계가 남으므로 장기 누수 검증이 완료됐다고 주장하지 않는다.
실제 웹캠·마이크 권한, 오디오와 카메라 동시 입력, background/resume는 아직 검증하지 않았다.

## Android ARM64 준비

`prepare-webp-android.sh`는 Unity의 NDK r27c(27.2.12479018)로 libwebp 1.6.0 shared decoder를 빌드한다.
source/결과 hash는 `ThirdParty/libwebp-android.json`, Android-only importer는 ARM64로 제한한다.
라이브러리의 최소 API는 24이고 현재 Unity player의 최소 API는 25다.
`ANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON`으로 빌드하고 실제 ELF LOAD alignment 0x4000을 확인했다.

MediaPipe AAR의 OpenCV는 `DT_NEEDED libc++_shared.so`를 명시한다. 같은 Unity NDK의 런타임과
NOTICE.toolchain을 `ai.livliv.mediapipe` 패키지에 포함했다. `prepare-android-runtime.mjs`로 복원하며
`ThirdParty/ndk-runtime.json`의 SHA-256을 확인한다. 런타임 ELF도 LOAD alignment 0x4000이다.
Android JNI 모듈은 Unity player 패키지 dependency에 명시하여 권한 API가 player target에서 컴파일되도록 했다.

```sh
scripts/unity/prepare-webp-android.sh
node scripts/unity/prepare-android-runtime.mjs
unity build ./unity --target Android --execute-method VerificationBuild.Android \
  --output-path "$PWD/unity/TestResults/Android/LivLivVerification.apk"
python3 scripts/unity/check-android-apk.py
```

APK 검사는 native 라이브러리 포함·ARM64·16KB ELF/ZIP 정렬·camera/mic permission 선언을 대상으로 한다.
실기기 검증과 동일하지 않다. 현재 `adb devices -l`에 연결된 장치는 없다.

2026-11-09 실제 APK 검사 통과: errors/warnings 0, APK **61,564,817 bytes**.
Unity build summary의 497,674,152 bytes는 부가 빌드 산출물을 포함하며 APK 파일 크기와 다르다.
ARM64 라이브러리 9개(`liblivlivwebp`, `mediapipe_jni`, `opencv_java4`, `libc++_shared`, Unity/IL2CPP 등),
WebPGetInfo/WebPDecodeRGBAInto 심볼, CAMERA/RECORD_AUDIO, 모든 ELF LOAD 16KB 이상과 zipalign -P16을 확인했다.
`TestResults/android-apk.json`과 `android-build.json`에 결과를 기록했다.
최초 APK에서 누락된 WebP는 PluginImporter 설정 수정 후 다시 빌드해 포함을 확인했다.

## iOS 준비

현재 원본 기준 d999dea/Player.Expressions·캡처 상태/샘플 정책을 별도 경로
`unity/TestResults/upstream-latest/ios/export`로 갱신했다. 기존 서명 준비용 `TestResults/iOS`는 유지했다.
Unity export errors/warnings 0, 10.725초, Unity 보고 958,098,216 bytes.
Xcode generic iPhoneOS Debug의 CODE_SIGNING_ALLOWED=NO 빌드/링크도 성공했다(error 0, warning 11줄).
실제 unsigned 앱은 174,348,756 bytes이며 ARM64·최소 iOS 15.0·WebP/MediaPipe 심볼/embedding,
모델·폰트·portrait·WAV/MP3·JPEG 자산과 export/app metadata 바이트 일치를 확인했다.
파일 감사는 core/UGUI/native Runtime C#와 샘플 소스·compatibility 기준 해시도 함께 기록한다.
이는 실기기 실행 증거가 아니며 사용자가 요청한 iOS 실기기 검증 보류를 유지한다.
증거: `unity/TestResults/upstream-latest/ios-export.log`, `ios/{export-summary,xcode-summary,app}.json`,
`ios/xcode.log`, `ios/app-audit.log`. 아래 수치는 각각 이전 빌드의 이력이다.

최신 컨텍스트 유실/로드 취소/IsSupported/JPEG 코드를 별도 경로 `unity/TestResults/context-loss/ios/export`에 export했다.
기존 `unity/TestResults/iOS` Xcode 프로젝트 대신 이 새 프로젝트를 unsigned Debug로 link했다.
Unity 6000.3.14f1 export는 errors/warnings 0, 11.85초, 소스 프로젝트 957,832,560 bytes다.
Xcode 16.4/iPhoneOS SDK 18.5는 **BUILD SUCCEEDED**, errors 0 / warning 11줄이다.
실제 앱은 174,281,364 bytes이며 ARM64·최소 iOS 15.0·WebP/MediaPipe 심볼/embedding,
모델/음성/폰트/portrait/JPEG 두 fixture의 실제 포함 바이트와 export metadata 일치를 확인했다.
JPEG 표시/해제나 카메라·마이크의 iPhone 실행 증거는 아니다. 실기기 검증은 사용자의 요청으로 보류한다.

최신 증거는 `unity/TestResults/context-loss/ios/`의 `export.log`, `export-summary.json`, `xcode.log`,
`xcode-summary.json`, `app.json`이다. 앱과 소스 SHA-256은 마지막 JSON에 포함한다.

```sh
python3 scripts/unity/check-ios-app.py \
  --derived-data unity/TestResults/context-loss/ios/derived \
  --export unity/TestResults/context-loss/ios/export --output unity/TestResults/context-loss/ios/app.json
```

이하의 기본 출력 경로와 이전 크기 수치는 JPEG 추가 전 검증 이력이다.

`prepare-webp-ios.sh`: Xcode 16.4 / iPhoneOS SDK 18.5, ARM64, deployment target 15.0의
libwebp 1.6.0 static decoder를 빌드했다. iOS-only importer와 source/artifact hash를 기록한다.
Unity CLI export 후 실제 Xcode iPhoneOS build/link까지 성공했다. Xcode 생성 코드의 deprecated API/
run-script output 경고는 남아 있으며, 서명 없이 빌드했다. Unity export의 errors/warnings는 0이다.

```sh
scripts/unity/prepare-webp-ios.sh
unity build ./unity --target iOS --execute-method VerificationBuild.iOS \
  --output-path "$PWD/unity/TestResults/iOS"
xcodebuild -project unity/TestResults/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Debug -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath unity/TestResults/ios-derived CODE_SIGNING_ALLOWED=NO
python3 scripts/unity/check-ios-app.py
```

이전 실제 `.app`는 173,965,860 bytes다(2026-11-09 갱신). app/UnityFramework/MediaPipe framework ARM64,
WebPGetInfo/WebPDecodeRGBAInto 링크 심볼, MediaPipe의 @rpath 의존성과 framework embedding,
최소 iOS 15.0, camera/mic usage description, Resources의 원본 model/캐릭터 ZIP 바이트를 확인했다.
`TestResults/ios-app.json`에 검사 결과, `ios-xcode.log`에 실제 Xcode build 로그를 저장했다. 서명·설치·iPhone 실행은 미검증이다.

### JPEG 추가 전 iOS 갱신 이력

Speech 상태 알림, UGUI 카메라 해상도 수정 등 현재 코드를 다시 export/link했다.
Unity CLI export는 errors/warnings 0, 12.96초다. Unity 보고 크기 956,854,931 bytes는
Xcode 소스 프로젝트 산출물이며 위 `.app` 크기와 다르다.
Xcode 16.4 / iPhoneOS SDK 18.5의 unsigned Debug build는 **BUILD SUCCEEDED**, error 0,
warning 11줄이다. 경고는 Unity 생성 C++/Objective-C의 deprecated API, 미사용 변수,
CameraCapture delegate 구현 경고와 GameAssembly script output 선언이다. 이를 무경고 빌드라고 기록하지 않는다.

강화한 `check-ios-app.py`는 다음을 검사한다.

- 실행 파일·UnityFramework·MediaPipe의 정확한 ARM64 아키텍처.
- 각 Mach-O의 최소 OS가 앱의 iOS 15.0 선언보다 높지 않음.
- WebP 두 진입 심볼, MediaPipe TaskRunner 진입 심볼과 framework embedding/link 경로.
- 실제 Resources의 캐릭터 ZIP·face task·폰트·portrait·MP3·float WAV 원본 바이트 포함.
- 앱의 IL2CPP metadata가 이번 Unity export의 metadata와 동일함.
- 실행 바이너리·metadata·resources/sharedassets 및 포함 입력의 SHA-256.

현재 로그는 `ios-current-export.log`, `ios-current-xcode.log`, `ios-current-build-summary.json`,
`ios-app.json`이다. 이전 app/build JSON과 Xcode 로그는
`ios-build-history/1794215463328804000/`에 보존했다.
원본 매핑 검사는 `ios-current-upstream.log`에 25모듈/22fixture, issue 0으로 남았다.

### 실기기 준비 상태

현재 iPhone 13 mini가 paired/available로 조회된다. 읽기 전용 확인 결과 OS는 27.0.1,
Developer Mode는 enabled다. 다만 개발 디스크 이미지를 마운트하지 못해 DDI services가 false이고,
Mac의 유효한 codesigning identity는 0개다. 설치된 Xcode는 16.4뿐이다.
기기 이름이 목록에 있다는 이유로 앱을 실행했다고 판단하지 않는다.
마운트 실패 원인을 특정 Xcode 버전 문제로 확정하지 않았으며, 해당 OS의 기기 지원 상태 확인이 필요하다.

사용자에게 Xcode의 기기 지원 오류 해결과 프로젝트의 개발 Team/서명 설정을 요청했으며,
2026-11-09 사용자가 iOS 실기기 검증은 나중에 진행하도록 지정했다. 실기기 검증은 보류한다.
서명 계정 생성·인증서 발급·Xcode/OS 업그레이드는 자동 수행하지 않았다.
아래 명령은 필요한 상태만 JSON으로 보존하며, serial/UDID/계정 이름을 결과에 저장하지 않는다.

```sh
python3 scripts/unity/check-ios-device.py --device <devicectl-device-id> --output unity/TestResults/ios-device-readiness.json
```

환경 준비가 안 됐으면 exit 2, 준비됐으면 exit 0이다. 이 preflight의 성공도 provisioning profile,
실제 서명·설치·입력 동작의 검증을 대신하지 않는다. 서명이 준비된 뒤 player를 설치하고
카메라/마이크 권한·동시 실행·background/resume·실제 GPU/음성 검증을 진행해야 한다.
