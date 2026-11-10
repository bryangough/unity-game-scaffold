using System.Text;
using UnityEngine;

namespace GameScaffold
{
    /// <summary>
    /// The credits as lines of text, turned into one TextMesh Pro rich-text block: a line starting "# " is a heading,
    /// "@ " a link to that site (tapped through <see cref="TextLinkOpener"/>), an empty string a gap. The maker is
    /// Player Settings' company name, so the by-line, the credits and the store agree. Add each pack that ships and
    /// needs a credit (see Store/licences.md) as a heading and lines.
    /// </summary>
    public static class Credits
    {
        public static string Maker => Application.companyName;

        public static string[] Lines => new[]
        {
            "by " + Maker,
            "",
            string.IsNullOrEmpty(GameInfo.Website) ? "" : "@ " + GameInfo.Website,
        };

        /// <summary>The lines as one rich-text block; headings and links in <paramref name="accent"/>, the rest as the label's colour.</summary>
        public static string RichText(Color accent, float headingSize = 32f)
        {
            var sb = new StringBuilder();
            string hex = ColorUtility.ToHtmlStringRGB(accent);
            foreach (string line in Lines)
            {
                if (line.StartsWith("# ")) sb.Append($"<size={headingSize}><color=#{hex}>{line.Substring(2)}</color></size>\n");
                else if (line.StartsWith("@ ")) sb.Append($"<link=\"https://{line.Substring(2)}\"><u><color=#{hex}>{line.Substring(2)}</color></u></link>\n");
                else sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }
    }
}
