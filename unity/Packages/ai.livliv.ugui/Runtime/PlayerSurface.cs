// Unity host for src/index.ts Player and puppet.ts render-size policy. Does not open capture devices.
using System;
using LivLiv.Platform;
using UnityEngine;
using UnityEngine.UI;
namespace LivLiv.Unity.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class PlayerSurface:MonoBehaviour
    {
        [SerializeField,Min(.01f)] private float maxPixelRatio=2;
        [SerializeField,Min(1)] private int maxPixels=4194304;
        private RawImage image;
        private Player player;
        private RenderTexture target;
        private Material material,originalMaterial;
        private Texture originalTexture;
        private readonly Vector3[] corners=new Vector3[4];
        private bool destroyed;
        public Player Player=>Initialize();
        public RenderTexture Target=>target;
        public bool ResolutionLimited {get;private set;}
        public double MaxPixelRatio
        {
            get=>maxPixelRatio;
            set{if(double.IsNaN(value)||double.IsInfinity(value)||value<=0||value>float.MaxValue)throw new ArgumentOutOfRangeException(nameof(value));maxPixelRatio=(float)value;}
        }
        public int MaxPixels {get=>maxPixels;set{if(value<1)throw new ArgumentOutOfRangeException(nameof(value));maxPixels=value;}}
        // Call before the first LateUpdate to choose Player options, then use the normal Player API.
        public Player Initialize(PlayerOptions options=null,IMarkTextRasterizer markText=null)
        {
            if(destroyed)throw new ObjectDisposedException(nameof(PlayerSurface));
            if(player!=null){if(options!=null||markText!=null)throw new InvalidOperationException("PlayerSurface is already initialized");return player;}
            if(options!=null)MaxPixelRatio=options.maxPixelRatio;
            image=GetComponent<RawImage>();originalTexture=image.texture;originalMaterial=image.material;
            var shader=Resources.Load<Shader>("LivLivSurface");
            if(shader==null||!shader.isSupported)throw new LivLivError("missing-feature","LivLiv UGUI surface shader unavailable");
            material=new Material(shader){name="LivLiv surface material",hideFlags=HideFlags.HideAndDontSave};
            try
            {
                player=new Player(null,options,markText:markText);image.material=material;UpdateSurface();return player;
            }
            catch
            {
                player?.Dispose();player=null;image.material=originalMaterial;image.texture=originalTexture;
                ReleaseTarget();TextureDecoder.Destroy(material);material=null;throw;
            }
        }
        /// <summary>Synchronize layout/target now and redraw without advancing animation (including a paused Player).</summary>
        public void Refresh(){Initialize();UpdateSurface();if(target!=null)player.Resize();}
        private void LateUpdate()
        {
            Initialize();bool changed=UpdateSurface();
            if(target==null||!image.enabled||image.canvasRenderer.cull)return;
            if(player.Playing)player.Tick(Time.unscaledDeltaTime);else if(changed)player.Resize();
        }
        private bool UpdateSurface()
        {
            var rect=image.rectTransform;double w=Math.Max(0,rect.rect.width),h=Math.Max(0,rect.rect.height);
            double density=1;var canvas=image.canvas;
            if(canvas!=null&&w>0&&h>0)
            {
                var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                if(canvas.renderMode!=RenderMode.ScreenSpaceOverlay&&camera==null)camera=Camera.main;
                rect.GetWorldCorners(corners);
                var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                var b=RectTransformUtility.WorldToScreenPoint(camera,corners[1]);
                var c=RectTransformUtility.WorldToScreenPoint(camera,corners[3]);
                var d=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                // Perspective gives opposite edges different projected lengths.
                // Size for the nearer edge regardless of the panel's tilt direction.
                density=Math.Max(Math.Max(Vector2.Distance(a,c),Vector2.Distance(b,d))/w,
                    Math.Max(Vector2.Distance(a,b),Vector2.Distance(c,d))/h);
            }
            var size=SurfaceSize.Calculate(w,h,density,maxPixelRatio,Math.Max(1,SystemInfo.maxTextureSize),maxPixels);
            ResolutionLimited=size.limited;
            bool changed=player.Width!=w||player.Height!=h;
            if(!size.Renderable)
            {
                changed|=target!=null;player.Target=null;image.texture=null;ReleaseTarget();player.Width=w;player.Height=h;return changed;
            }
            if(target==null||target.width!=size.width||target.height!=size.height||!target.IsCreated())
            {
                RenderTexture fresh=null;
                try
                {
                    fresh=new RenderTexture(size.width,size.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                        {name="LivLiv surface target",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear};
                    if(!fresh.Create())throw new LivLivError("missing-feature","Cannot create LivLiv render target");
                    var previous=RenderTexture.active;
                    try{RenderTexture.active=fresh;GL.Clear(true,true,Color.clear);}finally{RenderTexture.active=previous;}
                    var old=target;target=fresh;fresh=null;player.Target=target;image.texture=target;
                    if(old!=null){old.Release();TextureDecoder.Destroy(old);}changed=true;
                }
                finally{if(fresh!=null){fresh.Release();TextureDecoder.Destroy(fresh);}}
            }
            player.Width=w;player.Height=h;return changed;
        }
        private void ReleaseTarget(){if(target==null)return;target.Release();TextureDecoder.Destroy(target);target=null;}
        private void OnDestroy()
        {
            destroyed=true;
            try{player?.Dispose();}
            finally
            {
                player=null;
                if(image!=null){image.texture=originalTexture;image.material=originalMaterial;}
                ReleaseTarget();TextureDecoder.Destroy(material);material=null;
            }
        }
    }
}
