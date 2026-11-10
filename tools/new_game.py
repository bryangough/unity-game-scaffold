#!/usr/bin/env python3
"""Make a new game project from this scaffold.

    python tools/new_game.py "Cloak & Crate" --company "Your Studio" --bundle com.yourstudio.cloakandcrate
    python tools/new_game.py "Splat Swap" --folder D:/Unity/SplatSwap --namespace SplatSwap --no-git

What it does, in order:
  1. Copies the scaffold to the new folder (skipping Library, Temp, Logs, Builds, .git, generated project files).
  2. Renames the C# namespace (GameScaffold -> <Namespace>) in every .cs and .asmdef, the editor menu root
     (Game/ -> <Name>/), and GameInfo (Id, Title, Credit).
  3. Sets ProjectSettings: productName, companyName, applicationIdentifier (every platform), a fresh productGUID.
  4. Fills the placeholders in CLAUDE.md, Assets/docs/TASKS.md and the Store templates (__GAME__, __OWNER__, ...).
  5. git init + first commit (unless --no-git), and prints what to do next.

Then open the folder in Unity Hub (or run `python tools/unity_batch.py setup`) and start from CLAUDE.md.
To bring a prototype over from the idea test bed, run tools/graduate.py on the new project afterwards.
"""
import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys
import uuid

SCAFFOLD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIP_DIRS = {".git", "Library", "Temp", "Logs", "Builds", "obj", "UserSettings", ".utmp", "Data", "build", ".vs"}
SKIP_SUFFIXES = (".csproj", ".sln", ".slnx", ".user", ".apk", ".aab")
TEXT_SUFFIXES = (".cs", ".asmdef", ".md", ".json", ".txt", ".jslib", ".mm", ".py", ".asset", ".example")


def pascal(name):
    parts = re.findall(r"[A-Za-z0-9]+", name)
    return "".join(p[:1].upper() + p[1:] for p in parts) or "Game"


def slug(name):
    return re.sub(r"[^a-z0-9]", "", name.lower()) or "game"


def copy_tree(src, dst):
    for root, dirs, files in os.walk(src):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        rel = os.path.relpath(root, src)
        out = os.path.join(dst, rel) if rel != "." else dst
        os.makedirs(out, exist_ok=True)
        for f in files:
            if f.endswith(SKIP_SUFFIXES) or f.endswith(".meta") and f[:-5].endswith(SKIP_SUFFIXES):
                continue
            shutil.copy2(os.path.join(root, f), os.path.join(out, f))


