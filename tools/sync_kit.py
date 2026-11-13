#!/usr/bin/env python3
"""Carry kit fixes between the scaffold and a game made from it. Run from the scaffold.

    python tools/sync_kit.py D:/Unity/MyGame                 # report: what differs, and which side changed it
    python tools/sync_kit.py D:/Unity/MyGame --pull          # copy in what only the scaffold changed (and its new files)
    python tools/sync_kit.py D:/Unity/MyGame --diff Assets/Game/Scripts/Kit/Sfx.cs
    python tools/sync_kit.py D:/Unity/MyGame --push Assets/Game/Scripts/Kit/Sfx.cs   # a game's fix back into the scaffold

How it knows which side changed a file: the game's .scaffold.json (written by new_game.py) records the scaffold commit
it was made from and the names it was made with. Each scaffold file is rewritten the way new_game.py would (namespace,
menu, landscape, no-sample) and compared with the game's; the same file at the recorded commit is the common base:

    scaffold newer   only the scaffold changed it since         -> --pull copies it in
    game changed     only the game changed it                   -> a candidate for --push, or simply the game's own
    both changed     each side changed it                       -> merge by hand (--diff shows the two)
    new in scaffold  the game never had it                      -> --pull adds it (with its .meta, so GUIDs match)

--pull moves the recorded commit forward when nothing is left that both sides changed. --push writes the game's file
into the scaffold with the names turned back; read the scaffold's `git diff` before committing it. A game with no
.scaffold.json (made before this tool) is compared without a base: pass --namespace and files show as "differs".
"""
import argparse
import difflib
import json
import os
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import new_game  # noqa: E402  (the rewrite rules live there)

SCAFFOLD = new_game.SCAFFOLD
SYNC_DIRS = ("Assets/Game/Scripts/Kit", "Assets/Game/Editor", "Assets/Game/Tests", "Assets/Plugins", "Assets/WebGLTemplates", "tools", "docs")
SKIP_NAMES = {"new_game.py", "sync_kit.py", "smoke.py"}  # scaffold-only tools


def skipped(rel):
    name = os.path.basename(rel)
    return name.endswith(".meta") or name in SKIP_NAMES or name.startswith("Sample") or "__pycache__" in rel


def is_text(rel):
    return rel.endswith(new_game.TEXT_SUFFIXES)


def read(path):
    if is_text(path):
        with open(path, encoding="utf-8", errors="replace") as f:  # universal newlines: CRLF and LF checkouts compare equal
            return f.read()
    with open(path, "rb") as f:
        return f.read()


def transform(content, rel, pairs):
    if isinstance(content, bytes) or os.path.basename(rel) == "new_game.py":
        return content
    for a, b in pairs:
        content = content.replace(a, b)
    return content


def base_content(commit, rel):
    """The scaffold's file at the commit the game was made from, or None."""
    if not commit:
        return None
    try:
        raw = subprocess.check_output(["git", "show", f"{commit}:{rel}"], cwd=SCAFFOLD, stderr=subprocess.DEVNULL)
    except Exception:  # noqa: BLE001
        return None
    return raw.decode("utf-8", errors="replace").replace("\r\n", "\n") if is_text(rel) else raw


def scaffold_files():
    for d in SYNC_DIRS:
        top = os.path.join(SCAFFOLD, d)
        for root, dirs, files in os.walk(top):
            dirs[:] = [x for x in dirs if x not in new_game.SKIP_DIRS]
            for f in files:
                rel = os.path.relpath(os.path.join(root, f), SCAFFOLD).replace("\\", "/")
                if not skipped(rel):
                    yield rel


def classify(game, config, commit):
    pairs = new_game.text_pairs(config)
    rows = []
    for rel in sorted(scaffold_files()):
        ours = transform(read(os.path.join(SCAFFOLD, rel)), rel, pairs)
        theirs_path = os.path.join(game, rel)
        base = base_content(commit, rel)
        if base is not None:
            base = transform(base, rel, pairs)
        if not os.path.exists(theirs_path):
            rows.append((rel, "deleted in game" if base is not None else "new in scaffold"))
            continue
        theirs = read(theirs_path)
        if theirs == ours:
            continue
        if base is None:
            rows.append((rel, "differs" if not commit else "both changed"))  # new since the base and already different
        elif theirs == base:
            rows.append((rel, "scaffold newer"))
        elif ours == base:
            rows.append((rel, "game changed"))
        else:
            rows.append((rel, "both changed"))
    return rows


def kit_only_in_game(game):
    kit = os.path.join(game, "Assets/Game/Scripts/Kit")
    for root, _, files in os.walk(kit):
        for f in files:
            rel = os.path.relpath(os.path.join(root, f), game).replace("\\", "/")
            if f.endswith(".cs") and not os.path.exists(os.path.join(SCAFFOLD, rel)):
                yield rel


