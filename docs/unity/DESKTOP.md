# Windows / Linux 네이티브 검증 — 2026-11-09

로고·텍스트 경계 후속의 Windows/Linux CLI 빌드도 errors/warnings 0으로 갱신했다.
Linux Docker/Mesa에서 표시/텍스트/마스크/카메라 surface/포스터 회귀를 통과했다.
최신 Windows 전달 ZIP은 `unity/TestResults/mark-boundary-final/windows-transfer/LivLivWindowsVerification.zip`,
74,785,060 bytes, SHA-256 `2602e2f4b01443777253362f633bfb1195fbf23fbbf503b86d597e0515e84a24`.
압축 해제 후 282개 payload 무결성 검사는 통과했으나 Windows 실행 환경은 여전히 없어 runtimeExecuted=false다.
`unity/TestResults/mark-boundary-final/evidence.json`. 아래 ZIP과 크기는 이전 전달 빌드 기록이다.

사용자는 현재 Windows 실행 환경이 없다고 답했다. Windows runtime gate는 미검증으로 유지한다.
향후 실행용 `unity/TestResults/windows-verification/LivLivWindowsVerification.zip`을 준비했다.
최신 d999dea 빌드·소스/플러그인 감사와 실행 도구·282개 파일 SHA-256 manifest·라이선스를 포함한다.
압축 크기 74,785,546 bytes, SHA-256 `66d059d41302cba7ca1bfd9f4ebcbbbee5dadd779005149a9755fcfc2afa93ed`.
같은 입력으로 두 번 생성한 ZIP 해시가 일치했고 실제 압축 해제 후 전체 payload 무결성을 확인했다.

Python 3.10+의 `verify-windows-player.py`는 Windows에서만 플레이어를 실행한다.
기본 graphics API로 얼굴 10개 상태 → Surface/카메라/마스크/텍스트/포스터 → WAV/MP3/float WAV 음성·립싱크를
순서대로 검사하고 성공/실패 로그·JSON·PNG를 별도 ZIP으로 수집한다. 카메라/마이크 입력은 사용하지 않는다.
`--check-only`는 어떤 OS에서도 파일만 검사하고 `BundleVerified/runtimeExecuted=false`를 반환한다.
Mac 계약 검사 13개, 실제 Mac 결과 값의 공통 schema 대조, 비-Windows 실행 거절, 전달 후 파일 검사를 통과했다.
이는 Windows 실행의 대체 증거가 아니다. 자세한 실행법은 ZIP의 README 또는
`scripts/unity/windows-verification.md`. 증거는 `unity/TestResults/windows-verification/`에 있다.

원본 d999dea/Player.Expressions를 포함한 최신 PC 빌드를 갱신했다.
Windows x64 Mono는 errors/warnings 0, 2.856초, 런타임 파일 합계 181,152,320 bytes.
Linux x64 IL2CPP도 errors/warnings 0, 11.559초, 런타임 428,019,449 bytes다.
두 포함 파일 감사도 통과했다. Windows는 실제 실행이 아직 미검증이다.
Linux는 Docker/Mesa에서 10개 상태 모두 표정 개수 4와 대표 smile의 이름·영어 번역의 직접 읽기,
source 오류 격리/재시작·한 번 해제·scale=1을 통과했다. 물리 장치/실제 OS 장치 교체 검사는 아니다.
증거: `unity/TestResults/upstream-latest/desktop/`, `linux-face/1794223192157073000/`.
macOS ARM64/Metal의 같은 검사도 통과했다: `macos-smoke/1794223260989341000/`.
현재 원본 기준의 .NET 1,269/Unity EditMode 1,364/PlayMode 23도 failed/skipped 0.
아래 빌드·실행 수치는 각각 당시 소스의 이력이다.

