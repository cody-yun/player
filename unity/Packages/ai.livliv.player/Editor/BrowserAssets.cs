using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
namespace LivLiv.Unity.Editor
{
    // WebGL serves StreamingAssets loose. Copy after build; never writes generated vendor assets into Assets/.
    public sealed class BrowserAssets : IPostprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.WebGL)return;
            string source=Path.Combine(UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WebGLFaceInput).Assembly).resolvedPath,"Browser~");
            string target=Path.Combine(report.summary.outputPath,"StreamingAssets/LivLivFace");Copy(source,target);
        }
        private static void Copy(string source,string target)
        {
            Directory.CreateDirectory(target);foreach(var file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
            foreach(var dir in Directory.GetDirectories(source))Copy(dir,Path.Combine(target,Path.GetFileName(dir)));
        }
    }
}
