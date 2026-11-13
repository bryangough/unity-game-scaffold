// Opens a link in a new tab for ExternalLink.cs. Falls back to navigating this tab only if the popup was blocked.
// Not window.open(url, "_blank", "noopener"): with "noopener" the browser always returns null, so a fallback on null
// would also send the game's own tab away every time. The new tab is cut off from this one by clearing its opener.
mergeInto(LibraryManager.library, {
  GSOpenUrl: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var opened = null;
    try { opened = window.open(url, "_blank"); } catch (e) {}
    if (opened) {
      try { opened.opener = null; } catch (e) {}
    } else {
      try { window.location.href = url; } catch (e) {}
    }
  }
});
