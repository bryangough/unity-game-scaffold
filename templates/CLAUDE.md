# __GAME__

*One paragraph: what the game is, in the owner's words, with the date it was decided. Where it came from (an idea
test bed row, a pitch). What it looks like today.*

## Start here

1. `Assets/docs/TASKS.md` — the open work only: *Next up* is the order, *Waiting on __OWNER__* is their list, the
   **Road to a full game** table maps milestones M1 → M6 to open task ids, each phase holds the briefs of what is
   left. Tick a box by moving the task to that phase's *Done* line with its build notes; append to Key Decisions /
   Open Questions. `TASKS_archive.md` holds every finished task's full notes, by id: look there before re-deriving
   how something was built.
2. `Assets/docs/initial/` — the fun-test record (how the idea got here, the owner's playtest words) and the design
   doc if there is one. Reference only: TASKS.md is the plan.
3. `docs/lessons.md` — the traps the earlier games already paid for (a shared Editor, Play mode from a bot, phones
   and the web, working with the owner). Read it once before the first task, and again before a bot, a build or a
   store step. `docs/method.md` is how a task runs; `docs/tasks_guide.md` is how the task list is kept.

## Layout

| Path | What |
|------|------|
| `Assets/Game/Scripts/` | `Game.asmdef`, namespace `__NAMESPACE__`. **The rules in plain C#** (*name the class*: the state, `Step` / `Tick`, events for the views), the level data (*name it*; levels are entries in a list, never scenes), the views (MonoBehaviours that present the rules), the game manager (builds the current level on load, ticks the rules, win / lose, the HUD; the screens around it are the kit's `Shell`). |
| `Assets/Game/Scripts/Kit/` | **The shared kit: use it before writing anything like it.** Play: `FunTestKit` (timer, result banner, callout, restart, aspect fit), `GameFeel` (shake, hit-stop, bursts, popups, punch, flash), `GameInput` (every input as an action), `TouchInputAdapter` (press / drag / release), `InputMode` (key prompts only for keys and pads), `InputBot` (hands for an Editor bot), `ReplayBot` (plays every level's stored plan through the real game). Phone: `PhoneFit` (60 fps, safe area, background pause), `AspectMatch` (iPads match height), `Orientation` (tablets may rotate), `Haptics`. The save and what hangs on it: `Progress` (stars, bests, settings, one-time flags), `MissionBook` (three missions at a time), `Unlocks` (cosmetics behind a lifetime count). Screens: `Shell` (**the start screen, level select, result card, pause and settings are already built**: the manager sets `Shell.Catalog` (an `ILevelCatalog` from `LevelCatalog`: count and titles), builds `Shell.CurrentLevel`, ticks only while `Shell.Playing`, ends a level with `Shell.ShowResult`, and opens levels with `Shell.OpenLevel`), `ShellUI` (the dim, card, label and button the game's own screens are made from), `AppFlow` (screens, overlays, what Back does), `ScreenFade`, `PrototypeUIHelper` (the canvas setup), `UISkin`, `UIText`, `ButtonPress`, `Breathe`, `Credits` + `TextLinkOpener`, `ExternalLink`. Sound: `Sfx` + `SoundBank`. Dev and services: `DevMenu`, `DevFps`, `GameServices` (the empty ads / data slot), `GameInfo` (the game's id, title and credit). |
| `Assets/Game/Tests/` | `Game.Tests.asmdef` (EditMode, NUnit). Rules, the save, every shipped level; `DocsTests` fails when a script in `Kit/` or `Editor/` is not named in this file, so add a new tool to the table when you add the tool. Run before every hand-off (`__GAME__ > Run EditMode Tests` writes `Builds/Checks/tests.md`; the Unity MCP cannot open the Test Runner window, but it can run that menu). |
| `Assets/Game/Editor/` | `Game.Editor.asmdef`. **`SceneBuilder.cs` is the source of truth for the scene**: `__GAME__ > Build Main Scene` regenerates `Scenes/Main.unity` and puts it in Build Settings. Change the builder, rebuild, commit both (`FunTestKitBuilder` adds the kit and registers the scene). Plus the tools, all under `Menus.Root`: `ProjectSetup` (once per clone); builds (`PhoneBuilds`, `WebSize`, `TextureCaps` + `MeshCaps` for third-party art, `MusicBundles`: web music as small MP3s only if ffmpeg or the soundfile package is installed, otherwise bigger clips and a warning, never a failed build; `StoreBuilds`, `AndroidNoInternet`), sound (`SoundPicks`, `SfxImportRules`, `WavDownsample`), art (`ArtCutout`: generated images into sprites and the icon), checks (`LevelCheck`: **the level check's cache, parallel run, report and stored plans are already built**, the game registers a `LevelCheck.Spec` with its own solver; `LevelReplay`: the plans played back in Play mode; `SampleChecks`: the sample's registration of both, the pattern to copy and then delete; `ColourCheck`, `TextAudit`, `SmokeRun`, `InputCheck`, `Captures`, `BatchCapture` for a headless shot, `GameViewSize`), store screenshots (`StoreCaptures`: fill in its poses), `TestRunnerMenu`, `SavedDataMenu`, `ServicesMenu`. |
| `Assets/Settings/` | URP assets (`Mobile_RPAsset`, `PC_RPAsset`). |
| `Assets/docs/` | The task list, its archive, the fun-test record. |
| *(vendor folders, when they arrive)* | Read-only. Name each, its licence, and that the repo stays private while bought packs are in it. Only the subset a builder uses is copied in; sound libraries never. |
| *(the asset shelf, outside the project)* | Where the owner's packs live, one folder shared by every project. What fits this game, with paths and licences, is the *Asset shelf* section of TASKS.md. |

## Stack

- Unity 6000.6.7f1, URP, Input System 1.20, uGUI with TextMesh Pro. Enter Play Mode without domain reload: every
  static gets a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset.
- **Input System only** (Active Input Handling = Input System Package). Never use `UnityEngine.Input`, and never
  read a device directly: every input is an action on `GameInput` (one map built in code); the pointer goes through
  `TouchInputAdapter`.
- Target: **mobile, touch-first, portrait**: canvas 720 × 1280 matched on width (`PrototypeUIHelper`), camera fit
  9:16 (`FunTestKit.DesignAspect`), portrait-only Player Settings. The current level survives `R` in the save.
- *(the game's own numbers: speeds, timings, meters, with the date and the owner's words when they set them)*

## Controls (Editor)

*(fill in)* Mouse stands in for a finger. `R` restarts, `Esc` pauses (and is Back on every screen). Keys 1–9 pick a level in the Editor and development builds.
On keys or a gamepad the menus work without the mouse (`InputMode` notices the last device used; with a mouse or a
finger none of this shows).

## Conventions

- **Levels are data.** A new level is an entry in the list, never a new scene. *(name the drafting tool if there is one.)*
- **Rules live in the rules class** with EditMode tests; MonoBehaviours only present it. A rule change goes in the
  rules and gets a test; never decide a rule with a physics ray.
- **Every level must be winnable**, and must need its rule: the game's check (a solver, a perfect-player
  simulation; *name it*) is registered as `LevelCheck.Game` and run by `__GAME__ > Checks > Validate Levels` or
  `python tools/unity_batch.py levels` (`Builds/Checks/levels.md`; only changed levels are solved again). The check
  is plain C# with no Unity calls: it runs on worker threads. Plans are read with `LevelCheck.Plan`, never solved in
  Play mode; `Checks > Replay Levels (Play mode)` plays them through the real game (`Builds/Checks/replay.md`).
- Primitives first; a theme dresses them later, as data. A level never references a model directly.
- **Verify before hand-off:** compile clean (check `Editor.log`: the MCP console can hide compile errors, and with
  several Editors open the MCP may be attached to another project, so check `Application.dataPath` first), scene
  rebuilt from its menu, tests green, the changed levels played to a win and a loss, `R` restarts.
  **Never run Editor commands while __OWNER__ is in Play mode** (check `GetState` first; `Logs/FunTest.txt` shows
  their results), and never touch their save without asking (`Progress.Suspended` for bots).
- Commit per task: `type(phaseN): summary`.
