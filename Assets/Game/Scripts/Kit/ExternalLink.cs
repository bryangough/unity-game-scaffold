using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace GameScaffold
{
    /// <summary>
    /// Opens a web page from the game (the credits link, a store page). On the web it goes to a new tab
    /// (<c>Plugins/WebGL/ExternalLink.jslib</c>), since <c>Application.OpenURL</c> would navigate the game's own tab
    /// away. Everywhere else it is <c>Application.OpenURL</c>.
    /// </summary>
    public static class ExternalLink
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void GSOpenUrl(string url);
#endif

        public static void Open(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            GSOpenUrl(url);
#else
            Application.OpenURL(url);
#endif
        }
    }
}
