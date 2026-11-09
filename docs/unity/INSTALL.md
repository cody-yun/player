# 다른 Unity 프로젝트에 설치

현재 포트는 개발 버전 0.1.0이며 Unity **6000.3.14f1**로 검증한다.
전체 플랫폼/실기기 지원 완료 릴리스는 아니다. 이 문서는 검증 저장소의 Assets나 Library를 복사하지 않고
UPM 패키지를 소비자 프로젝트에 설치하는 경로다.

최신 로컬 core는 경계 수정·[명시적 대상 반환](RELEASE.md)·[파일 목록 ZIP 선택](LOADING.md)을 포함한다:
13,488,605 bytes, SHA-256 `3ddfe8ec16b1768ba08e0f70559c390dd7d6981bc14fec50fdfff5df73417653`.
core+UGUI 소비자 `unity/TestResults/consumers/1794232476916076000/`와 native 포함 소비자
`1794232477125284000/`에서 각각 PlayMode 1개, 선택 ZIP의 픽셀 일치·명시적 반환·설치 파일 감사를 통과했다.
증거는 `unity/TestResults/package-source/consumer-audit.json`, `native-consumer-audit.json`이다.
동일 소스로 7개 대상 빌드와 .NET 1,317/Unity EditMode 1,416/PlayMode 30 전체 회귀를 갱신했다.
파일 목록 ZIP 경로는 Unity/Metal에서 실행했다. 다른 플랫폼의 새 경로 실행과 물리 모바일/Safari,
Windows 실행 게이트는 미검증이다. `unity/TestResults/package-source/evidence.json`.
아래 이전 해시/실행 기록은 해당 시점 결과다. 현재 자체 tarball 사본은 `package-source/distribution/`에도 보존했다.

## 로컬 배포 파일 만들기

저장소 루트에서 기존 native/browser 의존성 준비와 해시 검사를 마친 후 실행한다.

```sh
node scripts/unity/check-native-artifacts.mjs
python3 scripts/unity/pack-upm.py
```

`unity/TestResults/upm-distribution/`에 다음 파일이 생성된다. 외부 서비스에 게시하지 않는다.

- `ai.livliv.player-0.1.0.tgz`: 원본 대응 코어·렌더러·native/WebGL 입출력 및 WebGL vendor assets.
- `ai.livliv.ugui-0.1.0.tgz`: RawImage PlayerSurface와 로딩 PosterPresenter.
- `ai.livliv.mediapipe-0.1.0.tgz`: 선택적 네이티브 얼굴 입력 어댑터.
- `com.github.homuler.mediapipe-0.16.3.tgz`: SHA-256으로 고정한 변경 없는 외부 의존성.
- `manifest.json`: 각 자체 archive/파일 SHA-256, 외부 의존성의 출처·해시.
- `INSTALL.md`: 이 안내.

자체 archive는 정렬 순서·시간·소유자·권한·gzip 헤더를 고정한다. Tests/fixture와 원본 캐릭터는 제외한다.
Runtime/Editor/Plugins/Resources/Browser~/ThirdParty, package.json, .meta 및 라이선스는 포함한다.
샘플 캐릭터는 별도 이용 조건이 있으므로 runtime 배포 파일에 넣지 않는다.

## 설치와 선택 의존성

Unity Package Manager에서 **Add package from tarball**로 core → UGUI 순서로 설치한다.
네이티브 얼굴 추적이 필요하면 homuler tarball → LivLiv MediaPipe 어댑터 순서로 추가한다.
core/UGUI만 사용하는 프로젝트에 homuler 의존성을 강제하지 않는다.
WebGL 얼굴 추적은 core의 WebGLFaceInput을 사용한다.

CLI/버전 관리에서는 모든 tarball을 소비자 프로젝트의 `LocalPackages/`에 복사한 뒤
`Packages/manifest.json`의 기존 dependencies에 아래 항목을 **병합**할 수 있다.
기존 manifest 전체를 덮어쓰지 않는다. file 경로는 `Packages/` 폴더 기준이다.

