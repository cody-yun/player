// ES5 ABI shim. Modern MediaPipe runs in the separate self-hosted ES module.
mergeInto(LibraryManager.library, {
  $LivLivFaceBridge: {
    next: 1, entries: {}, modules: {},
    load: function (url) {
      if (!LivLivFaceBridge.modules[url]) LivLivFaceBridge.modules[url] = new Promise(function (resolve, reject) {
        var script = document.createElement('script'); script.type = 'module'; script.src = url;
        script.onload = function () { if (window.LivLivFaceApi) resolve(window.LivLivFaceApi); else reject(new Error('Face module did not initialize')); };
        script.onerror = function () { delete LivLivFaceBridge.modules[url]; script.remove(); reject(new Error('Could not load face module ' + url)); };
        document.head.appendChild(script);
      });
      return LivLivFaceBridge.modules[url];
    },
    state: function (id) {
      var v = LivLivFaceBridge.entries[id];
      if (!v) return { status: -2, error: 'Input was disposed' };
      if (v.inner !== undefined) return v.api.state(v.inner);
      var progress = v.api && v.preload ? v.api.progress(v.options) : v.progress;
      return { status: v.status, step: v.status === 1 ? 'ready' : progress === 1 ? 'prepare' : v.step, progress: progress, error: v.error };
    }
  },
  LivLivFace_Start__deps: ['$LivLivFaceBridge', '$UTF8ToString'],
  LivLivFace_Start: function (json, preload) {
    var options = JSON.parse(UTF8ToString(json)), id = LivLivFaceBridge.next++, v = { status: 0, step: 'download', progress: 0, error: '' };
    v.preload = preload; v.options = options; LivLivFaceBridge.entries[id] = v;
    LivLivFaceBridge.load(options.moduleUrl).then(function (api) {
      if (v.disposed) return; v.api = api;
      if (preload) return api.preload(options).then(function () { if (!v.disposed) v.status = 1; });
      v.inner = api.start(options);
    }).catch(function (error) { if (!v.disposed) { v.status = -1; v.error = String(error).slice(0, 512); } });
    return id;
  },
  LivLivFace_State__deps: ['$LivLivFaceBridge'],
  LivLivFace_State: function (id) { return LivLivFaceBridge.state(id).status; },
  LivLivFace_Info__deps: ['$LivLivFaceBridge', '$stringToUTF8'],
  LivLivFace_Info: function (id, target, capacity) { stringToUTF8(JSON.stringify(LivLivFaceBridge.state(id)), target, capacity); },
  LivLivFace_Now: function () { return performance.now(); },
  LivLivFace_Poll__deps: ['$LivLivFaceBridge'],
  LivLivFace_Poll: function (id, now, target) {
    var v = LivLivFaceBridge.entries[id]; if (!v || !v.api || v.inner === undefined) return 0;
    var f = v.api.poll(v.inner, now); if (!f) return 0;
    var at = target >> 3, flags = 1;
    if (f.m) { HEAPF64.set(f.m, at); flags |= 2; }
    if (f.b) { HEAPF64.set(f.b, at + 16); flags |= 4; }
    if (f.l) { HEAPF64.set(f.l, at + 34); flags |= 8; }
    return flags;
  },
  LivLivFace_Stop__deps: ['$LivLivFaceBridge'],
  LivLivFace_Stop: function (id) {
    var v = LivLivFaceBridge.entries[id]; if (!v) return;
    v.disposed = true; delete LivLivFaceBridge.entries[id]; if (v.api && v.inner !== undefined) v.api.stop(v.inner);
  }
});
