# The task list: how `TASKS.md` works and why

The games were run from one Markdown file in the repo, read by both the owner and the assistant at the start of every
session. This is what the file is, how an entry is written, and the habits that kept it useful for weeks.

## The shape

```
# <Game> — Task List
Owner · Executor · Updated: <date> (<what changed in the list itself>)

> Where the game is (<date>): two or three sentences.
> History: where it came from; links to initial/.

## Ground rules            numbered; the non-negotiables (fixes before content, levels are data, Input System only, ...)
## The bar                 what every task is judged against (one tap to play, a level is 1–3 minutes, ...)
## Next up (AI, in order)  the executor's queue: ids with one line each
## Waiting on <Owner>      the owner's list: play this, listen to that, decide X, make account Y
## Road to a full game     M1 → M6 table: status, open ids, the owner's check in plain words
## Phase N — <name>        *Done:* one line of ids · then the open tasks of that phase with their briefs
## Open questions          numbered, each with a recommendation
## Key decisions           a table: topic → the owner's words, the date, what it means in the code
## Parked                  ideas kept, one line each, with why
## Asset shelf             what is on the shelf outside the project, where, its licence, what it is for
```

## A task

```
- [ ] **4.8 Sound effects (Owner approves by ear).** 18 ids, 2–4 variants each, wired through `Sfx`
  (`Editor/SoundPicks.cs`, `Game > Audio > Import Sound Picks`, report `Builds/Checks/sound.md`).
  Cause-in-code → the fix → the numbers. *Try:* play level 3; the push should sound heavy, the hop light.
```

- **An id** (`phase.number`) that never changes and never gets reused, so decisions, commits and the archive can
  point at it. Sub-steps get a letter (`4.4b`).
- **The owner's words in the title** when the task came from them, with the date: `(Owner, 2026-11-07: "menu
  screen needs to be more interesting")`. The brief then says what was understood and what will be built.
- **Cause → fix → numbers.** Where the problem is in the code (file:line when known), what will change, and the
  first-guess values (they are tuned by hand later; say so).
- **A *Try:* line**: what the owner does and what they should feel. If a task cannot be tried, it is probably two
  tasks or not a task.
- **A *Done* line** when it is finished, under the task, before it moves to the phase's *Done:* line or the
  archive: the date, what was built, the values chosen and why, and what was *not* verified. Honest notes
  ("compiled and scene rebuilt; not play-tested") saved hours later.

## The queue and the owner's list

*Next up* is short (five to ten lines) and ordered; the executor takes the top. *Waiting on the owner* is everything
only they can do: play a build, listen to candidates, answer a question, make a store account. Each line names the
task ids it unblocks. When the owner answers tersely ("no pickups", "keep the old saw"), the words go into the Key
decisions table that day, with the date, and the tasks move.

## Milestones

The road table makes the plan visible at a glance: six milestones from "ten levels prove the base" to "released",
each with its open ids and the owner's check in plain words (not "feature complete" but "three friends finish ten
levels unaided"). Update the status column when a milestone's check passes, in the owner's words.

## Decisions and open questions

A decision is a row: **topic** → the owner's words, the date, and what it means ("Portrait: 'portrait lets try',
then 'Portrait is fine' (2026-11-06). 720 × 1280 canvas matched on width, 9:16 camera fit. New levels are designed
taller than wide."). Open questions are numbered and each carries a recommendation; the owner answers the number.
Answered questions move to decisions; their numbers are never reused.

## Keeping it readable

- **Cut it down** when it passes ~300 lines or every month: finished tasks move to `TASKS_archive.md` **with their
  full Done notes** (the archive is a record of how things were built, so nothing is re-derived), the phases keep a
  one-line *Done:* of ids, and the header says when the cut was made.
- **Dates everywhere.** Relative words ("yesterday", "last week") rot; `2026-11-09` does not.
- **One file.** Store copy, story words and art prompts get their own files only when they are long; the task list
  links to them. The CLAUDE.md (or README) points at TASKS.md as the plan and at `initial/` as history.
- **Commit per task**, `type(phaseN): summary` (`feat(phase3)`, `fix(phase5)`, `docs(phase6)`, `chore(phase7)`,
  `build(phase2)` for the version bump a build makes).

## Why it works

The owner never has to hold the plan in their head: the next thing to play and the next thing to decide are always
at the top. The executor never has to ask what to do next, and its hand-off is written where the next session will
read it. Decisions carry the words they were made in, so a month later "why is there no clock?" has an answer with a
date. And the archive means a feature is explained once, in the place its id points to.
