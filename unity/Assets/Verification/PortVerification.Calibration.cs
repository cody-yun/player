// Explicit QA only. Generated detector frames, isolated preference key; never activates capture.
using System;
using System.IO;
using LivLiv;
using LivLiv.Runtime;
using LivLiv.Unity;
using Newtonsoft.Json.Linq;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class CalibrationRequest{public string phase,id;}
    [Serializable]private sealed class CalibrationEvidence
    {public string phase,id,result,error,stored,platform;public FacePose zero,opposite;public bool calibrated;}
    private sealed class CalibrationInput:IFaceInput
    {public bool Ready=>true;public double Fps=>0;public bool Dark=>false;public FaceFrame Next(double now)=>null;public void Dispose(){} }
    private static FaceFrame CalibrationFrame(int index)
    {
        double a=10*Math.PI/180;var b=new double[18];b[0]=.2;b[1]=.4;
        return new FaceFrame{t=index*1000d/30,m=new[]{Math.Cos(a),0,-Math.Sin(a),0,0,1d,0,0,Math.Sin(a),0,Math.Cos(a),0,6,2,-43,1},b=b};
    }
    private static CalibrationEvidence RunCalibration(CalibrationRequest request)
    {
        var r=new CalibrationEvidence{phase=request.phase,id=request.id,platform=Application.platform.ToString()};
        try
        {
            if(request.id==null||!request.id.StartsWith("qa-calibration-",StringComparison.Ordinal))throw new Exception("QA requires an isolated key");
            string key="lm_calib:"+request.id;var store=UnityCalibrationStore.Instance;
            if(request.phase=="legacy")store.Set(key,"{\"x\":-13,\"y\":-8,\"z\":0,\"bl\":0.23,\"br\":0.33}");
            if(request.phase=="malformed")store.Set(key,"{invalid json");
            using(var driver=new FaceTrackingDriver(new CalibrationInput(),()=>0,new FaceTrackingOptions{remember=request.id,mirror=false,calibrationStore=store}))
            {
                var t=driver.Tracker;
                if(request.phase=="write"||request.phase=="legacy")
                {for(int i=0;i<32;i++){var f=CalibrationFrame(i);t.Poll(f,f.t);}if(request.phase=="write")t.Calibrate();}
                r.zero=t.zero;r.calibrated=t.calibrated;r.stored=store.Get(key);
                if(request.phase=="malformed")
                {if(t.calibrated)throw new Exception("Invalid calibration accepted");}
                else if(!t.calibrated||Math.Abs(t.zero.x-13)>1e-8||Math.Abs(t.zero.px-6)>1e-8||t.zero.pz!=-43)throw new Exception("Calibration was not restored/captured");
                if(request.phase=="legacy"&&(t.zero.y!=-8||t.zero.bl!=.23||t.zero.br!=.33))throw new Exception("Legacy angles/eyes were overwritten");
            }
            using(var other=new FaceTrackingDriver(new CalibrationInput(),()=>0,new FaceTrackingOptions{remember=request.id,mirror=true,calibrationStore=store}))
            {r.opposite=other.Tracker.zero;if(request.phase!="malformed"&&(Math.Abs(r.opposite.x+13)>1e-8||Math.Abs(r.opposite.px+6)>1e-8))throw new Exception("Mirror-independent restore failed");}
            if(request.phase!="malformed")
            {var saved=JObject.Parse(r.stored);if(Math.Abs((double)saved["x"]+13)>1e-8||(double)saved["px"]!=-6||saved["at"]==null)throw new Exception("Source storage format mismatch");}
            r.result="Passed";
        }
        catch(Exception e){r.result="Failed";r.error=e.ToString();}
        return r;
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    public void CalibrationProbe(string json){LivLivProbe_Snapshot(JsonUtility.ToJson(RunCalibration(JsonUtility.FromJson<CalibrationRequest>(json))));}
#else
    private void StartCalibrationSmokeIfRequested()
    {
        var args=Environment.GetCommandLineArgs();for(int i=0;i+3<args.Length;i++)if(args[i]=="-livlivCalibrationSmoke")
        {
            string output=args[i+1],phase=args[i+2],id=args[i+3];Directory.CreateDirectory(output);
            if(phase=="cleanup")
            {
                if(!id.StartsWith("qa-calibration-",StringComparison.Ordinal))throw new Exception("QA requires an isolated key");
                PlayerPrefs.DeleteKey("lm_calib:"+id);PlayerPrefs.Save();
                File.WriteAllText(Path.Combine(output,"cleanup.json"),new JObject{["removed"]=!PlayerPrefs.HasKey("lm_calib:"+id)}.ToString());
                Application.Quit(0);return;
            }
            var report=RunCalibration(new CalibrationRequest{phase=phase,id=id});File.WriteAllText(Path.Combine(output,phase+".json"),JsonUtility.ToJson(report,true));Application.Quit(report.result=="Passed"?0:1);return;
        }
    }
#endif
}
