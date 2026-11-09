using System.Collections;
using System.Linq;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LivLiv.Tests
{
    public sealed class PlayerSupportTests
    {
        [UnityTest]public IEnumerator RepeatedSupportChecksPreserveCallerTargetAndReleaseProbe()
        {
            var target=new RenderTexture(16,16,24);target.Create();var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=target;
                for(int i=0;i<10;i++)
                {
                    Assert.That(Player.IsSupported(),Is.True,"This graphics-enabled test environment must render LivLiv");
                    Assert.That(RenderTexture.active,Is.SameAs(target));Assert.That(target.IsCreated(),Is.True);
                }
                yield return null;yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv support probe"),Is.Zero);
            }
            finally{RenderTexture.active=previous;target.Release();Object.Destroy(target);}
        }
    }
}
