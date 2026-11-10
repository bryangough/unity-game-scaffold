# __GAME__ — what ships, and the licence for each

Checked __DATE__. Every third-party file that goes into a build is from one of the sources below. Re-check this list
when a pack is added (the sound picks are in `Assets/Game/Editor/SoundPicks.cs`; the art comes from the theme
builders in `Assets/Game/Editor/`). A bought pack's raw files never leave the repo (keep it private while they
are in it); a sound library is never copied in whole, only the picks.

| Source | What the game uses | Licence | Credit needed | Notes |
|---|---|---|---|---|
| **Unity: TextMesh Pro essentials** | The default font (LiberationSans SDF) and TMP shaders (`Assets/TextMesh Pro/`) | Unity Companion licence; the font is under the SIL Open Font Licence | No | Imported by Setup, not committed. |
| **Unity packages** (URP, Input System, uGUI, Shader Graph) | The engine | Unity Companion licence | No | `com.unity.ai.assistant` is editor tooling (the MCP relay); its runtime assemblies are inert. |
| **Our own art** | The icon, title logo, screenshots (`Assets/Game/Art/`, `Store/`) | Ours | — | |
| *(add each pack here as it arrives: asset store packs = Asset Store EULA, use in a game, never redistribute; CC0 packs need no credit; royalty-free music usually forbids distributing the tracks on their own, so stream them packed, never as loose files)* | | | | |

**Credits screen:** "A game by __COMPANY__". None of the licences above requires a credit.
