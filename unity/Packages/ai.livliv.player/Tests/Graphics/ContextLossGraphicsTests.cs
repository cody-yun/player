using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class ContextLossGraphicsTests
    {
        [Test]public void ConfirmedLossRejectsBothPublicLoadEntrypointsBeforeDecode()
        {
            using(var player=new Player(null))
            {
                player.NotifyContextLost();
                var sync=Assert.Throws<LivLivError>(()=>player.Load((ModelPackage)null));
                Assert.That(sync.code,Is.EqualTo("context-lost"));
                var asyncError=Assert.ThrowsAsync<LivLivError>(async()=>await player.LoadAsync((ModelPackage)null));
                Assert.That(asyncError.code,Is.EqualTo("context-lost"));Assert.That(player.Info,Is.Null);
            }
        }
    }
}
