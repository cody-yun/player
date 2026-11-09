# 입력 반복 실행과 리소스 회수 — 2026-11-09

## 긴 프레임의 단계별 계측 후속

앞선 1,013.94 ms 간격의 원인 분리를 위해 `--phases`/`-livlivSustainedPhases`를 추가했다.
Mac QA 앱에서만 사용하며 원본/라이브러리 수식과 입력 rate는 변경하지 않는다.
50 ms 이상 간격이 관측되면 같은 프레임 번호의 Update 전체, Player.Tick, 로그 Report,
얼굴 입력 전체와 그 안의 생성 영상 복사, PCM 생성/분석 Read 시간을 저장한다.
계측 자체의 메모리 수집/JSON 저장 시간도 별도로 기록한다. GC 완료 횟수는 보조 정보이며
GC 정지 시간으로 해석하지 않는다. 시간은 CPU 순수 사용량이 아닌 wall span이므로 선점도 포함한다.

각 단계의 독립 최대값은 서로 다른 프레임에서 나온 값일 수 있다. 긴 프레임의 상세 기록은 같은
프레임에서 얻은 값이며, 영상 복사 시간은 얼굴 입력 전체에 포함되므로 합산 시 중복 계산하지 않는다.
Player.Tick 잔여 시간에는 합성/물리/변형/렌더 제출이 포함되고 GPU 완료 시간은 아니다.
Update·계측 저장 밖의 시간에는 엔진 작업/프레임 pacing/OS 스케줄링 등이 섞일 수 있다.

30초 최초 검증 `unity/TestResults/macos-sustained/1794229081044791000/`은 프레임 번호 대응·입력 유지·해제를
통과했다. 이후 모든 단계의 값이 수집되고 부분 구간이 부모 구간을 넘지 않는지를 host에서 검사하도록 보강했다.
같은 600초 조건의 새 실행 `unity/TestResults/macos-sustained/1794229239143937000/`도 **600.0126초**의
입력 유지·해제를 통과했다. Update 55,633회와 Unity/OS sample 64개, 평균 간격 10.7851 ms,
구간별 p95 18.3107~18.8301 ms, 최악 간격 44.0265 ms였다. 50 ms 이상 상세 기록은 0개다.
프레임 번호 대응, 전체 경과 시간과 간격 합계, 부분 구간이 부모 구간을 넘지 않는 검사도 통과했다.

| 단계 | 활성 구간의 독립 최대값 |
|---|---:|
| Sample Update 전체 | 18.4503 ms |
| Player.Tick | 18.2504 ms |
| 얼굴 입력 전체 | 12.5761 ms |
| 그 안의 생성 영상 source.Read | 2.5143 ms |
| PCM 생성/분석 Read | 0.1172 ms |
| 로그 Report | 3.4360 ms |
| 메모리 수집/JSON 저장 | 2.8158 ms |

이 값들은 서로 다른 프레임의 최대값일 수 있으므로 합산·상쇄해서 특정 프레임을 설명하지 않는다.
이번에는 원래 약 1초의 지연이 재현되지 않아 원인도 확정하지 못했다. 44 ms 구간은 상세 기록 조건인
50 ms보다 짧아 같은 프레임의 단계 내역이 없다. 코드 최적화를 적용한 실험이 아니므로 앞선 실행 대비
성능이 개선됐다고 해석하지 않는다. OS/할당기 상태와 계측 유무 등 실행 간 차이가 남아 있다.

활성 RSS는 396.64~415.98 MiB, 마지막 5개 평균 399.88 MiB였다. 이 실행에서 약 9분 이후 RSS가 감소해도
50 ms 이상의 간격은 발생하지 않았다. 앞선 RSS 감소와 지연의 동시 관측만으로 인과관계를 주장할 수 없다.
종료 후 camera/clip/Texture2D는 0/0/56으로 복귀했고 소유 source/voice 해제도 확인했다.
원본 JSON/로그, 독립 최대값·시간 집계 `summary.json`, 그래프 `sustained-trend.png`와 앱·소스 해시를 보존했다.
이전 1초 지연은 미해결 이슈로 유지한다. 검사를 통과했다는 것은 지정한 실행 유지·회수·측정 조건의 의미다.

```sh
python3 scripts/unity/verify-macos-sustained.py --seconds 600 --phases
```

## 생성 입력 10분 동시 유지 측정

