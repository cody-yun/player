# 네이티브 표시 어댑터 실행 검증 — 2026-11-09

아래는 최초 표시 어댑터 검증 기록이다. 이후 cropped eye mask를 포함한 macOS/BlueStacks 재검증과
당시 APK/iOS 산출물은 [MASK-TARGETS.md](MASK-TARGETS.md#추가-네이티브-검증--2026-11-09)에 기록했다.
Linux IL2CPP/Docker/Mesa의 표시·mask·text 후속 실행은 [DESKTOP.md](DESKTOP.md)에 기록했다.

Unity 6000.3.14f1 CLI로 빌드한 macOS ARM64/Metal 및 BlueStacks Android 13 ARM64/OpenGLES3
IL2CPP player에서 `PlayerSurface`, `SnapshotPng`, `PosterPresenter`를 실행했다.
카메라·마이크·HTTP 요청 없이 내장 Aoi 모델과 PNG/WebP를 사용한다. 원본 계산 코어는 변경하지 않았다.

## 실행 결과

| 항목 | macOS ARM64 | BlueStacks ARM64 |
|---|---|---|
| 결과 | Passed | Passed |
| 세로 logical 320×480 | 320×480 pixels | 640×960 pixels |
| 같은 크기 재사용 | 동일 target ID | 동일 target ID |
| 가로 logical 480×320 | 480×320 pixels, 새 target | 960×640 pixels, 새 target |
| density 3 / cap 2 | 640×960 pixels | 640×960 pixels |
| 10,000 pixels 예산 | 81×122 pixels | 81×122 pixels |
| 너비 0 → 복구 | 타깃 해제 → 새 타깃 | 타깃 해제 → 새 타깃 |
| paused clock / sample scale | 모든 상태 0 / 1 | 모든 상태 0 / 1 |
| PNG alpha 차이 | 0 | 0 |
| PNG premultiplied 왕복 최대 오차 | 0.498039 LSB | 0.498039 LSB |
| 종료 후 소유 RT / material / poster texture | 0 / 0 / 0 | 0 / 0 / 0 |

각 플랫폼 7개 상태를 검사했다. PNG 오차는 같은 프레임의 GPU raw 픽셀과 PNG 전체 픽셀의 비교이며,
원본 웹 renderer와 그림 전체가 동일하다는 의미가 아니다. 두 플랫폼의 세로 PNG를 직접 열어
캐릭터와 mark 출력을 확인했다. BlueStacks는 검증 후 원래 샘플로 복귀한다.

포스터는 제어 가능한 `PlayerBase` 테스트 host를 사용했다. 실제 UGUI 및 PNG/WebP decoder로
PNG data URL, 기본 `UnityModelTransport`의 WebP file URL, 잘못된 이미지의 독립 오류,
교체 후 늦게 도착한 결과 무시와 첫 프레임 후 해제를 확인했다.
이 검사는 HTTP 모델 다운로드·설치 전체 생명주기를 검증하지 않는다.
그 경로의 Editor/WebGL 증거는 [POSTER.md](POSTER.md)에 별도로 기록했다.

## 증거와 재현

`unity/TestResults/` 아래 로컬 산출물(Git 제외):

- `macos-presentation/1794206631711254000/evidence.json`: 최종 macOS 실행.
- `bluestacks/presentation/1794206592789718000/evidence.json`: 최종 Android 실행.
- 각 디렉터리의 `presentation-player.json`, 상태별 PNG, macOS `player.log`.
- `android-apk.json`: 최종 APK **63,343,598 bytes**, ARM64 native library 9개 및 16KB ELF/ZIP 정렬 검사.
- `ios-app.json`: unsigned iPhoneOS app **171,356,658 bytes**, ARM64·MediaPipe embedding·WebP 심볼·Resources 검사.
- `presentation-ios-xcode.log`: `BUILD SUCCEEDED`. Unity 생성 코드의 deprecated API 등과 app icon 경고가 남는다.

결과 JSON에는 관련 소스 11개의 SHA-256 및 실행 바이너리 SHA-256을 기록한다.
macOS launcher만으로 IL2CPP 코드 동일성을 판단하지 않도록 GameAssembly, metadata, resources,
sharedassets의 hash도 기록한다. 최종 APK SHA-256은
`e26c0b2f0e5aced51cda247040fd74fc2396b980867f27bc0e4b4850c7756bea`다.

Unity CLI로 각 최신 player를 빌드한 뒤 실행한다:

```sh
python3 scripts/unity/verify-native-presentation.py macos
python3 scripts/unity/verify-native-presentation.py android
python3 scripts/unity/check-android-apk.py
python3 scripts/unity/check-ios-app.py
```

Android runner는 승인된 BlueStacks `127.0.0.1:5555`에 APK를 설치하고 GLES3로 실행한다.
probe 진입점은 `PortVerification.Presentation.cs`의 `-livlivPresentationSmoke OUTPUT_DIR`다.
일반 샘플 실행에는 검증 컴포넌트와 파일 출력을 추가하지 않는다.

첫 Android 시도(`bluestacks/presentation/1794206453308273000/evidence.json`)는 초기 Canvas layout이
확정되기 전 320×480 target과 이후 실제 배율 2의 640×960 target을 동일 크기로 취급해 실패했다.
QA가 화면·타깃·transform 배율의 안정화를 기다리도록 수정했고, 최종 양쪽 실행에서 재검증했다.
런타임 해상도 계산이나 renderer를 변경한 것은 아니다. 실패 기록도 보존한다.

## 검증 범위

macOS/Android Unity build reports의 errors/warnings는 0이다. iOS Unity export도 errors/warnings 0이며
Xcode unsigned build/link와 산출물 검사까지 통과했다. iOS export에는 QA layout 대기 수정 전 probe가
들어 있지만 제품 패키지 코드는 같다. iPhone에서 이 probe를 실행한 결과는 없다.

물리 Android/iOS, macOS x64, Windows/Linux, Safari/mobile browser, World Space/perspective Canvas,
큰 타깃의 장시간 성능·메모리는 아직 별도 검증이 필요하다. 이 결과는 전체 suite 재실행이나
모든 플랫폼 지원 완료를 의미하지 않는다.


소유자 텍스트 워터마크 추가 후 macOS/BlueStacks의 7개 표시 상태·8개 마스크 상태·8개 텍스트 상태를
재검증했다. 최신 실행 경로와 APK/폰트 관련 증거는 [TEXT-MARK.md](TEXT-MARK.md#native-il2cpp-실행)에 있다.
