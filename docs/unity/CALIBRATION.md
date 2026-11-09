# 얼굴 보정 저장·복원 — 2026-11-09

원본 `src/runtime/tracking.ts`의 `save/restore/calibrate/read`를 같은 이름의 C# 코어에 대응시켰다.
`src/tracking.ts`의 `remember`는 `FaceTrackingOptions.remember`다. 저장소 I/O만 `ICalibrationStore`로
분리하고, 계산 어셈블리의 `noEngineReferences`를 유지한다. 기존 얼굴·음성 드라이버의 계산 순서는 바꾸지 않는다.

## 연결

```csharp
var face = LivLiv.FaceTracking.Attach(player, input, nowMilliseconds,
    new LivLiv.FaceTrackingOptions {
        remember = "user-id:character-id",
        calibrationStore = LivLiv.Unity.UnityCalibrationStore.Instance,
        mirror = true,
        smoothing = .5,
        follow = 0
    });
// Same public control as upstream: median of the most recent ten detections.
face.Calibrate();
```

보정값을 저장하려면 `remember`와 `calibrationStore`를 함께 전달한다. 엔진 독립 코어에는 전역 저장소나
Unity 의존성을 주입하지 않는다. 저장소는 빌려 쓰며 Stop/Dispose에서 해제하지 않는다.
`remember`가 비어 있거나 저장소가 없으면 보정은 해당 tracker 인스턴스 안에서만 유지된다.
host가 사용자·캐릭터별로 안정적인 ID를 선택한다. 샘플의 일반 카메라 경로는 `livliv-sample:char06`을 쓴다.
생성 영상 smoke는 그 키를 사용하지 않는다. TraceFaceInput과 이를 감싼 RecordingFaceInput은 IFaceInputMode.IsReplay를 알리므로
FaceTrackingDriver가 저장소를 자동으로 배제한다. 사용자 정의 replay 입력도 이 표시를 구현한다.

## 원본 계약

- 키는 `lm_calib:<remember>`, 값은 `x/y/z/px/py/pz/bl/br/at` JSON이다.
  `at`는 UTC Unix milliseconds이며 원본처럼 만료 판정에는 사용하지 않는다.
- 저장 좌표는 미러가 켜진 기준으로 통일한다. 미러가 꺼져 있으면 x/z/px의 부호를 바꿔 저장하고,
  복원 시 현재 미러 설정에 맞춘다. 미러 전환만으로 디스크 값을 덮어쓰지 않는다.
- 정면 자세는 최근 샘플의 중앙값이다. 수동 보정은 마지막 10개, 자동 보정은 안정된 20개를 사용한다.
  새 기기에서 처음 안정된 자세를 잡았을 때와 사용자가 보정했을 때 저장한다. 매 프레임 저장하지 않는다.
- x/y/z/bl/br가 유한한 JSON number일 때만 복원한다. 눈 기준값은 0..0.5로 제한한다.
  문자열 숫자, null, boolean, 비정상 JSON은 무시한다.
- 위치가 없던 구형 저장값은 기존 각도·눈 기준을 보존한다. 이후 안정된 20개 샘플로 위치만 채운 뒤
  전체 값을 다시 저장한다. 위치 셋 중 하나라도 유효하지 않으면 위치를 모르는 상태로 취급한다.
- 저장소 접근 거부·용량 부족·잘못된 JSON은 얼굴 추적을 중단시키지 않는다. 원본과 같은 best-effort 동작이다.

## 플랫폼 경계

`UnityCalibrationStore`는 Unity main thread에서 사용한다.

| 플랫폼 | 저장 경로 | 경계 |
|---|---|---|
| native PC / Android / iOS | PlayerPrefs string | Set 후 Save하여 앱 종료 전 기록한다. 앱 ID/OS 사용자별 저장소다. |
| WebGL | 브라우저 localStorage | 원본과 같은 키·JSON을 사용한다. Unity WebGL PlayerPrefs의 IndexedDB와 구분한다. |
| 사용자 저장소 | ICalibrationStore 구현 | Get/Set의 동기 계약과 예외 처리를 따른다. |

PlayerPrefs.Save는 변경된 preference 전체를 디스크에 기록하고 잠시 지연될 수 있다.
host가 별도 저장 타이밍을 관리한다면 사용자 저장소를 전달한다.
근거: [Unity PlayerPrefs](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/playerprefs),
[PlayerPrefs.Save](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/playerprefs/save).
실제 빌드·테스트에 사용하는 에디터는 6000.3.14f1이다.

샘플은 최종 표시 드라이버에서 scale=1을 유지하므로 저장된 얼굴 위치로 확대·축소 기능이 되살아나지 않는다.
이번 변경은 기존 UGUI 레이아웃·스크롤 없는 정책과 고개 미리보기 제한을 변경하지 않는다.

