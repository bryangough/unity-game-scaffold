# Lessons: the tricks and the traps

Things that cost an afternoon once and are now a line in a file. Most were learned with an AI assistant driving the
Unity Editor through Unity's MCP relay while the owner played in the same Editor; the fixes are in the scaffold where
they could be.

## Working in a shared Editor

- **Check Play mode before any Editor command.** The owner plays in the same Editor the assistant drives. A scene
  rebuild sent during their Play session failed (`EditorSceneManager` cannot be used in Play mode) and broke their
  session. Ask the Editor's state first; if it is playing and you did not start it, `Logs/FunTest.txt` shows whether
  someone is playing (the kit appends every result there), and that is also useful playtest data.
- **The save is theirs.** `Progress` is PlayerPrefs; the owner's real wins sit in it. Bots and captures set
  `Progress.Suspended = true` and call `Progress.Reload()` when done; nothing clears the save without asking.
  Compare the save before and after a test that wins levels.
- **One Editor per project, and know which one you are in.** With several Editors open, MCP calls can land in
  either and then stick to the wrong one. Start a command with a guard on `Application.dataPath`, and prefer menu
  items over ad-hoc commands for anything that writes.
- **Never run a long or parallel search in the owner's Editor.** A helper agent's exhaustive search (a crate on
  every cell) ran past the MCP's timeout while another agent was also solving, and Unity crashed. One candidate per
  call, a capped budget, one helper on the Editor at a time; the normal checks (validate, replay) are fine, but not
  while something else is solving.

## Finding out what really happened

- **The MCP console hides compile errors.** A `CS1010` sat unreported while the console looked clean. Read
  `Logs/Editor.log` (or `%LOCALAPPDATA%/Unity/Editor/Editor.log`) and grep `error CS` after a refresh.
  `tools/unity_batch.py compile` does this headless.
- **Compilation happens when the Editor gets a call.** After editing files, a refresh command triggers it; the first
  command right after a recompile can fail once with "Could not find type ..." — resend it. Poll
  `Library/ScriptAssemblies/*.dll` mtimes if you need to know it finished.
- **Reports beat console scrolling.** Every check writes `Builds/Checks/<name>.md`; the build writes
  `build_log.txt` and `size_<target>.md`. A later session reads the file instead of the Console.
- **Build warnings worth ignoring**, so you can see the ones that matter: TMP's "method was given its own cpp file",
  Emscripten's WebGPU/Firefox notes, "pre-baked collision is disabled" on vendor meshes.

## Play mode from a bot

- **Play mode may or may not tick with the Editor in the background.** One day it stayed at frame 1–2; another day
  it ran fine. Check the frame count after a second before assuming either. `EditorApplication.Step()` in a loop
  inside a command throws ("PlayerLoop ... called recursively").
- **The keyboard device is switched off when the Game view loses focus**, and a disabled device drops every event
  queued to it: a replay started from a menu pressed nothing and the player never moved. Changing the focus setting
  does not switch it back on; `InputSystem.EnableDevice(Keyboard.current)` does (`InputBot.WakeKeyboard`). Also set
  `backgroundBehavior = IgnoreFocus` and `editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView`.
- **Queue a key at the end of the frame the game reaches the beat**, and pin `Time.captureDeltaTime` to the game's
  tick, so one tick runs per frame however fast the frames come. Then the replay and the solver agree exactly, and
  a level the solver wins but the game loses points at the views or the input, not the rules.
