using LivLiv.Runtime;
namespace LivLiv.Platform
{
    // Bounded, top-down coverage including shadow padding. When shadowColor is set,
    // bounds may extend ceil(1.5 * blur) pixels beyond the canvas to preserve shadow ink.
    // Existing providers returning canvas-clipped bounds remain supported.
    // Font/shaping stays outside the source-mapped core; coverage is limited to 16M pixels.
    public sealed class MarkTextCoverage
    {
        public int left,top,width,height;
        public float[] alpha;
    }
    // Optional preflight so unsupported fonts fail before replacing a loaded model.
    public interface IMarkTextValidator { void Validate(string text); }
    public interface IMarkTextRasterizer
    {
        MarkTextCoverage Rasterize(int canvasWidth,int canvasHeight,BrandDraw command);
    }
}