## 검증과 재현

`generate-calibration.mjs`는 원본 TS의 save/restore/read를 실행한다. localStorage와 Date.now만
결정적인 대체 구현으로 바꾸며 C# 결과로 기대값을 만들지 않는다. **50개 원본 시나리오**에
정상/구형/잘못된 값, 양쪽 미러, 자동·수동 보정, 보정 전 얼굴 없음, 저장소 오류와 기억 비활성을 포함한다.

- .NET **63개 통과**: CalibrationTests, DriverParityTests, 기존 24초 Tracking 시퀀스.
- Unity CLI EditMode **80개 통과**, 07:31:12Z–07:31:14Z, 2.048초.
  위 검증과 기본 Params/filter/action/vowel/voice/trace 회귀, 실제 PlayerPrefs Get/Set을 포함한다.
- Node WebGL bridge **2개 통과**: UTF-8/종단 크기·범위, 원본 키, 권한/용량 오류.
- 원본 fixture 재생성 `--check` 일치. 전체 suite를 다시 실행했다는 의미는 아니다.

```sh
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-calibration.mjs --check
node --test scripts/unity/test-calibration-bridge.mjs
unity test ./unity --mode EditMode \
  --filter 'LivLiv.Tests.CalibrationTests;LivLiv.Tests.UnityCalibrationStoreTests;LivLiv.Tests.DriverParityTests;LivLiv.Tests.ParityTests' \
  --output ./unity/TestResults/calibration.xml --timeout 240
```

브라우저 재시작 검증은 `verify-webgl-calibration.mjs`에 절대 `root`와 기존 허용 공간의
`calibrationSpaceId`를 주입해 ego-browser에서 실행한다. native 검증은 최신 `.app`/`.apk`를 Unity CLI로
빌드한 뒤 `python3 scripts/unity/verify-native-calibration.py macos|android`로 실행한다.
고유한 `qa-calibration-*` 키만 만들고 끝나면 해당 키만 제거한다. 카메라·마이크는 켜지 않는다.
실행 결과는 아래에 기록한다.


## 실제 빌드 실행

- **Chrome 156 WebGL**: 저장 → 새 페이지 로드 → 복원, 구형 값 보완 → 새 페이지 로드 → 복원,
  malformed 값 거절의 **5개 상태 Passed**. 저장 직후 브라우저 localStorage의 원본 키와 C# 반환 문자열이
  같고, 재로딩 후 timestamp까지 같은 문자열로 복원됐다. `webgl-calibration.json`에 9개 소스 hash를 기록했다.
  최종 샘플 Ready/scale=1/입력 Off, viewport와 document 888×990으로 페이지 overflow가 없다.
- **macOS ARM64 IL2CPP**: 각 단계마다 별도 player process를 실행해 동일한 **5개 상태 Passed**.
  `macos-calibration/1794209576590033000/evidence.json`에 8개 소스 hash, launcher/GameAssembly hash와
  저장/복원 결과를 기록했다. 종료 후 전용 키 삭제를 확인했다.
- **BlueStacks Android ARM64 IL2CPP**: 앱을 종료·재시작하여 동일한 **5개 상태 Passed**.
  `bluestacks/calibration/1794209683934648000/evidence.json`에 source/APK hash를 기록했다.
  전용 키 삭제를 확인하고 일반 샘플로 복귀했다. 물리 Android 기기 검증은 아니다.
- **iOS ARM64**: Unity export와 Xcode unsigned build/link **성공**, iPhoneOS app **173,643,818 bytes**.
  `ios-app.json`의 Resources/폰트/MediaPipe embedding/WebP 심볼 감사 통과. iPhone 실행은 하지 않았다.
  Xcode의 generated API/app icon 관련 경고는 남으며 서명·설치·실제 저장 복원을 확인한 결과는 아니다.

모든 경로의 Unity build report errors/warnings는 0이다. 최신 WebGL은 **53,237,732 bytes / 19.755021초**,
Android 개발 APK는 **73,581,652 bytes**, ARM64 라이브러리 9개와 16KB ELF/ZIP 정렬 감사 통과.
APK SHA-256: `40f6a821c31e982b960c2f4764374f2227df08be971ef1fa9fa17e387a57461b`.
로그: `calibration-{webgl,macos,android,ios}-build.log`, `calibration-ios-xcode.log`.
위 증거 파일은 모두 `unity/TestResults/` 아래의 로컬 산출물이며 Git에 포함하지 않는다.
Windows/Linux 및 실제 Android/iOS, Safari/mobile browser의 실행은 별도 검증이 남는다.