CLI 전용 `-livlivSustained <directory> -livlivSustainedSeconds <30..7200>`를 추가했다.
normal window에서 생성 portrait의 CPU MediaPipe 추론(maxFps 30), 650 Hz PCM 분석/립싱크와
기존 렌더러를 함께 유지한다. 실제 카메라·마이크와 출력 오디오는 사용하지 않고 강제 GC도 하지 않는다.
10초마다 Unity 객체/메모리·상태·프레임 간격을 기록하고 host가 같은 PID의 RSS를 관측한다.
OS 측정 시각과 Unity sample 시각은 별도로 기록하며 재시작 검사의 ack 동기화와 구분한다.

30초 예비 검사 `unity/TestResults/macos-sustained/1794228012966299000/`는 입력 유지·배율 1·
source/voice 해제와 카메라/클립/텍스처 기준값 복귀를 통과했다. 다만 이 실행의 프레임 간격은
`Time.unscaledDeltaTime`을 사용해 합계가 실제 경과 시간과 맞지 않았다. 성능 수치로 사용하지 않는다.
후속은 `Time.realtimeSinceStartupAsDouble`로 관측한 Update 프레임 간격을 사용하고,
각 구간 평균×프레임 수가 실제 경과 시간(허용 0.1초)과 맞으며 관측 누락이 없는지 host가 검사한다.

600초 실행 `unity/TestResults/macos-sustained/1794228119277575000/`은 **600.0046초**의 활성 유지와
종료 후 입력 회수 조건을 통과했다. 같은 PID의 Unity/OS 관측 64개와 실제 Update 55,750회를 기록했다.
Apple M3 Max/36 GiB/macOS 15.6.1/Metal, 렌더 512×768, 창 960×720이다.
관측된 62개 활성 sample 모두 얼굴 검출/배율 1이며, 생성 입력과 renderer 객체 수는 일정했다.

| 항목 | 결과 |
|---|---:|
| 전체 평균 Update 간격 | 10.7624 ms |
| 10초 구간별 p95 범위 | 17.9067~18.7870 ms |
| 가장 긴 관측 Update 간격 | **1,013.9355 ms** |
| sample 시점 얼굴 추론 FPS 범위 | 11~27 |
| 활성 RSS 범위 | 330.31~452.14 MiB |
| 처음/마지막 5개 sample 평균 RSS | 446.31 / 334.70 MiB |
| 마지막 5분 RSS 범위/기울기 | 331.27~336.95 MiB / +0.369 MiB/분 |
| 활성 Unity 예약 메모리 | 227.0625 MiB, 일정 |
| 활성 Unity 사용 메모리 | 110.83~113.95 MiB |
| 종료 후 camera/clip/Texture2D | 0 / 0 / 56, 시작 기준값 복귀 |

평균과 p95는 GPU 완료 시간이 아니라 관측한 Update 간격이다. 구간별 p95 범위는 전체 프레임의
pooled p95가 아니다. 최악 간격은 제거하지 않았으며, 약 1초 지연의 원인은 이번 계측으로 분리하지 못했다.
따라서 끊김 없는 성능 또는 모든 플랫폼의 성능 합격으로 판정하지 않는다. 앱 pause 로그는 초기
`paused=False` 한 번뿐이지만 이것으로 OS/GC/추론 중 어느 것이 지연 원인인지 확정할 수 없다.

입력 해제 후에도 renderer는 모델을 유지한다. 렌더 타깃/Mesh는 초기 2/25 → 활성 후 3/26으로
한 번 증가한 상태로 유지됐고 Material은 18이었다. 해당 수가 매번 시작값으로 돌아왔다거나 전체
Player Dispose까지 검사했다고 해석하지 않는다. 이전 반복 교체/해제 검사는 별도 기록이다.
RSS는 초반 이후 감소했고 후반 변동폭도 작았지만 마지막 5분의 작은 양의 기울기도 그대로 보고한다.
이 실행으로 앞선 task 재생성 시 RSS 증가의 원인이나 장시간 네이티브 누수 유무를 확정할 수 없다.

원본 JSON/로그, 앱·소스 hash, `summary.json`, `sustained-trend.png`를 같은 폴더에 보존했다.
초기 그래프와 수치 파일은 `*-v1`으로 남기고 최악 지연을 명시한 그래프를 재생성했다. 실행은 반복하지 않았다.
분석기는 예비 Unity delta-time 결과의 성능 집계를 거절하며, 해당 거절 로그도 예비 폴더에 보존했다.
제품 패키지는 변경하지 않았으며 Mac QA 앱만 이 CLI 진단을 포함해 갱신했다.

