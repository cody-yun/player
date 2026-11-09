# 비동기 모델 로드

## 선택한 파일 목록의 패키지 처리

`Player.LoadAsync(IReadOnlyDictionary<string,byte[]>)`와 `ModelPackage.FromFiles`는 원본
`index.ts/fromFiles`처럼 파일 목록 안의 단일 `.livliv`/ZIP도 연다. 파일 확장자가 달라도 ZIP magic으로
판단한다. 별도 파일 선택 UI는 포함하지 않으며 host가 선택 결과를 byte[] 목록으로 전달한다.

```csharp
await player.LoadAsync(new Dictionary<string, byte[]> {
    ["avatar.livliv"] = selectedBytes
});
```

- `model.json`이 직접 있으면 ZIP보다 우선한다. 여러 JSON 중 경로 문자열이 가장 짧은 것을 사용한다.
- JSON이 없으면 열거 순서의 첫 ZIP이 전체 목록을 대체한다. 그 ZIP이 손상되거나 모델이 없어도
  다음 ZIP으로 넘어가지 않는다. ZIP 안의 다른 ZIP을 재귀적으로 열지 않는다.
- GLB magic의 단일 바이트 입력이나 JSON 없는 `.vrm`/`.glb` 파일 목록은 원본의 `not-a-model` 진단을
  반환한다. 진단의 BodyPlayer/VrmBody 명칭은 원본 안내이며 Unity 포트가 그 기능을 제공한다는 뜻은 아니다.
- 기존 `FromFiles(files, lang)` 서명은 유지한다. 추가 overload의 CancellationToken을 ZIP 해제까지
  전달하며 `Player.LoadAsync`는 현재 load job의 토큰을 사용한다. 실패/취소는 이전 모델을 유지한다.

원본 private 함수를 파일 수정 없이 Node loader에서 노출해 실행한 22개 사례를
`Fixtures/package-source.json`에 보존했다. 생성기는 `scripts/unity/generate-package-source.mjs`이며
Node 22에서 `--import ./test/ts.mjs`로 실행하고 `--check`로 읽기 전용 비교한다.
처음 17개 사례 중 수정 전 8개 실패를 재현했다. 후속 22개 원본 사례와 취소 2개는 .NET에서 통과했다.
Unity PlayMode는 실제 Aoi ZIP과 파일 목록 로드의 PNG 일치, 잘못된 첫 ZIP과 선행 취소의 모델 보존을
확인했다. 원시 증거는 `unity/TestResults/package-source/`다. 빌드와 실제 플랫폼 실행 범위는 별도로 기록한다.

## 그래픽 컨텍스트 유실

원본 `onContextLost`의 공통 상태 처리를 `OnContextLost`/`NotifyContextLost()`에 연결했다.
확인된 그래픽 컨텍스트 유실은 메인 스레드에서 한 번 전달한다. 상태는 되돌리지 않으며
진행 중인 로드는 `LivLivError("context-lost")`로 취소되고 이후 draw/로드를 차단한다.
드라이버와 렌더러의 소유권은 명시적 Dispose까지 유지한다. 이는 원본의 유실 처리 순서와 같다.
WebGL Player는 공용 Unity canvas의 `webglcontextlost`를 JS listener 하나로 구독하고,
AOT 콜백으로 각 살아 있는 Player에 직접 전달한다. Unity Tick을 기다리지 않는다.
Dispose는 등록을 해제하며 마지막 Player가 해제되면 canvas listener도 제거한다.
이미 유실된 컨텍스트에 등록해도 즉시 알린다. 각 기존 Player의 유실 상태는 복구 이벤트 후에도 유지한다.
Chrome 실제 WASM에서 Unity canvas의 `WEBGL_lose_context`로 WebGL 컨텍스트를 유실시켜
직접 AOT 콜백을 검증했다. 살아 있는 Player 두 개에 각각 1회, 미리 Dispose한 Player에는 0회 알렸다.
콜백에서 두 Player를 Dispose한 뒤 canvas 유실 리스너 제거 1회도 확인했다.
`gl.isContextLost()`는 true였으며 JS에서 C# 결과를 읽었으므로 단순 DOM 이벤트 모형 검사가 아니다.
이전 Player는 유실 상태를 유지하고, 같은 QA 페이지를 reload해 새 Unity 런타임이 Ready로 시작함을 확인했다.
Native 호스트는 확인된 이벤트를 직접 전달한다. 실제 하드웨어 드라이버 리셋/모바일 OS 메모리 회수는 미검증이다.
Unity의 일반 타깃 resize/Release나 앱 pause를 컨텍스트 유실로 간주하지 않는다.

