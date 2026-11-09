#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
public sealed partial class PortVerification
{
    [DllImport("__Internal")]private static extern void LivLivProbe_Context(string json);
    [Serializable]private sealed class ContextEvidence
    {public int mainCalls,auxCalls,disposedCalls;public bool mainLost,auxLost,mainDisposed,auxDisposed,loadAwaitCompleted;public string loadState;}
    private System.Threading.Tasks.Task<LivLiv.Unity.ModelInfo> contextLoad;
    private LivLiv.Unity.Player contextAux;
    private ContextEvidence contextEvidence;
    public void ArmContextLossProbe()
    {
        contextEvidence=new ContextEvidence();
        player.OnContextLost+=()=>{contextEvidence.mainCalls++;player.Dispose();ReportContextLoss();};
        contextAux=new LivLiv.Unity.Player(null);
        contextAux.OnContextLost=()=>{contextEvidence.auxCalls++;contextAux.Dispose();ReportContextLoss();};
        var removed=new LivLiv.Unity.Player(null);removed.OnContextLost=()=>{contextEvidence.disposedCalls++;ReportContextLoss();};removed.Dispose();
        ReportContextLoss();
    }
    public async void ArmContextLossLoadingProbe(string url)
    {
        ArmContextLossProbe();contextEvidence.loadState="loading";ReportContextLoss();
        try{contextLoad=contextAux.LoadAsync(url);await contextLoad;contextEvidence.loadState="unexpected-success";}
        catch(LivLiv.LivLivError error){contextEvidence.loadState=error.code;}
        catch(Exception error){contextEvidence.loadState=error.GetType().Name;}
        contextEvidence.loadAwaitCompleted=true;ReportContextLoss();
    }
    private void ReportContextLoss()
    {
        if(contextLoad!=null&&contextLoad.IsFaulted&&contextLoad.Exception.GetBaseException() is LivLiv.LivLivError error)contextEvidence.loadState=error.code;
        contextEvidence.mainLost=player.ContextLost;contextEvidence.auxLost=contextAux!=null&&contextAux.ContextLost;
        contextEvidence.mainDisposed=player.IsDisposed;contextEvidence.auxDisposed=contextAux!=null&&contextAux.IsDisposed;
        LivLivProbe_Context(JsonUtility.ToJson(contextEvidence));
    }
}
#endif
