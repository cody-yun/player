// WebAudio platform boundary for src/runtime/voice.ts. ES5 syntax for Unity's JS linker.
mergeInto(LibraryManager.library, {
  $LivLivBrowser: {
    next: 1,
    voices: {},
    release: function (v) {
      if (v.released) return;
      v.released = true;
      if (v.ended && v.stream) v.stream.getTracks().forEach(function (track) { track.removeEventListener('ended', v.ended); });
      if (v.unlock) {
        window.removeEventListener('pointerdown', v.unlock);
        window.removeEventListener('keydown', v.unlock);
      }
      if (v.stream) v.stream.getTracks().forEach(function (track) { if (track.readyState !== 'ended') track.stop(); });
      [v.source, v.band, v.an, v.spec].forEach(function (node) { if (node) { try { node.disconnect(); } catch (_) {} } });
      if (v.ctx && v.ctx.state !== 'closed') { try { v.ctx.close().catch(function () {}); } catch (_) {} }
    },
    fail: function (v, error) {
      v.error = (error && error.name ? error.name + ': ' : '') + (error && error.message ? error.message : String(error));
      v.status = -1;
      LivLivBrowser.release(v);
    },
    start: function () {
      var id = LivLivBrowser.next++;
      var v = { status: 0, error: '', disposed: false, released: false };
      LivLivBrowser.voices[id] = v;
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        LivLivBrowser.fail(v, new Error('Microphone capture needs a secure context and getUserMedia'));
        return id;
      }
      var request;
      try { request = navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } }); }
      catch (error) { LivLivBrowser.fail(v, error); return id; }
      request.then(function (stream) {
        if (v.disposed) { stream.getTracks().forEach(function (track) { track.stop(); }); return; }
        v.stream = stream;
        try {
          v.ended = function () { if (!v.disposed && !v.released) LivLivBrowser.fail(v, new Error('Microphone capture ended; reconnect and start again')); };
          var tracks = stream.getTracks();
          tracks.forEach(function (track) { track.addEventListener('ended', v.ended); });
          if (!tracks.length || tracks.some(function (track) { return track.readyState === 'ended'; })) { v.ended(); return; }
          var AudioContextClass = window.AudioContext || window.webkitAudioContext;
          if (!AudioContextClass) throw new Error('Web Audio is unavailable');
          var ctx = v.ctx = new AudioContextClass();
          v.band = ctx.createBiquadFilter(); v.band.type = 'bandpass'; v.band.frequency.value = 650; v.band.Q.value = 0.55;
          v.an = ctx.createAnalyser(); v.an.fftSize = 1024;
          v.spec = ctx.createAnalyser(); v.spec.fftSize = 1024; v.spec.smoothingTimeConstant = 0;
          v.samples = new Float32Array(1024); v.db = new Float32Array(512);
          v.source = ctx.createMediaStreamSource(stream); v.source.connect(v.band); v.source.connect(v.spec); v.band.connect(v.an);
          // Capture is analysed, never monitored through the speakers.
          v.unlock = function () { if (!v.disposed && v.ctx.state === 'suspended') v.ctx.resume().catch(function () {}); };
          window.addEventListener('pointerdown', v.unlock); window.addEventListener('keydown', v.unlock);
          v.unlock(); v.status = 1;
        } catch (error) { LivLivBrowser.fail(v, error); }
      }, function (error) { if (!v.disposed) LivLivBrowser.fail(v, error); });
      return id;
    },
    state: function (id) {
      var v = LivLivBrowser.voices[id];
      if (!v) return -2;
      if (v.status !== 1) return v.status;
      if (v.ctx.state === 'closed') { LivLivBrowser.fail(v, new Error('AudioContext was closed')); return -1; }
      return v.ctx.state === 'running' ? 1 : 2;
    }
  },
  LivLivVoice_Start__deps: ['$LivLivBrowser'],
  LivLivVoice_Start: function () { return LivLivBrowser.start(); },
  LivLivVoice_State__deps: ['$LivLivBrowser'],
  LivLivVoice_State: function (id) { return LivLivBrowser.state(id); },
  LivLivVoice_Now: function () { return performance.now(); },
  LivLivVoice_Error__deps: ['$LivLivBrowser', '$stringToUTF8'],
  LivLivVoice_Error: function (id, target, capacity) {
    var v = LivLivBrowser.voices[id];
    stringToUTF8(v ? v.error : 'Input was disposed', target, capacity);
  },
  LivLivVoice_Read__deps: ['$LivLivBrowser'],
  LivLivVoice_Read: function (id, samples, power) {
    if (LivLivBrowser.state(id) !== 1) return 0;
    var v = LivLivBrowser.voices[id];
    try {
      v.an.getFloatTimeDomainData(v.samples); v.spec.getFloatFrequencyData(v.db);
      // Read HEAPF32 afresh: memory may have grown since the preceding Unity frame.
      HEAPF32.set(v.samples, samples >> 2);
      for (var i = 0; i < 512; i++) HEAPF32[(power >> 2) + i] = Math.pow(10, v.db[i] / 10);
      return v.ctx.sampleRate;
    } catch (error) { LivLivBrowser.fail(v, error); return 0; }
  },
  LivLivVoice_Stop__deps: ['$LivLivBrowser'],
  LivLivVoice_Stop: function (id) {
    var v = LivLivBrowser.voices[id]; if (!v) return;
    v.disposed = true; delete LivLivBrowser.voices[id]; LivLivBrowser.release(v);
  }
});
