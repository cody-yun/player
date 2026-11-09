# LivLiv UGUI presentation

Optional adapter for `ai.livliv.player`. Requires Unity 6000.3 and UGUI 2.0.0.
The renderer and source-mapped controllers remain independent of UGUI.

Place a dedicated RawImage behind the character RawImage under the same display-bounds parent.
Disable this RawImage initially if it has no background texture. Add `LivLiv.Unity.UI.PosterPresenter`
(which also requires AspectRatioFitter), then bind it on Unity's main thread:

```csharp
presenter.Bind(player, new System.Uri("https://example.com/characters/"));
await player.LoadAsync("https://example.com/characters/aoi.livliv",
    new LivLiv.LoadOptions { poster = "cover.webp" });
```

PNG/WebP images fit in the parent, centered without cropping. Only an empty player shows a poster;
a loaded character remains visible during replacement. The first drawn frame removes the poster.
`LastError` and `Failed` report image errors without failing the model load.

The presenter owns only downloaded textures and requests. Disable cancels/restores the original image/layout;
Enable resumes the current poster. `Unbind` releases the binding; it never disposes the Player or caller textures.
Encoded images are limited to 32 MiB and the player's decoder pixel limits. Downloads time out after 60 seconds.
Relative URLs need a base URI on native platforms; WebGL defaults to the page URI. Browser CORS applies.
Base64 PNG/WebP data URLs are also supported. JPEG/SVG and animated posters are not implemented.

Current execution evidence: Unity Editor PlayMode, desktop Chrome/WebGL, macOS ARM64/Metal and
BlueStacks Android ARM64/OpenGLES3 IL2CPP. Native poster probes use a controlled PlayerBase host and
real file/data decoding, without HTTP. Physical mobile devices and other targets remain unverified.
See docs/unity/NATIVE-PRESENTATION.md for native evidence and limitations.
The repository's docs/unity/POSTER.md contains source mapping, reproduction scripts and the full evidence scope.


## PlayerSurface

Add `PlayerSurface` to a sized RectTransform under a Canvas. It requires RawImage and owns its Player, premultiplied
UI material and linear render target. Call `Initialize(PlayerOptions, IMarkTextRasterizer)` before the first LateUpdate to customize
options and the optional owner-text font adapter, or access `surface.Player` for lazy defaults. Use that Player's normal Load/driver/Snapshot API.
The default density cap is 2 and pixel budget is 4,194,304; `MaxPixelRatio`/`MaxPixels` are configurable.
Zero-area surfaces release their target; resizing reuses or replaces storage, including paused redraw.
Disable stops frame pumping while retaining the model and Playing state. Destroy releases owned resources and restores
caller texture/material. Pointer/capture input remains host-controlled. Do not overwrite the owned Player.Target or RawImage bindings.
Hard rectangular clipping and stencil masks are supported; soft clipping remains pending.
Camera surfaces size from all four projected edges so perspective tilt accounts for the nearer edge.
Screen Space Camera, nested Canvas and World Space perspective/orthographic rendering were checked through Editor tests
and eight built-player states on macOS ARM64/Metal and BlueStacks ARM64/GLES3.
XR/stereo, near-plane crossings and multiple rendering cameras with different densities are not validated.
Repository docs/unity/SURFACE.md contains source mapping and actual Unity/WebGL/native evidence.

Owner text uses the player's included NanumGothic font by default. For a host font, pass
`new LivLiv.Unity.UnityMarkText(includedDynamicFont)` as `markText` to `Initialize`.
The font is borrowed; keep it alive while the Player uses it. Missing glyphs fail before model replacement.
See repository docs/unity/TEXT-MARK.md for shaping, appearance and platform limitations.
