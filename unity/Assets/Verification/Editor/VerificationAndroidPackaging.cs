#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// VerificationBuild.Android creates a debug APK. Reusing its zipflinger archive
// leaves deleted entries as free regions, growing successive development APKs.
// Regenerate only this packaging output/state; keep the IL2CPP/Gradle compile caches.
public sealed class VerificationAndroidPackaging : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!VerificationBuild.RepackAndroidApk) return;
        string root=Directory.GetParent(Path.GetFullPath(path)).FullName;
        string launcher=Path.Combine(root,"launcher");
        if (!File.Exists(Path.Combine(launcher,"build.gradle")))
            throw new BuildFailedException("Verification APK packaging: expected launcher/build.gradle next to the Unity library at "+path);
        string apk=Path.Combine(launcher,"build/outputs/apk/debug/launcher-debug.apk");
        string state=Path.Combine(launcher,"build/intermediates/incremental/packageDebug");
        long bytes=File.Exists(apk)?new FileInfo(apk).Length:0;
        if (File.Exists(apk)) File.Delete(apk);
        if (Directory.Exists(state)) Directory.Delete(state,true);
        Debug.Log("LivLiv verification: regenerate debug APK packaging (previous archive "+bytes+" bytes); compile caches retained.");
    }
}
#endif