```json
{
  "dependencies": {
    "ai.livliv.player": "file:../LocalPackages/ai.livliv.player-0.1.0.tgz",
    "ai.livliv.ugui": "file:../LocalPackages/ai.livliv.ugui-0.1.0.tgz",
    "com.github.homuler.mediapipe": "file:../LocalPackages/com.github.homuler.mediapipe-0.16.3.tgz",
    "ai.livliv.mediapipe": "file:../LocalPackages/ai.livliv.mediapipe-0.1.0.tgz"
  }
}
```

Unity registry 의존성은 UPM이 해결하므로 최초 설치에는 네트워크 또는 해당 UPM 캐시가 필요하다.
homuler의 원본 tarball에는 Unity Test Framework 의존성도 선언되어 있다. 이를 임의 수정하지 않는다.
패키지 갱신 시 새 archive와 해시를 함께 검토하고 소비자 lock 파일을 갱신한다.
현재 고정 버전의 tarball을 덮어쓴 개발 빌드는 정식 버전 업그레이드로 간주하지 않는다.

## 최초 캐릭터 표시

Canvas 아래 크기가 지정된 RectTransform/RawImage를 만들고 PlayerSurface를 붙인다.

```csharp
var surface = characterRect.gameObject.AddComponent<LivLiv.Unity.UI.PlayerSurface>();
var player = surface.Initialize(new LivLiv.PlayerOptions { idle = true });
await player.LoadAsync("https://example.com/character.livliv");
// PlayerSurface가 프레임 갱신/크기/소유 리소스 해제를 담당한다.
```

완성된 LivLiv 모델 패키지를 호스트가 제공한다. URL은 실제 모델 URL로 바꾸고 WebGL CORS를 허용한다.
로컬/StreamingAssets의 bytes를 사용할 때는 호스트에서 비동기로 수신한 뒤 `ModelPackage.FromZip(bytes)`를
`player.Load`에 전달한다. APK 내부 StreamingAssets를 일반 파일 경로로 가정하지 않는다.
모델·카메라·마이크를 자동으로 시작하는 샘플은 배포 패키지에 포함하지 않는다.
얼굴/음성 입력과 권한·background 중지는 호스트가 명시적으로 연결한다.

macOS native MediaPipe는 **13.0 이상**을 요구한다. Android/iOS 권한 선언과 모델 task bytes,
대상별 player settings는 소비자 프로젝트에서 설정한다. WebGL은 secure context·사용자 gesture가 필요하다.
이 설치 검증이 해당 장치나 권한 흐름의 실행 검증을 대신하지 않는다.

## 독립 소비자 검증 재현

```sh
python3 scripts/unity/prepare-consumer.py
# 출력된 새 프로젝트 경로를 사용한다. 아래 placeholder를 실제 경로로 바꾼다.
unity test <consumer-project> --mode PlayMode --filter ConsumerSmoke --output <absolute-result-xml-path> --timeout 300
python3 scripts/unity/check-consumer.py <consumer-project> --results <absolute-result-xml-path>
# 선택적 native 어댑터를 제외한 별도 새 프로젝트도 검사한다.
python3 scripts/unity/prepare-consumer.py --without-native
```

매번 Library가 없는 새 프로젝트를 생성한다. core/UGUI/native adapter와 homuler를 tarball로 설치하고,
검증용 모델만 그 임시 프로젝트의 Assets에 복사한다. 저장소 Verification scene/코드를 사용하지 않는다.
소비자 검증용 Test Framework/assembly만 추가해 실제 PlayMode에서 공개 API로
모델 로드·paused 렌더·PNG snapshot·UGUI 소유권 해제를 실행한다.
`Evidence/result.json`, `consumer.png`와 Unity 로그, `Packages/packages-lock.json`이 증거다.
에디터 PlayMode 렌더 검증이며 새 소비자 player build/실제 카메라 입력까지 완료했다는 의미는 아니다.

## 확인한 결과 — 2026-11-09

현재 원본 기준은 `d999dea`다. 타입 label 수정 검토 후 `Player.Expressions`를 추가한 최신 tarball은
새 core+UGUI 소비자 `unity/TestResults/consumers/1794222432566989000/`에서 PlayMode 1개/239파일 감사를 통과했다.
소비자 코드가 ModelExpression[]의 label과 번역 JObject를 직접 읽어 타입/내용 보존을 확인했다.
증거: `unity/TestResults/upstream-latest/consumer*`. 이후 아래 수치는 각 이전 배포의 이력이다.

