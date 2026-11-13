# unity-game-scaffold (this repo is the template; a game made from it gets `templates/CLAUDE.md` as its own `CLAUDE.md`)

A Unity 6 starting point for small mobile games, with the kit, the editor tools and the working method taken from
three games built the same way. `README.md` is the front page; `docs/method.md`, `docs/tasks_guide.md` and
`docs/lessons.md` are the method; `tools/new_game.py` makes a game from it.

## Working on the scaffold itself

- **Nothing copyrighted goes in**: no art, sound, fonts or UI packs, nothing from an asset store or a bought library,
  not even one sprite. The TextMesh Pro essentials are imported by Setup and git-ignored. Primitives only.
- Keep it generic. A piece of code belongs here when two games would want it unchanged; a game's own rules, levels,
  themes and its own screens do not (the screens every game has are the kit's `Shell`). The sample game (`Scripts/Sample/`) exists to exercise the kit and is meant to be deleted.
- Names are placeholders that `tools/new_game.py` rewrites: namespace `GameScaffold`, menu root `Menus.Root`
  (`Game/`), `GameInfo.Id / Title / Credit`, Player Settings product and company, `__GAME__`-style tokens in the
  docs and Store templates. Never hard-code a game's name anywhere else.
- Verify before hand-off: `python tools/unity_batch.py compile`, `tests`, and `setup` into a fresh copy made by
  `new_game.py` (the smoke test of the whole thing). The project must not be open in another Editor while the batch
  runs (`Temp/UnityLockfile`).

## Layout

| Path | What |
|------|------|
| `Assets/Game/Scripts/Kit/` | `Game.asmdef`, namespace `GameScaffold`. The kit: `FunTestKit`, `GameFeel`, `GameInput`, `TouchInputAdapter`, `InputMode`, `PhoneFit`, `AspectMatch`, `Orientation`, `Progress`, `MissionBook`, `Unlocks`, `AppFlow`, `Shell` + `ShellUI` + `LevelCatalog` (the screens around the game: start, level select, result card, pause, settings), `ScreenFade`, `Sfx` + `SoundBank`, `Haptics`, `GameServices`, `DevMenu`, `UISkin`, `UIText`, `Credits`, `TextLinkOpener`, `ButtonPress`, `Breathe`, `DevFps`, `ExternalLink`, `InputBot`, `GameInfo`. |
| `Assets/Game/Scripts/Sample/` | `SampleLevel(s)` (levels as data), `SampleRules` (plain C#), `SampleGame` (the manager that presents them, inside the `Shell`'s screens). |
| `Assets/Game/Editor/` | `Game.Editor.asmdef`. `Menus` (the menu root), `ProjectSetup`, `SceneBuilder` (**source of truth for the scene**), `FunTestKitBuilder`, `PhoneBuilds`, `WebSize`, `TextureCaps` + `MeshCaps`, `MusicBundles`, `StoreBuilds`, `AndroidNoInternet`, `SoundPicks`, `SfxImportRules`, `WavDownsample`, `ArtCutout`, `TestRunnerMenu`, `SavedDataMenu`, `ServicesMenu`, `Captures`, `StoreCaptures`, `ColourCheck`, `TextAudit`, `SmokeRun`, `InputCheck`, `BatchCapture`, `GameViewSize`. |
| `Assets/Game/Tests/` | `Game.Tests.asmdef` (EditMode, NUnit): `ProgressTests`, `MissionBookTests`, `UnlocksTests`, `AppFlowTests`, `ShellTests`, `SampleRulesTests`, `ColourBlindTests`, `DocsTests` (both `CLAUDE.md` files name every script in `Kit/` and `Editor/`); `EditModeSuite.Run()` for a RunCommand. |
| `Assets/docs/` | `TASKS.md`, `TASKS_archive.md`, `PLAYTEST_NOTES.md`, `art_prompts.md`, `names.md`, `initial/fun_test_record.md` (templates). |
| `Assets/Plugins/` | `iOS/Haptics.mm`, `WebGL/Haptics.jslib`, `WebGL/ExternalLink.jslib`, `WebGL/WebMusic.jslib`. |
| `Assets/WebGLTemplates/Game/` | The web page: fills its frame, no footer, focus for keys in an iframe. |
| `Assets/Settings/` | URP assets (`Mobile_RPAsset`, `PC_RPAsset`, renderers, the global settings). |
| `Assets/docs/` | `TASKS.md` (the plan template), `TASKS_archive.md`, `initial/fun_test_record.md`. |
| `templates/` | `CLAUDE.md` for a game made from the scaffold. |
| `docs/` | `method.md`, `tasks_guide.md`, `lessons.md`. They are copied into every new game, so write them for a game's assistant as well as for this repo. |
| `tools/` | `new_game.py`, `graduate.py`, `unity_batch.py`, `web_music.py` (the web build's MP3 stems). |
| `Store/` | `listing.md`, `privacy.md`, `licences.md` templates. |

## Stack and conventions

- Unity 6000.6.7f1, URP, Input System 1.20 (Active Input Handling = Input System Package), uGUI with TextMesh Pro,
  Enter Play Mode without domain reload (so every static has a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`
  reset).
- **Input System only.** Never `UnityEngine.Input`; every input is an action on `GameInput`; the pointer goes
  through `TouchInputAdapter`.
- **Portrait, touch first**: canvas 720 × 1280 matched on width (`PrototypeUIHelper`), camera fit 9:16
  (`FunTestKit.DesignAspect`), portrait-only Player Settings. A landscape game flips both.
- Rules in plain C# with EditMode tests; MonoBehaviours present them. Levels are data. Scenes are generated.
- Menu items hang under `Menus.Root`; log tags are `[ClassName]`; reports go to `Builds/Checks/*.md` (git-ignored).
- Commit per task: `type(phaseN): summary`.
