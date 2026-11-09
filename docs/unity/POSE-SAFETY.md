# 고개 미리보기 변형 완화 — 2026-11-09

사용자 보고: 고개 미리보기에서 얼굴이 지나치게 일그러진다.

## 원본 비교

Aoi(char06), 원본 commit `011b1f54d62c189cee49f29d3427c6d22fafdffd`의 수정하지 않은
`Puppet`을 로컬 브라우저에서 실행했다. 기존 샘플의 `angleX=20, angleY=-10`을 그대로 넣으면
원본에서도 한쪽 얼굴/눈의 폭이 줄고 코와 턱이 비대칭으로 이동한다. 이 문제를 Unity 전용 렌더러 결함으로 분류하지 않는다.
원본 `headTrig/headXform`은 2D 레이어를 가상의 볼륨에 따라 변형한다. 입력 값은 실제 물리 각도(도)가 아니며 모델 rig와 tuning에 따라 변형 정도가 달라진다.

같은 params와 physics reset/update(0) 조건으로 비교했다. 512×768 출력의 얼굴 영역
`x=[205,325), y=[170,280)` RGB 채널 평균 절대 차이는 255 기준 **0.4343**, p99는 2였다.
이것은 해당 얼굴 영역의 비교이며 전체 그림/모든 자세의 완전 일치를 주장하지 않는다.

- 이전 Unity: `unity/TestResults/turn-current.png`
- 동일 조건 원본: `unity/TestResults/turn-origin-old-reset.png`
- 원본 120프레임 경과 결과는 별도 `turn-origin-exact.png`이며 physics reset 캡처와 혼동하지 않는다.

## 수정

원본 렌더러/driver의 1:1 계산은 유지하고 샘플 `PortVerification.SamplePresentation`에서 처리한다.

- 고개 미리보기 목표: **20/-10 → 8/-3**.
- 즉시 자세 전환 대신 시간 상수 0.1초의 지수 보간. 작은 오차에서 목표로 수렴한다.
- 샘플의 최종 좌우·상하 입력은 `(angleX/12)^2 + (angleY/8)^2 <= 1`인 범위로 제한한다.
  대각선 회전에서도 두 축이 동시에 끝까지 적용되지 않게 한다. 카메라 추적에도 같은 규칙을 적용한다.
- 고개 기울기(Z), 표정·입·눈·시선, 좌우·상하 위치는 이 제한으로 변경하지 않는다.
- 얼굴 거리 확대·축소 제거(`move.s=1`)는 유지한다.
- 카메라/마이크 시작 시 미리보기 자세와 선택 표시를 기본으로 되돌린다.

이 값들은 현재 샘플 Aoi를 비교하며 선택한 보수적인 표시 정책이다. 원본의 새로운 기본값이나 모든 모델에서 왜곡이 없다는 보장이 아니다.
새 캐릭터를 기본 샘플로 선택할 때 해당 모델의 정면/좌우/상하/대각선 출력을 다시 확인해야 한다.

## 검증

수정된 Unity와 원본 8/-3 출력도 같은 조건으로 비교했다. 동일 얼굴 영역 RGB 평균 차이 **0.3743**, p99 2.
두 이미지를 직접 열어 줄어든 비대칭을 확인했다.

- 결과·비교 조건·소스 SHA-256: `unity/TestResults/turn-comparison.json`.
- 수정 후 렌더: `unity/TestResults/turn-safe.png`, 원본: `turn-origin-safe-reset.png`.
- WebGL 실제 미리보기 전환: `turn-transition.json`; 0/0 → 8/-3으로 목표 초과 없이 수렴, 배율 1.
- 공용 원본 driver 비교 `.NET DriverParityTests`: 8 passed. upstream 25 modules, issues 0.
- Android/WebGL CLI IL2CPP build: errors/warnings 0.

BlueStacks에도 최신 APK를 설치했다. UGUI 고개 버튼을 키보드로 선택·실행한 뒤 실제 Android 로그가 angleX=8, angleY=-3, scale=1임을 확인했다. `bluestacks/turn-safe.json/png`. 생성 영상 7단계 추론/유실/재검출/해제 검증도 통과했다.