공통 유실/기존 load/player 회귀는 .NET 17개, Unity EditMode 21개 통과했다.
Unity 그래픽 검사 1개에서 실제 Player의 동기/비동기 로드가 유실 후 디코딩 전에 거절되는지도 확인했다.
TRX/XML·CLI 로그·소스 해시는 `unity/TestResults/context-loss/`에 보존했다.
WebGL JS 등록/알림/재진입 해제 검사 4개와 AOT/WASM 빌드도 통과했다.
`unity/TestResults/context-loss/webgl/node.log`, `build.log`가 증거다.
이 JS 검사는 VM 이벤트 모형이며 실제 브라우저 GPU 유실 또는 C# 콜백 실행 증거는 아니다.
후속 Chrome 실행 증거는 `unity/TestResults/context-loss/webgl/browser.json`과 `browser.log`다.
소스·실행 WASM/data/framework SHA-256을 함께 기록했다. Ego 공간 21은 검증/재로드 후 닫았다.
첫 브라우저 검사에서는 입력/네트워크 로드를 실행하지 않았다.

후속 검사에서 응답 본문을 16바이트만 보내고 대기시키는 실제 HTTP 모델 요청과 유실을 겹쳤다.
최초 실행은 C# 알림/Dispose 후에도 loadState가 loading에 남아 10초 제한에 실패했다.
`loading-browser-before-fix.log`, `loading-browser-fixed.log`에 초기/중간 실패를 보존했다.
Unity 프레임 정지로 Task.Yield와 외부 await의 진행을 기다리는 경로가 원인이었다.

- UnityModelTransport는 토큰 취소 시 요청을 직접 Abort하고 취소 신호가 프레임 대기를 깨운다.
- LoadCoordinator는 공개 Task를 별도로 완료해 내부 작업이 취소에 응답하지 않아도 context-lost를 즉시 전달한다.
  뒤늦은 성공은 이미 실패한 공개 Task를 바꾸지 않는다. 기존 모델 commit/취소 회귀를 유지한다.
- JS AOT 경계는 Player 등록 시의 SynchronizationContext를 보존·복원해 유실 취소의 C# continuation이
  Unity의 멈춘 프레임을 기다리지 않고 처리될 수 있게 한다.

