// Web Audio/fetch boundary for runtime/speech.ts. Queue/envelope/WAV parsing stay in C#.
mergeInto(LibraryManager.library, {
  $LivLivSpeechBrowser: {
    next: 1, outputs: {}, pieces: {}, loads: {}, decodes: {},
    error: function (e) { return (e && e.name ? e.name + ': ' : '') + (e && e.message ? e.message : String(e)); },
    unlock: function (o) {
      if (!o || o.disposed) return false;
      try {
        if (!o.ctx) {
          var AC = window.AudioContext || window.webkitAudioContext;
          if (!AC) throw new Error('Web Audio is unavailable');
          try { o.ctx = window.webkitAudioContext ? new AC() : new AC({ sampleRate: 24000 }); }
          catch (_) { o.ctx = new AC(); }
          try { if (navigator.audioSession) navigator.audioSession.type = 'playback'; } catch (_) {}
          o.gain = o.ctx.createGain(); o.gain.gain.value = o.volume; o.gain.connect(o.ctx.destination);
          o.ctx.onstatechange = function () { if (!o.disposed) o.stateChanges++; };
        }
        if (o.ctx.state !== 'running') {
          o.ctx.resume().catch(function () {});
          var b = o.ctx.createBuffer(1, 1, 22050), s = o.ctx.createBufferSource();
          s.buffer = b; s.connect(o.ctx.destination); s.onended = function () { s.disconnect(); }; s.start();
        }
        return true;
      } catch (e) { o.error = LivLivSpeechBrowser.error(e); return false; }
    },
    heard: function (o) {
      if (!o || !o.ctx) return 0;
      var ctx = o.ctx, ts = ctx.getOutputTimestamp && ctx.getOutputTimestamp();
      if (ts && ts.contextTime && ts.performanceTime) return ts.contextTime + Math.max(0, performance.now() - ts.performanceTime) / 1000;
      return ctx.currentTime - (ctx.outputLatency || 0) - (ctx.baseLatency || 0);
    },
    stopPiece: function (id) {
      var p = LivLivSpeechBrowser.pieces[id]; if (!p) return;
      delete LivLivSpeechBrowser.pieces[id]; p.source.onended = null;
      try { p.source.stop(); } catch (_) {}
      try { p.source.disconnect(); } catch (_) {}
      try { p.source.buffer = null; } catch (_) {}
    },
    cancelLoad: function (id) {
      var l = LivLivSpeechBrowser.loads[id]; if (!l) return;
      delete LivLivSpeechBrowser.loads[id]; l.disposed = true; l.chunks = [];
      l.abort.abort();
      if (l.reader) { try { l.reader.cancel().catch(function () {}); } catch (_) {} }
    },
    load: function (output, url, maxBytes) {
      var id = LivLivSpeechBrowser.next++, l = { output: output, state: 0, error: '', chunks: [], offset: 0, bytes: 0, wav: false, disposed: false, abort: new AbortController() };
      LivLivSpeechBrowser.loads[id] = l;
      function push(bytes) {
        if (l.disposed) return;
        if (bytes.length > maxBytes - l.bytes) throw new Error('Speech exceeds the encoded byte limit');
        l.bytes += bytes.length; if (bytes.length) l.chunks.push(bytes);
      }
      function next() {
        return l.reader.read().then(function (r) {
          if (l.disposed) return;
          if (r.value) push(r.value);
          if (r.done) { l.state = 1; return; }
          return next();
        });
      }
      Promise.resolve().then(function () {
        if (l.disposed) return;
        return fetch(new URL(url, document.baseURI).href, { signal: l.abort.signal }).then(function (r) {
          if (l.disposed) return;
          if (!r.ok) throw new Error('Speech HTTP ' + r.status);
          l.wav = /wav/.test(r.headers.get('content-type') || '');
          if (r.body && r.body.getReader) { l.reader = r.body.getReader(); return next(); }
          // Without a ReadableStream the source also decodes the whole file.
          l.wav = false;
          return r.arrayBuffer().then(function (b) { if (!l.disposed) { push(new Uint8Array(b)); l.state = 1; } });
        });
      }).catch(function (e) {
        if (l.disposed) return;
        l.error = LivLivSpeechBrowser.error(e); l.state = -1;
        l.abort.abort();
        if (l.reader) { try { l.reader.cancel().catch(function () {}); } catch (_) {} }
      });
      return id;
    },
    decode: function (output, bytes, maxSamples) {
      var id = LivLivSpeechBrowser.next++, d = { output: output, state: 0, error: '', samples: null, rate: 24000, disposed: false };
      LivLivSpeechBrowser.decodes[id] = d;
      var o = LivLivSpeechBrowser.outputs[output];
      Promise.resolve().then(function () {
        if (d.disposed) return;
        if (!o || !o.ctx || o.disposed) throw new Error('Speech context was disposed');
        return o.ctx.decodeAudioData(bytes).then(function (audio) {
          if (d.disposed || o.disposed) return;
          if (audio.length * audio.numberOfChannels > maxSamples) throw new Error('Speech exceeds the decoded sample limit');
          d.rate = audio.sampleRate;
          d.samples = audio.duration > .05 ? audio.getChannelData(0).slice() : new Float32Array(0);
          d.state = 1;
        });
      }).catch(function (e) { if (!d.disposed) { d.error = LivLivSpeechBrowser.error(e); d.state = -1; } });
      return id;
    },
    dispose: function (id) {
      var o = LivLivSpeechBrowser.outputs[id]; if (!o) return;
      o.disposed = true; delete LivLivSpeechBrowser.outputs[id];
      Object.keys(LivLivSpeechBrowser.pieces).forEach(function (p) { if (LivLivSpeechBrowser.pieces[p].output === id) LivLivSpeechBrowser.stopPiece(p); });
      Object.keys(LivLivSpeechBrowser.loads).forEach(function (l) { if (LivLivSpeechBrowser.loads[l].output === id) LivLivSpeechBrowser.cancelLoad(l); });
      Object.keys(LivLivSpeechBrowser.decodes).forEach(function (d) { var item = LivLivSpeechBrowser.decodes[d]; if (item.output === id) { item.disposed = true; item.samples = null; delete LivLivSpeechBrowser.decodes[d]; } });
      if (o.gain) { try { o.gain.disconnect(); } catch (_) {} }
      if (o.ctx) { o.ctx.onstatechange = null; try { o.ctx.close().catch(function () {}); } catch (_) {} }
      o.stateChanges = 0;
    }
  },
  LivLivSpeech_Create__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Create: function () {
    var id = LivLivSpeechBrowser.next++;
    LivLivSpeechBrowser.outputs[id] = { volume: 1, error: '', disposed: false, stateChanges: 0 };
    return id;
  },
  LivLivSpeech_Unlock__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Unlock: function (id) { return LivLivSpeechBrowser.unlock(LivLivSpeechBrowser.outputs[id]) ? 1 : 0; },
  LivLivSpeech_Running__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Running: function (id) { var o = LivLivSpeechBrowser.outputs[id]; return o && o.ctx && o.ctx.state === 'running' ? 1 : 0; },
  LivLivSpeech_StateChanges__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_StateChanges: function (id) { var o = LivLivSpeechBrowser.outputs[id]; if (!o) return 0; var n = Math.min(4096, o.stateChanges); o.stateChanges -= n; return n; },
  LivLivSpeech_HasOpenContext__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_HasOpenContext: function (id) { var o = LivLivSpeechBrowser.outputs[id]; return o && o.ctx && o.ctx.state !== 'closed' ? 1 : 0; },
  LivLivSpeech_Time__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Time: function (id, heard) { var o = LivLivSpeechBrowser.outputs[id]; return heard ? LivLivSpeechBrowser.heard(o) : (o && o.ctx ? o.ctx.currentTime : 0); },
  LivLivSpeech_WallTime: function () { return performance.now() / 1000; },
  LivLivSpeech_Volume__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Volume: function (id, volume) { var o = LivLivSpeechBrowser.outputs[id]; if (o) { o.volume = volume; if (o.gain) o.gain.gain.value = volume; } },
  LivLivSpeech_Error__deps: ['$LivLivSpeechBrowser', '$stringToUTF8'],
  LivLivSpeech_Error: function (id, kind, target, capacity) {
    var collection = kind === 0 ? LivLivSpeechBrowser.outputs : kind === 1 ? LivLivSpeechBrowser.loads : LivLivSpeechBrowser.decodes;
    var item = collection[id]; stringToUTF8(item ? item.error : 'Speech resource was disposed', target, capacity);
  },
  LivLivSpeech_Schedule__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Schedule: function (id, source, n, rate, at) {
    var o = LivLivSpeechBrowser.outputs[id]; if (!o || !o.ctx) return 0;
    var node;
    try {
      var buffer = o.ctx.createBuffer(1, n, rate); buffer.getChannelData(0).set(HEAPF32.subarray(source >> 2, (source >> 2) + n));
      node = o.ctx.createBufferSource(); node.buffer = buffer; node.connect(o.gain);
      var piece = LivLivSpeechBrowser.next++, p = { output: id, source: node, over: false };
      node.onended = function () { p.over = true; }; node.start(at);
      LivLivSpeechBrowser.pieces[piece] = p; return piece;
    } catch (e) {
      if (node) { try { node.stop(); node.disconnect(); } catch (_) {} }
      o.error = LivLivSpeechBrowser.error(e); return 0;
    }
  },
  LivLivSpeech_Ended__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Ended: function (piece) { var p = LivLivSpeechBrowser.pieces[piece]; return !p || p.over ? 1 : 0; },
  LivLivSpeech_StopPiece__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_StopPiece: function (piece) { LivLivSpeechBrowser.stopPiece(piece); },
  LivLivSpeech_Dispose__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Dispose: function (id) { LivLivSpeechBrowser.dispose(id); },
  LivLivSpeech_Load__deps: ['$LivLivSpeechBrowser', '$UTF8ToString'],
  LivLivSpeech_Load: function (id, url, limit) { return LivLivSpeechBrowser.load(id, UTF8ToString(url), limit); },
  LivLivSpeech_LoadState__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_LoadState: function (id) { var l = LivLivSpeechBrowser.loads[id]; return l ? l.state : -2; },
  LivLivSpeech_LoadWav__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_LoadWav: function (id) { var l = LivLivSpeechBrowser.loads[id]; return l && l.wav ? 1 : 0; },
  LivLivSpeech_LoadRead__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_LoadRead: function (id, target, capacity) {
    var l = LivLivSpeechBrowser.loads[id]; if (!l || !l.chunks.length) return 0;
    var head = l.chunks[0], n = Math.min(capacity, head.length - l.offset);
    HEAPU8.set(head.subarray(l.offset, l.offset + n), target); l.offset += n;
    if (l.offset === head.length) { l.chunks.shift(); l.offset = 0; }
    return n;
  },
  LivLivSpeech_LoadCancel__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_LoadCancel: function (id) { LivLivSpeechBrowser.cancelLoad(id); },
  LivLivSpeech_Decode__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_Decode: function (id, source, n, limit) { return LivLivSpeechBrowser.decode(id, HEAPU8.slice(source, source + n).buffer, limit); },
  LivLivSpeech_DecodeState__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_DecodeState: function (id) { var d = LivLivSpeechBrowser.decodes[id]; return d ? d.state : -2; },
  LivLivSpeech_DecodeLength__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_DecodeLength: function (id) { var d = LivLivSpeechBrowser.decodes[id]; return d && d.samples ? d.samples.length : 0; },
  LivLivSpeech_DecodeRead__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_DecodeRead: function (id, target, capacity) {
    var d = LivLivSpeechBrowser.decodes[id]; if (!d || d.state !== 1 || d.samples.length > capacity) return 0;
    HEAPF32.set(d.samples, target >> 2); return d.rate;
  },
  LivLivSpeech_DecodeCancel__deps: ['$LivLivSpeechBrowser'],
  LivLivSpeech_DecodeCancel: function (id) { var d = LivLivSpeechBrowser.decodes[id]; if (d) { d.disposed = true; d.samples = null; delete LivLivSpeechBrowser.decodes[id]; } }
});
