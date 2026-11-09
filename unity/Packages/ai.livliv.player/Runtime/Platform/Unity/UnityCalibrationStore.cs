using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace LivLiv.Unity
{
    // Main-thread storage boundary. Native uses PlayerPrefs; WebGL preserves upstream localStorage keys.
    public sealed class UnityCalibrationStore:LivLiv.Platform.ICalibrationStore
    {
        public static readonly UnityCalibrationStore Instance=new UnityCalibrationStore();
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]private static extern int LivLivCalibration_Read(string key,[Out]byte[] buffer,int capacity);
        [DllImport("__Internal")]private static extern void LivLivCalibration_Write(string key,string json);
#endif
        public string Get(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int size=LivLivCalibration_Read(key,null,0);if(size<=0)return null;
            var bytes=new byte[size];int actual=LivLivCalibration_Read(key,bytes,size);
            return actual==size?Encoding.UTF8.GetString(bytes,0,size-1):null;
#else
            return PlayerPrefs.HasKey(key)?PlayerPrefs.GetString(key):null;
#endif
        }
        public void Set(string key,string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivCalibration_Write(key,json);
#else
            PlayerPrefs.SetString(key,json);PlayerPrefs.Save();
#endif
        }
    }
}
