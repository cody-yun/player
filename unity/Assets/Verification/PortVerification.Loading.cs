// CLI/browser load probes. Only loopback test URLs are used by verification scripts.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using LivLiv;
using UnityEngine;
public sealed partial class PortVerification
{
    private int loadSequence;private CancellationTokenSource modelLoad;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]private static extern void LivLivProbe_Load(string json);
#endif
    [Serializable]private sealed class LoadStep {public string step;public double progress;}
    [Serializable]private sealed class LoadEvidence
    {public int id,startFrame,endFrame;public string url,state,model,error;public bool keptPrevious;public LoadStep[] progress;}
    public async void LoadModel(string url)
    {
        var request=new CancellationTokenSource();modelLoad=request;
        var steps=new List<LoadStep>();var previous=player.Renderer.Model;
        var result=new LoadEvidence{id=++loadSequence,url=url,state="loading",startFrame=frames};
        void ReportLoad()
        {
            result.endFrame=frames;result.progress=steps.ToArray();result.model=player.Info?.name;result.keptPrevious=ReferenceEquals(previous,player.Renderer.Model);
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivProbe_Load(JsonUtility.ToJson(result));
#endif
        }
        ReportLoad();
        try
        {
            await player.LoadAsync(url,new LoadOptions{cancellation=request.Token,onProgress=p=>{steps.Add(new LoadStep{step=p.step,progress=p.progress});ReportLoad();}});
            result.state="ready";
        }
        catch(OperationCanceledException){result.state="cancelled";}
        catch(Exception e){result.state="failed";result.error=e is LivLivError error?error.code:e.GetType().Name;}
        finally{if(ReferenceEquals(modelLoad,request))modelLoad=null;request.Dispose();ReportLoad();}
    }
    public void CancelModelLoad()=>modelLoad?.Cancel();
}
