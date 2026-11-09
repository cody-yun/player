# 정점 변형 설정 읽기 최적화 — 2026-11-09

추가 단계별 계측 10분 실행에서는 이전 1,013.94 ms 지연이 재현되지 않았다(최악 44.03 ms).
측정값 대응과 라이프사이클 검사는 통과했지만 원인은 미확정이다. 입력 전체·그 안의 source 복사·
Tick·Report·계측 저장을 구분해 기록할 수 있도록 Mac QA 도구를 보강했다.
이것은 제품 최적화에 따른 개선 실험이 아니다. 상세 경계와 독립 최대값은 [CAPTURE-SOAK.md](CAPTURE-SOAK.md).

후속 생성 입력 동시 실행: M3 Max/macOS Metal/512×768에서 CPU 얼굴 추론·PCM 분석·renderer를
600초 유지했다. 평균 관측 Update 간격 10.76 ms, 10초 구간별 p95 범위 17.91~18.79 ms,
최악 1,013.94 ms였다. 이 긴 지연의 원인 분리는 남아 있어 부드러운 실행의 합격 증거로 사용하지 않는다.
아래의 순수 CPU Deform/Render 제출 비용과는 측정 경계가 다르다. 구체적인 메모리/객체 수·시간 검증과
raw evidence는 [CAPTURE-SOAK.md](CAPTURE-SOAK.md)의 10분 기록을 따른다.

## 병목과 변경 경계

[MODEL-SOAK.md](MODEL-SOAK.md)의 처리 지연을 조사하기 위해 실제 macOS ARM64/Metal
IL2CPP 앱에서 `UpdatePhysics`, `Deform`, `Render`를 분리 측정했다.
초기 v07 모델의 평균은 각각 0.023 / 40.296 / 0.559 ms였으며 정점 변형이 지배적이었다.
초기 측정: `unity/TestResults/macos-model-profile/1794216448737175000/`.

`puppet.cs`의 `headXform`/`bodyOffset` 계산에서 매 정점마다 동일한 JObject 필드를
조회하고 숫자로 변환하고 있었다. `ReadHeadFrame`/`ReadBodyFrame`은 이를 **Deform 호출마다
한 번** 읽는다. 공개 `HeadXform`/`BodyOffset`은 호출마다 현재 값을 읽고 같은 계산 함수를 사용한다.
`HeadTrig`의 기존 삼각함수 캐시는 유지하며 원본 연산 순서·배열·float 저장 경계를 바꾸지 않는다.
모델의 격자 밀도, 변형 범위, 물리 시간 간격, 출력 해상도는 줄이지 않았다.

컨텍스트는 프레임 사이에 보존하지 않는다. public model/rig/tuning의 수정, body/bust와 Params의
변화가 다음 변형에 반영된다. helper는 같은 파일의 대응 함수 옆에 두며 원본 기준 SHA와
source→target 매핑은 갱신하지 않는다. 원본 변경 적용 시 계산 함수와 설정 읽기를 함께 검토한다.

## 검증 방법

- .NET과 Unity CLI EditMode의 DynamicsParityTests 각각 23개 통과.
  원본 21개 모델×120프레임 fixture와 직접 head/body probe를 비교한다.
  더블 허용 오차는 상대 1e-9, Float32 변형/체인은 상대 1e-6이다.
- 추가 live-edit 검사는 이전 프레임을 실행한 객체와 새 객체에 같은 anchor/rig/tuning/Params
  변경을 적용하고 output/outB 배열의 정확한 일치를 확인한다.
- Unity CLI `LivLiv.Tests.GraphicsTests` 60개 통과. 원본 이미지 sampling·동적 변형·키폼·마스크·텍스트 등의 회귀 범위다.
- 실제 모델 성능 probe는 모델당 10프레임 준비 후 60프레임을 수집한다.
  입력/driver 시간과 GPU 완료 시간은 포함하지 않는다. Render 측정은 CPU 제출 경계다.
- 기존 lifecycle PNG는 `SnapshotPng()`가 실제 시간으로 로고 눈을 다시 그린다.
  최초 전후 비교에서 19장은 동일했지만 v04 53픽셀, v07 84픽셀이 달랐다.
  차이 경계는 각각 `(443,729)-(451,738)`, `(441,728)-(451,738)`이었다.
  이 strict 비교 실패는 `frame-context/macos-pixels.log`와 후속 soak의 `previous-comparison.json`에 보존한다.
- 전체 픽셀 검증에는 별도 profile에서 `Renderer.Render(target, 0, 1)`로 로고 시각을 고정한다.
  그 직후 읽은 premultiplied RGBA 512×768을 수정 전·후 같은 모델/자세에서 비교한다.
  로고 영역 제외나 이미지 오차 허용은 사용하지 않는다. 제품의 Snapshot API와 시간 동작은 그대로다.

## 재현

고정 시각 전후 비교는 **21/21 PNG의 RGBA 바이트가 완전히 일치**, 최대 채널 차이 0으로 통과했다.
두 실행의 모델/프로파일 설정/계측 코드는 같고 런타임 소스 해시는 `puppet.cs`만 다르다.

