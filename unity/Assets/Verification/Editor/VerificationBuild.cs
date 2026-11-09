using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class VerificationBuild
{
    internal static bool RepackAndroidApk { get; private set; }
    public static void WebGL()=>Build(BuildTarget.WebGL,"WebGL","webgl");
    public static void MacOS()=>Build(BuildTarget.StandaloneOSX,"MacOS/LivLivVerification.app","macos");
    public static void MacOSIntel()=>Build(BuildTarget.StandaloneOSX,"MacOSIntel/LivLivVerification.app","macos-intel",OSArchitecture.x64);
    public static void iOS()=>Build(BuildTarget.iOS,"iOS","ios");
    public static void Android()=>Build(BuildTarget.Android,"Android/LivLivVerification.apk","android");
    public static void Windows()=>Build(BuildTarget.StandaloneWindows64,"Windows/LivLivVerification.exe","windows");
    public static void Linux()=>Build(BuildTarget.StandaloneLinux64,"Linux/LivLivVerification.x86_64","linux");
    private static void Build(BuildTarget target,string defaultPath,string label,OSArchitecture macArchitecture=OSArchitecture.ARM64)
    {
        // Editor callbacks use the active target's compilation symbols. Switching only the
        // BuildPlayerOptions target can omit Android's APK repack callback entirely.
        if(EditorUserBuildSettings.activeBuildTarget!=target)
            throw new BuildFailedException("Start the editor for the requested target before invoking VerificationBuild. Use unity build --target "+target+" --execute-method VerificationBuild."+(label=="macos-intel"?"MacOSIntel":target==BuildTarget.StandaloneOSX?"MacOS":target==BuildTarget.StandaloneWindows64?"Windows":target==BuildTarget.StandaloneLinux64?"Linux":label=="ios"?"iOS":target.ToString())+" ./unity");
        string repo=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        string resources=Path.Combine(Application.dataPath,"Verification/Resources");Directory.CreateDirectory(resources);
        string pack=Path.Combine(resources,"LivLivVerification.bytes");if(File.Exists(pack))File.Delete(pack);
        ZipFile.CreateFromDirectory(Path.Combine(repo,"characters/char06"),pack,System.IO.Compression.CompressionLevel.Optimal,false);
        foreach(var pair in new[]{new[]{"LivLivFaceModel.bytes","unity/Packages/ai.livliv.player/Browser~/face_landmarker.task"},new[]{"LivLivPortrait.bytes","unity/TestResults/mediapipe/portrait.jpg"},new[]{"LivLivSpeechMp3.bytes","unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/speech-stereo.mp3.bytes"},new[]{"LivLivSpeechFloat.bytes","unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/speech-float.wav.bytes"}})
        {string destination=Path.Combine(resources,pair[0]);if(target!=BuildTarget.WebGL)File.Copy(Path.Combine(repo,pair[1]),destination,true);else if(File.Exists(destination))File.Delete(destination);}
        foreach(string kind in new[]{"baseline","progressive"})
            File.Copy(Path.Combine(repo,"unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-"+kind+".jpg"),Path.Combine(resources,"LivLivJpeg-"+kind+".bytes"),true);
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        // Clear the backbuffer before the sample UGUI overlay. The character is a RawImage of its RT.
        var camera=new GameObject("VerificationBackground").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.12f,.12f,.12f,1);camera.cullingMask=0;camera.gameObject.AddComponent<AudioListener>();
        var sample=new GameObject("LivLivVerification");sample.AddComponent<PortVerification>();
        const string materialPath="Assets/Verification/Avatar.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null){material=new Material(Shader.Find("LivLiv/SampleAvatar"));AssetDatabase.CreateAsset(material,materialPath);}
        sample.AddComponent<VerificationHud>().Build(AssetDatabase.LoadAssetAtPath<Font>("Assets/Verification/Fonts/NanumGothic-Regular.ttf"),material);
        const string scenePath="Assets/Verification/Verification.unity";EditorSceneManager.SaveScene(scene,scenePath);
        PlayerSettings.productName="LivLiv Unity Verification";PlayerSettings.defaultWebScreenWidth=960;PlayerSettings.defaultWebScreenHeight=720;
        PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.decompressionFallback=false;PlayerSettings.runInBackground=true;
        if(target==BuildTarget.StandaloneOSX)
        {
            UnityEditor.OSXStandalone.UserBuildSettings.architecture=macArchitecture;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.IL2CPP);
        }
        if(target==BuildTarget.StandaloneWindows64)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        if(target==BuildTarget.StandaloneLinux64)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.IL2CPP);
        if(target==BuildTarget.Android)
        {
            // The verification sample must launch with the same tested backend as its CLI probes.
            // BlueStacks' Vulkan implementation crashes in Unity before the first frame.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"ai.livliv.verification");
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
        }
        if(target==BuildTarget.iOS)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS,ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString="15.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,"ai.livliv.verification");
        }
        PlayerSettings.iOS.cameraUsageDescription="Use your camera to animate the character's face.";
        PlayerSettings.iOS.microphoneUsageDescription="Use your microphone to animate the character's mouth.";
        string output=Path.Combine(repo,"unity/TestResults/"+defaultPath);var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-buildOutput")output=args[i+1];
        BuildReport result;
        RepackAndroidApk=target==BuildTarget.Android;
        try { result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},locationPathName=output,target=target,options=BuildOptions.Development}); }
        finally { RepackAndroidApk=false; }
        File.WriteAllText(Path.Combine(repo,"unity/TestResults/"+label+"-build.json"),JsonUtility.ToJson(new Evidence{result=result.summary.result.ToString(),errors=result.summary.totalErrors,warnings=result.summary.totalWarnings,bytes=result.summary.totalSize,seconds=result.summary.totalTime.TotalSeconds},true));
        if(result.summary.result!=BuildResult.Succeeded)throw new Exception(label+" verification build failed: "+result.summary.result);
        if(target!=BuildTarget.WebGL)return;
        // Expose the verification instance for reproducible browser checks (not part of the package API).
        string htmlPath=Path.Combine(output,"index.html"),html=File.ReadAllText(htmlPath),marker="}).then((unityInstance) => {";
        if(!html.Contains(marker))throw new Exception("Unity WebGL template changed; update verification instance hook");
        html=html.Replace(marker,marker+"\nwindow.livlivUnity=unityInstance;");
        html=html.Replace("</head>","<style>html,body{margin:0;height:100%;overflow:hidden;background:#11191f}#unity-container,#unity-container.unity-desktop{position:fixed;inset:0;transform:none;width:100%;height:100%}#unity-canvas{width:100%!important;height:100%!important;display:block}#unity-footer{display:none}</style></head>");
        File.WriteAllText(htmlPath,html.Replace("user-scalable=no","user-scalable=yes"));
    }
    [Serializable]private sealed class Evidence{public string result;public int errors,warnings;public ulong bytes;public double seconds;}
}
