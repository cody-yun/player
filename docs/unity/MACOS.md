# macOS ARM64 / x64 — 2026-11-09

로고·텍스트 경계 후속을 포함한 ARM64/x64 IL2CPP 앱을 errors/warnings 0으로 갱신했다.
각각 Metal 및 Rosetta/Metal에서 표시/텍스트/마스크/카메라 surface/포스터 회귀를 통과했다.
증거: `unity/TestResults/macos-presentation/1794227508140245000/`,
`macos-intel-presentation/1794227537136633000/`, `mark-boundary-final/evidence.json`.
이 실행은 생성/파일 입력이며 물리 Intel 또는 실제 장치 입력 재검증은 아니다. 아래 수치는 이전 빌드 이력이다.

최신 ARM64 IL2CPP 앱도 원본 d999dea/Player.Expressions로 갱신했다.
CLI errors/warnings 0, 3.784초, Unity 보고 645,349,622 bytes.
ARM64/Metal의 10개 상태에서 표정 개수 4와 대표 smile의 이름·영어 번역, source 오류 격리/재시작/한 번 해제와 scale=1을 확인했다.
생성 입력을 사용했으며 물리 카메라·마이크 검증은 아니다.
증거: `unity/TestResults/macos-smoke/1794223260989341000/`, `upstream-latest/desktop/macos-arm-*`.

원본 d999dea의 Player.Expressions를 포함한 x64 IL2CPP 앱도 갱신했다.
CLI build errors/warnings 0, 21.363초, Unity 보고 651,454,169 bytes.
Apple Silicon/Rosetta에서 x86_64 실행을 강제해 Metal·네이티브 추론의 10개 상태를 통과했다.
모든 상태에서 Expressions의 개수 4와 대표 smile의 이름·영어 번역을 직접 읽고, source 오류 격리/재시작과 해제를 확인했다.
scale=1도 유지했다. 이는 물리 Intel Mac의 GPU/카메라/마이크 검증이 아니다.
증거: `unity/TestResults/macos-intel-smoke/1794222881925749000/`,
`upstream-latest/players/macos-intel-*`. 재현은 새 앱 빌드 후
`verify-macos-player.py --architecture x86_64 --require-expression-labels`다.

최신 ARM64 IL2CPP/Metal 샘플을 ICaptureStatus 및 입력 오류 처리까지 포함해 갱신했다.
CLI build errors/warnings 0, 11.306초, Unity 보고 645,330,882 bytes.
생성 portrait 추적 검사도 7개에서 10개 상태로 늘려 source 오류 → 추적 입력만 종료 → 새 source 재검출을 확인했다.
실패 source와 재시작 source는 각각 한 번 해제됐고, 모델/렌더 프레임과 scale=1을 유지했다.
이 검사는 물리 카메라·마이크나 실제 OS 장치 제거를 사용하지 않는다. 이후 x64 갱신 결과는 위에 기록했다.
재시작 후 PNG도 직접 확인했다. 증거: `unity/TestResults/macos-smoke/1794221966161144000/`,
`capture-status-desktop/macos-build.{log,json}`, `macos-face.{log,json}`.

아래 universal/x64 검증은 각각 당시 빌드의 이력이다.

libwebp 디코더를 ARM64 전용에서 ARM64/x86_64 universal dylib로 교체했다.
Intel용 IL2CPP 앱에서 Metal 렌더·얼굴 추론·음성 재생을 실행했다.
실행 호스트는 Apple M3 Max의 Rosetta이므로 물리 Intel Mac의 GPU/장치 검증과 구분한다.

## 바이너리와 최소 OS

- libwebp 1.6.0 decoder-only, 소스 SHA-256
  `e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564`.
- Xcode 16.4 (16F6), SDK 15.5, Apple Clang 17.0.0, CMake 4.0.0.
  두 아키텍처를 개별 빌드 후 lipo 결합·ad hoc 서명한다.
- universal artifact SHA-256
  `4616310cb4bd97fa2bcca3a2c13954a7719fba0725e18481911eb1947b181453`.
  두 CMake 빌드 디렉터리를 삭제하고 재빌드해 동일 해시를 확인했다.
- `ThirdParty/libwebp-macos-universal.json`에 현재 출처와 슬라이스 해시를 기록했다.
  `libwebp.json`은 이전 Homebrew ARM64 artifact의 이력이며 현재 바이너리 해시가 아니다.
- homuler MediaPipe의 기존 universal dylib는 **두 슬라이스 모두 macOS 13.0 이상**을 요구한다.
  샘플의 기존 12.0 설정을 13.0으로 올렸고 실제 앱 plist도 확인한다.
  MediaPipe 패키지를 사용하는 다른 호스트 앱도 같은 최소 OS를 적용해야 한다.
- `.meta`는 OSX Editor/Standalone에서 AnyCPU로 설정했다. 다른 플랫폼 활성화는 유지한다.

`macos_player.py`는 앱 실행 파일·GameAssembly·UnityPlayer·플러그인의 아키텍처와
카메라/마이크 설명, 최소 OS, exported symbols, 플러그인 서명을 검사한다.
WebP는 패키지 원본 바이트와 완전히 같아야 한다.
Unity는 기존에 서명되지 않은 MediaPipe x64 슬라이스에 ad hoc 서명을 추가한다.
이 경우 임시 복사본에서 서명과 그로 인해 커진 `__LINKEDIT.vmsize`만 정규화하고,
두 슬라이스의 나머지 바이트 전체가 원본과 일치하는지 검사한다.
원본/포함 파일의 원시 해시와 정규화 해시 모두 실행 증거에 남긴다.
이는 배포용 Developer ID 서명/notarization 검증이 아니다.