수정 후 Chrome 검사에서 실제 gl.isContextLost=true, fetch의 AbortSignal 발생,
loadState=context-lost와 loadAwaitCompleted=true를 확인했다. 서버 응답 gate는 이 확인 이후에만 해제했다.
입력 Off/scale=1의 새 페이지 시작도 확인했다. 하드웨어 드라이버 리셋·입력 동시 부하는 포함하지 않는다.
공통 .NET 회귀 18개, Unity EditMode/그래픽 회귀 23개가 통과했다.
증거는 `unity/TestResults/context-loss/webgl/loading-browser.json`, `loading-browser-final.log`,
최종 코드는 `loading-regression-fixed-build.log`, `loading-browser-verified.log`, 상위 디렉터리의
`loading-regression-final.trx`, `unity-loading-verified.xml`에 대응한다.
일반 Chrome HTTP 로드의 404/ZIP/folder/길이 미상/재시도/불량 패키지/취소/교체 9개 기록도 통과했다.
`loading-regression.json`, `loading-regression-final.log`를 보존했다.
중간에 완료 전달의 ConfigureAwait(false)로 WebGL 정상 성공 통지가 멈춘 실패도 재현했고,
완료 통지의 Unity 실행 컨텍스트를 유지해 수정했다. 해당 실패는 `loading-regression.log`에 남겼다.
외부 worker-thread 취소는 Unity API를 직접 호출하지 않고 캡처된 Unity 컨텍스트의 정리를 기다리도록 한다.
이 경로도 실제 Unity Editor/Metal에서 loopback HTTP를 Task.Run의 별도 스레드로 취소해 확인했다.
취소 완료 후 실행 스레드가 원래 Unity 스레드이고 기존 ModelInfo/renderer가 유지되며,
다음 정상 HTTP 로드도 성공함을 검사했다. 기존 HTTP 회귀와 함께 2개 검사가 통과했다.
증거: `unity/TestResults/context-loss/native/worker.xml`, `worker.log`.
최신 tarball도 두 새 소비자에서 공개 유실 통지 → 동기/비동기 로드 거절 → 해제를 통과했다.
자체 파일 대조는 core+UGUI 237개, native 포함 262개이며 두 PNG와 재패키징 해시가 같다.
증거는 `unity/TestResults/context-loss/*-consumer.xml`, `*-consumer-audit.json`, `reproducibility.json`과
[INSTALL.md](INSTALL.md)에 기록한 두 소비자 프로젝트다. 이 소비자 검사는 Editor PlayMode이며 native player 실행을 대신하지 않는다.
후속 macOS ARM64/Metal 및 BlueStacks ARM64/GLES3 IL2CPP player에서도 IsSupported=true,
확인된 유실 알림 1회와 이후 동기/비동기 로드 거절, 명시적 해제까지 통과했다.
지원 검사 타깃은 종료 후 0개였고 기존 Surface 7개/카메라 8개/마스크 8개/텍스트 8개 및 포스터 회귀도 통과했다.
이 native 검사는 NotifyContextLost를 직접 호출하며 실제 드라이버 유실이나 native HTTP 중단 검사는 아니다.
증거: `unity/TestResults/macos-presentation/1794219561859004000/`,
`unity/TestResults/bluestacks/presentation/1794219650651823000/`와 `context-loss/native/`의 빌드/실행 로그.
두 CLI 빌드는 errors/warnings 0이다. Android APK는 53,497,322 bytes,
SHA-256 `d1a0c9ff2752da253c7ba72388ccc6b7a1e7d2e7981b5ad76900446981a7f143`이며 포함 자산/서명/ARM64/16KB 감사를 통과했다.
Linux IL2CPP Docker/Mesa도 같은 지원 확인/명시적 유실/로드 거절·표시 회귀를 통과했다.
증거: `unity/TestResults/linux-presentation/1794219801346229000/`와 `context-loss/desktop/`.
Windows는 최신 Mono 빌드·관리 DLL/플러그인/입력 자산 감사까지 통과했다. [DESKTOP.md](DESKTOP.md).

샘플 호스트에도 유실 콜백을 연결했다. 실제 입력/대기 중인 시작 요청을 Stop·취소하고 Player를 Dispose한다.
fatal/유실/Dispose 이후에는 StartFace/StartMicrophone을 거절한다. 뒤늦은 비동기 완료가 Report를 호출해도
Context lost 상태를 유지한다. 이는 원본 계산 코어 밖의 샘플 수명주기 정책이다.
WebGL QA의 다중 Player observer도 이 샘플 콜백을 덮어쓰지 않고 함께 등록한다.

Chrome 실제 WASM에서 생성 portrait 영상과 650Hz 오디오로 얼굴 추론/립싱크를 활성화한 뒤
Unity canvas를 WEBGL_lose_context로 유실시켰다. 영상/음성 트랙은 모두 ended,
샘플 AudioContext는 closed, microphone/tracking=false, Player disposed를 확인했다.
별도 실행에서는 getUserMedia 두 요청을 대기시킨 뒤 유실시키고 늦게 스트림을 전달해 즉시 ended됨을 확인했다.
유실 뒤 재시작 호출은 새 요청/트랙을 만들지 않았으며 각 실행 후 reload로 Ready/입력 Off를 복구했다.
물리 장치는 사용하지 않았다.
증거: `unity/TestResults/context-loss/webgl/capture-browser.json`, `capture-pending-browser.json`,
각 CLI 로그와 `capture-build.log`. 최초 QA 버튼이 canvas 뒤에 놓였던 실패 로그도 별도로 보존했다.
Ego 공간 22는 같은 페이지에서 실패를 조사·재검증한 뒤 정상 재로드 후 닫았다.

