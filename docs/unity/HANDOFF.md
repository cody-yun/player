# Unity 포트 마감 및 잔여 검증

2026-11-09. 이번 마감 범위는 기존 Unity 포트와 **세로 UGUI의 실시간 입력 수명주기 검증**이다.
추가 플랫폼 검증과 기능 확장은 보류한다. 현재 포트는 개발 버전이며 전체 플랫폼 인증 완료 상태는 아니다.

## 최종 완료 항목

- 원본 기준 `d999dea`, 파일별 매핑 25개/독립 fixture 24개. read-only upstream 감사 issues 0.
- 공통 2.5D 렌더·추적·음성 코어, Unity/브라우저 입력 어댑터, 선택적 native MediaPipe와 UGUI UPM 패키지.
- 샘플의 얼굴 거리 배율은 1 고정, 확대/축소 옵션 없음. 고개 프리뷰 제한과 스크롤 없는 UGUI 유지.
- .NET 1,317·Unity EditMode 1,416·PlayMode 30 통과. 직전 공통 소스의 7개 대상 CLI 빌드 통과.
  이후 변경은 샘플 QA 진단이며 최종 Android 빌드 errors/warnings 0. 라이브러리는 변경하지 않았다.
- 고정 클릭 좌표를 제거하고 UGUI의 실제 버튼 중심을 이용한다. CLI 전용 `-livlivUiOrientation Portrait`
  옵션으로 시험 방향을 명시하며 일반 샘플 시작에는 적용하지 않는다.
- BlueStacks에서 실제 전달된 카메라·마이크를 동시 실행하고 Home/복귀/명시적 재시작을 3회 수행했다.
  초기 상태 포함 10개 상태 모두 Portrait 1080×1920, 동일 PID, 배율 1, 버튼 6개,
  잘림/겹침/스크롤 0. background/resume에서는 두 AppOps가 닫히고 자동 재시작하지 않았다.
  종료 후 일반 샘플의 Ready·두 입력 Off·AppOps 닫힘까지 확인했다.

재현: `python3 scripts/unity/verify-bluestacks-lifecycle.py --orientation Portrait`.
기존 권한 허용 상태와 실행 중인 BlueStacks를 사용한다. raw 카메라 영상/음성은 저장하지 않는다.

최종 원시 증거는 로컬 `unity/TestResults/bluestacks/lifecycle/1794236042644738000/`의
`evidence.json`, `states.json`, `restoration.json`, 단계별 PNG/AppOps/메모리 기록이다.
빌드: `unity/TestResults/build-refresh/1794235974197708000/evidence.json`.
로컬 APK: `unity/TestResults/Android/LivLivVerification.apk`, 53,311,574 bytes,
SHA-256 `bd663421814835fabdda2d17f6c88aaaf3b2594a9a646bd6472d8e9f21bc18f7`.
서명·ARM64·16KB·포함 자산·미참조 ZIP 영역 0 감사 통과. TestResults/캐시/빌드 산출물은 Git 제외다.

## 보류 항목

1. Windows 실제 실행: 환경 없음. 빌드/파일/전달 ZIP 검사까지 검증했다.
2. 물리 Android/iOS의 권한·동시 입력·회전·복귀·성능: BlueStacks로 대체 인증하지 않는다.
   iOS 실기기 검증은 보류 상태이며 unsigned export/link/파일 검사까지 있다.
3. Safari와 모바일 WebGL의 실제 장치·오디오 unlock·중단/복원·지연 검증.
4. [ORIENTATION.md](ORIENTATION.md): BlueStacks에서 반대 방향 요청은 Android에 전달되지만 display rotation은
   기본 방향으로 남는다. 네 방향 전체 결과는 Failed이며 Android/가상 디스플레이 경계의 원인은 미확정이다.
5. [PERFORMANCE.md](PERFORMANCE.md), [CAPTURE-SOAK.md](CAPTURE-SOAK.md): 한 10분 실행에서 관측한 약 1초 지연의
   원인이 미확정이다. 후속 실행에서 재현되지 않았으며 성능 해결이나 전체 장치 누수 없음으로 선언하지 않는다.

자세한 설치는 [INSTALL.md](INSTALL.md), 지원 목표와 완료 게이트는 [PLATFORMS.md](PLATFORMS.md),
원본 변경 대응은 [UPSTREAM.md](UPSTREAM.md)와 `compatibility.json`을 따른다.
BodyPlayer/VRM/PNGTuber는 별도 확장 범위로 pending을 유지한다.
