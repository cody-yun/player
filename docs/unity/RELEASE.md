# Player와 렌더 대상 해제

원본 `src/release.ts`의 지연 반환·claim 취소·반환 후 `released` 오류를
`Runtime/release.cs`의 `CanvasRelease`로 대응한다. 원본은 변경하지 않았다.
Unity 경계는 `Runtime/Platform/Unity/Player.Release.cs`와 `Player.cs`다.

## 기본 해제와 명시적 반환

```csharp
player.Dispose(); // driver/모델/소유 GPU 객체 해제. 호출자 소유 RenderTexture 저장소는 유지.

// 이 논리적 렌더 대상을 더 이상 쓰지 않을 때:
player.Dispose(new LivLiv.Unity.PlayerDisposeOptions { release = true });
```

두 번째 호출은 현재 대상의 GPU 저장소를 다음 Unity main-thread 작업에서 반환한다.
RenderTexture 객체 자체는 호출자 소유로 남는다. 객체까지 필요 없으면 호출자가 Destroy한다.
일반 Dispose 뒤에 옵션을 넣어 다시 호출하는 것도 원본처럼 허용한다.
호출과 대상 변경은 Unity main thread에서 수행한다.

반환이 완료된 RenderTexture는 `Create()`로 GPU 저장소를 다시 만들어도 새 Player가 거절한다.
새 RenderTexture 객체를 사용해야 한다. 재사용할 풀의 대상에는 기본 Dispose를 사용한다.
PlayerSurface는 Player와 대상을 직접 소유하므로 일반적인 component teardown 경로를 유지한다.
호스트는 빌려 받은 Surface.Player를 별도로 해제하는 대신 Surface의 수명주기를 사용한다.

## 같은 작업에서 교체

```csharp
old.Dispose(new LivLiv.Unity.PlayerDisposeOptions { release = true });
var next = new LivLiv.Unity.Player(sameTarget); // await/yield 전의 즉시 claim: 대기 중인 반환 취소
```

새 Player는 renderer 자원을 만들기 전에 대상을 claim한다. 실제로 다른 대상으로 옮기는 Target 변경도
claim하며, 같은 값을 다시 대입하는 것은 새 claim이 아니다. 반납 전의 교체는 취소할 수 있지만
이미 반납한 대상은 되살리지 않는다. 거절된 retarget은 현재 대상과 수신 등록을 유지한다.

반환 시 같은 대상을 이미 사용하는 다른 Player에는 멈춰 있는 경우에도 `OnContextLost`를 한 번 알린다.
다른 대상으로 이동한 Player는 이전 대상의 알림에서 해제된다. 별도 RenderTexture의 Player는 유지된다.
기존 컨텍스트 유실 계약처럼 영향받은 호스트는 입력/Player를 정리하고 새 Player로 복구한다.

## Unity의 소유권 경계

브라우저 원본은 해당 canvas의 WebGL 컨텍스트를 포기한다. Unity에서는 엔진이 컨텍스트를 공유하므로
개별 Player가 `WEBGL_lose_context`를 호출하지 않는다. 옵션이 반환하는 것은 해당 RenderTexture의
GPU 저장소다. 브라우저의 canvas 개수 제한이나 Unity 엔진 컨텍스트 종료를 재현하는 API가 아니다.
이 차이를 플랫폼 경계에 명시하고 원본 순서·상태 기계와 분리했다.

## 검증과 원본 업데이트

- 원본 함수를 실제 실행해 만든 10개 작업 순서: 보통 반환, 즉시 claim, 취소 뒤 재요청,
  중복 요청, 컨텍스트 없음/확장 없음, 여러 대상, backend 실패, 예약 실패.
- 전체 .NET 1,293개·Unity EditMode 1,392개·PlayMode 29개 통과. PlayMode에는 대상 수명주기 6개가 있다.
- 실제 player probe는 기본 버퍼 보존, 취소, 반환 후 거절, 기존 같은 대상 소유자 알림,
  retarget 분리와 별도 Player의 PNG alpha를 검사한다. 메인 Player의 프레임도 계속 진행해야 한다.
- 결과와 빌드 경계는 `unity/TestResults/release-port/`에 기록한다. Windows 실행 환경 없음과 iOS 실기기
  보류는 유지하며 컴파일과 실행 증거를 구분한다.

통합 증거는 `unity/TestResults/release-port/evidence.json`이다. 최종 실행은 다음 범위다.

| 실행 환경 | 결과 | 원시 증거 (TestResults 기준) |
| --- | --- | --- |
| Chrome WebGL | 10개 상태 조건, alpha 1,862 pixels, 메인 프레임 진행 | `release-port/browser-final.json` |
| macOS ARM64 Metal | 동일 조건 통과 | `macos-release/1794231620003513000/evidence.json` |
| BlueStacks GLES3 | 동일 조건 통과 | `bluestacks/release/1794232032182442000/evidence.json` |
| Linux x64 Docker/Mesa | 동일 조건 통과, 소프트웨어 GPU | `linux-release/1794231733731672000/evidence.json` |

브라우저 검증 후 Ready·입력 Off·배율 1·스크롤 없음으로 복구하고 자체 Ego 공간 31을 닫았다.
Android는 별도로 Unity 옵션 없는 일반 실행 Ready/12초 생존도 통과했다.
7개 CLI 빌드는 `build-refresh/1794231536825242000/evidence.json`의 105개 소스 해시에 대응한다.
iOS는 unsigned link/ARM64·포함 자산 검사까지이며 실행 증거가 아니다.
새 tarball 두 소비자에서 명시적 반환을 실행한 결과는 `release-port/consumer-audit.json`과
`native-consumer-audit.json`이다. macOS x64는 컴파일했지만 이번 API의 별도 실행은 하지 않았다.

fixture는 C# 결과로 만들지 않는다. Node 22에서 다음 명령으로 원본 결과를 다시 확인한다.

```sh
node --import ./test/ts.mjs scripts/unity/generate-release.mjs --check
dotnet test tools/Parity/Parity.csproj --filter FullyQualifiedName~ReleaseParityTests
unity test ./unity --mode PlayMode --filter ReleaseTargetTests
```

`compatibility.json`의 release 모듈과 base 어댑터 목록에 연결해 upstream 변경 영향에 함께 표시한다.
브라우저 전용 컨텍스트 포기와 여러 플랫폼의 실기기 검증까지 완료했다는 뜻은 아니므로 partial을 유지한다.