Android 샘플에도 같은 정책을 포함해 다시 빌드했다(errors/warnings 0).
`verify-android-capture-loss.py`는 기본 OpenGLES3 APK에서 BlueStacks 카메라/마이크를 실제 활성화하고
명시적 NotifyContextLost를 두 번 호출한다. 실제 드라이버 리셋 검사는 아니다.
입력 두 개 종료·Player disposed·콜백 한 번·재시작 거절을 통과했고, 생성 카메라/AudioClip은 각각 0으로 복귀했다.
Android AppOps에도 두 입력의 실행 중 상태가 남지 않았다. 검사 후 일반 샘플로 재실행했다.
같은 APK의 MAIN/LAUNCHER 일반 실행 생존 검사와 ARM64/서명/정렬 감사도 통과했다.
APK는 53,256,254 bytes, SHA-256 `5d838db1446898d18230d509e71980ae97bbfba6713b6362685cb3f1da6b63b4`.
증거: `unity/TestResults/bluestacks/capture-loss/1794221064882548000/`,
`android-startup/1794221046722379000/`, `android-capture-loss-build.log`, `android-apk.json`.
다른 native 샘플 빌드 갱신과 물리 Android/GPU 장애 검증은 남아 있다.

`Player.LoadAsync`는 PC·Android/iOS·WebGL에 공통인 API다. Unity 메인 스레드에서 호출한다.

```csharp
using var lifetime = new CancellationTokenSource();
var info = await player.LoadAsync("https://your-host.example/models/aoi.livliv", new LoadOptions {
    cancellation = lifetime.Token,
    onProgress = p => Debug.Log(p.step == "build" ? "Building" : $"Download {p.progress:P0}")
});
```

절대 HTTP(S) URL의 `.livliv`/`.zip`, 폴더 URL, `model.json` URL을 받는다. 바이트 배열 ZIP,
`IReadOnlyDictionary<string, byte[]>`, 이미 읽은 `ModelPackage` 오버로드도 있다.
Unity에는 `document.baseURI`가 없으므로 상대 URL은 호출자가 절대 URL로 해석한다.
URL query는 model.json 및 이미지 요청에 유지한다. 파일 경로는 기존 모델 schema 검사를 거친다.

## 원본 대응

| 원본 | Unity |
|---|---|
| `base.ts start/LoadOptions/LoadJob` | `Runtime/loading.cs`, `PlayerBase.StartLoad` |
| `index.ts load/loadNow/fetchPackage` | `Unity/Player.cs`, `Platform/ModelDownload.cs` |
| `puppet.ts load/locate/loadModel` | `ModelDownload`, `ModelPackage`, `PuppetRenderer.Load` |
| `base.ts readBody` | `UnityModelTransport` / bounded `DownloadHandlerScript` |

새 로드는 진행 중인 이전 로드를 취소한다. 미리 취소된 토큰은 기존 로드에 영향을 주지 않는다.
실패·취소 시 이전 모델과 Info를 유지하며, Dispose는 진행 중인 다운로드를 취소한다.
완료된 GPU commit 뒤의 취소는 이미 성공한 작업을 소급해서 실패시키지 않는다.
동기 `Load(ModelPackage)`도 이전 비동기 로드를 교체한다.

진행률은 증가하는 `download` 값과 한 번의 `build` 이벤트다. ZIP은 다운로드 바이트 비율,
폴더는 완료한 이미지 수의 비율이다. Content-Length가 없거나 Content-Encoding이 있으면
중간 바이트 비율을 추측하지 않는다. 파일/바이트 입력에는 `build`만 발생한다.
진행률 콜백의 예외는 기록하고 로드를 계속한다.

ZIP HTTP 다운로드만 네트워크 실패 또는 5xx 응답 때 1회 재시도한다. 4xx·취소·용량 초과는
재시도하지 않는다. 폴더 이미지 실패는 `missing-file`, model.json HTTP 실패는 `http`이며
선택적 keyforms 실패는 기존 warning/rig fallback을 따른다. `textures.bin`은 SDK 원본처럼 요청하지 않는다.

`tex`가 없으면 원본의 `JSON.stringify(layers) + Last-Modified` 31배 UTF-16 해시/base36을 사용한다.
CLR/Mono의 JSON 숫자 직렬화 차이를 이 해시 경계에서 정규화한다.
원본을 직접 실행하는 `scripts/unity/generate-loading.mjs`가 진행률 6개 시퀀스와 모델 21개의 해시를 고정한다.