def write_into_game(game, rel, pairs):
    src, dst = os.path.join(SCAFFOLD, rel), os.path.join(game, rel)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    content = transform(read(src), rel, pairs)
    if isinstance(content, bytes):
        shutil.copy2(src, dst)
    else:
        with open(dst, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
    if os.path.exists(src + ".meta") and not os.path.exists(dst + ".meta"):
        shutil.copy2(src + ".meta", dst + ".meta")  # the same GUID as the scaffold's, so later syncs line up


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("game", help="the game's project folder")
    ap.add_argument("--pull", action="store_true", help="copy the scaffold's newer and new files into the game")
    ap.add_argument("--diff", metavar="PATH", help="show the scaffold's (renamed) version against the game's for one file")
    ap.add_argument("--push", metavar="PATH", help="write the game's version of one file into the scaffold, names turned back")
    ap.add_argument("--check", action="store_true", help="exit 1 when anything differs (the smoke test: a fresh game must match)")
    ap.add_argument("--namespace", help="for a game with no .scaffold.json: its C# namespace")
    a = ap.parse_args()

    game = os.path.abspath(a.game)
    if not os.path.isdir(os.path.join(game, "Assets", "Game")):
        sys.exit(f"{game} is not a game made from the scaffold (no Assets/Game)")
    stamp_path = os.path.join(game, new_game.STAMP)
    if os.path.exists(stamp_path):
        with open(stamp_path, encoding="utf-8") as f:
            config = json.load(f)
    elif a.namespace:
        config = {"name": os.path.basename(game), "namespace": a.namespace, "id": new_game.slug(os.path.basename(game)), "company": "Your Studio",
                  "owner": "the owner", "bundle": "", "website": "", "landscape": False, "sample": True}
        print(f"no {new_game.STAMP}: comparing without a base (GameInfo and Menus will show as different)")
    else:
        sys.exit(f"{game} has no {new_game.STAMP}; pass --namespace <its C# namespace> to compare without a base")
    commit = config.get("scaffold_commit")
    pairs = new_game.text_pairs(config)

    if a.diff:
        rel = a.diff.replace("\\", "/")
        ours = transform(read(os.path.join(SCAFFOLD, rel)), rel, pairs)
        theirs = read(os.path.join(game, rel))
        if isinstance(ours, bytes):
            sys.exit("a binary file: nothing to show")
        sys.stdout.writelines(difflib.unified_diff(ours.splitlines(True), theirs.splitlines(True), "scaffold/" + rel, "game/" + rel))
        return

    if a.push:
        rel = a.push.replace("\\", "/")
        content = read(os.path.join(game, rel))
        if not isinstance(content, bytes):
            for old, new in reversed(new_game.code_pairs(config)):  # the names turned back
                content = content.replace(new, old)
            with open(os.path.join(SCAFFOLD, rel), "w", encoding="utf-8", newline="\n") as f:
                f.write(content)
        else:
            shutil.copy2(os.path.join(game, rel), os.path.join(SCAFFOLD, rel))
        print(f"wrote {rel} into the scaffold; read `git diff` there before committing (and keep it generic)")
        return

    rows = classify(game, config, commit)
    if a.pull:
        pulled = [rel for rel, state in rows if state in ("scaffold newer", "new in scaffold")]
        for rel in pulled:
            write_into_game(game, rel, pairs)
            print(f"  pulled  {rel}")
        rows = classify(game, config, commit)
        head, _ = new_game.scaffold_commit()
        if head and not any(state in ("both changed", "differs") for _, state in rows):
            config["scaffold_commit"] = head  # the new base; what the game changed still shows as the game's
            with open(stamp_path, "w", encoding="utf-8", newline="\n") as f:
                json.dump(config, f, indent=2)
                f.write("\n")
            rows = classify(game, config, head)
        print(f"{len(pulled)} file(s) pulled. Open the game in Unity (or `python tools/unity_batch.py compile` there), run its tests, commit.")

    order = ["scaffold newer", "new in scaffold", "both changed", "differs", "game changed", "deleted in game"]
    for state in order:
        group = [rel for rel, s in rows if s == state]
        if group:
            print(f"\n{state} ({len(group)}):")
            for rel in group:
                print("  " + rel)
    extra = list(kit_only_in_game(game))
    if extra:
        print(f"\nonly in the game's Kit ({len(extra)}; if two games would want one unchanged, it belongs in the scaffold):")
        for rel in extra:
            print("  " + rel)
    if not rows:
        print("the game's kit, tools and docs match the scaffold")
    elif not commit:
        print("\n(no scaffold commit recorded: which side changed a file is not known)")
    if a.check and rows:
        sys.exit(1)


if __name__ == "__main__":
    main()
