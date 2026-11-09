using System;
using System.IO;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed class PosterGraphicsTests
    {
        [Test] public void DirectLoadReplacesPendingPosterBeforeFirstFrame()
        {
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var player=new Player(target,new PlayerOptions{idle=false,autoplay=true}))
                {
                    var package=ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06")));
                    bool replaced=false;string before=null,after="not called";
                    var pending=player.LoadAsync(package,new LoadOptions{poster="cover.webp",onProgress=progress=>
                    {before=player.Poster;player.Load(package);after=player.Poster;replaced=true;}});
                    Assert.CatchAsync<OperationCanceledException>(async()=>await pending);
                    Assert.That(replaced,Is.True);Assert.That(before,Is.EqualTo("cover.webp"));Assert.That(after,Is.Null);
                    Assert.That(player.Info,Is.Not.Null);Assert.That(player.Playing,Is.True);
                }
            }
            finally{target.Release();TextureDecoder.Destroy(target);}
        }
    }
}