최신 tarball은 WebGL 장치 track ended 정리와 네이티브 입력의 선택적 ICaptureStatus를 포함한다.
기본 WebGL 얼굴 모듈은 Unity build GUID로 URL을 구분해 이전 모듈 캐시와 새 WASM의 혼합을 막는다.
이 수정을 포함한 새 core+UGUI 소비자 `unity/TestResults/consumers/1794221824265495000/`는
PlayMode 1개와 자체 파일 239개 감사를 통과했다(`unity/TestResults/device-ended/final-consumer*`).
직전 ICaptureStatus 추가 시점의 core+UGUI+native 소비자 `unity/TestResults/consumers/1794221535071076000/`에서
PlayMode 1 passed, failed/skipped 0, 설치 파일 264개 감사를 통과했다.
증거는 `unity/TestResults/native-capture-end/native-consumer*`다. 출력 PNG는 아래와 동일하다.
이는 Editor 설치/렌더 회귀이며 브라우저나 물리 장치 분리 검증은 아니다.
직전 WebGL 종료 처리만 포함한 core+UGUI 소비자는 `unity/TestResults/consumers/1794221280502638000/`에서
PlayMode 1개/237파일을 통과했다(`unity/TestResults/device-ended/core-consumer*`).
아래 두 소비자 표는 그보다 앞선 core의 검증 이력이다. UGUI tarball은 그때와 바이트가 같다.

현재 패키지는 컨텍스트 유실 알림과 프레임 정지 중 로드 취소 수정까지 포함한다. 두 새 소비자에서
IsSupported → 렌더 → 유실 통지 → 동기/비동기 로드 거절 → 해제를 확인했다.

`Player.IsSupported()`는 Unity 그래픽 장치 초기화 후 메인 스레드에서 호출한다.
셰이더와 작은 임시 렌더 타깃 생성을 검사하고 즉시 해제한다. codec/모델/입력 권한 검증은 각각 사용하는 시점에 수행한다.
Metal의 10회 반복/호출자 타깃 보존과 실제 `-nographics`의 Null 장치 false/무할당 검사가 통과했다.
증거는 `unity/TestResults/player-support/`다. 이 API가 실제 카메라·마이크 사용 가능 여부를 뜻하지는 않는다.

직전 로컬 배포는 정점 변형 최적화·JPEG·Player.IsSupported()·컨텍스트 유실 처리를 포함한다. Unity CLI 6000.3.14f1 / macOS ARM64 / Metal에서
Library가 없는 두 새 프로젝트의 PlayMode 로드·320×480 렌더·해제와 설치 캐시 감사를 통과했다.

| 설치 | PlayMode | 자체 파일 감사 | 증거 프로젝트 |
|---|---|---:|---|
| core + UGUI | 1 passed, failed/skipped 0 | 237 | `unity/TestResults/consumers/1794219411554464000/` |
| core + UGUI + native + homuler | 1 passed, failed/skipped 0 | 262 | `unity/TestResults/consumers/1794219411359975000/` |

두 출력은 활성 픽셀 76,141개이며 PNG SHA-256은
`d03ed85d550305ffc9252e982c163bad637eab1a08f4ac6f8a8140c9db036909`로 일치한다.
캐시 파일은 tarball과 대조하며 package.json의 UPM 추가 `_fingerprint`만 별도 검증한다.
테스트/캐릭터는 배포물에서 제외했다. 소비자 PlayMode 검증은 player build나 실제 입력 장치 검증을 대신하지 않는다.

현재 tarball:

- ai.livliv.player: 13,485,691 bytes, SHA-256 `c46a8fedeef7f6e77ed436ad8b77a5771a072833e79e85a3a66b8bd6c4fa0f0d`.
- ai.livliv.ugui: 11,533 bytes, SHA-256 `03bcd78e3a3baf79ecc2f66f4fe4523276a62484aa0f890788f29609cc51fdee`.
- ai.livliv.mediapipe: 599,472 bytes, SHA-256 `332cb64913ce1232db1fbfb73c4ffc1dbf854ed04ab6c685ebf7c0b9cbbe8ce1`.