ICaptureStatus·개별 입력 오류 처리·최신 샘플 수명주기를 두 대상의 CLI player에 반영했다.
Windows x64 Mono는 errors/warnings 0, 3.446초, 런타임 파일 합계 181,151,688 bytes다.
Linux x64 IL2CPP도 errors/warnings 0, 11.936초, 런타임 428,007,528 bytes다.
Unity 보고 크기와 런타임 폴더 합계는 별도로 기록한다. ARM64 호스트에서 만든 x64 산출물이다.
두 대상의 PE/ELF x64 아키텍처·다른 OS 플러그인 없음·입력 자산·변경 소스와 실제 관리 DLL/metadata 감사도 통과했다.

Linux는 기존 Docker Ubuntu 24.04/Xvfb/Mesa 환경에서 생성 portrait 영상의 10개 상태를 통과했다.
기존 회전/유실/재검출/거리 변화에 더해 source terminal 오류 → 추적 입력만 정리 → 새 source 재검출을 확인했다.
실패 source와 재시작 source는 각각 한 번 해제됐고, 실패 상태에서도 모델 Aoi와 렌더 프레임 진행·scale=1을 유지했다.
재시작 PNG도 직접 확인했다. 이는 실제 카메라/마이크·물리 GPU·OS 장치 제거 검증이 아니다.
Windows는 빌드/파일 감사까지이며 실제 실행과 D3D11 입력/표시 검증은 여전히 남아 있다.

증거: `unity/TestResults/capture-status-desktop/`의 각 build log/JSON·audit JSON,
`linux-face/1794222020306310000/`. macOS ARM64/Metal의 같은 10개 상태 검증은 [MACOS.md](MACOS.md).
아래 결과들은 각각 당시 빌드의 이력이다.

macOS ARM64/x64의 바이너리·최소 OS·Rosetta 실행 범위는 [MACOS.md](MACOS.md)에 있다.

Windows x64 Mono player 빌드·포함 파일 검사를 통과했다. Linux x64 IL2CPP player는
Docker의 x86_64 실행 환경에서 얼굴 추론·렌더·표시 어댑터까지 통과했다.
물리 Windows/Linux PC의 카메라·마이크·GPU 실행은 아직 검증하지 않았다.
원본 TypeScript는 유지한다. C# 정점 변형의 프레임 단위 설정 읽기 최적화는 아래 후속 빌드에 반영했다.

## 최적화 후 갱신

컨텍스트 유실/즉시 로드 취소/IsSupported 변경까지 Windows/Linux 빌드를 갱신했다.
두 빌드 모두 errors/warnings 0이며 Windows x64 Mono는 3.55초/런타임 181,138,860 bytes,
Linux x64 IL2CPP는 26.37초/런타임 427,848,140 bytes다.
실행 파일·필수 라이브러리·입력 자산·소스 해시와 Windows의 관리 DLL 감사를 통과했다.
Linux Docker/Mesa에서도 IsSupported=true, 지원 검사 probe 0개, 명시적 유실 알림 1회,
유실 후 동기/비동기 로드 거절 및 명시적 Dispose를 확인했다.
기존 Surface 7개/카메라 8개/마스크 8개/텍스트 8개와 PNG/WebP/JPEG 포스터 및 종료 리소스 조건도 통과했다.
증거: `unity/TestResults/context-loss/desktop/`와
`unity/TestResults/linux-presentation/1794219801346229000/`.
이는 실제 Linux GPU 리셋/입력 장치/HTTP 중단 검사가 아니며 Windows 실제 실행도 미검증이다.

이하 JPEG·최적화 직후 수치는 이전 소스의 검증 이력이다.

JPEG 추가 후에도 Windows/Linux를 다시 빌드하고 포함 파일 감사를 통과했다.
두 빌드 모두 errors/warnings 0이며 Windows x64 Mono는 3.71초/런타임 181,130,712 bytes,
Linux x64 IL2CPP는 12.40초/런타임 427,728,172 bytes다.
Windows의 LivLiv Runtime/Unity/UGUI/MediaPipe DLL 해시와 두 JPEG fixture의 실제 포함 바이트도 기록했다.
Linux는 동일 Docker/Mesa 환경에서 baseline JPEG data URL과 progressive JPEG file URL의
32×48 크기·색상/행 방향·알파·첫 프레임 해제를 확인했다. 기존 Surface 7개, 카메라 8개,
마스크 8개, 텍스트 8개와 PNG/WebP/오류/취소 회귀 및 종료 리소스 조건도 통과했다.
JPEG 실행 증거는 `unity/TestResults/linux-presentation/1794217978207208000/`,
빌드 요약/로그/파일 감사는 `unity/TestResults/jpeg/desktop/`에 있다.
Windows JPEG 실제 실행은 미검증이며 아래 최적화 직후 수치는 이전 빌드 이력이다.

