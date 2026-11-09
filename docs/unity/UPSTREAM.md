# 원본 업데이트 검토와 리허설

## 실제 upstream 업데이트 적용 — 2026-11-09

공식 Git remote를 `upstream=https://github.com/livliv-dev/player.git`로 추가하고 main을 fetch했다.
원격 main/HEAD는 `d999dea88981124168f9e9ab948eda8d2f23c453`, 기존 기준은
`011b1f54d62c189cee49f29d3427c6d22fafdffd`였다. 차이는 issue #8의 expression label 타입 수정이다.
원본 `src/base.ts`는 expressions getter 반환 타입을 `Info['expressions']`로 명시했고
`test/types.mjs`는 Player/BodyPlayer의 label/labels 타입과 미정의 필드 거절을 검사한다.
런타임 계산 변경은 없다. 먼저 기존 baseline에서 감사가 exit 1로 두 변경과 간접 영향/fixture를 탐지함을 확인했다.

별도 detached worktree에 후보 원본을 준비했다. upstream에 lockfile이 없어 npm ci는 실패했으며
해당 임시 worktree에서만 npm install --ignore-scripts로 의존성을 받고 lockfile을 생성했다.
원본 build와 test:types가 node16/nodenext/bundler 세 설정 모두 통과했다. 의존성 설치 로그를 보존했다.
개발 checkout에는 node_modules를 복사하거나 원본 package.json을 변경하지 않았다.

Unity Player에는 Info.expressions 외에 대응 public getter가 없어 `ModelExpression[] Expressions`를 추가했다.
빈 상태/실패한 교체/정상 교체/Dispose와 영어·일본어 label 및 전체 translations를 Unity 테스트로 확인했다.
현재 BodyPlayer는 미구현 상태를 유지하며 해당 Unity 타입 지원까지 완료했다고 보지 않는다.
플랫폼 어댑터 검토 목록에는 Player metadata/context-loss와 공용 ICaptureStatus 경계도 보완했다.

검토 후 로컬 main을 upstream/main으로 fast-forward했다. 원본 tracked 파일 변화는 위 2개이며
compatibility.json 기준 commit/해시를 새 원본으로 갱신했다. 기존 3개 fixture를 원본 생성기로 다시 만들고
sourceHashes를 제외한 JSON 전체가 이전과 완전히 같음을 검증했다(460 frames, 6 progress sequences/21 versions, 8 poster scenarios).
현재 작업 파일/명시 upstream ref 감사는 각각 모듈 25개·fixture 22개, issues/graphNotes 0이다.
.NET 전체 1,269 passed, 감사 도구 Node 12 passed. Unity/API 및 배포 소비자 결과도 아래 증거에 보존한다.
Unity EditMode 영향 범위 41개와 최신 tarball의 새 core+UGUI 소비자 PlayMode 1개/239파일 감사도 통과했다.
소비자는 Expressions의 구체 타입/번역 map을 직접 읽고 렌더·유실·해제 회귀도 확인했다.
원본 타입 테스트용 의존성 lockfile을 증거로 복사한 후 임시 worktree를 제거했다.
새 baseline에서 WAV 리허설도 다시 통과했다. 이제 예상 변경 파일을 고정 1개로 가정하지 않고 Git diff와
감사 결과를 대조하며, WAV 변경과 voice 간접 영향은 필수로 확인한다. 역사 후보에는 base.ts 타입 수정 차이도
포함된다고 기록한다. 21개 WAV 사례 중 과거의 잘못된 rate 12개 검출 및 현재 fixture/포트/작업 파일 보존 검사는 유지했다.
로컬 main의 기준만 이동했으며 origin/main은 기존 `011b1f5`를 가리킨다.

후속 IL2CPP 실행: Android ARM64/BlueStacks GLES3 및 macOS x64/Rosetta Metal 앱을 새 기준으로 빌드했다.
각 10개 생성 영상 상태에서 Player.Expressions의 개수 4와 대표 smile의 이름/영어 번역을 직접 읽었다.
원래의 회전/얼굴 유실/거리 scale 고정과 source 오류 격리/재시작/한 번 해제를 유지했다.
Android는 일반 MAIN/LAUNCHER 실행/Ready/12초 생존과 서명·ARM64·16KB 정렬 감사도 통과했다.
물리 Android/Intel 장치를 사용한 검사는 아니다. 증거는 `upstream-latest/players/`에 추가 기록한다.
같은 기준 WebGL IL2CPP build/link도 errors/warnings 0으로 통과했다. Chrome 실제 WASM에서도 생성 입력의
종료/재시작 6개 상태 모두 표정 개수/대표 smile 이름·번역·scale=1을 확인했다.
추가로 macOS ARM64 Metal과 Linux Docker/Mesa도 10개 상태에서 metadata/입력 수명주기를 통과했다.
Windows Mono build/파일 감사와 iOS 최신 export/unsigned Xcode link/심볼·자산·metadata 감사도 통과했다.
현재 전체 회귀는 .NET 1,269 · Unity EditMode 1,364 · PlayMode 23, failed/skipped 0이다.
증거는 `upstream-latest/{desktop,ios,current-full}/` 및 `browser.json`이다. Windows 실제 실행·물리 장치는 별도다.

