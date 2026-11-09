// src/base.ts snapshot(): draw(0), then canvas.toDataURL('image/png').
// Unity targets hold premultiplied RGBA; PNG must contain straight RGBA.
using System;
using UnityEngine;

namespace LivLiv.Unity
{
    public sealed partial class Player
    {
        /// <summary>The current pose and package watermark as a PNG data URL. Call on Unity's main thread.</summary>
        public string Snapshot() => "data:image/png;base64," + Convert.ToBase64String(SnapshotPng());

        /// <summary>PNG bytes without a data URL allocation. Does not advance animation or change Playing/visible.</summary>
        public byte[] SnapshotPng()
        {
            var target = Target;
            if (target == null) throw new InvalidOperationException("LivLiv: snapshot needs a render target");
            // Bound the synchronous GPU readback and CPU copies before drawing or allocating an image.
            if ((long)target.width * target.height > 16 * 1024 * 1024)
                throw new LivLivError("too-large", "LivLiv: snapshot exceeds 16M pixels");
            if (!target.IsCreated()) throw new InvalidOperationException("LivLiv: snapshot target is not created");
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                Draw(0); // Deliberately draws paused/hidden players, just like the source snapshot().
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true)
                    { name = "LivLiv snapshot readback", hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                var pixels = image.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    if (c.a == 0) c.r = c.g = c.b = 0;
                    else
                    {
                        c.r = Straight(c.r, c.a); c.g = Straight(c.g, c.a); c.b = Straight(c.b, c.a);
                    }
                    pixels[i] = c;
                }
                image.SetPixels32(pixels);
                // Encoding consumes the readable CPU copy; no GPU upload or mipmaps are needed.
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                TextureDecoder.Destroy(image);
            }
        }
        private static byte Straight(byte channel, byte alpha) => (byte)Math.Min(255, (channel * 255 + alpha / 2) / alpha);
    }
}
