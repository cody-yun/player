#if UNITY_WEBGL && !UNITY_EDITOR
// Test-only SendMessage endpoints; the sample's visible controls remain unchanged.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LivLiv;
using LivLiv.Unity;
using UnityEngine;
public sealed partial class PortVerification
{
    [DllImport("__Internal")] private static extern void LivLivProbe_Speech(string json);
    [Serializable] private sealed class WebSpeechRequest { public string id; public string[] urls; public string bytes; }
    [Serializable] private sealed class WebSpeechState
    {
        public string id, error;
        public bool busy, disposed, stopped, running;
        public int idle, errors, frames, stateChanges;
        public List<bool> stateEvents = new List<bool>();
        public double mouth, maximumMouth, scale;
    }
    private Speech webSpeech;
    private WebSpeechState webSpeechState;
    public void StartSpeechProbe(string json)
    {
        webSpeech?.Dispose(); webSpeech = null;
        var request = JsonUtility.FromJson<WebSpeechRequest>(json);
        webSpeechState = new WebSpeechState { id = request.id };
        ResetPreview();
        webSpeech = UnitySpeech.Create(player, options: new UnitySpeechOptions
        {
            volume = .02, mouth = new LipSyncOptions { motion = 0 },
            onError = error => { webSpeechState.errors++; webSpeechState.error = error.Message; },
            onState = running => { webSpeechState.running = running; webSpeechState.stateChanges++; webSpeechState.stateEvents.Add(running); }
        });
        webSpeech.onIdle = () => webSpeechState.idle++;
        webSpeech.Unlock();
        if (request.urls != null) foreach (var url in request.urls) webSpeech.Say(url);
        if (!string.IsNullOrEmpty(request.bytes)) webSpeech.Say(Convert.FromBase64String(request.bytes));
        UpdateSpeechProbe();
    }
    public void StopSpeechProbe() { webSpeech?.Stop(); if (webSpeechState != null) webSpeechState.stopped = true; UpdateSpeechProbe(); }
    public void UnlockSpeechProbe() { webSpeech?.Unlock(); UpdateSpeechProbe(); }
    public void DisposeSpeechProbe()
    {
        webSpeech?.Dispose(); webSpeech = null;
        if (webSpeechState != null) webSpeechState.disposed = true;
        UpdateSpeechProbe();
    }
    private void UpdateSpeechProbe()
    {
        if (webSpeechState == null) return;
        webSpeechState.busy = webSpeech?.Busy ?? false;
        webSpeechState.mouth = player.Renderer.Core.@params.mouthOpen;
        webSpeechState.maximumMouth = Math.Max(webSpeechState.maximumMouth, webSpeechState.mouth);
        webSpeechState.scale = ReadEvidence().scale;
        webSpeechState.frames++;
        LivLivProbe_Speech(JsonUtility.ToJson(webSpeechState));
    }
}
#endif