증거: `unity/TestResults/upstream-latest/`의 `candidate.json`, `applied-{working,ref}.json`,
`fixture-comparison.json`, 원본 build/types/설치 로그, `all.trx`, `unity.xml`, `expressions.xml` 및 소비자 결과.
이전 기준을 사용하는 아래 WAV 리허설과 기능별 player 빌드는 당시 소스의 이력이다.

원본의 25개 모듈은 `unity/Packages/ai.livliv.player/compatibility.json`에서 C# 파일과
플랫폼 어댑터로 연결한다. `check-upstream.mjs`는 후보 커밋을 checkout하거나 실행하지 않고
변경과 영향 범위를 JSON으로 출력한다. 포팅 완료 판정이나 baseline 자동 갱신 도구가 아니다.

## 설치와 실행

저장소 루트에서 Node 22.18 이상을 사용한다. 원본 `package.json`은 변경하지 않고
별도 `tools/UpstreamAudit`에 TypeScript parser 6.0.3과 npm integrity lock을 고정했다.

```sh
npm --prefix tools/UpstreamAudit ci --ignore-scripts
node scripts/unity/check-upstream.mjs
node scripts/unity/check-upstream.mjs --ref upstream/main > unity/TestResults/upstream-candidate.json
```

`--ref`에는 **이미 로컬에 존재하는** branch/tag/commit을 지정한다. 보고서에는 해석한 전체 commit ID가
기록된다. 이 명령은 fetch하지 않는다. 새 원본을 받을 때 fork `origin`과
`https://github.com/livliv-dev/player`의 `upstream`을 먼저 구별한다.
ref 없이 실행하면 현재 파일을 읽으므로 아직 추적되지 않은 `src/*.ts` 추가도 감지한다.

종료 코드:

- `0`: 현재 검사 범위에서 변경/불일치 없음. pending/partial 기능의 완료나 모든 테스트 통과를 뜻하지 않는다.
- `1`: 검토할 소스·계약·자산 변경, 매핑/fixture 불일치 또는 소스 구문 오류가 발견됨.
- `2`: 잘못된 인자·ref·manifest 형식 또는 parser 설치 등으로 감사 자체를 수행하지 못함.

## 보고서 읽기

| 필드 | 의미 |
|---|---|
| `sourceChanges` | 추가·삭제·내용 변경과 이전/후 SHA-256, 대응 C# 경로·포팅 상태 |
| `impactedModules` | 직접 변경과 해당 모듈을 참조하는 간접 영향, 변경 원인 목록, C#/어댑터 검토 파일 |
| `otherChanges` | API/포맷/의존성/스키마/테스트/캐릭터/빌드 파일 등 원본의 나머지 변경 |
| `fixtureReview` | 선언된 sourceHashes와 영향받는 모듈을 연결한 기존 fixture, 참조 테스트/생성기, 읽기 전용 `--check` 명령 |
| `issues` | 소스 변경 외에 baseline/hash 불일치, 누락된 port/adapter, 잘못된 fixture 출처 등 |
| `graphNotes` | 비문자열 동적 import, 해석하지 못한 상대 import처럼 사람이 검토할 항목 |
| `unimplemented` | 매핑은 있으나 pending인 모듈. 해시 일치와 구현 완료를 혼동하지 않도록 별도 표시 |

소스 의존성은 TypeScript AST의 import/type import/reexport/literal dynamic import를 따른다.
이전·후보 그래프를 합쳐 삭제/이름 변경 전의 참조도 유지하며 cycle은 한 번만 순회한다.
따라서 영향 범위는 보수적인 모듈 단위다. 실행 call graph나 타입 검사 결과가 아니다.
전역 상태, 문자열로 만든 경로, 외부 라이브러리/별칭, 런타임 계약의 영향은 수동 검토가 필요하다.
`graphNotes`가 없더라도 완전한 semantic 영향 분석을 의미하지 않는다.

`otherChanges`에서는 이 포팅 전용 `unity/`, `tools/`, `docs/unity/`, `scripts/unity/`를 제외한다.
원본이 나중에 같은 디렉터리를 도입한다면 제외 정책을 먼저 재검토한다.
이름이 바뀐 파일은 추가/삭제로 보고하며 rename 추정으로 자동 매핑하지 않는다.

fixture 출처 해시는 **baseline commit의 실제 내용**과 다시 비교한다.
manifest/hash만 새 값으로 덮어써 변경을 숨기거나, fixture 출처만 바꿔 최신으로 표시하면 실패한다.
테스트 참조는 실제 파일명 문자열을 연결한 안내다. 테스트가 모든 동작을 검증한다는 증거가 아니므로
새 API/동작에는 별도 테스트를 추가한다. `checkCommands`는 현재 checkout을 실행하므로 후보 커밋 검토 후
그 코드를 준비한 별도 작업 디렉터리에서 실행해야 한다. 감사 도구 자체는 이 명령들을 실행하지 않는다.

## 적용 순서

