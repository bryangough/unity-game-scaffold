using GameScaffold.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The colour check itself (see <see cref="ColourCheck"/>): pairs are compared under normal vision, deuteranopia,
    /// protanopia, tritanopia and greyscale. The game's own palette is tested beside its rules: put every "this means
    /// X" colour in a static field and pass them all to <see cref="ColourCheck.ClosePairs"/>, so every pair is
    /// compared and a colour-blind player never has to be told (the sample's is <c>SampleColourTests</c>).
    /// </summary>
    public class ColourBlindTests
    {
        [Test]
        public void ClosePairs_PassesColoursThatDifferInLightness()
        {
            var close = ColourCheck.ClosePairs(new[] { "dark", "bright" },
                new[] { new Color(0.38f, 0.42f, 0.52f), new Color(1f, 0.85f, 0.25f) });
            Assert.IsEmpty(close, string.Join("\n", close));
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