정점 변형 최적화와 UGUI 카메라 해상도 수정을 포함해 두 대상의 CLI 빌드·포함 파일 감사를 다시 통과했다.
Unity 6000.3.14f1 기준 Windows x64 Mono는 errors/warnings 0, 4.24초이며 런타임 폴더는
181,124,671 bytes다. Linux x64 IL2CPP도 errors/warnings 0, 17.95초이며 런타임은
427,688,163 bytes다. Linux 소스/디버그 백업을 포함한 Unity 보고값은 820,334,681 bytes다.

Linux는 동일 Docker/Mesa 환경에서 얼굴 상태 7개를 통과했다. 생성 영상 회전은 −14.2143°,
유실/재획득과 거리에 따른 scale=1, 입력 해제를 확인했다.
표시 상태 7개, 눈 마스크 8개, 텍스트 8개, poster 회귀와 함께 UGUI 카메라 8개 상태
(front/right/left/up/down/orthographic/screen/nested)도 통과했다.
종료 후 probe 소유 target/material/poster/mask/text coverage 리소스 조건도 통과했다.

후속 증거:

- `unity/TestResults/post-optimization/desktop/build-evidence.json`: 실제 빌드 요약·파일 감사·현재 코어 소스 SHA-256.
- 같은 디렉터리의 `windows-build.log`, `linux-build.log`, 각 `*-audit.json`.
- `unity/TestResults/linux-face/1794217324274783000/`: 얼굴 실행 증거.
- `unity/TestResults/linux-presentation/1794217333949167000/`: 표시/카메라 실행 증거.

각 실행 증거에 실제 GameAssembly/metadata와 `puppet.cs` 해시를 포함했다.
Windows 실행과 물리 Linux GPU/카메라/마이크, 음성 출력 및 장시간 성능은 아직 미검증이다.
이하의 초기 빌드 수치와 이미지는 이전 소스에 대한 이력으로 보존한다.

## 빌드와 의존성

Unity 6000.3.14f1 ARM64 CLI에서 `windows-mono`, `linux-mono`, `linux-il2cpp` 모듈을 설치했다.
Windows 검증 빌드는 Mono, Linux는 IL2CPP다. Linux 선택 시 Unity가 추가한
`com.unity.sdk.linux-x86_64`와 `com.unity.toolchain.macos-arm64-linux` 1.1.1을 manifest/lock에 기록했다.
Windows IL2CPP는 이번 Mac 빌드의 검증 범위에 포함하지 않는다.

| 항목 | Windows x64 | Linux x64 |
|---|---|---|
| Unity build | Succeeded, errors 0 / warnings 1 | Succeeded, errors/warnings 0 |
| 개발 빌드 시간 | 11.88초 | 63.22초 |
| 런타임 파일 합계 | 181,097,742 bytes | 427,108,304 bytes |
| 별도 Unity 소스/디버그 백업 | 0 | 391,816,751 bytes |
| WebP decoder | `livlivwebp.dll`, 574,894 bytes | `liblivlivwebp.so`, 471,416 bytes |
| MediaPipe | homuler 0.16.3 x64 DLL | homuler 0.16.3 x64 SO |
| 실제 player 실행 | 미검증 | 생성 영상 및 표시 probe 통과 |

Windows warning 1개는 Mac 빌드 에디터의 기본 오디오 장치 변경/초기화 경고다.
Linux 전체 출력 폴더는 818,925,055 bytes이며 `BackUpThisFolder_ButDontShipItWithYourGame`을
포함한 값이다. 백업을 제외한 개발 빌드 크기도 release/stripping 최적화 크기와 구분한다.

