using System;
using System.IO;
using LivLiv.Unity;
using UnityEngine;
using UnityEngine.Rendering;
public static class PlayerSupportVerification
{
    public static void VerifyNullDevice()
    {
        if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)throw new Exception("Expected -nographics Null graphics device");
        for(int i=0;i<10;i++)if(Player.IsSupported())throw new Exception("Null device was reported supported");
        foreach(var target in Resources.FindObjectsOfTypeAll<RenderTexture>())
            if(target.name=="LivLiv support probe")throw new Exception("Null check allocated a render target");
        var args=Environment.GetCommandLineArgs();string output=null;
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-supportOutput")output=args[i+1];
        if(output==null)throw new Exception("Provide -supportOutput");
        File.WriteAllText(output,"{\"result\":\"Passed\",\"graphics\":\"Null\",\"supported\":false,\"calls\":10,\"probeTargets\":0}");
    }
}
