# 그래픽 비교 기록 — 2026-11-09

기준: upstream `011b1f54d62c189cee49f29d3427c6d22fafdffd`, Unity 6000.3.14f1 macOS ARM64 Metal.
현재 renderer는 공용 lattice/alpha culling, update/deform/physics와 keyform 준비, 동적 mesh upload와 입·표정·soft iris 합성을 포함한다.

## 실행 방법

1. `unity test ./unity --mode EditMode --output ./unity/TestResults/editmode.xml --timeout 240`으로 GPU PNG 생성.
2. Node 22에서 `node --disable-warning=ExperimentalWarning scripts/unity/reference-server.mjs` 실행. localhost:8769만 listen한다.
3. ego-browser 스킬의 동일 TaskSpace에서 `http://127.0.0.1:8769`를 열고 `window.ready`를 기다린다.
4. `page.evaluate(input => window.renderMark(input), input)` 반환 data URL을 `browser-mark-{w}-{h}-{center|corner}.png`로 저장한다.
   사례: `(512,768,0,true)`, `(768,512,5252.5,true)`, `(512,768,0,false)`; dpr=1.
5. `page.evaluate(id => window.renderNeutral(id), id)`를 d01, char06에 실행하고 `browser-neutral-{id}.png`로 저장한다.
   이 helper는 원본 `Puppet.load({pack:false}) → update(0) → render() → Mark.draw(0,1)`을 실행한다.
   `update(0)` 없이 render만 호출하면 원본 vertex가 아직 설정되지 않아 빈 캐릭터 이미지가 된다.
6. Pillow가 설치된 Python에서 `python3 scripts/unity/compare-renders.py` 실행. 출력은 `unity/TestResults/render-comparison.json`.

서버는 원본 TS에서 타입만 제거한다. 수식이나 shader는 복제/수정하지 않는다. 같은 WebP 파일과 512×768 출력 사용.
브라우저 PNG는 straight alpha, Unity RenderTexture PNG는 premultiplied alpha이므로 비교 시 브라우저 RGB를 먼저 premultiply한다.
완전히 투명한 영역으로 오차를 희석하지 않도록 양쪽 alpha>0의 합집합에서 RGBA 채널 오차를 계산한다.

## 실제 결과

| 사례 | 활성 pixel | 채널 평균 오차 / 255 | 최대 채널 오차 | 채널 오차 >16인 pixel 비율 |
|---|---:|---:|---:|---:|
| Yui / d01 중립 | 255,328 | 0.3008 | 50.0 | 0.0184% |
| Aoi / char06 중립 | 191,315 | 0.1900 | 105.89 | 0.0758% |
| 세로 중앙+모서리 로고 | 7,901 | 1.2514 | 105.89 | 1.9744% |
| 가로 눈 감은 중앙+모서리 | 7,715 | 1.2220 | 105.89 | 1.9183% |
| 세로 모서리 로고만 | 1,279 | 4.5553 | 105.89 | 11.2588% |

원본과 Unity의 Yui 및 로고 PNG를 직접 열어 확인했다. 평균만으로 완전 동등성을 주장하지 않는다.
작은 로고에는 Canvas stroke AA와 CPU 4×4 supersampling / Gaussian shadow의 차이가 눈에 띄는 일부 pixel에 집중된다.
원본 그리기 명령의 64 사례와 cache의 35 전환은 별도 source-derived fixture로 정확히 검증한다.
GPU texture upload와 합성 방향은 공용 raster RGBA와 채널별 ±1로 검증한다.

## 공용 격자와 Canvas alpha 검증

정적 quad를 원본의 전역 격자로 교체했다. `puppet.ts`의 `buildLayer` 정점 순서·UV·삼각형,
head/hair/jaw/neck/brow 가중치와 `skinOf`를 `Runtime/runtime/puppet.cs`에 옮겼다.
21 모델의 139 레이어를 원본 함수로 직접 실행한 fixture에서 비교한다. 이 수치 fixture는 Canvas readback만 주입하며
이미지에서 얻는 brow coverage/closed lash/nasal room을 검증한 것은 아니다. 그 경계는 아래 실제 동적 이미지 fixture로 별도 확인한다.