![생성 입력 10분 동시 유지](../../unity/TestResults/macos-sustained/1794228119277575000/sustained-trend.png)

```sh
unity build ./unity --target StandaloneOSX --execute-method VerificationBuild.MacOS
python3 scripts/unity/verify-macos-sustained.py --seconds 600
```

이하 기록은 완료된 30회 시작/종료 검사다.

BlueStacks ARM64 / Android 13 / GLES3 / Unity 6000.3.14f1 IL2CPP 개발 APK에서
실제 카메라·마이크를 **30회 열고 닫는 152.80초 검사**를 완료했다.
이는 물리 모바일 또는 수 시간 실행의 검증이 아니다.

## 검사 경계

샘플의 CLI 전용 `-livlivCaptureSoak <output-directory>`는 다음 순서로 동작한다.

1. 입력 Off 기준값을 기록한다.
2. 카메라·마이크를 시작하고 2초 실행한다.
3. Unity 메모리/객체 수를 기록하고 host가 Android 메모리/AppOps를 측정할 때까지 기다린다.
4. 두 입력을 Stop하고 1초 후 같은 측정을 한다. 이 순서를 30회 반복한다.
5. 자연 상태로 10초 기다린 뒤 측정한다.
6. 진단용 GC를 수행하고 1초 후 별도로 측정한다.

host는 각 sample의 `ack-N` 파일로 측정 완료를 알린다. 덕분에 active sample의 Android PSS를
입력이 이미 꺼진 다음 읽는 경쟁 조건을 피한다. 63개 sample 모두 같은 PID에서 수집했고,
active 때는 CAMERA/RECORD_AUDIO AppOps가 running, 나머지 때는 모두 종료된 상태를 확인했다.

일반 샘플에는 반복 동작이나 강제 GC를 넣지 않았다. 검사 시작은 CLI 인수로만 허용한다.
완료/실패 후 host가 일반 샘플을 입력 Off로 다시 열며 권한/앱 데이터를 초기화하지 않는다.

## 결과

| 항목 | 관측 |
|---|---|
| WebCamTexture / AudioClip | 30회 모두 종료 후 각각 0 / 0 |
| Texture2D | 종료 후 기준값 56개로 매번 복귀 |
| 렌더 타깃 / Material | 최초 1 / 16 → 2 / 18, 이후 일정 |
| Mesh | 최초 25 → 3번째 반복부터 26, 이후 일정 |
| 종료 후 스레드 | 62~63개, 반복 횟수에 비례해 늘지 않음 |
| Unity 예약 메모리 | 모든 종료 sample에서 215.91 MiB |
| 마지막 10회 종료 후 Unity 사용 메모리 | 86.66~89.72 MiB, 일시 상승 후 복귀 |
| 마지막 10회 종료 후 Android PSS | 281.50~290.05 MiB |
| 마지막 10회 종료 후 PSS 선형 기울기 | -402.69 KiB/회 |
| 최종 입력 상태 / 캐릭터 배율 | Off / 1 |

렌더 타깃의 추가 객체는 `LivLiv eye masks`, 머티리얼 추가 객체는
`Hidden/VideoDecode`, `Hidden/VideoDecodeAndroid`다. 이름과 instance ID를 기록했으며,
이 세 객체는 반복마다 새로 누적되지 않았다. Mesh 1개 증가의 소유자는 이번 기록으로 식별하지
않았지만, 미리 정한 5회 준비 구간 이후 25회의 종료 sample에서는 개수가 일정했다.

처음 5회의 종료 후 평균 PSS는 262.38 MiB, 마지막 5회는 287.15 MiB로 더 높다.
전체 상승을 무시하고 처음부터 일정했다고 해석하지 않는다. 후반에는 증가가 둔화되고 하락도
관측됐으며, Unity 예약량·장치 객체·렌더 객체는 일정했다. 이 결과만으로 Unity 외부의 모든
네이티브 할당이 해제됐다고 증명할 수는 없다.

관리 메모리는 GC 전 idle sample 3.39 MiB, 명시적 GC **1초 후** 3.57 MiB였다.
이 필드는 `managedWithGarbage`이며 그 사이 진단/UI 할당도 포함한다. GC 직후의 순수 live heap
크기 또는 강제 GC로 사용량이 감소했다는 증거로 취급하지 않는다.
Profiler API의 사용량/예약량 구분은
[Unity Profiler 문서](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/profiling/profiler)를 따른다.

