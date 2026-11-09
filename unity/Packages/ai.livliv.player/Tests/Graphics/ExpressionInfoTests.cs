using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed class ExpressionInfoTests
    {
        [TestCase("en","Smile"),TestCase("ja","笑顔")]
        public void ExpressionsRetainConcreteLabelsAcrossLoadFailureReplacementAndDispose(string language,string expected)
        {
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06"));
            using var player=new Player(null,new LivLiv.PlayerOptions{idle=false,lang=language});
            ModelExpression[] empty=player.Expressions;
            Assert.That(empty,Is.Empty);
            var first=player.Load(ModelPackage.FromDirectory(root));
            ModelExpression[] expressions=player.Expressions;
            Assert.That(expressions,Is.SameAs(first.expressions));
            var smile=expressions.Single(e=>e.id=="smile");
            string label=smile.label;JObject labels=smile.labels;
            Assert.That(label,Is.EqualTo(expected));Assert.That((string)labels["en"],Is.EqualTo("Smile"));
            Assert.That((string)labels["ja"],Is.EqualTo("笑顔"));
            Assert.Throws<LivLiv.LivLivError>(()=>player.Load(ModelPackage.FromFiles(new Dictionary<string,byte[]>
                {{"model.json",File.ReadAllBytes(Path.Combine(root,"model.json"))}})));
            Assert.That(player.Expressions,Is.SameAs(expressions),"failed replacement retains expression metadata");
            var second=player.Load(ModelPackage.FromDirectory(root));
            Assert.That(player.Expressions,Is.SameAs(second.expressions));Assert.That(player.Expressions,Is.Not.SameAs(expressions));
            player.Dispose();Assert.That(player.Expressions,Is.Empty);Assert.That(player.Info,Is.Null);
        }
    }
}
