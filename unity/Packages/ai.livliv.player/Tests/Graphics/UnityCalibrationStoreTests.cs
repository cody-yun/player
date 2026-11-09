using System;
using LivLiv.Runtime;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed class UnityCalibrationStoreTests
    {
        [Test]public void NativeStorePersistsSourceJsonAndUsesOnlyItsKey()
        {
            string id="qa-calibration-"+Guid.NewGuid().ToString("N"),key="lm_calib:"+id;var store=UnityCalibrationStore.Instance;
            try
            {
                Assert.That(store.Get(key),Is.Null);
                const string json="{\"x\":12,\"y\":-8,\"z\":3,\"px\":6,\"py\":2,\"pz\":-43,\"bl\":0.2,\"br\":0.4}";
                store.Set(key,json);Assert.That(new UnityCalibrationStore().Get(key),Is.EqualTo(json));
                var tracker=new FaceTracker(store){calibId=id};tracker.SetMirror(false);tracker.RestoreCalibration();
                Assert.That(tracker.calibrated,Is.True);Assert.That(tracker.zero.x,Is.EqualTo(-12));Assert.That(tracker.zero.px,Is.EqualTo(-6));
            }
            finally{PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
    }
}