1. baseline과 후보의 전체 commit ID 및 JSON 보고서를 보관한다. 원본 Git diff를 직접 읽는다.
2. `sourceChanges`의 C#과 `impactedModules`의 어댑터/호스트를 검토한다. 계산 순서·기본값·좌표·단위를 유지한다.
   새 모듈은 mapping을 추가하고 미구현 기능은 pending으로 남긴다.
3. 기존 fixture의 출처와 변화할 기대값을 검토한다. 원본 코드를 준비한 별도 작업 디렉터리에서
   원본 테스트와 해당 생성기의 `--check`를 실행한다. 실패 이유를 확인한 뒤 원본으로만 새 기대값을 생성한다.
4. 대응 C# 수치 테스트 → Unity CLI 테스트 → 필요한 renderer/브라우저/장치 실행을 순서대로 확인한다.
   의존 모듈/플랫폼 영향이 남아 있으면 통과 범위를 넓혀서 기록하지 않는다.
5. 코드·fixture·상태·문서와 실제 검증 증거를 함께 검토한 뒤 baseline commit/module hashes를 의도적으로 갱신한다.
   이후 `check-upstream.mjs`가 새 원본 기준에서 통과하는지 확인한다. 자동 갱신 옵션은 제공하지 않는다.

## 2026-11-09 리허설 증거

로컬 Git 이력의 실제 WAV 수정 `98eeda1b942706a7b8b97bd3169f961943ac0a2a`와
그 이전 `42a483ae9205e342bda91d063601ce91d4319f84`를 사용했다.
현재 baseline `011b1f54d62c189cee49f29d3427c6d22fafdffd`는 이미 이 수정을 포함한다.
최신 원격 fetch/새 업데이트 적용의 증거가 아니라, 알려진 수정 전/후를 이용한 실패 감지 리허설이다.

- 이전 커밋을 후보로 감사: exit 1, `src/runtime/speech.ts` 직접 변경 1개,
  `src/voice.ts` 간접 영향, C#/어댑터 검토 파일 총 10개를 식별했다.
- 원본 `test/bodies.html`, `test/smoke.mjs`의 관련 테스트 변경도 탐지했다.
- `drivers.json.gz`, `speech-wav.json.gz`, `speech.json.gz` 및 실제 참조 C# 테스트/생성기를 연결했다.
  상태 알림 fixture 추가 후 재실행에서는 `speech-state.json`까지 4개가 검토 대상으로 나왔다.
- 수정 전 `Incoming` 코드를 임시 폴더에서 실행: WAV `--check`가 실패했다.
  출처 해시를 제외하고 결과를 비교해도 21개 중 12개(8/16/44.1/48 kHz × 1/2/3채널)의 rate가
  잘못된 기본 24 kHz였다. PCM 샘플·fallback 결과는 같았다.
- 수정 후 원본은 같은 21개 fixture에 `--check` 성공. 기존 포트의 SpeechWavReader가
  실제로 sampleRate와 샘플을 비교하는 테스트를 포함해 .NET **81/81**, Unity CLI EditMode **81/81** 통과.
  구성은 SpeechWavTests 26, SpeechTests 39, DriverParityTests 8, SpeechStateTests 8이다.
- 도구의 별도 Node **12/12** 통과: 추가/삭제/이름 변경, 간접 import/cycle, 누락된 port/adapter,
  baseline/fixture 해시만 갱신하거나 출처를 제거한 경우 감지, 자산/빌드 파일 변경, dirty working tree와 명시 ref 분리,
  parse 오류, 잘못된 ref, 읽기 전용 보존을 검사했다.
- 상태 알림 추가 후 현재 작업 파일 감사는 모듈 25개·fixture 22개, issues/graphNotes 0이다.
  manifest/원본/대표 포트/fixture 해시와 Git 상태가 리허설 전후 같았다.

재현 명령:

```sh
node --test scripts/unity/test-upstream-audit.mjs
node scripts/unity/rehearse-upstream.mjs
dotnet test tools/Parity/Parity.csproj --no-restore --filter 'FullyQualifiedName~SpeechWavTests|FullyQualifiedName~SpeechTests|FullyQualifiedName~SpeechStateTests|FullyQualifiedName~DriverParityTests' --logger 'trx;LogFileName=upstream-rehearsal.trx' --results-directory unity/TestResults/upstream-rehearsal
unity test ./unity --mode EditMode --filter 'SpeechWavTests;SpeechTests;SpeechStateTests;DriverParityTests' --output ./unity/TestResults/upstream-rehearsal/unity.xml --timeout 240 -- -logFile "$PWD/unity/TestResults/upstream-rehearsal/unity.log"
```

로컬 증거는 `unity/TestResults/upstream-rehearsal/`의 `evidence.json`, `historical.json`, `current.json`,
수정 전/후 fixture 로그, `upstream-rehearsal.trx`, `unity.xml`, `unity.log`다.
Node 검사 로그는 `unity/TestResults/upstream-audit-tests.log`다. 실행 결과는 Git에서 제외한다.
리허설은 원본·포트·baseline을 고치지 않고 임시 디렉터리만 정리한다.
기능별 장치 실행과 전체 suite는 이번 81개 테스트의 검증 범위에 포함하지 않는다.
