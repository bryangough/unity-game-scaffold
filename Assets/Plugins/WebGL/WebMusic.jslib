// For Sfx on the web: MP3 data unpacked from a music AssetBundle becomes a blob: URL that UnityWebRequestMultimedia
// can load, so the browser decodes it without the MP3 ever being a file on the web server. Revoke it once loaded.
mergeInto(LibraryManager.library, {
  GSMusicBlobUrl: function (pointer, length) {
    var bytes = HEAPU8.slice(pointer, pointer + length);
    var url = URL.createObjectURL(new Blob([bytes], { type: "audio/mpeg" }));
    var size = lengthBytesUTF8(url) + 1;
    var buffer = _malloc(size);
    stringToUTF8(url, buffer, size);
    return buffer;
  },
  GSRevokeMusicBlobUrl: function (urlPointer) {
    URL.revokeObjectURL(UTF8ToString(urlPointer));
  }
});
