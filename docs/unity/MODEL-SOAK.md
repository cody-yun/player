# 21개 모델 반복 로드·교체·해제 — 2026-11-09

macOS ARM64/Metal과 BlueStacks ARM64/GLES3에서 각각 21개 모델을 3회 순회했다.
이 문서의 수치는 최적화 전 기준값이다. 후속 설정 읽기 최적화와 픽셀 일치 결과는 [PERFORMANCE.md](PERFORMANCE.md)에 있다.
플랫폼마다 **63회 load → replace → animation → dispose**, 총 126회 로드와
1,890개 animation Tick을 완료했다. 각 해제 후 검사한 LivLiv 렌더 객체의 잔존은 0개였다.
제한된 시간의 생명주기 검사이며 전체 플랫폼의 장시간 안정성 완료를 뜻하지 않는다.

## 방법

- 원본 21개 모델을 로컬 검증 전용 ZIP으로 만든다. UPM 배포물에 포함하지 않는다.
- 각 순회에서 정렬한 모델을 로드하고 다음 모델로 교체한 뒤 30개 Tick을 실행한다.
  각도는 좌우 ±8/상하 ±3, 입 열림은 0~1이며 샘플 배율 1을 매번 검사한다.
- 출력은 순서대로 512×768, 320×480, 512×768이다. 첫 순회에 21개 PNG를 저장한다.
- 기존 샘플을 정지하고 객체 instance ID를 기준값으로 보존한다. 별도 Player와 타깃을
  생성하고 Dispose/Destroy 후 두 프레임을 기다려 신규 객체가 남는지 검사한다.
- 이름이 `LivLiv `로 시작하는 Texture2D/RenderTexture/Mesh와 shader가 `LivLiv/`인
  Material을 집계한다. 모든 엔진/네이티브 할당을 포괄하는 검사는 아니다.
- 카메라·마이크·네트워크 모델 입력·강제 GC는 사용하지 않는다.
  두 플랫폼 검사는 빌드 종료 후 순차 실행했다.

## 결과

| 항목 | macOS ARM64 / Metal | BlueStacks / GLES3 |
|---|---:|---:|
| 실행 시간 | 67.47초 | 90.90초 |
| 완료 cycle / load / animated Tick | 63 / 126 / 1,890 | 63 / 126 / 1,890 |
| 해제 후 신규 LivLiv 렌더 객체 | 매회 0 | 매회 0 |
| 마지막 순회 로드 / 교체 평균 | 144.31 / 144.05 ms | 159.71 / 158.72 ms |
| 마지막 순회 Tick 평균 | 23.89 ms | 25.43 ms |
| 마지막 순회 cycle별 Tick p95의 최댓값 | 45.92 ms | 46.38 ms |
| 마지막 순회 프레임 간격 평균 | 24.18 ms | 33.86 ms |
| 마지막 순회 Unity 할당량 | 72.27~72.41 MiB | 82.03~82.07 MiB |
| 마지막 순회 Unity 예약량 | 205.20 MiB | 228.32 MiB |

호스트는 Apple M3 Max이며 Unity는 6000.3.14f1이다. Android GPU 문자열
`Adreno (TM) 650`은 에뮬레이터 보고값이고 물리 Adreno 기기 측정이 아니다.
Tick은 CPU에서 호출 전후를 측정하며 렌더 명령 제출/드라이버 대기를 포함할 수 있다.
GPU 완료 시간으로 해석하지 않는다. 프레임 간격에는 프레임 제한과 스케줄링이 포함된다.
각 cycle의 p95는 30개 Tick/29개 간격에서 계산했다. 전체 표본의 pooled p95가 아니다.

320×480에서 Tick 평균은 macOS 24.11 ms, BlueStacks 25.46 ms로,
해상도를 줄여도 큰 감소가 없었다. macOS 마지막 순회의 v07은 평균 41.29 ms다.
**60 fps 성능 목표를 충족했다고 볼 수 없다.** 다음 단계에서 변형 계산과 렌더 제출을
분리 측정해 병목을 확인하고, 원본 수치/픽셀 회귀 검사를 유지하며 최적화한다.

양쪽 갤러리 21장을 확인했으며 빈 캐릭터나 큰 합성 붕괴는 보이지 않았다.
이는 제한된 자세의 렌더링 범위 확인이며 원본과의 21개 모델 픽셀 동등성 검사는 아니다.

![macOS 모델 갤러리](../../unity/TestResults/macos-model-soak/1794215951553384000/model-gallery.png)

## 재현과 증거

```sh
python3 scripts/unity/prepare-model-soak.py
# 현재 Verification 코드로 Unity CLI macOS/Android 빌드를 준비한 후 순차 실행
python3 scripts/unity/verify-model-soak.py macos
python3 scripts/unity/verify-model-soak.py android
python3 scripts/unity/summarize-model-soak.py <출력-디렉터리> # Pillow 필요
python3 scripts/unity/plot-model-soak.py <출력-디렉터리> # Pillow/matplotlib 필요
```

- macOS: `unity/TestResults/macos-model-soak/1794215951553384000/`
- Android: `unity/TestResults/bluestacks/model-soak/1794216046120344000/`
- 각 디렉터리의 `evidence.json`은 모델 원본/ZIP·런타임 소스·실제 빌드 해시를 포함한다.
  `model-soak.json`, `summary.json`, PNG 21개, 갤러리와 시간/메모리 그래프를 보존했다.
- CLI 빌드: `model-soak-macos-build.log`와 `model-soak-android-build.log`, 각각 errors/warnings 0.
- APK: 53,471,878 bytes, SHA-256
  `5fda0aefb19b5ddb3d4242afcf8361fe413bea67268f9c92d6f36205ec1c0af8`.
  서명·포함 자산·ARM64 패키징 감사도 통과했다.

수 시간 실행, OS/native heap과 장치 handle 회수, 카메라/마이크 동시 부하,
물리 모바일의 온도·배터리·성능, Windows/iOS/WebGL의 같은 반복 검사는 남아 있다.
입력 장치 반복 검사는 별도 [CAPTURE-SOAK.md](CAPTURE-SOAK.md)에 기록한다.