`check-desktop-player.py`가 실행 파일·라이브러리의 PE/ELF x64 아키텍처, 플러그인 소스 SHA-256,
WebP/MediaPipe 진입 심볼, 모델 ZIP·face task·폰트의 실제 포함 바이트를 확인한다.
각 대상의 Plugins에 다른 OS 라이브러리가 들어가지 않았고, Linux GameAssembly/metadata도 확인했다.

Linux MediaPipe는 EGL/GLES 및 libstdc++를 요구한다. 바이너리에서 확인한 최대 요구 버전은
GLIBC 2.34 / GLIBCXX 3.4.32다. 현재 실행 검증 환경은 Ubuntu 24.04이며 더 오래된 배포판의
지원 완료를 뜻하지 않는다. WebP SO는 libc만 동적 의존한다.
Windows MediaPipe는 VC++ 런타임(`MSVCP140`, `VCRUNTIME140`, `VCRUNTIME140_1`, `CONCRT140`)과
Media Foundation(`MF`, `MFPlat`, `MFReadWrite`), D3D11/DXGI 등의 OS DLL을 요구한다.
WebP DLL의 동적 의존성은 KERNEL32/msvcrt다. 이 목록은 import 분석 결과이며 Windows 로딩 성공 증거는 아니다.

## Linux 실행 증거

Mac ARM64의 Docker Desktop/Rosetta x86_64, Ubuntu 24.04, Xvfb 1280×720,
Mesa 25.2.8 llvmpipe/LLVM 20.1.2, Unity OpenGLCore 4.5에서 실행했다.
컨테이너의 네트워크를 차단하고 물리 카메라·마이크를 전달하지 않았다.
오디오 장치가 없어 Unity가 nosound로 초기화하므로 음성 출력/장치 지연 검증도 아니다.

- 얼굴 probe 7개 상태: 내장 Aoi 로드, 생성 portrait 영상의 CPU 추론, Z 회전 -14.2309°,
  유실·재검출, 멀고 가까운 영상에서도 scale=1, 추적 입력 해제, exit 0.
- 표시 probe 7개 상태: 세로/가로, 동일 타깃 재사용, DPR cap, 10,000 pixel 예산,
  크기 0에서 해제 후 복구. PNG premultiplied 왕복 최대 오차 0.49804/255.
- 실제 UGUI PNG data URL / WebP file poster, 잘못된 이미지 오류, 늦게 도착한 포스터 무시.
  포스터 생명주기는 제어 가능한 PlayerBase host이며 HTTP 검증과 구분한다.
- 눈 마스크 8개 상태: 채널 2개, 화면 밖 생략, 크기 증가/재사용/복구, sample scale=1.
  Aoi 512×768에서 mask 저장량 1,572,864 → 98,304 bytes.
- 소유자 텍스트 8개 상태: 기본/사용자 폰트, 한글·영문·literal·DPR2·예산 오류.
  별도 픽셀 검사는 텍스트가 정해진 모서리에만 표시되는지 확인했다.
- 종료 후 probe 소유 target/material/poster/mask 및 text coverage 리소스 카운터가 모두 0.
  Unity 엔진 종료 시 자체 allocation 로그는 별도로 남으며 장시간 전체 메모리 누수 없음의 증거는 아니다.

Linux의 세로 캐릭터, 회전, 한글 소유자 PNG를 직접 열어 확인했다.
기존 WebGL PNG와의 mask 3종 비교는 채널 평균 오차 0.11771–0.16384/255,
최대 35–68/255다. 동일 Linux 상태의 타깃 재사용/축소/복구는 최대 0–1/255다.
이 수치를 기록하며 graphics API 간 픽셀 완전 일치를 주장하지 않는다.

`unity/TestResults/`의 로컬 산출물(Git 제외):