| 모델 21개 평균 CPU 경계 | 수정 전 | 수정 후 |
|---|---:|---:|
| 물리 계산 | 0.0250 ms | 0.0218 ms |
| 정점 변형 | 22.3843 ms | 0.9258 ms |
| 렌더 명령 제출 | 0.3761 ms | 0.2581 ms |

정점 변형의 관측 개선은 약 24.18배다. 단일 제한 시간 실행의 비교이며 절대 성능 보장은 아니다.
대상 하드웨어는 Apple M3 Max, Unity 6000.3.14f1 ARM64 IL2CPP/Metal이다.

- 고정 시각 수정 전: `unity/TestResults/macos-model-profile/1794216834524312000/`
- 고정 시각 수정 후: `unity/TestResults/macos-model-profile/1794216892753592000/`
- 엄격한 비교: 수정 후 디렉터리의 `previous-comparison.json`, `frame-context/fixed-clock-pixels.log`.
- macOS 반복 검사: `unity/TestResults/macos-model-soak/1794216691818887000/`.
  63회 모두 잔존 객체 0. 마지막 순회 Tick 평균 23.8854 → 1.4678 ms(약 16.27배).
  이는 위의 phase 분리 측정과 다르게 Player의 Tick 전체를 측정한다.

수정 전 빌드는 보존한 최적화 전 `puppet.cs`로 만들고 실행 완료 후 최적화 소스를 복구해
수정 후 빌드·실행을 진행했다. 각 결과의 `sourceHashes`와 앱 바이너리 해시가 그 경계를 기록한다.
초기 프로파일 2회와 시각 미고정 PNG 비교 실패도 덮어쓰지 않았다.

BlueStacks ARM64/GLES3에서도 최신 APK의 **63 cycle / 126 load / 1,890 animated Tick**을
84.05초 동안 실행했고 매회 잔존 LivLiv 렌더 객체는 0개였다.
마지막 순회 Tick 평균은 25.4328 → 3.9446 ms(약 6.45배), cycle별 p95의 최댓값은 14.06 ms다.
프레임 간격 평균은 32.23 ms로 CPU Tick 감소를 그대로 60 fps 달성으로 해석하지 않는다.
해제 후 Unity 할당량은 82.09~82.72 MiB, 예약량 228.32 MiB였다.
물리 Android 성능 측정이나 Android 고정 시각 픽셀 비교는 아니다.

- Android 후속 증거: `unity/TestResults/bluestacks/model-soak/1794216907205195000/`.
- 실제 APK: 53,479,226 bytes, SHA-256
  `bfa7e3433e8b86e626b2705f8a652b9a48dd70dcb22846747360f5beb1de9970`.
- Unity Android build errors/warnings 0, 49.33초. APK 서명/ARM64/16KB/포함 자산 감사 통과.
- `unity/TestResults/frame-context/`에 CLI build/test/audit 로그를 보존했다.
- 변경 후 원본 매핑 감사는 기준 25모듈/22fixture에서 issues 0, 원본 변경 없음이다.

```sh
python3 scripts/unity/prepare-model-soak.py
# 해당 소스로 macOS ARM64 Verification 앱을 Unity CLI build한 뒤
python3 scripts/unity/verify-model-profile.py
python3 scripts/unity/verify-model-soak.py macos
# 같은 probe로 빌드한 수정 전·후 결과를 비교
python3 scripts/unity/compare-model-profile.py <before-directory> <after-directory>
```

profile 증거는 실제 앱 바이너리·모델 ZIP·런타임 소스의 SHA-256을 포함한다.
timing은 제한된 길이의 로컬 실행값이며 모바일 실기기, 입력 동시 부하, 열화/배터리,
최종 앱의 60 fps 유지 또는 전체 플랫폼의 성능 합격을 뜻하지 않는다.

## 후속 검증

이 변경을 포함한 WebGL CLI 빌드가 errors/warnings 0으로 통과했다(53,336,894 bytes, 18.70초).
`unity/TestResults/post-optimization/webgl-evidence.json`에 소스와 산출물 해시를 보존했다.
Chrome 실제 WASM에서 Surface 8개 상태·음성 상태 9개·재생 회귀 8개·눈 마스크 4개를 재검증했다.
증거는 `unity/TestResults/post-optimization/browser/`와 [WEBGL.md](WEBGL.md)에 기록했다.
로컬 UPM tarball을 갱신하고 native 포함/core+UGUI의
새 소비자 프로젝트 PlayMode와 설치 파일 감사를 각각 통과했다. [INSTALL.md](INSTALL.md).
Windows/Linux CLI 산출물 갱신과 포함 파일 감사를 통과했다. Linux Docker/Mesa의 생성 얼굴 추론,
표시/마스크/텍스트 및 UGUI 카메라 8개 상태도 통과했다. [DESKTOP.md](DESKTOP.md).
iOS도 JPEG를 포함한 최신 코드의 별도 unsigned export/link 및 파일 감사를 통과했다. [NATIVE-BUILDS.md](NATIVE-BUILDS.md).
Windows 실제 실행과 iOS 기기 실행은 아직 남아 있다.
iOS 실기기 검증은 사용자의 요청으로 보류한다. 앞선 산출물의 성공 기록은 당시 소스에 대한 증거이며
현재 정점 변형 코드까지 실행했다는 뜻이 아니다.
