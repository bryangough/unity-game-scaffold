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

## Layout

| Path | What |
|------|------|
| `Assets/Game/Scripts/` | `Game.asmdef`, namespace `__NAMESPACE__`. **The rules in plain C#** (*name the class*: the state, `Step` / `Tick`, events for the views), the level data (*name it*; levels are entries in a list, never scenes), the views (MonoBehaviours that present the rules), the game manager (builds the current level on load, ticks the rules, win / lose, NEXT, the HUD). `Kit/`: the shared kit (`FunTestKit`, `GameFeel`, `GameInput`, `TouchInputAdapter`, `InputMode`, `PhoneFit`, `Progress`, `Sfx`, `Haptics`, `GameServices`, `UISkin`, `UIText`, `DevFps`, `ExternalLink`, `InputBot`). |
| `Assets/Game/Tests/` | `Game.Tests.asmdef` (EditMode, NUnit). Rules, the save, every shipped level. Run before every hand-off (`__GAME__ > Run EditMode Tests` writes `Builds/Checks/tests.md`; the Unity MCP cannot open the Test Runner window, but it can run that menu). |
| `Assets/Game/Editor/` | `Game.Editor.asmdef`. **`SceneBuilder.cs` is the source of truth for the scene**: `__GAME__ > Build Main Scene` regenerates `Scenes/Main.unity` and puts it in Build Settings. Change the builder, rebuild, commit both. Plus the tools: builds (`PhoneBuilds`, `WebSize`, `MusicBundles`, `StoreBuilds`, `AndroidNoInternet`), sound (`SoundPicks`, `SfxImportRules`), checks (`ColourCheck`, `SmokeRun`, `InputCheck`, `Captures`), store screenshots (`StoreCaptures`: fill in its poses), `SavedDataMenu`, `ServicesMenu`. |
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

*(fill in)* Mouse stands in for a finger. `R` restarts. Keys 1–9 pick a level in the Editor and development builds.
On keys or a gamepad the menus work without the mouse (`InputMode` notices the last device used; with a mouse or a
finger none of this shows).

## Conventions

- **Levels are data.** A new level is an entry in the list, never a new scene. *(name the drafting tool if there is one.)*
- **Rules live in the rules class** with EditMode tests; MonoBehaviours only present it. A rule change goes in the
  rules and gets a test; never decide a rule with a physics ray.
- **Every level must be winnable** by construction or by a check (a solver, a perfect-player simulation) that runs
  from a menu and writes `Builds/Checks/…`. *(name it.)*
- Primitives first; a theme dresses them later, as data. A level never references a model directly.
- **Verify before hand-off:** compile clean (check `Editor.log`: the MCP console can hide compile errors, and with
  several Editors open the MCP may be attached to another project, so check `Application.dataPath` first), scene
  rebuilt from its menu, tests green, the changed levels played to a win and a loss, `R` restarts.
  **Never run Editor commands while __OWNER__ is in Play mode** (check `GetState` first; `Logs/FunTest.txt` shows
  their results), and never touch their save without asking (`Progress.Suspended` for bots).
- Commit per task: `type(phaseN): summary`.