별도 실제 이미지 검증은 **Chrome 156 / macOS의 원본 `alphaGrid`를 실행한 21 모델 514 레이어**다.
PNG/WebP를 Unity에서 디코딩한 뒤 계산한 occupancy 배열이 모두 정확히 일치했다.
경계 회귀 7개는 브라우저의 원본 alpha 이미지도 fixture로 보존해 .NET과 Unity Mono 양쪽에서 검사한다.
이 결과를 IL2CPP나 다른 브라우저 엔진 검증으로 확대하지 않는다.

단순 bilinear 축소는 원본 HTML 이미지의 결과와 달랐다. Chromium의 이미지 캐시는 먼저 ceil mip 크기로
축소하고 Canvas가 한 번 더 샘플링한다. 공용 어댑터에 두 단계, 홀수 크기 필터, pixel-center clip,
정수 Q15 보간 및 명시적 Float32 저장 경계를 구현했다. PNG는 `LoadImage`가 ARGB32를 고를 수 있어
raw Color32 reinterpretation 대신 `GetPixels32`로 채널을 정규화한다.

참고한 구현: [Chromium 이미지 캐시](https://github.com/chromium/chromium/blob/main/cc/tiles/software_image_decode_cache_utils.cc),
[mip 크기 선택](https://github.com/chromium/chromium/blob/main/cc/tiles/mipmap_util.cc),
[Skia mip 선택](https://github.com/google/skia/blob/a6c6ea6d7f1fecf5bd71d622744c0918cc6789c1/src/core/SkMipmap.cpp),
[정수 보간](https://github.com/google/skia/blob/a6c6ea6d7f1fecf5bd71d622744c0918cc6789c1/src/opts/SkRasterPipeline_opts.h).
Q15 보간을 이식한 부분의 BSD 고지/라이선스는 `ThirdParty/Skia-LICENSE.md`, 출처는 `skia-sampling.json`에 있다.
Skia 네이티브 바이너리는 추가하지 않았다. 현재 참조 Canvas의 geometric-mean mip 선택을 명시했으며,
다른 Skia 설정/버전은 픽셀 결과가 달라질 수 있으므로 브라우저 업데이트도 fixture로 감지한다.

원본 fixture를 의도적으로 갱신할 때, 위 reference server를 켜고 **ego-browser**에서만 실행한다.
아래는 저장소 루트에서 절대 경로를 안전하게 JS 선언으로 전달하는 명령이다. 두 script는 같은 TaskSpace를 재사용한다.

```sh
python3 -c 'from pathlib import Path; import json; print("const root="+json.dumps(str(Path.cwd())+"/")+";");print(Path("scripts/unity/capture-alpha-reference.mjs").read_text())' | ego-browser nodejs
python3 -c 'from pathlib import Path; import json; print("const root="+json.dumps(str(Path.cwd())+"/")+";");print(Path("scripts/unity/capture-alpha-sampling.mjs").read_text())' | ego-browser nodejs
node --disable-warning=ExperimentalWarning --import ./test/ts.mjs scripts/unity/generate-puppet.mjs --check
```

fixture에는 원본 소스/모델 SHA-256을 기록한다. 경계 fixture에는 입력 이미지 SHA-256도 포함한다.
격자로 교체한 뒤 Yui/Aoi의 PNG 비교 수치는 유지됐고, 이후 WebP premultiply 경계를 맞춘 결과를 위 표에 반영했다.

## 남은 검증

- 텍스트 watermark: 폰트 공급·Unicode glyph·600 weight 및 baseline 대응. 현재 명시적으로 missing-feature.
- 작은 로고의 AA/그림자 차이 개선, Mobile에서 재생성 비용/메모리와 프레임 예산 측정.
- 나머지 캐릭터·해상도·DPR·색 공간의 동적 프레임 비교. view/focus/pan/zoom과 cropped eye-mask RT의 추가 플랫폼 검증.
- Windows/Linux, Android/iOS, WebGL에서 동일 fixture의 실제 build/render 실행.

macOS Metal과 Chrome의 비교는 플랫폼 전체 지원이나 Player 완성을 의미하지 않는다.

## 실제 동적 프레임

`capture-animation-reference.mjs`는 unmodified 원본의 load/update/render와 Mark를 사용해
Yui(d01), Aoi(char06), Nana refined(d03ft)의 turn/blink/talk/neutral 12개 프레임을 생성한다.
turn/blink/talk는 지정한 입력을 1/60초씩 30회 적용한다. d03ft의 keyforms.json도 원본 그대로 로드한다.
각 레이어 33곳의 정점과 fol/folB/pivY/pivC/soft, nasal 값을 fixture에 저장하고 Unity 실제 이미지 경로와 비교한다.
표시되지 않는 레이어는 원본처럼 이전 정점이 남으므로 output 비교에서 제외하고 샘플 경계 값은 계속 비교한다.
원본 소스·모델·이미지·keyform의 SHA-256을 검사한다. 그림은 `TestResults/browser-animated-*.png`, `animated-*.png`다.
Nana 말하기 원본/Unity와 Aoi 눈 감김 PNG를 실제로 열어 확인했다.

입은 표정 위로 지연해서 그리고 talk 전용 texture를 선택하며, 소리가 멈춘 뒤 latch가 끝나면 표정의 정지 입으로 돌아간다.
반투명 눈 mask의 fractional alpha와 stencil 모드 전환, 6프레임(shut 포함) 입 전환/합성 순서는 synthetic GPU pixel 테스트로 별도 검증한다.
GPU 코드는 원본 bbox/2px 여백/64px 성장 규칙의 cropped RGBA mask RT를 사용한다.
Metal의 기존 full-size 출력 12개와 전체 픽셀이 일치한다. 세부 구현·WebGL 검증과 한계는 [MASK-TARGETS.md](MASK-TARGETS.md)를 참고한다.

속눈썹 곡선은 원본 Canvas RGBA 6개로 수치식을 검증하고, 실제 Unity 이미지 디코딩 뒤 같은 곡선이 나오는지도 검사한다.
WebP의 premultiplied mode는 채널×alpha/255를 버림한다. 기존 반올림과 1~2 차이가 나며 darkest-pixel 선택을 바꾸었다.
[libwebp 1.6.0 alpha 처리 구현](https://github.com/webmproject/libwebp/blob/v1.6.0/src/dsp/alpha_processing.c)의 규칙을 반영했다.
Canvas unpremultiply의 half-tie 반올림은 엔진별로 일부 색 바이트가 다를 수 있다. 6개 원본 속눈썹 곡선 결과는 허용 오차 안에서 일치하며 모든 RGBA 바이트의 동일성을 주장하지 않는다.

같은 TaskSpace에서 fixture 갱신:

```sh
python3 -c 'from pathlib import Path; import json; print("const root="+json.dumps(str(Path.cwd())+"/")+";");print(Path("scripts/unity/capture-animation-reference.mjs").read_text())' | ego-browser nodejs
python3 -c 'from pathlib import Path; import json; print("const root="+json.dumps(str(Path.cwd())+"/")+";");print(Path("scripts/unity/capture-lash-reference.mjs").read_text())' | ego-browser nodejs
```

동적 PNG의 활성 영역 비교 (255 기준; PNG straight-alpha 재변환의 양자화 오차 포함):

| 모델/자세 | 평균 채널 오차 | 최대 | >16 pixel 비율 |
|---|---:|---:|---:|
| char06-blink | 0.2206 | 105.89 | 0.1765% |
| char06-neutral | 0.1900 | 105.89 | 0.0758% |
| char06-talk | 0.2458 | 105.89 | 0.1098% |
| char06-turn | 0.3724 | 105.89 | 0.2519% |
| d01-blink | 0.3217 | 77.00 | 0.0736% |
| d01-neutral | 0.3008 | 50.00 | 0.0184% |
| d01-talk | 0.3482 | 50.00 | 0.0333% |
| d01-turn | 0.4401 | 63.00 | 0.1415% |
| d03ft-blink | 0.1973 | 85.00 | 0.1010% |
| d03ft-neutral | 0.1709 | 55.00 | 0.0207% |
| d03ft-talk | 0.3064 | 55.00 | 0.1317% |
| d03ft-turn | 0.5584 | 128.00 | 0.5874% |

이 수치는 회귀 진단이며 완전한 pixel parity 판정이 아니다. 최대 오차에는 로고 AA와 일부 texture/mesh 경계가 포함된다.


## 소유자 텍스트 워터마크

원본 `drawMarkText` 명령을 Unity Font/TextGenerator의 glyph coverage로 연결했다.
기본 NanumGothic 또는 host의 Font를 사용하며 글리프 누락을 모델 교체 전 검사한다.
문자열·DPR·PNG·리소스 생명주기의 Editor/실제 WebGL 검증과 폰트/Canvas 차이는
[TEXT-MARK.md](TEXT-MARK.md)에 기록했다. 이 경로의 성공을 system-ui와 픽셀 동등성으로 해석하지 않는다.
