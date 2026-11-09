mergeInto(LibraryManager.library, {
  $LivLivContext: {
    entries: {}, canvas: null, listener: null, lost: false,
    notify: function () {
      var self = LivLivContext;
      if (self.lost) return;
      self.lost = true;
      Object.keys(self.entries).forEach(function (key) {
        var callback = self.entries[key];
        if (callback) self.call(callback, +key);
      });
    },
    call: function (callback, id) { {{{ makeDynCall('vi', 'callback') }}}(id); }
  },
  LivLivContext_Register__deps: ['$LivLivContext', '$GL'],
  LivLivContext_Register: function (id, callback) {
    var self = LivLivContext;
    if (!self.canvas) {
      self.canvas = Module.canvas;
      self.lost = !!(GL.currentContext && GL.currentContext.GLctx.isContextLost());
      self.listener = function () { self.notify(); };
      self.canvas.addEventListener('webglcontextlost', self.listener);
    }
    self.entries[id] = callback;
    if (self.lost) self.call(callback, id);
  },
  LivLivContext_Unregister__deps: ['$LivLivContext'],
  LivLivContext_Unregister: function (id) {
    var self = LivLivContext;
    delete self.entries[id];
    if (!Object.keys(self.entries).length && self.canvas) {
      self.canvas.removeEventListener('webglcontextlost', self.listener);
      self.canvas = self.listener = null;
      self.lost = false;
    }
  }
});