`unity/TestResults/context-loss/core-consumer.xml`, `native-consumer.xml`과 각 `*-audit.json`,
각 소비자의 `Evidence/`, `consumer-inputs.json`, manifest/lock에 입력과 결과를 보존했다.
같은 소스로 다시 묶어 archive/manifest 일치를 확인한 기록은 `unity/TestResults/context-loss/reproducibility.json`이다.

<details>
<summary>JPEG 추가 전 검증 이력</summary>

정점 변형 [성능 변경](PERFORMANCE.md)을 포함해 tarball을 다시 만들고 두 새 소비자 프로젝트에서 재검증했다.
같은 입력으로 두 번 묶은 manifest/archive 해시도 일치했다.

Unity CLI 6000.3.14f1 / macOS ARM64 / Metal에서 두 개의 새 프로젝트를 각각 검사했다.

| 설치 | PlayMode | 캐릭터 출력 | 해제 | 설치 캐시 감사 |
|---|---|---|---|---|
| core + UGUI + native + homuler | 1 passed, failed/skipped 0 | 320×480, 불투명도 >0인 픽셀 76,141 | Player/target/material 해제 확인 | 자체 패키지 256파일 확인 |
| core + UGUI | 1 passed, failed/skipped 0 | 같은 결과 | 같은 결과 | 자체 패키지 231파일 확인; lock에 native/homuler 없음 |

두 이미지의 SHA-256은 `d03ed85d550305ffc9252e982c163bad637eab1a08f4ac6f8a8140c9db036909`로 같다.
UGUI에 전달되는 renderer snapshot을 저장·직접 확인했으며, 전체 OS 창 스크린샷은 아니다.
설치 캐시의 자체 패키지 모든 파일은 archive 원본 바이트와 일치했다.
예외는 각 `package.json`에 UPM이 추가하는 `_fingerprint`뿐이며, 이 필드만 제거한 JSON 전체가 원본과
같은지 검사하고 fingerprint가 PackageCache 폴더와 일치하는지도 확인했다.
원본 archive 해시는 별도로 검사한다. 원본 캐릭터와 Tests 디렉터리가 배포 archive에 없음을 확인했다.

자체 tarball 크기는 core 13,481,070 bytes, UGUI 11,524 bytes, native adapter 599,081 bytes다.
고정 homuler 의존성은 별도 원본 tarball이다. 같은 입력으로 다시 묶어 세 archive와 manifest가 동일함을 확인했다.
JPEG 추가 전 최적화 스냅샷 archive SHA-256:

- core: `3208ee72e3c476b4f4de5143954cde1156d0cb5cd9204d9e5b4d65d2bbb19a31`
- UGUI: `690ab33d91c3e30ac0a89708aae6e9dbccdde5796ea5e7bd643f99bff7c6c694`
- native adapter: `b699d50d840ca667d6e487a2719c211187d8e81910b4161d72081e856844bcef`

Git 제외 로컬 증거:

- `unity/TestResults/post-optimization/native.xml`, `native-audit.json`.
- `unity/TestResults/post-optimization/core.xml`, `core-audit.json`.
- `unity/TestResults/consumers/1794217065630783000/Evidence/`: native 포함.
- `unity/TestResults/consumers/1794217065821049000/Evidence/`: core+UGUI.
- 각 소비자의 manifest/lock과 `consumer-inputs.json`에 설치 입력을 보존했다.
- `unity/TestResults/post-optimization/reproducibility.json`, `upm-distribution/manifest.json`.

최초 검증 스크립트의 PackageInfo 이름 충돌과 EditMode에서 실행한 MonoBehaviour 수명주기 검증은
실패 로그(`consumer-import.log`, `consumer-final-import.log`)로 보존했다.
소비자 검증을 실제 PlayMode로 옮긴 위 결과가 최종 증거다. 이를 런타임 패키지 결함으로 분류하지 않는다.

설치 형식 근거: [Unity local tarball paths](https://docs.unity.com/en-us/engine/6000.6/manual/packages-list/dependencies-lp/upm-localpath).


</details>
