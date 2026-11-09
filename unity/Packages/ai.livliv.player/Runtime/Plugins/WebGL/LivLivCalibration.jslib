mergeInto(LibraryManager.library, {
  LivLivCalibration_Read__deps: ["$UTF8ToString", "$lengthBytesUTF8", "$stringToUTF8"],
  LivLivCalibration_Read: function(key, buffer, capacity) {
    try {
      var value=localStorage.getItem(UTF8ToString(key));
      if(value===null)return 0;
      var size=lengthBytesUTF8(value)+1;
      if(buffer&&capacity>=size)stringToUTF8(value,buffer,capacity);
      return size;
    } catch(e) { return 0; }
  },
  LivLivCalibration_Write__deps: ["$UTF8ToString"],
  LivLivCalibration_Write: function(key, json) {
    try { localStorage.setItem(UTF8ToString(key),UTF8ToString(json)); } catch(e) { /* upstream: denied/quota is non-fatal */ }
  }
});
