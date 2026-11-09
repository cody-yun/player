# LivLiv Player for Unity — development package

Source-mapped port of livliv-dev/player 0.3.0. Unity 6000.3.14f1 is the verified editor.
See `compatibility.json` for the pinned upstream commit, file mapping and incomplete coverage.
The original implementation and Unity platform adapters remain separate.

Install this local `.tgz` with Unity Package Manager's **Add package from tarball**.
Use `ai.livliv.ugui` for the Canvas/RawImage host. Native face input additionally needs
the pinned homuler MediaPipe 0.16.3 tarball and `ai.livliv.mediapipe`.
WebGL face input is included here and does not use the native MediaPipe adapter.
Its default face module URL includes Unity's build GUID so updates do not reuse a previous build's
cached bridge code. A custom BrowserFaceOptions.moduleUrl is used verbatim; version that URL in the host.
Dependencies on Unity modules and Newtonsoft JSON are declared in `package.json`.

```csharp
// Call on Unity's main thread, after its graphics device has initialized.
if (!LivLiv.Unity.Player.IsSupported()) return;
var target = new UnityEngine.RenderTexture(512, 768, 24,
    UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.Linear);
target.Create();
var player = new LivLiv.Unity.Player(target, new LivLiv.PlayerOptions { idle = true });
await player.LoadAsync("https://example.com/character.livliv");
// Call player.Tick(Time.unscaledDeltaTime) from your host's frame loop.
// On teardown: player.Dispose(); target.Release(); Destroy(target).
```

Models contain `model.json`, textures and optional keyforms. The package includes no example characters;
obtain a model package from LivLiv Studio or provide a compatible package with appropriate rights.
Camera/microphone input starts only through the host's explicit actions. For Android StreamingAssets,
use an asynchronous loader to obtain bytes rather than assuming filesystem access.

Device adapters may implement `LivLiv.Platform.ICaptureStatus`. In the host's main-thread loop,
read `Error` before retiring a failed input, stop its FaceTracking/LipSync driver, and offer an
explicit restart. Native microphone capture and native/WebGL face/voice adapters expose this status.
Terminal capture failure releases the owned device resources; silence and a missing fresh frame
are not terminal errors. Stop all input drivers on background/teardown even when no error is reported.

This is a development port, not a completed platform-support release. Windows execution and physical
mobile/Intel/device validation remain incomplete. Native MediaPipe requires macOS 13+.
Model texture support is PNG/WebP/JPEG. `Player.IsSupported()` probes the graphics device, layer shader
and a temporary render target; codecs, model contents and input permissions are validated when used.
Native/WebGL dependencies, font and license/provenance notices
are in `ThirdParty/`; retain these when redistributing. Repository verification tests are excluded
from the local tarballs because their fixtures depend on the upstream checkout and character assets.
