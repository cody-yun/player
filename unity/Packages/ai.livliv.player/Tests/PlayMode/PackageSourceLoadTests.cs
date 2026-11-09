using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LivLiv.Tests
{
    public sealed class PackageSourceLoadTests
    {
        [UnityTest]public IEnumerator PickedArchiveRendersAndFailedReplacementRetainsCurrentModel()
        {
            var asset=Resources.Load<TextAsset>("LivLivVerification");
            Assert.That(asset,Is.Not.Null,"Run in the verification project containing the licensed sample package");
            var target=new RenderTexture(64,64,24);target.Create();Player player=null;
            try
            {
                player=new Player(target,new PlayerOptions{autoplay=false,idle=false});
                var direct=player.LoadAsync(asset.bytes);while(!direct.IsCompleted)yield return null;
                Assert.That(direct.IsCompletedSuccessfully,Is.True,direct.Exception?.ToString());
                var expected=player.SnapshotPng();
                var files=new Dictionary<string,byte[]>{{"note.txt",new byte[]{1,2,3}},{"avatar.data",asset.bytes}};
                var picked=player.LoadAsync(files);while(!picked.IsCompleted)yield return null;
                Assert.That(picked.IsCompletedSuccessfully,Is.True,picked.Exception?.ToString());
                CollectionAssert.AreEqual(expected,player.SnapshotPng(),"ZIP bytes and picked ZIP must render identically");
                var info=player.Info;
                var broken=player.LoadAsync(new Dictionary<string,byte[]>{{"first.zip",new byte[]{0x50,0x4b,3,4}},{"second.zip",asset.bytes}});
                while(!broken.IsCompleted)yield return null;
                Assert.That(broken.IsFaulted,Is.True);Assert.That(broken.Exception.GetBaseException(),Is.TypeOf<LivLivError>());
                Assert.That(((LivLivError)broken.Exception.GetBaseException()).code,Is.EqualTo("not-a-model"));
                Assert.That(player.Info,Is.SameAs(info));CollectionAssert.AreEqual(expected,player.SnapshotPng());
                using(var cancel=new CancellationTokenSource())
                {
                    cancel.Cancel();var canceled=player.LoadAsync(files,new LoadOptions{cancellation=cancel.Token});
                    while(!canceled.IsCompleted)yield return null;
                    Assert.That(canceled.IsCanceled,Is.True);Assert.That(player.Info,Is.SameAs(info));
                }
            }
            finally{player?.Dispose();target.Release();UnityEngine.Object.Destroy(target);}
        }
    }
}