![메모리 측정 추이](../../unity/TestResults/bluestacks/capture-soak/1794211537971503000/memory-trend.png)

## 재현과 증거

```sh
# 최신 Android 개발 APK가 준비된 상태, 기존 카메라/마이크 권한 허용 필요
python3 scripts/unity/verify-bluestacks-capture-soak.py
python3 scripts/unity/summarize-capture-soak.py <출력-디렉터리>
python3 scripts/unity/plot-capture-soak.py <출력-디렉터리> # matplotlib 필요
```

최종 증거 디렉터리: `unity/TestResults/bluestacks/capture-soak/1794211537971503000/`.
`evidence.json`에는 실제 APK/소스 SHA-256, Unity 63개 sample, Android 63개 sample 및 AppOps가 있다.
`capture-soak.json`, `os-samples.json`, `summary.json`, `*-meminfo.txt`, `*-status.txt`, `player.log`와
`memory-trend.png`를 함께 보존했다. 그래프는 matplotlib 3.9.4/Python 3.12로 생성했다.

최초 진단 실행(`1794211399489180000`)은 모든 렌더 객체가 최초 기준값으로 돌아온다는 가정 때문에
2번째 반복에서 실패했다. 해당 실패도 보존했다. 후속 검사는 장치 회수와 렌더러/엔진의 지속 캐시를
분리하고, 준비 구간 이후 렌더 객체 수가 일정한지를 검사한다. 이전 실패를 성공으로 덮어쓰지 않았다.

최종 APK: 53,442,442 bytes,
SHA-256 `292ad4c4d9ef724d11d50e795a38b01f578336f9cf5966a844e9e5671348247a`.
CLI build errors/warnings 0, 47.10초. 서명·ARM64/16KB·자산 감사와 미참조 ZIP 영역 0 확인.

## 남은 범위

- `Passed`는 지정한 30회 입력 회수·준비 이후 객체 수·AppOps 조건 통과다. 전체 플랫폼의 누수 없음 선언이 아니다.
- 에뮬레이터가 `/proc/PID/fd` 접근과 `run-as`를 제한해 파일 디스크립터 수는 측정하지 못했다.
- 물리 Android/iOS, Safari, 수 시간 실행·온도·배터리·프레임 지연은 남아 있다. macOS와 WebGL 후속은 아래 범위로 검증했다.
- 얼굴/음성 입력 정확도와 실제 장치 연결 해제/재연결은 별도 검사 대상이다.
- 모델 반복 교체는 macOS/BlueStacks에서 21개×3회 검사를 완료했다. 범위와 남은 성능 문제는 [MODEL-SOAK.md](MODEL-SOAK.md)를 따른다.

## macOS 실제 입력과 추론 반복 후속

ARM64 IL2CPP/Metal 일반 창 모드의 실제 카메라·마이크를 30회 열고 닫았다(125.31초).
같은 PID에서 Unity/OS 63개 sample을 동기화했다. 매 종료 후 WebCamTexture/AudioClip은 0/0,
Texture2D는 기준값으로 돌아왔고 준비 5회 이후 렌더 객체 수도 일정했다. scale은 모두 1이었다.
host는 완료 후 자신이 실행한 앱만 종료한다. 원본 영상이나 PCM은 저장하지 않는다.

| 종료 후 메모리 | 실제 캡처 반복 | 생성 영상 추론 task 반복 |
|---|---:|---:|
| 처음 5회 평균 RSS | 653.30 MiB | 566.13 MiB |
| 마지막 5회 평균 RSS | 765.28 MiB | 738.93 MiB |
| 마지막 10회 RSS 기울기 | +3,078.11 KiB/회 | -1,256.05 KiB/회 |
| 준비 이후 Unity 예약 메모리 | 241.06 MiB | 229.06 MiB |
| 준비 이후 Unity 사용 메모리 | 109.40~109.82 MiB | 109.48~110.97 MiB |

RSS 증가 조사를 위해 카메라·마이크 없이 생성 portrait와 새 MediaPipe task만 만들고 해제하는
30회 검사를 추가했다(108.84초, 63 sample). 여기서도 초기 RSS 증가가 관측됐다. 종료 후
Texture2D는 56개, 렌더 타깃/Material/Mesh는 2/18/25로 복귀했다. 장치 캡처가 없어도
증가할 수 있다는 결과이며 MediaPipe만이 원인이라는 증거는 아니다. 렌더러와 진단도 함께 실행된다.

