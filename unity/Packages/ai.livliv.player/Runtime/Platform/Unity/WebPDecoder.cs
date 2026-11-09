using System;
using System.Runtime.InteropServices;

namespace LivLiv.Unity
{
    // The renderer only needs a size probe and RGBA decode. Size is checked before allocating output.
    // Keeping this independent of Texture2D also permits a host-provided platform decoder.
    public interface IWebPDecoder
    {
        bool GetInfo(byte[] data,out int width,out int height);
        bool DecodeRgba(byte[] data,byte[] output,int width,int height);
    }

    public sealed class NativeWebPDecoder : IWebPDecoder
    {
#if (UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
        // iOS: linked static library. WebGL: Emscripten-built libwebp linked into the player WASM.
        // The same C API is used; a desktop dylib must never be included in these builds.
        private const string Library="__Internal";
#else
        // Platform-specific DLL/dylib/so supplied by the native build matrix.
        private const string Library="livlivwebp";
#endif
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]
        private static extern int WebPGetInfo(byte[] data,UIntPtr size,out int width,out int height);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]
        private static extern IntPtr WebPDecodeRGBAInto(byte[] data,UIntPtr size,byte[] output,UIntPtr outputSize,int stride);
        public bool GetInfo(byte[] data,out int width,out int height)=>WebPGetInfo(data,(UIntPtr)data.Length,out width,out height)!=0;
        public bool DecodeRgba(byte[] data,byte[] output,int width,int height)=>WebPDecodeRGBAInto(data,(UIntPtr)data.Length,output,(UIntPtr)output.Length,width*4)!=IntPtr.Zero;
    }
}
