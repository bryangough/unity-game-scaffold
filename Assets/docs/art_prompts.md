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

### 1.1 App icon (1024 x 1024, opaque)
> *(prompt)*

### 1.2 Title logo (transparent, ~1024 wide)
> *(prompt)*

### 1.3 Feature graphic (Google Play, 1024 x 500)
> *(prompt)*

## 2. In the game

### 2.1 Badges / stamps (512, transparent)
> *(prompt per badge)*

### 2.2 Portraits (256, round)
> *(prompt per character)*
