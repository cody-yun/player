using System.Collections;
using System.Linq;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LivLiv.Tests
{
    public sealed class ReleaseTargetTests
    {
        [UnityTest]public IEnumerator ExistingPausedOwnersLoseOnlyTheirReleasedTarget()
        {
            var target=Target();var other=Target();Player first=null,alias=null,independent=null;int losses=0;
            try
            {
                first=Open(target);alias=Open(target);independent=Open(other);alias.OnContextLost+=()=>losses++;
                first.Dispose(new PlayerDisposeOptions{release=true});alias.Target=target;
                yield return null;yield return null;
                Assert.That(target.IsCreated(),Is.False);Assert.That(alias.ContextLost,Is.True);Assert.That(losses,Is.EqualTo(1));
                Assert.That(independent.ContextLost,Is.False);Assert.That(other.IsCreated(),Is.True);
                Assert.That(Assert.Throws<LivLivError>(()=>alias.Load((LivLiv.Platform.ModelPackage)null)).code,Is.EqualTo("context-lost"));
                first.Dispose(new PlayerDisposeOptions{release=true});yield return null;Assert.That(losses,Is.EqualTo(1));
            }
            finally{first?.Dispose();alias?.Dispose();independent?.Dispose();foreach(var t in new[]{target,other}){t.Release();Object.Destroy(t);}}
        }
        [UnityTest]public IEnumerator RetargetingDetachesOldLossAndFailedClaimKeepsCurrentTarget()
        {
            var old=Target();var next=Target();Player trigger=null,moved=null,second=null;
            try
            {
                trigger=Open(old);moved=Open(old);moved.Target=next;trigger.Dispose(new PlayerDisposeOptions{release=true});
                yield return null;yield return null;Assert.That(moved.ContextLost,Is.False);
                Assert.That(Assert.Throws<LivLivError>(()=>moved.Target=old).code,Is.EqualTo("released"));Assert.That(moved.Target,Is.SameAs(next));
                second=Open(next);second.Dispose(new PlayerDisposeOptions{release=true});yield return null;yield return null;
                Assert.That(moved.ContextLost,Is.True);
            }
            finally{trigger?.Dispose();moved?.Dispose();second?.Dispose();foreach(var t in new[]{old,next}){t.Release();Object.Destroy(t);}}
        }
        private static RenderTexture Target()
        {var target=new RenderTexture(32,32,24);Assert.That(target.Create(),Is.True);return target;}
        private static Player Open(RenderTexture target)=>new Player(target,new PlayerOptions{autoplay=false,idle=false});
        [UnityTest]public IEnumerator DefaultDisposePreservesBorrowedStorageAndAllowsReuse()
        {
            var target=Target();Player next=null;
            try
            {
                Open(target).Dispose();yield return null;yield return null;
                Assert.That(target.IsCreated(),Is.True);next=Open(target);next.Dispose();
            }
            finally{next?.Dispose();target.Release();Object.Destroy(target);}
        }
        [UnityTest]public IEnumerator ImmediateReplacementCancelsReleaseButCompletedReleaseIsTerminal()
        {
            var target=Target();Player replacement=null;
            try
            {
                Open(target).Dispose(new PlayerDisposeOptions{release=true});
                Assert.That(target.IsCreated(),Is.True,"Release is deferred");replacement=Open(target);
                yield return null;yield return null;Assert.That(target.IsCreated(),Is.True,"Immediate claim cancels release");
                replacement.Dispose(new PlayerDisposeOptions{release=true});yield return null;yield return null;
                Assert.That(target.IsCreated(),Is.False);Assert.That(target!=null,Is.True,"The caller still owns the RenderTexture object");
                var before=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.shader!=null&&m.shader.name=="Hidden/LivLiv/Layer");
                Assert.That(Assert.Throws<LivLivError>(()=>Open(target)).code,Is.EqualTo("released"));
                yield return null;yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.shader!=null&&m.shader.name=="Hidden/LivLiv/Layer"),Is.EqualTo(before),"Rejected construction must not leak a renderer material");
                Assert.That(target.Create(),Is.True);
                Assert.That(Assert.Throws<LivLivError>(()=>Open(target)).code,Is.EqualTo("released"),"Recreating storage does not unspend the logical target");
                yield return null;yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.shader!=null&&m.shader.name=="Hidden/LivLiv/Layer"),Is.EqualTo(before));
            }
            finally{replacement?.Dispose();target.Release();Object.Destroy(target);}
        }
        [UnityTest]public IEnumerator IndependentTargetsAndRetargetingKeepReleaseScoped()
        {
            var old=Target();var other=Target();var next=Target();Player owner=null;
            try
            {
                owner=Open(old);owner.Target=next;
                owner.Dispose(new PlayerDisposeOptions{release=true});yield return null;yield return null;
                Assert.That(old.IsCreated(),Is.True);Assert.That(other.IsCreated(),Is.True);Assert.That(next.IsCreated(),Is.False);
                using(var reuse=Open(old)){}using(var reuse=Open(other)){}
            }
            finally{owner?.Dispose();foreach(var target in new[]{old,other,next}){target.Release();Object.Destroy(target);}}
        }
        [UnityTest]public IEnumerator ReleaseCanBeRequestedAfterOrdinaryDisposalAndCancelsOnRetargetClaim()
        {
            var target=Target();var other=Target();Player disposed=null,claimant=null;
            try
            {
                disposed=Open(target);disposed.Dispose();disposed.Dispose(new PlayerDisposeOptions{release=true});
                claimant=Open(other);claimant.Target=target;yield return null;yield return null;
                Assert.That(target.IsCreated(),Is.True);claimant.Dispose(new PlayerDisposeOptions{release=true});
                yield return null;yield return null;Assert.That(target.IsCreated(),Is.False);Assert.That(other.IsCreated(),Is.True);
            }
            finally{disposed?.Dispose();claimant?.Dispose();foreach(var t in new[]{target,other}){t.Release();Object.Destroy(t);}}
        }
    }
}
