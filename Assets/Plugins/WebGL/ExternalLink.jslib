// Opens a link in a new tab for ExternalLink.cs. Falls back to navigating this tab if the popup is blocked.
mergeInto(LibraryManager.library, {
  GSOpenUrl: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var opened = null;
    try { opened = window.open(url, "_blank", "noopener"); } catch (e) {}
    if (!opened) {
      try { window.location.href = url; } catch (e) {}
    }
  }
});