## 플랫폼 어댑터의 의도적인 차이

- 동시에 가져오는 이미지는 최대 6개다. 원본은 모든 이미지를 한 번에 요청한다.
  WebGL의 단일 스레드 실행을 위해 ThreadPool/SemaphoreSlim 대기열 대신 Unity context를 유지하는 6개 async 루프를 사용한다.
- 수신 중 개별 응답과 보관된 모델 바이트에 256 MiB 상한을 적용한다. JSON 4 MiB, keyforms 32 MiB 제한도 유지한다.
  이는 전체 프로세스 peak memory 상한이 아니다. 브라우저/네트워크 및 디코딩 버퍼는 별도 메모리를 사용한다.
- 다운로드 timeout은 요청당 60초다.
- CancellationToken을 사용하므로 브라우저 AbortSignal의 임의 `reason` 값은 전달하지 않는다.
  호출자는 `OperationCanceledException`(파생 `TaskCanceledException` 포함)으로 취소를 처리한다.
- 네트워크 전송은 await로 양보하지만 JSON/ZIP 검사, 이미지 디코딩 및 GPU 모델 교체는 메인 스레드의 동기 작업이다.
  큰 모델의 프레임 중단/메모리 peak를 모바일 실기기에서 추가 측정해야 한다.
- CSS poster 상태는 `LoadOptions.poster` / `Player.Poster` / `PosterChanged`로 대응한다. 선택 UGUI 어댑터는 PNG/WebP/JPEG를 표시한다. 형식별 검증 범위는 [POSTER.md](POSTER.md). snapshot은 [SNAPSHOT.md](SNAPSHOT.md)의 공개 API로 연결했다.
- WebGL은 동일 출처 또는 서버의 CORS 허용이 필요하다. model.json 재검증용 `Cache-Control: no-cache` 요청 헤더도 허용해야 한다. HTTPS 페이지에서 HTTP 모델을 요청하면 브라우저 정책이 적용된다.
  Mobile의 HTTPS/cleartext 정책도 우회하지 않는다. 실서비스 모델 URL은 HTTPS를 사용한다.

## 검증

공통 NUnit은 작업 교체·선취소·콜백 재진입·Dispose·진행률·재시도와 소스 해시를 검사한다.
`LoadingGraphicsTests`는 자체 loopback HTTP 서버와 실제 UnityWebRequest/PNG/WebP/GPU 모델 설치로
ZIP·폴더·재시도·실패 후 이전 모델 유지·취소·교체·수신 용량 제한·Dispose를 검사한다.
WebGL 재현용 loopback origin은 `python3 scripts/unity/serve-load-tests.py`다.
이 서버는 외부 네트워크에 공개하지 않고 `127.0.0.1:8771`에서만 응답한다.

최신 결과(2026-11-09): 공통 13 tests + Unity HTTP/GPU 1 test 통과. WebGL IL2CPP 빌드 errors/warnings 0,
Chrome에서 ZIP/폴더/길이 미지정/503 재시도/404/손상 ZIP/취소/구 작업 교체/새 작업 완료 9개 시나리오 통과.
`unity/TestResults/webgl-loading.json`에 진행률 이벤트·모델 보존·실행 프레임 수·현재 코드 hash를 기록한다.
실제 Android/iOS HTTP 다운로드와 CORS/기기별 네트워크 정책, 모바일 메모리 peak는 별도 실기기 검증 대상이다.

```sh
# 별도 터미널: loopback 검증 서버
python3 scripts/unity/serve-load-tests.py
# WebGL CLI 빌드 이후, 기존 ego-browser TaskSpace 3을 사용하며 root를 절대 경로로 주입
python3 - <<'PYCODE' | ego-browser nodejs
from pathlib import Path
import json
print('const root='+json.dumps(str(Path.cwd())+'/')+';')
print(Path('scripts/unity/verify-webgl-loading.mjs').read_text())
PYCODE
```

Unity의 수신 콜백/중단 계약: [DownloadHandler.ReceiveData](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/networking/downloadhandler/receivedata).
API 문서 조회 버전은 6000.6이며, 실제 컴파일/실행 검증은 고정된 Editor 6000.3.14f1에서 수행했다.