추론 반복 중 두 시점의 `vmmap -summary`에서 malloc allocated는 88.2→82.5 MiB,
physical footprint는 826.8→810.2 MiB로 감소했지만 malloc resident는 221.6→359.4 MiB로 증가했다.
이는 할당기가 해제한 페이지를 유지하는 설명과 양립한다. 두 스냅샷으로 네이티브 누수 유무나
장시간 상한을 확정할 수 없다. 이 실행에는 vmmap 진단 영향도 포함되므로 두 실행의 기울기를
통제된 성능 비교로 사용하지 않는다. 메모리 증가는 미해결로 유지하며 추측에 따른 풀링/강제 GC를
일반 샘플에 도입하지 않았다.

![생성 영상 추론 반복 메모리](../../unity/TestResults/macos-inference-soak/1794226162831416000/memory-trend.png)

재현:

```sh
python3 scripts/unity/verify-macos-capture-soak.py
python3 scripts/unity/verify-macos-capture-soak.py --inference-only
python3 scripts/unity/summarize-capture-soak.py <출력-디렉터리>
python3 scripts/unity/plot-capture-soak.py <출력-디렉터리>
```

증거: `unity/TestResults/macos-capture-soak/1794225620902848000/` 및
`macos-inference-soak/1794226162831416000/`의 원본 JSON, 로그, 앱/소스 해시, summary와 그래프.
후자에는 `vmmap-early.txt`, `vmmap-later.txt`도 있다. Passed는 30회 객체 회수 조건이며
RSS 안정성 또는 모든 네이티브 할당 해제를 의미하지 않는다. 다른 플랫폼 앱은 이 CLI 전용 추론 진단
추가 이전 빌드다. 제품 런타임 패키지는 변경하지 않았다.

## WebGL 생성 입력 빠른 반복

Chrome 156/macOS의 실제 Unity WASM에서 생성 portrait canvas와 650 Hz oscillator의 MediaStream을
30회 시작·종료했다. 기준값 1개와 active/stopped 각 30개, 총 61개 상태를 기록했다.
25.50초의 빠른 반복이며 각 active 검출 뒤 60 render frame 진행을 기다렸다. 프레임 수를
실시간 2초로 간주하지 않으며, 위 native 검사의 유지 시간과 동일한 조건도 아니다.

- 활성 시 얼굴 검출·립싱크, 살아 있는 video/audio 트랙 각각 1개와 캡처 AudioContext 1개.
- 매 종료 시 모든 기존 트랙 ended, 캡처 AudioContext closed, 두 입력 Off.
- 60개 active/stopped 상태에서 배율 1, 표정 개수 4, foreground와 가로/세로 스크롤 없음.
- 최종 generator AudioContext도 닫고 원본 getUserMedia/AudioContext를 복구했다.
  페이지를 reload해 Ready/Aoi/입력 Off/배율 1/스크롤 없음을 확인한 뒤 Ego 공간 26을 닫았다.

JS heap 관측치는 종료 후 28.32~35.79 MiB, 처음 5회 평균 29.42 MiB, 마지막 5회 33.11 MiB였다.
`performance.memory.usedJSHeapSize`는 GC 전 garbage와 검사기가 보관한 트랙/컨텍스트 참조를 포함하며,
Unity/MediaPipe WASM·GPU·브라우저 프로세스 전체 메모리가 아니다. 장시간 또는 네이티브 누수 판정에
사용하지 않는다. 브라우저 모델/task 공유 캐시는 정상적으로 유지되는 구조이며 매회 제거를 요구하지 않았다.
실제 장치·권한 창, 모바일 WebGL, Safari 및 장시간 반복은 미검증이다.

재현 스크립트 `scripts/unity/verify-webgl-capture-soak.mjs`는 ego-browser nodejs에서
절대 저장소 root(끝 `/` 포함)와 기존 활성 `soakSpaceId`를 주입해 실행한다.
`unity/TestResults/webgl-capture-soak/` 부모 디렉터리와 8770 WebGL 서버가 필요하다.
기존 결과를 덮어쓰지 않으며 진행 상태와 최종 결과를 실행별 디렉터리에 저장한다.
원본 `evidence.json`, `progress.json`, `recovery.json`, 복구 화면 `recovered.png` 및 소스/빌드 해시는
`unity/TestResults/webgl-capture-soak/1794226546634/`에 보존했다.
