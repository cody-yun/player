// Sample host policy, intentionally outside the source-mapped runtime package.
mergeInto(LibraryManager.library, {
  $LivLivSampleVisibility: { next: 1, bindings: {} },
  LivLivSampleVisibility_Bind__deps: ['$LivLivSampleVisibility', '$UTF8ToString'],
  LivLivSampleVisibility_Bind: function (objectName) {
    var name=UTF8ToString(objectName), id=LivLivSampleVisibility.next++;
    var suspend=function () { Module['SendMessage'](name,'SuspendBrowserCapture'); };
    var change=function () { if(document.hidden)suspend(); };
    LivLivSampleVisibility.bindings[id]={change:change,hide:suspend};
    document.addEventListener('visibilitychange',change);
    window.addEventListener('pagehide',suspend);
    return id;
  },
  LivLivSampleVisibility_Unbind__deps: ['$LivLivSampleVisibility'],
  LivLivSampleVisibility_Unbind: function (id) {
    var binding=LivLivSampleVisibility.bindings[id];if(!binding)return;
    document.removeEventListener('visibilitychange',binding.change);
    window.removeEventListener('pagehide',binding.hide);
    delete LivLivSampleVisibility.bindings[id];
  },
  LivLivSampleVisibility_Hidden: function () { return document.hidden?1:0; }
});
