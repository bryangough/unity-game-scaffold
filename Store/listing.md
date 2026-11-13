# __GAME__ — store listing (draft)

Written by the assistant from the task list; __OWNER__ edits. Character limits in brackets: count after every edit.
Keep every claim true of the build that ships (no ads, nothing collected, offline: see `privacy.md`). The store rules
below were checked when the jelly and stealth games shipped (2026-11); confirm in each console when uploading, since
both stores change them from time to time.

## One line

> __GAME__: *one sentence a stranger understands.*

## Google Play

- **App name** (30): __GAME__ — or with search words: `__GAME__: <genre words>`
- **Short description** (80):
- **Full description** (4,000): keep a copy-ready plain-text version in `Store/description_play.txt` (no quote
  marks, no markdown) and keep it in step with this file. A shape that worked: a two-line hook, what you do, a
  capitalised heading per feature group, then a "PICK UP AND PLAY" list ending with *Plays offline. No account. No ads
  and no in-app purchases. Collects no data at all.*
- **Category:** Games › Puzzle / Arcade · **Tags:** up to 5 · **Contains ads:** no · **In-app purchases:** no
- **Content rating (IARC questionnaire):** write the answers here before filling it in, one line per topic:
  violence (a robot "catching" the player or a saw "cutting" is *cartoon or fantasy violence, implied*, no blood; the
  owner decides how to answer, it is the difference between PEGI 3 and PEGI 7), blood, sexual content, language,
  drugs, gambling, fear, user interaction / sharing, location, purchases.
- **Target audience** (owner's decision): an age group that includes under-13s brings in Google's **Families policy**:
  the game passes it (no ads, no data) but it adds review steps. The simpler choice is 13+ ("not designed for
  children").
- **Data safety:** "Does your app collect or share any of the required user data types?" **No** (the form then skips
  the rest).

## App Store

- **Name** (30): __GAME__
- **Subtitle** (30):
- **Promotional text** (170, changes without a new build):
- **Keywords** (100, commas, no spaces after them; don't repeat words in the name):
- **Description** (4,000): the Play full description works as is.
- **Category:** Games, subcategories (two).
- **Age rating:** the same answers as the IARC questionnaire. "None" to everything is 4+; mild cartoon violence is 9+.
- **Price:** free · **In-app purchases:** none · **Privacy label:** Data Not Collected.
- **Devices** (owner's decision): Player Settings target iPhone **and** iPad (`targetDevice: 2`) by default, which
  means an iPad screenshot set as well. Or ship iPhone-only: iPads still run it, in iPhone mode.

## itch.io

- **Title:** __GAME__ · **Tagline:** (the Play short description works)
- **Description:** copy-ready in `Store/description_itch.txt`: the Play text with a line on top, *Plays in your
  browser on a computer or a phone. Tap / swipe, or use the keyboard or a gamepad.*
- **Genre** and **tags** (up to 10).
- **Kind:** HTML. Upload the web build's zip, tick *This file will be played in the browser*.
- **Embed options:**
  - Viewport **450 × 800** (fits a 1080p screen under itch's header). The page (`Assets/WebGLTemplates/Game`) fills
    whatever frame it gets, so nothing is drawn at a fixed size.
  - **Mobile friendly** on, orientation portrait.
  - **Fullscreen button** on: the page has no Unity footer or fullscreen button of its own, so itch's is the only one.
  - **Automatically start on page load** off: the click to start lets the browser play sound at once.
- **Pricing:** free (or name your own price).
- **Page look (theme):** a background image (2560 × 1440, no repeat, top centre), BG = the image's darkest edge so the
  page blends where it ends, BG2 = the game page's own `--back` colour so the embed sits seamless in the column, text,
  link and button colours taken from the game's UI.

## Devices

- Phones first, portrait. iPad: see the App Store's Devices line.
- Minimum: Android 8 (API 26), iOS 15.

## Screenshots and graphics

Made by `__GAME__ > Store > Capture Screenshots (Play mode)` (`StoreCaptures`: the game lists its poses, each found by
name so renumbering levels never breaks a shot), at every store size, into `Store/Screenshots/<store>_<w>x<h>/`.

| What | Size | Notes |
|------|------|-------|
| App Store iPhone 6.9" | **1320 × 2868** portrait (1290 × 2796 also accepted) | 1–10. One set at the largest size is scaled down for smaller iPhones. |
| App Store iPad 13" | **2064 × 2752** portrait (or 2048 × 2732) | Only if the build keeps iPad. |
| Google Play phone | **1080 × 1920** | 2–8, no transparency, sides 320–3840, **long side at most twice the short side**: a raw 1080 × 2424 capture from a tall phone is rejected. |
| Google Play feature graphic | 1024 × 500 | Required, no transparency. |
| itch.io | any size | The Play captures work. Cover image **630 × 500** (required); banner up to **960 × 320** transparent (replaces the title text). |
| App icon | 1024 × 1024 opaque (App Store); 512 × 512 (Play) | See `Assets/docs/art_prompts.md` for the safe zones. |

## Before submitting

- [ ] Every character count rechecked after the last edit.
- [ ] `privacy.md` hosted (both stores want a link even when nothing is collected) and its contact email filled in.
- [ ] `licences.md` lists every third-party asset that ships, **fonts included, with their embedding rights**: a font
      whose licence or `fsType` says "restricted" or "preview & print" may not be shipped in an app (one game had to
      swap its font for that).
- [ ] Unity's hardware statistics are off (`submitAnalytics: 0` in ProjectSettings).
- [ ] The Android build asks only for the permissions it needs (`AndroidNoInternet` strips `INTERNET`).
