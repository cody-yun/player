# LivLiv native face input

Optional native adapter for `ai.livliv.player`, tested with Unity 6000.3.14f1 and
homuler MediaPipe Unity Plugin 0.16.3 (MediaPipe 0.10.22). Install the exact upstream
release tarball before installing this package. The repository verification project uses
`node scripts/unity/prepare-mediapipe-native.mjs` to restore the SHA-256-pinned tarball.
This adapter assembly excludes WebGL; use the core package's `WebGLFaceInput` there.

```csharp
// Unity main thread, in the user's camera-start action. Provide the task model as bytes.
// For APK/URL/StreamingAssets loading, supply bytes through the host's asynchronous asset loader.
var input = await LivLiv.Unity.MediaPipe.WebCamFaceSource.StartAsync(modelBytes,
    new LivLiv.Unity.MediaPipe.NativeFaceOptions { preferFront = true, maxFps = -1 }, token);
var face = LivLiv.FaceTracking.Attach(player, input,
    LivLiv.Unity.MediaPipe.NativeFaceInput.Now, new LivLiv.FaceTrackingOptions { follow = 1 });
// face.Calibrate(); face.Stop(); // stops the owned capture and destroys the native task
```

Use the same `face_landmarker.task` pinned by `ai.livliv.player/ThirdParty/mediapipe-webgl.json`.
This initial portable path uses CPU inference on every native platform. It performs synchronous
VIDEO inference on the Unity main thread; performance and GPU/worker alternatives remain work.
It does not claim numerical equivalence between native MediaPipe 0.10.22 and browser Tasks Vision 1.1.1.

The host must stop the session on background/teardown and restart explicitly on foreground.
The bundled macOS ARM64/x64 MediaPipe dylib requires macOS 13.0 or newer; set the host minimum OS accordingly.
Set camera usage descriptions on macOS/iOS and camera permission on Android; permission and hardware
behavior require target-device validation. Android also needs the NDK C++ shared runtime required
by the upstream plugin. See `docs/unity/NATIVE-FACE.md` for setup and current evidence.

`INativeFaceSource` supports host-provided recorded/live images. `TryRead` returns a readable
RGBA32 Texture2D whose **first memory row is the upright image's top**; this differs from ordinary
Unity texture display storage. The source owns its texture; `NativeFaceInput` takes ownership of
and disposes the source, including constructor failure. Calls must remain on the Unity main thread.
`Next` consumes at most one new frame and uses strictly increasing integer millisecond task timestamps.

`NativeFaceInput` also implements `LivLiv.Platform.ICaptureStatus`. The host can read `Error` on
the Unity main thread and stop its tracking driver while keeping the character renderer alive.
The webcam adapter reports a stopped active WebCamTexture; custom sources may optionally implement
the same status interface to report a terminal failure. NativeFaceInput then disposes its task and
source once. A temporarily unready source or no new frame does not by itself indicate device failure.

Homuler's Z-axis matrix conversion is undone before forwarding column-major values to the original
LivLiv tracking math. Webcam orientation corrects vertical mirroring and clockwise 0/90/180/270 degrees;
horizontal user mirroring remains the existing `FaceTrackingOptions.mirror` calculation.

Actual checks: macOS ARM64 Editor CPU inference on a public test photo, face loss/darkness,
source ownership, and synthetic matrix/orientation tests. macOS ARM64 IL2CPP/Metal player smoke also passed with generated video. Android ARM64 APK and unsigned iPhoneOS app build/link audits passed. macOS x64 IL2CPP/Metal also passed generated video on Apple Silicon/Rosetta (not physical Intel). Linux x64 IL2CPP passed generated video in Ubuntu 24.04 Docker/Mesa. Android BlueStacks/GLES3 execution passed. Physical mobile/Intel/Windows devices and Windows runtime remain unverified. See docs/unity/MACOS.md, DESKTOP.md and BLUESTACKS.md for scope.
