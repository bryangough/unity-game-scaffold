using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameScaffold
{
    /// <summary>Opens the URL of a TMP <c>&lt;link="…"&gt;</c> tag when it is tapped (the credits). The label needs <c>raycastTarget</c> on.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextLinkOpener : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            var text = GetComponent<TMP_Text>();
            int i = TMP_TextUtilities.FindIntersectingLink(text, e.position, e.pressEventCamera);
            if (i < 0) return;
            string url = text.textInfo.linkInfo[i].GetLinkID();
            if (!string.IsNullOrEmpty(url)) ExternalLink.Open(url);
        }
    }
}