- `windows-build.json`, `windows-build.log`, `windows-player-audit.json`.
- `linux-build.json`, `linux-il2cpp-build.log`, `linux-player-audit.json`.
- `linux-face/1794212492663514000/evidence.json`, `native-player.json`, PNG 및 실행 로그.
- `linux-presentation/1794212507698358000/evidence.json`, `presentation-player.json`, PNG,
  `text-mark-pixels.json` 및 실행 로그.
- `native-mask-pixels.json`: 입력 이미지 SHA-256을 포함한 기존 WebGL/Mac/Android 및 Linux 비교.

얼굴/표시 증거의 GameAssembly SHA-256은
`b1492ec1d4ec04722d7692cd5e97ca8dce1d3b6d7941dcfde00fd3d27c14963a`,
metadata는 `dc590decf41eb146b34c0d44fb6aa4f573e853430750c44407ce40fd8f7cc231`다.
재빌드 뒤에는 새 산출물과 해당 실행 증거를 함께 갱신한다.

## 재현

저장소 루트에서 Docker가 실행 중이고 Unity 라이선스가 활성화된 상태로 실행한다.
기존 모델/portrait/MediaPipe 검증 자산 준비는 [NATIVE-BUILDS.md](NATIVE-BUILDS.md)를 따른다.

```sh
unity install-modules -e 6000.3.14f1 -a arm64 -m windows-mono linux-mono linux-il2cpp --yes --non-interactive
scripts/unity/prepare-webp-desktop.sh
node scripts/unity/check-native-artifacts.mjs
unity build ./unity --target StandaloneWindows64 --execute-method VerificationBuild.Windows --output-path "$PWD/unity/TestResults/Windows/LivLivVerification.exe" --log-file "$PWD/unity/TestResults/windows-build.log" --no-tail
python3 scripts/unity/check-desktop-player.py windows
unity build ./unity --target StandaloneLinux64 --execute-method VerificationBuild.Linux --output-path "$PWD/unity/TestResults/Linux/LivLivVerification.x86_64" --log-file "$PWD/unity/TestResults/linux-il2cpp-build.log" --no-tail
python3 scripts/unity/check-desktop-player.py linux
python3 scripts/unity/verify-linux-face.py
python3 scripts/unity/verify-native-presentation.py linux
```

`prepare-webp-desktop.sh`는 libwebp 1.6.0 소스 archive 해시와 Ubuntu base image digest를 고정하고
GCC/MinGW로 decoder만 빌드한다. 컴파일러/CMake/패키지 버전과 이미지 ID를
`ThirdParty/libwebp-{linux,windows}-x86_64.json`에 기록했다.
apt 저장소의 패키지가 나중에 바뀔 수 있으므로 새 이미지 빌드의 바이트가 다르면 기존 manifest를
덮어쓰지 않고 실패한다. 해당 compiler/provenance 검토 후 의도적인 갱신이 필요하다.
Windows PE timestamp는 비활성화했다. 플러그인 `.meta`는 해당 OS/x64 Editor·Standalone만 활성화한다.

## 해결한 검증 환경 문제와 남은 범위

첫 Linux Mono 빌드와 포함 파일 검사는 성공했다. `xvfb-run`의 준비 신호 대기가 Rosetta에서
진행되지 않아 전용 Xvfb wrapper로 바꿨고, 이후 Unity Mono 초기화에서
`x86-codegen.h:410 offset == (gint32)offset` assertion이 발생했다.
LivLiv scene 실행 전의 실패로, Linux 검증 backend를 IL2CPP로 전환한 뒤 위 probe가 통과했다.
실패한 `linux-face/1794212215057488000`, `1794212305696073000` 및
`linux-mono-build.*`, `linux-mono-player-audit.json`도 보존했다.
이를 물리 Linux의 Mono가 실패한다는 결론으로 일반화하지 않는다.

Windows 실제 실행/D3D11·IL2CPP, Linux 실제 GPU/Vulkan 및 배포판 범위,
두 플랫폼의 물리 입력·권한·장치 제거·background/resume·장시간 성능은 남아 있다.
전체 플랫폼 완료 여부는 [PLATFORMS.md](PLATFORMS.md)의 게이트를 따른다.
