// Short vibration for Haptics.cs on the web. Android browsers support it; iPhone Safari ignores it.
mergeInto(LibraryManager.library, {
  GSVibrate: function (milliseconds) {
    if (typeof navigator !== "undefined" && navigator.vibrate) {
      try { navigator.vibrate(milliseconds); } catch (e) {}
    }
  }
});
