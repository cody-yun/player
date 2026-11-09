mergeInto(LibraryManager.library, {
  LivLivProbe_Context__deps: ['$UTF8ToString'],
  LivLivProbe_Context: function (json) { window.livlivContextLoss=JSON.parse(UTF8ToString(json)); },
  LivLivProbe_Surface__deps: ['$UTF8ToString'],
  LivLivProbe_Surface: function (json,png) { window.livlivSurface=JSON.parse(UTF8ToString(json)); if(png)window.livlivSurfacePng=UTF8ToString(png); },
  LivLivProbe_Poster__deps: ['$UTF8ToString'],
  LivLivProbe_Poster: function (json) { window.livlivPoster = JSON.parse(UTF8ToString(json)); },
  LivLivProbe_PosterLoad__deps: ['$UTF8ToString'],
  LivLivProbe_PosterLoad: function (json) { var r=JSON.parse(UTF8ToString(json)); (window.livlivPosterLoads || (window.livlivPosterLoads={}))[r.id]=r; },
  LivLivProbe_Snapshot__deps: ['$UTF8ToString'],
  LivLivProbe_Snapshot: function (json) { window.livlivSnapshot = JSON.parse(UTF8ToString(json)); },
  LivLivProbe_Speech__deps: ['$UTF8ToString', '$LivLivSpeechBrowser'],
  LivLivProbe_Speech: function (json) {
    var result = JSON.parse(UTF8ToString(json)), debug = LivLivSpeechBrowser;
    var previous = window.livlivSpeechVerification;
    result.peak = previous && previous.id === result.id ? previous.peak : 0;
    result.contexts = [];
    Object.keys(debug.outputs).forEach(function (id) {
      var output = debug.outputs[id];
      if (!output.ctx || !output.gain) return;
      if (!output.probeAnalyser) {
        output.probeAnalyser = output.ctx.createAnalyser(); output.probeAnalyser.fftSize = 1024;
        output.probeSamples = new Float32Array(1024); output.gain.connect(output.probeAnalyser);
      }
      output.probeAnalyser.getFloatTimeDomainData(output.probeSamples);
      for (var i = 0; i < output.probeSamples.length; i++) result.peak = Math.max(result.peak, Math.abs(output.probeSamples[i]));
      result.contexts.push({state: output.ctx.state, rate: output.ctx.sampleRate, current: output.ctx.currentTime, heard: debug.heard(output)});
    });
    result.pieces = Object.keys(debug.pieces).length; result.loads = Object.keys(debug.loads).length; result.decodes = Object.keys(debug.decodes).length;
    window.livlivSpeechDebug = debug; window.livlivSpeechVerification = result;
  },
  LivLivProbe_Load__deps: ['$UTF8ToString'],
  LivLivProbe_Load: function (json) { var result = JSON.parse(UTF8ToString(json)); (window.livlivLoads || (window.livlivLoads = {}))[result.id] = result; },
  LivLivProbe_Image__deps: ['$UTF8ToString'],
  LivLivProbe_Image: function (base64) { window.livlivFrame = UTF8ToString(base64); },
  LivLivProbe_Report__deps: ['$UTF8ToString'],
  LivLivProbe_Report: function (json) { window.livlivVerification = JSON.parse(UTF8ToString(json)); }
});