def rewrite(path, pairs):
    with open(path, encoding="utf-8") as f:
        s = f.read()
    t = s
    for a, b in pairs:
        t = t.replace(a, b)
    if t != s:
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(t)
        return True
    return False


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("name", help='the game\'s name as shown to people, e.g. "Cloak & Crate"')
    ap.add_argument("--folder", help="where to make it (default: a sibling of the scaffold named after the game)")
    ap.add_argument("--namespace", help="C# namespace and asmdef root (default: the name in PascalCase)")
    ap.add_argument("--company", default="Your Studio", help="companyName in Player Settings and the credits line")
    ap.add_argument("--bundle", help="application identifier, e.g. com.yourstudio.cloakandcrate")
    ap.add_argument("--owner", default="the owner", help="who decides (used in TASKS.md / CLAUDE.md placeholders)")
    ap.add_argument("--website", default="", help="the link on the credits card (may be empty)")
    ap.add_argument("--no-git", action="store_true", help="skip git init and the first commit")
    a = ap.parse_args()

    ns = a.namespace or pascal(a.name)
    gid = slug(a.name)
    folder = os.path.abspath(a.folder or os.path.join(os.path.dirname(SCAFFOLD), pascal(a.name)))
    bundle = a.bundle or f"com.{slug(a.company)}.{gid}"
    if os.path.exists(folder) and os.listdir(folder):
        sys.exit(f"{folder} exists and is not empty; pick another --folder")

    print(f"copying scaffold -> {folder}")
    copy_tree(SCAFFOLD, folder)

    today = datetime.date.today().isoformat()
    code_pairs = [
        ("namespace GameScaffold.Editor", f"namespace {ns}.Editor"),
        ("namespace GameScaffold.Tests", f"namespace {ns}.Tests"),
        ("namespace GameScaffold", f"namespace {ns}"),
        ("using GameScaffold.Editor;", f"using {ns}.Editor;"),
        ("using GameScaffold;", f"using {ns};"),
        ("GameScaffold.Editor.", f"{ns}.Editor."),
        ('"rootNamespace": "GameScaffold.Editor"', f'"rootNamespace": "{ns}.Editor"'),
        ('"rootNamespace": "GameScaffold.Tests"', f'"rootNamespace": "{ns}.Tests"'),
        ('"rootNamespace": "GameScaffold"', f'"rootNamespace": "{ns}"'),
        ('public const string Root = "Game/";', f'public const string Root = "{a.name.replace(chr(34), "")}/";'),
        ('public const string Id = "gamescaffold";', f'public const string Id = "{gid}";'),
        ('public const string Title = "Game Scaffold";', f'public const string Title = "{a.name}";'),
        ('public const string Credit = "A game by Your Studio";', f'public const string Credit = "A game by {a.company}";'),
        ('public const string Website = "";', f'public const string Website = "{a.website}";'),
    ]
    doc_pairs = [
        ("__GAME__", a.name), ("__NAMESPACE__", ns), ("__OWNER__", a.owner), ("__COMPANY__", a.company),
        ("__BUNDLE__", bundle), ("__DATE__", today), ("__FOLDER__", folder.replace("\\", "/")),
    ]
    changed = 0
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if not f.endswith(TEXT_SUFFIXES):
                continue
            p = os.path.join(root, f)
            if f.endswith(".asset"):
                continue  # Unity assets are touched below, by hand
            if rewrite(p, code_pairs + doc_pairs):
                changed += 1
    print(f"renamed namespace/menu/placeholders in {changed} files")

    ps = os.path.join(folder, "ProjectSettings", "ProjectSettings.asset")
    with open(ps, encoding="utf-8") as f:
        s = f.read()
    s = re.sub(r"productGUID: [0-9a-f]+", "productGUID: " + uuid.uuid4().hex, s)
    s = re.sub(r"companyName: .*", "companyName: " + a.company, s)
    s = re.sub(r"productName: .*", "productName: " + a.name, s)
    s = s.replace("com.yourstudio.gamescaffold", bundle)
    with open(ps, "w", encoding="utf-8", newline="\n") as f:
        f.write(s)
    print(f"ProjectSettings: {a.name} by {a.company}, {bundle}")

    # the scaffold's own README is about the scaffold; the game gets a short one pointing at CLAUDE.md
    readme = os.path.join(folder, "README.md")
    with open(readme, "w", encoding="utf-8", newline="\n") as f:
        f.write(f"# {a.name}\n\nA Unity game made from the game scaffold. Start with [`CLAUDE.md`](./CLAUDE.md) and "
                f"[`Assets/docs/TASKS.md`](./Assets/docs/TASKS.md).\n\n"
                f"- Open the folder in Unity Hub (Unity 6000.6), run **{a.name} > Setup Project (once)**, press Play.\n"
                f"- Headless: `python tools/unity_batch.py setup`, `tests`, `build webgl`.\n"
                f"- To bring a prototype in from the idea test bed: `python tools/graduate.py <proto folder> {folder}`.\n")
    for name in ("LICENSE",):
        p = os.path.join(folder, name)
        if os.path.exists(p):
            os.remove(p)  # the scaffold's licence is the scaffold's; the game picks its own
    for name in ("docs",):
        p = os.path.join(folder, name)
        if os.path.isdir(p):
            shutil.rmtree(p)  # the method docs stay with the scaffold; the README links to them
    # the game's CLAUDE.md is the template; the scaffold's own is about the scaffold
    tpl = os.path.join(folder, "templates", "CLAUDE.md")
    if os.path.exists(tpl):
        shutil.move(tpl, os.path.join(folder, "CLAUDE.md"))
        shutil.rmtree(os.path.join(folder, "templates"))

    if not a.no_git:
        try:
            subprocess.check_call(["git", "init", "-q"], cwd=folder)
            subprocess.check_call(["git", "add", "-A"], cwd=folder)
            subprocess.check_call(["git", "commit", "-q", "-m", f"chore(phase0): {a.name} from the game scaffold"], cwd=folder)
            print("git: initialised with the first commit")
        except Exception as e:  # noqa: BLE001
            print("git skipped:", e)

    print(f"""
done: {folder}

next:
  1. Open the folder in Unity Hub (Unity 6000.6), or: python tools/unity_batch.py setup
  2. In the Editor: {a.name} > Setup Project (once)  (TMP essentials, sound bank, the Main scene), then Play.
  3. Bringing a prototype from the idea test bed? python tools/graduate.py <path to Proto_NN_Name> "{folder}"
  4. Read CLAUDE.md, then fill in Assets/docs/TASKS.md: "Where the game is", the bar, phase 1.
  5. Delete Assets/Game/Scripts/Sample/ (and its tests) once your own game manager is in; keep the Kit.
""")


if __name__ == "__main__":
    main()