## x64 실행 증거

Unity 6000.3.14f1 CLI, x64 IL2CPP build: errors/warnings 0,
41.71초, Unity 보고 크기 649,773,573 bytes (개발 빌드 산출물).
실행 파일·GameAssembly·UnityPlayer가 실제 x86_64인지 확인한 후
`/usr/bin/arch -x86_64`로 실행했다. 모든 probe exit 0.

- 얼굴 7개 상태: 생성 portrait 영상의 CPU 추론, 회전 -14.2202°,
  유실·재검출·해제. 거리 변경을 포함해 scale=1.
- 표시 7개 상태, 눈 마스크 8개, 텍스트 8개: 타깃 재사용·DPR·예산·0 크기 복구,
  PNG alpha 왕복 오차 최대 0.49804/255, PNG/WebP poster 및 취소/오류 처리.
  소유한 target/material/poster/mask/text 리소스가 종료 후 0.
  별도 픽셀 검사로 한글/영문 텍스트가 지정한 우하단 영역에만 나타남을 확인했다.
- 음성: PCM16 WAV/MP3/float WAV 순서·입 동작·중단·해제,
  상태 이벤트 `[false,true,false]`, peak 0.00353553, mouth 0.98860163,
  남은 piece/clip 0. 시스템 출력의 실제 청취나 마이크 검증은 아니다.

Git 제외 로컬 증거는 `unity/TestResults/` 아래에 있다.

- `macos-intel-build.json`, `macos-intel-build.log`
- `macos-intel-smoke/1794214390297435000/evidence.json`
- `macos-intel-presentation/1794214421197822000/evidence.json`, PNG, `text-mark-pixels.json`
- `macos-intel-speech/1794214445007058000/evidence.json`
- `webp-macos-prepare.log`, `webp-macos-rebuild.log`, `webp-macos/provenance.json`

## ARM64 회귀와 픽셀 비교

같은 universal 디코더로 ARM64 IL2CPP 앱도 재빌드했다(errors/warnings 0,
643,681,530 bytes). ARM64 Editor 그래픽 62개가 통과했다. 이 테스트에는
21개 제공 모델의 모든 이미지 로드/해제, alpha/orientation, 3모델×4 동적 GPU 프레임,
눈 마스크·텍스트·워터마크·로딩 회귀가 포함된다.
새 ARM64 player의 얼굴 7개 상태(회전 -14.2140°), 표시 7개·마스크 8개·텍스트 8개도 통과했다.
음성 3종·립싱크·중단·해제·상태 알림 `[false,true,false]`도 재검증했다.

이전 ARM64 디코더의 `macos-presentation/1794209012166567000`을 고정 기준으로,
새 ARM64와 x64의 표시 PNG 각각 21장을 비교했다. 양쪽 모두 RGBA 전체 픽셀의 최대 차이가 0이다.
이 결과는 동일 M3 Max/Metal에서 해당 상태의 결과이며 타 GPU의 완전 일치를 뜻하지 않는다.

추가 로컬 증거:

- `macos-universal-graphics.xml`: 62 passed / failed 0 / skipped 0.
- `macos-universal-arm64-build.log`, `macos-build.json`.
- `macos-smoke/1794214575638681000/evidence.json`.
- `macos-presentation/1794214548195762000/evidence.json`, PNG 및 `text-mark-pixels.json`.
- `macos-speech/1794214595872633000/evidence.json`: ARM64 음성 회귀.
- `macos-universal-pixels.json`: 비교 이미지 경로·해시·최대/평균 채널 차이.
- `macos-universal-upstream.log`: 기준 25개 모듈 / fixture 22개, issue 0.

`check-macos-pixels.py --baseline <이전 표시 증거 폴더> --arm64 <새 ARM64 폴더> --x64 <새 x64 폴더> --output <json>`으로 비교를 재현한다(Pillow 필요).

## 재현

저장소 루트에서 Xcode command line tools/CMake와 활성화된 Unity CLI가 필요하다.
기존 모델·MediaPipe 준비는 [NATIVE-BUILDS.md](NATIVE-BUILDS.md)를 따른다.

```sh
scripts/unity/prepare-webp-macos.sh
node scripts/unity/check-native-artifacts.mjs
unity build ./unity --target StandaloneOSX --execute-method VerificationBuild.MacOSIntel --output-path "$PWD/unity/TestResults/MacOSIntel/LivLivVerification.app" --log-file "$PWD/unity/TestResults/macos-intel-build.log" --no-tail
python3 scripts/unity/verify-macos-player.py --architecture x86_64
python3 scripts/unity/verify-native-presentation.py macos --architecture x86_64
python3 scripts/unity/verify-macos-speech.py --architecture x86_64
```

ARM64는 `VerificationBuild.MacOS`, 출력 `MacOS/LivLivVerification.app`,
probe의 `--architecture arm64`를 사용한다. 기본값도 ARM64다.
재빌드가 고정 artifact 해시와 다르면 provenance를 자동 덮어쓰지 않고 실패한다.
원본 소스·도구 체인 변경을 검토한 뒤 의도적으로 갱신해야 한다.

남은 범위: 물리 Intel Mac, macOS 13.0 자체 실행, 실제 카메라·마이크 권한/장치 제거,
GPU별·장시간 성능, release signing/notarization. 전체 완료 게이트는 [PLATFORMS.md](PLATFORMS.md)를 따른다.
