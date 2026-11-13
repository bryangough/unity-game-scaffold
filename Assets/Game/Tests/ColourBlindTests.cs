using GameScaffold.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The colours a player must tell apart, checked under normal vision, deuteranopia, protanopia, tritanopia and
    /// greyscale (see <see cref="ColourCheck"/>). Put every "this means X" colour in a static field and add it to the
    /// palette below; every pair is compared, so a colour-blind player never has to be told.
    /// </summary>
    public class ColourBlindTests
    {
        private static readonly string[] Names = { "dark", "lit", "cursor" };
        private static readonly Color[] Palette = { SampleGame.TileDark, SampleGame.TileLit, SampleGame.TileCursor };

        [Test]
        public void Palette_EveryPairReadsApart_InEveryVisionMode()
        {
            var close = ColourCheck.ClosePairs(Names, Palette);
            Assert.IsEmpty(close, "Too close:\n" + string.Join("\n", close));
        }

        [Test]
        public void LitTile_IsClearlyBrighterThanADarkTile()
        {
            // brightness alone carries "lit", for total colour blindness and a dim screen in sunlight
            float dL = ColourCheck.Lightness(SampleGame.TileLit) - ColourCheck.Lightness(SampleGame.TileDark);
            Assert.Greater(dL, 30f, $"lit is only {dL:0.0} L* brighter than dark");
        }

        [Test]
        public void ClosePairs_FlagsRedAndGreenOfTheSameLightness()
        {
            // the classic trap: a red and a green a deuteranope cannot separate
            var close = ColourCheck.ClosePairs(new[] { "red", "green" },
                new[] { new Color(0.8f, 0.3f, 0.2f), new Color(0.45f, 0.5f, 0.15f) });
            Assert.AreEqual(1, close.Count);
            StringAssert.Contains("deutan", close[0]);
        }
    }
}