- **Statics survive Play transitions** when domain reload is off (the scaffold's setting): every static needs a
  `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset, and an `EditorApplication.update` driver
  registered in one command keeps running across scene loads (useful: a smoke run that opens each scene, waits,
  captures, and moves on). `FunTestKit.OnResult` is cleared when the kit is destroyed, so a hook made once only sees
  the first scene; read `Logs/FunTest.txt` instead.
- **Headless Play mode shows the opening frame only.** A batch Unity (`-batchmode`, graphics on) enters Play mode
  and renders, but with no Game view the player loop barely ticks (frame 4 after 120 editor ticks; `Time.captureDeltaTime`
  does not help). `tools/unity_batch.py capture` is good for "does the scene open and draw"; anything that must run
  needs the real Editor. Use `-nographics` for compile, tests and builds (faster), never for a capture.
- **Colours are sRGB, lighting is linear.** A material colour of 0.2 is 0.03 once lit, so "dark grey" primitives
  read as near-black. Keep albedos roughly in the 0.3–0.9 range and let the light do the darkening.
- **Plans come from a compiled list, never from solving at Play start.** A replay that solved every level on the
  main thread when its cache was missing ran for minutes and crashed the Editor. Solve in a menu that writes the
  plans into code (or a cache); the replay only reads.

## The rules of the code that paid off

- **Rules in plain C# with tests; MonoBehaviours present.** Every bug that took a day was in a MonoBehaviour that
  had quietly started deciding things. A rule decided with a physics ray (the hop ray at 1.4 m over 1.3 m crates;
  the guard's eye at 1.7 m over 1.35 m crates) was wrong twice before the rule moved into the board class.
- **Levels as data, the scene generated.** Renumbering 100 levels, moving a whole chapter, reskinning everything:
  none of it touched a scene. Tools find a level by its title, not its number, so renumbering never breaks a
  screenshot or a hint.
- **A level must need its rule.** The validator's "without crates / without plates / with the lasers off: cannot
  win" columns caught a dozen levels that taught nothing. Levels 1–2 may be soft; from 3 on the column must read
  "cannot win".
- **Every input is an action.** A gamepad and keyboard menus came for free; a sim pointer lets bots play; the
  release frame reads the pointer's final position so a one-frame flick still counts.
- **The current level lives in the save**, so `R`, a scene reload and quitting the app all come back to it, and
  tools that load levels go through one static.
- **Feel is cheap and first.** Shake, hit-stop, bursts, popups, punch and flash with no assets; "the test passes if
  the solution feels like your idea".

## Phones and the web

- **Portrait, matched on width.** A canvas of 720 × 1280 matched on width gives tall phones extra height instead of
  clipped sides; an orthographic camera framed for 9:16 is widened by the kit on wider views. Check 9:21.5 and 3:4.
- **The web download has a budget and a report.** 23 MB became 10: four 2048 palette textures capped at 512 (a
  render compared pixel by pixel: 0.08 % of pixels moved), a logo padded to multiples of 4 so it block-compresses
  (it had shipped raw at 2.4 MB), flat UI art crunched on the web only, Brotli with the fallback, the wasm built for
  size (7.9 → 5.5 MB; a cold web build takes ~5 min). Phones hardly change: their apk is mostly engine code.
- **Music streams from per-track AssetBundles** built per platform before each build: out of the first download,
  loaded when a chapter opens, and never a loose audio file on a server (licences). In the Editor the stems play
  straight from the project. Never give the bank asset the loaded clips: in the Editor that writes them back into
  the asset and into the build.
- **Unity re-encodes every web sound to ~128 kbps AAC**, whatever the import settings say. The web build's music is
  therefore encoded outside Unity (`tools/web_music.py`: 40 kbps mono MP3), packed as data in the same bundles and
  decoded by the browser; the jelly game's download went 24.8 → 12.0 MB. Time the loop by the source's length, not
  the decoded clip's: MP3 encoders pad the ends. **The encoder is optional** (ffmpeg on PATH, or `pip install
  soundfile`): without one the web build packs Unity's clips, about three times the size, and says so in one warning.
  The scaffold's MP3 path compiles for WebGL and falls back cleanly, but has not yet shipped in a web build.
- **Sound libraries stay outside the project.** A picks list, an import menu, mobile import rules (mono, 22 kHz,
  ADPCM for short one-shots, Vorbis for loops, Streaming for music), and a WAV downsampler so a 30 MB stem lands in
  git at 4.6 MB. A pack with no licence to read was removed before release.
- **Haptics need a tiny native shim per platform** (`Handheld.Vibrate` is one long buzz): a one-shot
  `VibrationEffect` on Android, `UIImpactFeedbackGenerator` on iOS, `navigator.vibrate` on the web (Android browsers
  only). Light on a step, medium on an action, heavy on a loss; off with the setting.
- **Android asks for VIBRATE only**: strip the INTERNET permission Unity adds, after the Gradle project is written.
  Then the store forms can say "nothing collected" and mean it.
- **A signed Play bundle without a key in the repo**: the key lives in `~/.keys/<game>/`, the build sets the
  keystore fields, builds, and puts them back, so ProjectSettings never holds a path or a password. Play App Signing
  keeps the app key; a lost upload key can be reset through support.
- **Texture caps belong in a build step**, not in importers: the next build puts them back if someone changes an
  importer by hand.

## From the duel game and the slope game

- **Never edit scripts while playing.** The domain reload drops the session and the run; the owner's Play session
  dies with it.
- **The Editor only sends keys to a focused Game view.** Click it first; a replay bot wakes the keyboard device
  instead (`InputBot`).
- **Judge under the phone's quality level.** Phones render the Mobile URP asset, the Editor the PC one: switch the
  Quality level (or `QualitySettings.SetQualityLevel(0, true)` in Play mode) before judging light, shadows or
  bloom. Settings changed on an RP asset in Play mode are not saved: set them again in edit mode.
- **Cap third-party textures, by what the build reaches.** Uncapped packs made 345–387 MB APKs and ran a WebGL
  build out of memory. `TextureCaps` walks the build scenes' dependencies and caps everything outside `Assets/Game` per
  platform (secondary maps at 256); `MeshCaps` compresses their meshes; both run before every build.
- **Readability floors from a phone test** (duel game, iPhone 16 Pro, on a 1080-wide canvas): body text ≥ 42 px,
  captions ≥ 34, buttons ≥ 120 tall with ≥ 44 px labels. On the scaffold's 720-wide canvas that is body ≥ 28,
  captions ≥ 23, button labels ≥ 30, buttons ≥ 80 tall; `TextAudit` checks the floors and overflow. Judge sizes on
  the 9:20 capture, and the HUD over the scene with an underlay or an outline.
- **iPads match height.** Width-matched canvases lose height on 3:4 screens and push the bottom of a layout off;
  `AspectMatch` flips to height-match when the screen is wider than the reference. Check with the 3:4 capture.
- **Content as re-runnable setup scripts.** The duel game built every prefab, asset and data table from
  `Phase*Setup` editor scripts (idempotent, run in a documented order), never by hand: a rebuild is a menu click,
  and the plan's phase ids are in the code. The scaffold's `SceneBuilder` is the first of those.
- **Theme is data; rules never read it.** Two full themes (samurai, swashbuckler) shared one rules core because
  every word, sprite, prefab and clip came through a `ThemeSO`, and the build stripped the inactive theme. New
  mechanics go behind rule flags or theme data, never into one theme's content.
- **Settings are not progress.** A player resetting their progress did not ask for the music back on: keep the
  settings in their own place (or, as `Progress.ClearAll` does here, carry them over the reset).
- **Save ids never change for a theme or a rename.** Rename at display time (`Words`, a theme table); a changed id
  is a lost unlock. The same goes for the bundle id: a renamed app keeps `com.studio.oldname`.
- **A dev menu pays for itself in the first week.** IMGUI, no prefab, opened by a tab or F1, present in dev builds
  and unlocked on a release build by seven taps on the version line (`DevMenu`): jump levels, win now, add
  currency, clear the save, measure.
- **A headless render can lie about lighting.** Objects made at run time in the first frames drew floor-grey (the
  sun had not reached them); re-enabling the lights before the shot fixed it (`BatchCapture` does). When a capture
  looks wrong, paint the objects primary colours and shoot again before believing a lighting or geometry theory.
- **Generated art needs a cut-out step.** AI images come as JPGs on a flat background; a flood fill from the border
  (alpha by distance from the border colour), a despeckle and a trim turn them into sprites in one menu
  (`ArtCutout`). Portraits get a round mask; the icon is cropped 4 % in because generators draw their own corners.
- **Phones portrait, tablets may rotate.** The slope game let iPads turn (`Orientation.TabletsRotate`); the canvas
  then scales by height and keeps a centred column, and the chase camera simply sees wider.
- **Missions are a cheap second reason to play.** Three at a time from a pool, each done is a star, one skip a day
  (`MissionBook`); add new missions at the end of the pool, since saves point at them by index.
- **Write the art prompts down** (`Assets/docs/art_prompts.md`): one shared style line, no text in any image, one
  subject per image, the same seed across a set. **Brainstorm the name in a file** (`names.md`) and check every
  finalist on the stores before the id is set.

## From the releases (the jelly game and the stealth game)

- **Cache slow checks by a hash of everything the answer depends on.** The stealth game's level check (a solver per
  room, plus the same room with each mechanic taken out, to prove the room needs it) took minutes. Keyed by a SHA1 of
  the level's data, the solver's budget and the **full source of the rules files**, one cache entry per level: change
  a level and only it is solved again, change a rule and every one is. Titles and art stay out of the key. The misses
  run in a `Parallel.ForEach` (plain C# with no shared state), capped at cores − 1 and at most 4, since one big search
  holds a few hundred thousand boards.
- **Offer the hint after three failures, and count a stuck restart as one.** The hint (a ghost walking the solution)
  appears after three catches in a level; a restart after a few moves counts too, or a player stuck without failing
  (a crate pushed into a corner) never sees it. Using it gives up the mastery star for that try, not the level.
- **Set sound levels by ear, in the game.** Clips from different packs sat ~20 dB apart: a robot's step at 0.2 drowned
  the player's own (0.07 was right). Repeated sounds from other characters fade with distance from the player. Music
  goes in at one loudness (the full layer at −24 dB RMS), then the owner tunes it while playing.
- **Fewer tracks.** Ten tracks, one per chapter, were 29 MB of bundles; four, unlocked by stars and one chosen for the
  whole game, were 12 MB and read as a reward (`UnlockTable`).
- **A build step that edits an asset must write it and check it.** A deferred save once committed an empty sound bank;
  write with `AssetDatabase.SaveAssets` (or the file directly) and read it back before the build goes on.
- **Store text the owner can paste.** Markdown quotes (`>`) don't copy into a store form: keep a plain-text
  description per store (`Store/description_play.txt`), one line per paragraph, next to the draft.
- **Check the font's embedding rights before it ships.** One game's font was marked restricted for embedding
  (`fsType`) and had to be swapped; read the licence with the art pick, not at submission.
- **The web page is part of the game.** Unity's default page put a footer under the canvas that pushed it 19 px out
  of itch's 800-tall frame, and keys did nothing until the player clicked into the iframe: the scaffold's template
  (`Assets/WebGLTemplates/Game`) fixes both. Leave itch's "start on page load" off: the click lets sound play at once.

## Working with the owner

- **Answers are short; write them down verbatim with the date.** "Keep the old saw. no idea why it's there but it is
  clear what it is." is a decision; so is "skip the size thing till later".
- **Recommend, don't survey.** One recommendation per open question; the owner answers the number.
- **Playtest notes are data.** Each line in the owner's words, a one-word read ("Up: promising", "Broken, no verdict
  yet"), and what was changed because of it.
- **Fixes before content**, and the next thing to play is always at the top of their list.
