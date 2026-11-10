# unity-game-scaffold

A starting point for small Unity games, distilled from three games built the same way (a grid stealth puzzle, a
colour-matching arcade puzzle, a physics runner): the code every one of them ended up needing, the editor tools that
got them onto phones and the web, and the **working method** — a task list that an AI coding assistant can execute
top to bottom while the owner plays, decides and steers.

It is a Unity 6 project (URP, Input System only, portrait, touch first) that opens, compiles and plays a tiny sample
game out of the box. No art, sound or UI packs are included: everything in here is code, settings and docs you may
use freely (MIT). Your own packs stay on a shelf outside the project and only the picks a builder uses are copied in.

## What's in the box

| Where | What |
|---|---|
| `Assets/Game/Scripts/Kit/` | **The kit.** `FunTestKit` (timer, WIN / LOSE banner, callout, RESTART + `R`, EventSystem, aspect fit), `GameFeel` (shake, hit-stop, bursts, confetti, popups, punch, flash; no assets), `GameInput` (every input as an Input System action; touch, keys and a pad), `TouchInputAdapter` (press / drag / release), `InputMode` (shows key prompts only to keyboard and pad players), `PhoneFit` + `AspectMatch` + `Orientation` (60 fps, safe area, pause in the background; iPads match height; tablets may rotate), `Progress` (the save: per-level stars and bests, settings, one-time flags; JSON in PlayerPrefs, versioned, tested), `MissionBook` (three missions at a time from a pool, tested), `AppFlow` (screens, overlay stack and what Back does, tested), `ScreenFade`, `Sfx` + `SoundBank` (one-shots by id, level-driven loops, ducking, stem music crossfaded by intensity, streamed from per-track AssetBundles), `Haptics` (Android, iOS, web), `GameServices` (the empty slot where ads and analytics would go), `DevMenu` (an IMGUI dev panel the game fills; unlocked on a release build by seven taps), `UISkin`, `UIText`, `Credits` + `TextLinkOpener`, `ButtonPress`, `Breathe`, `DevFps`, `ExternalLink`, `InputBot` (hands for an Editor bot). |
| `Assets/Game/Scripts/Sample/` | A 150-line sample game that uses all of it: rules in plain C# with tests, levels as data, a manager that only presents. Delete it when yours is in. |
| `Assets/Game/Editor/` | **The tools**, under one menu. `Build Main Scene` (the scene is generated, never hand-edited), `Setup Project`, `Build > WebGL / Android / iOS` with a size report and a web budget, `Build > Store >` an upload key, a signed Play bundle and an itch.io zip, `WebSize` (our art's caps, Brotli, wasm for size), `TextureCaps` + `MeshCaps` (third-party art the build reaches, capped per platform), `MusicBundles`, `AndroidNoInternet`, `Audio > Import Sound Picks` (picks copied in from a library outside the project, mobile import rules, a downsampler), `Art > Cut Out Generated Art` (AI images into sprites, portraits, the app icon), `Run EditMode Tests` (to a report file), `Checks >` captures at phone / tall phone / tablet shapes, a colour-blindness capture, a text-fit audit and a smoke run, `Dev > Game View` sizes, `Saved Data >`, `Services >`. |
| `Assets/Game/Tests/` | EditMode tests for the save, missions, app flow, the sample's rules and the colours a player must tell apart; `EditModeSuite.Run()` runs them all from a command when the Test Runner window is out of reach. |
| `Assets/docs/` | **The plan** (`TASKS.md`, as a template: ground rules, the bar, the road to a full game (M1 → M6), phases 0–7 with the briefs every game needed, decisions, open questions, the asset shelf), its archive, the playtest notes and scorecard, the art prompts and the name brainstorm templates, `initial/` for the fun-test record. |
| `CLAUDE.md` | What an AI assistant reads first: the layout, the stack, the conventions. `templates/CLAUDE.md` is the game version. |
| `docs/` | [`method.md`](docs/method.md) (idea → two-minute playable → verdict → own project → milestones → store), [`tasks_guide.md`](docs/tasks_guide.md) (how the task list works and why), [`lessons.md`](docs/lessons.md) (the tricks and the traps, most of them learned with an assistant driving the Editor). |
| `tools/` | `new_game.py` (a new project from the scaffold, renamed and ready), `graduate.py` (bring a prototype over from an idea test bed), `unity_batch.py` (compile, set up, test and build headless). |
| `Store/` | Templates for the listing, the privacy policy and the licence list. |

## Quick start

Requirements: Unity 6000.6 (the exact version is in `ProjectSettings/ProjectVersion.txt`; the Hub installs it), the
Android / iOS / WebGL build modules for the builds you want, Python 3 for the tools, git.

```
git clone https://github.com/<you>/unity-game-scaffold.git
cd unity-game-scaffold
python tools/new_game.py "My Game" --company "My Studio" --bundle com.mystudio.mygame
```

That makes `../MyGame`, renamed throughout (namespace, menu, save keys, Player Settings), with a first commit. Open
it in Unity Hub, run **My Game > Setup Project (once)** (it imports TextMesh Pro's essentials, builds the sound bank
and the main scene), press Play: tap the lit tiles. Or headless: `python tools/unity_batch.py setup`, then `tests`.

Then read the new project's `CLAUDE.md` and fill in `Assets/docs/TASKS.md`: what the game is, the bar it is judged
against, the first phase. Replace `Scripts/Sample/` with your game; keep the kit.

## Starting a game

**From an idea test bed.** The games this came from started as two-minute prototypes in one throwaway Unity project
(a `Proto_NN_Name/` folder each, with `Scripts/`, an `Editor/…SceneBuilder.cs` and the same kit), got a playtest
verdict, and the winners moved out. `tools/graduate.py <Proto folder> <new project>` copies the scripts into the
new project's assemblies and namespace, rewrites the kit calls, lists what still reads legacy input, and writes
`Assets/docs/initial/fun_test_record.md` from the test bed's task row, playtest notes and design doc. The method is
in [`docs/method.md`](docs/method.md).

**From scratch.** `new_game.py`, then write the pitch and the bar into `TASKS.md` and build the first playable loop
where the sample was: input → the core verb → feedback → a win and a lose both reachable in two minutes → restart.

## Getting the most out of it

The scaffold is half code and half method. The method, in one breath:

1. **The plan is one file**, `Assets/docs/TASKS.md`: ground rules, the bar, *Next up* in order, *Waiting on the
   owner*, milestones M1 → M6, phases with briefs, decisions with the owner's words and the date. The assistant
   works it top to bottom; the owner plays and answers. Finished tasks keep their build notes and move to an archive.
   [`docs/tasks_guide.md`](docs/tasks_guide.md).
2. **Fixes before content.** The next item is a bug or a feel fix unless the owner asks for content.
3. **Levels are data, scenes are generated, rules are plain C# with tests.** A new level is a few lines; the scene
   comes from a builder menu; a rule change goes in the rules class and gets a test.
4. **Every task ends in something the owner can try**, with a *Try:* line saying what they should feel.
5. **Verify before hand-off:** compile clean (read `Editor.log`, not just the console), scene rebuilt from its menu,
   tests green, a win and a lose played, `R` restarts. Commit per task, `type(phaseN): summary`.
6. **Assets live outside the project** until an art pick; then only the subset a builder uses, and never a whole
   sound library. Licences are written down before anything ships.
7. **Measure, don't guess:** the build writes a size report; the web download has a budget; the sound report says
   what the clips cost; a colour-blindness capture and a smoke run are one menu each.

The traps, with the fixes that worked, are in [`docs/lessons.md`](docs/lessons.md): the Unity MCP console hiding
compile errors, Play mode not ticking in the background, the keyboard device switched off when the Game view loses
focus, the owner's save and Play session shared with the assistant, a brute-force search crashing the Editor.

## Working with an AI assistant

The scaffold was built with Claude Code driving the Unity Editor through Unity's MCP relay (the
`com.unity.ai.assistant` package is in the manifest; `.mcp.json.example` shows the client side). `CLAUDE.md` is
the assistant's briefing; `TASKS.md` is the work. Nothing here requires an assistant, but the method assumes two
roles: one who plays and decides, one who builds and verifies.

## Licence

MIT (see `LICENSE`). TextMesh Pro's essential resources are imported from Unity's package on setup and are not part
of this repository.
