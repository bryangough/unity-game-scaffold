using GameScaffold.Editor;
using NUnit.Framework;
using UnityEngine;

namespace GameScaffold.Tests
{
    /// <summary>
    /// The colours a player must tell apart, checked under normal vision, deuteranopia, protanopia, tritanopia and
    /// greyscale (Machado et al. 2009, full severity). Put every "this means X" colour in a static field and add a
    /// pair here; a colour-blind player then never has to be told.
    /// </summary>
    public class ColourBlindTests
    {
        [Test]
        public void LitTile_ReadsApartFromADarkTile_InEveryVisionMode()
        {
            for (int i = 0; i < ColourCheck.Filters.Length; i++)
            {
                Color lit = ColourCheck.Filters[i](SampleGame.TileLit);
                Color dark = ColourCheck.Filters[i](SampleGame.TileDark);
                Assert.Greater(ColourCheck.Distance(lit, dark), 0.5f, $"{ColourCheck.FilterNames[i]}: lit vs dark");
            }
            // and in plain brightness, for total colour blindness and a dim screen
            Assert.Greater(ColourCheck.Luminance(SampleGame.TileLit) - ColourCheck.Luminance(SampleGame.TileDark), 0.35f, "lit must also be clearly brighter");
        }

        [Test]
        public void Cursor_ReadsApartFromALitTile_InColourVisionModes()
        {
            for (int i = 0; i < 4; i++)
            {
                Color lit = ColourCheck.Filters[i](SampleGame.TileLit);
                Color cursor = ColourCheck.Filters[i](SampleGame.TileCursor);
                Assert.Greater(ColourCheck.Distance(lit, cursor), 0.35f, $"{ColourCheck.FilterNames[i]}: cursor vs lit");
            }
        }
    }
}
