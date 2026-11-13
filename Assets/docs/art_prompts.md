# __GAME__: image prompts for the 2D art

The pictures the game and the store need that are not 3D models or drawn in code: icon, title logo, splash,
badges, portraits, callout decals, store graphics. Generate them, drop them into `Assets/Game/Art/Generated/`
(`icon_*.png`, `logo_*.jpg`, `portrait_<name>.jpg`, `splash_*.jpg`, anything else as a cut-out sprite) and run
`__GAME__ > Art > Cut Out Generated Art`: the background is removed, portraits get a round mask, the icon is set.
Then the task that wires each one in. From the duel and slope games, where this file kept a set of images consistent.

## Shared style (put this at the start of every prompt)

> *(the game's look in one sentence: medium, palette with hex values, lighting, what to avoid)*
> Flat lighting, no gradients or glow, no 3D render look, **no text, no letters, no watermark, no signature**.

Rules that keep the set consistent:
- **No text in any image.** Generators garble lettering; the game draws its own text in its font.
- Square images: subject centred, with a 10 % empty margin. Icons on a plain, even background (the cut-out removes it).
- One subject per image, readable at 64 px. Squint test: the silhouette must read in pure black.
- Generate at 1024 px or larger; the sizes below are what Unity imports.
- Same seed or style reference across a set (all badges, all portraits), so they match.

## 1. Store and device (needed before launch)

The specs below are what the stores took when the jelly and stealth games shipped; sizes for the screenshots are in
`Store/listing.md`.

### 1.1 App icon (1024 x 1024, opaque)
- 24-bit PNG, **no alpha**, full bleed. **Don't round the corners**: iOS and Android apply their own masks.
- Everything that matters inside the **central 72 % circle** (~736 px), so a circular or squircle mask clips nothing.
- Readable at 48 px. Same camera angle as the game, if the game has one. Copies: `Store/icon_1024.png`,
  `Store/icon_512.png` (Play).
> *(prompt)*

### 1.2 Android adaptive icon (two layers, 1084 x 1084 each)
- Background: opaque, the icon's backdrop only. Foreground: transparent, the subject strictly inside the **central
  66 % circle** (715 px, centred): launchers crop and move the layers.
> *(prompt, or cut the 1.1 icon into its two layers)*

### 1.3 Title logo (transparent, ~1024 wide)
- On a plain black or white background for the cut-out; a dark outline round the whole logo so it reads over any
  screen. Pad to sides that are multiples of 4 (`WebSize.PadToFour`) so it block-compresses. The one place text in an
  image is allowed: check every letter.
> *(prompt)*

### 1.4 Feature graphic (Google Play, 1024 x 500, opaque)
- Scene on the left ~55 %, logo and a tagline on the right; Play may overlay a play button in the middle.
> *(prompt)*

### 1.5 itch.io (cover 630 x 500 opaque, banner 960 x 320 transparent, page background 2560 x 1440)
- Cover: logo over a scene, fully framed with margins (5:4). Banner: the logo ~300 px tall, centred on a transparent
  strip. Background: a render of the game's world, darkest at its edges so the page colour can match.
> *(prompt)*

## 2. In the game

### 2.1 Badges / stamps (512, transparent)
> *(prompt per badge)*

### 2.2 Portraits (256, round)
> *(prompt per character)*
