#!/usr/bin/env python3
"""Make a new game project from this scaffold.

    python tools/new_game.py "Cloak & Crate" --company "Your Studio" --bundle com.yourstudio.cloakandcrate
    python tools/new_game.py "Splat Swap" --folder D:/Unity/SplatSwap --namespace SplatSwap --no-git
    python tools/new_game.py "Slope Run" --landscape --no-sample

What it does, in order:
  1. Checks the names it was given (a namespace that is a real C# name and not one of the kit's classes, a bundle id
     the stores accept) before anything is copied.
  2. Copies the scaffold to the new folder (skipping Library, Temp, Logs, Builds, .git, generated project files).
  3. --no-sample: takes the sample game out and puts a small stub in its place (templates/game: rules, levels, a
     manager, a scene part, a level check and tests), so the project starts from the game's own files.
  4. Renames the C# namespace (GameScaffold -> <Namespace>) in every .cs and .asmdef, the editor menu root
     (Game/ -> <Name>/), and GameInfo (Id, Title, Credit). --landscape: turns the canvas, the camera fit, the web
     page and the Player Settings on their side (the Shell's screens lay themselves out for either).
  5. Sets ProjectSettings: productName, companyName, applicationIdentifier (every platform), a fresh productGUID.
  6. Fills the placeholders in CLAUDE.md, Assets/docs/TASKS.md and the Store templates (__GAME__, __OWNER__, ...);
     the method docs (docs/) go along, so the game's assistant can read the lessons.
  7. Writes .scaffold.json (what it was made with and from which scaffold commit; tools/sync_kit.py reads it to
     carry kit fixes between the game and the scaffold), then git init + first commit (unless --no-git).

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
SKIP_DIRS = {".git", "Library", "Temp", "Logs", "Builds", "obj", "UserSettings", ".utmp", "Data", "build", ".vs", "__pycache__"}
SKIP_SUFFIXES = (".csproj", ".sln", ".slnx", ".user", ".apk", ".aab")
TEXT_SUFFIXES = (".cs", ".asmdef", ".md", ".json", ".txt", ".jslib", ".mm", ".py", ".asset", ".example", ".html")
STAMP = ".scaffold.json"

IDENT = r"[A-Za-z_][A-Za-z0-9_]*"
# namespaces that would hide a type the code names without a qualifier
RESERVED_NAMESPACES = {"Game", "Editor", "Tests", "System", "Unity", "UnityEngine", "UnityEditor", "TMPro", "NUnit"}

# the sample's files; --no-sample removes them and templates/game takes their place
SAMPLE_PATHS = (
    "Assets/Game/Scripts/Sample", "Assets/Game/Editor/SampleScene.cs", "Assets/Game/Editor/SampleChecks.cs",
    "Assets/Game/Tests/SampleRulesTests.cs", "Assets/Game/Scenes/Main.unity",  # the scene is rebuilt by Setup
)


def pascal(name):
    parts = re.findall(r"[A-Za-z0-9]+", name)
    return "".join(p[:1].upper() + p[1:] for p in parts) or "Game"


def slug(name):
    return re.sub(r"[^a-z0-9]", "", name.lower()) or "game"


def cs_string(s):
    """The inside of a C# string literal."""
    return s.replace("\\", "\\\\").replace('"', '\\"')


def yaml_scalar(s):
    """A YAML value Unity reads back as the same string: plain when it is safe, single-quoted otherwise."""
    if re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9 ._-]*", s) and not s.endswith(" "):
        return s
    return "'" + s.replace("'", "''") + "'"


def kit_class_names():
    names = set()
    for sub in ("Assets/Game/Scripts", "Assets/Game/Editor", "templates/game"):
        for root, _, files in os.walk(os.path.join(SCAFFOLD, sub)):
            names.update(f[:-3] for f in files if f.endswith(".cs"))
    return names


def make_config(a):
    """Validates the arguments and returns everything the rewrite needs (also what .scaffold.json records)."""
    name = a.name.strip()
    if not name or "\n" in name:
        sys.exit("the game's name is empty")
    for label, value in (("--company", a.company), ("--owner", a.owner)):
        if not value.strip() or "\n" in value:
            sys.exit(f"{label} is empty")

    taken = kit_class_names() | RESERVED_NAMESPACES
    if a.namespace:
        ns = a.namespace
        if not re.fullmatch(rf"{IDENT}(\.{IDENT})*", ns):
            sys.exit(f"--namespace {ns!r} is not a C# namespace (letters, digits and _, not starting with a digit)")
        if ns.split(".")[0] in taken or ns.split(".")[-1] in taken:
            sys.exit(f"--namespace {ns!r} is also a class in the kit (or a Unity namespace): code inside it could not name that class. Try {ns}Game.")
    else:
        ns = pascal(name)
        if ns[0].isdigit():
            ns = "Game" + ns
        if ns in taken:
            print(f"note: the namespace {ns} would clash with a class in the kit; using {ns}Game (--namespace to choose)")
            ns += "Game"

    gid = slug(name)
    if gid[0].isdigit():
        gid = "game" + gid
    segment = r"[A-Za-z][A-Za-z0-9_]*"
    if a.bundle:
        bundle = a.bundle
        if not re.fullmatch(rf"{segment}(\.{segment})+", bundle):
            sys.exit(f"--bundle {bundle!r}: use two or more dot-separated parts, each starting with a letter (com.yourstudio.mygame)")
    else:
        studio = slug(a.company)
        if studio[0].isdigit():
            studio = "studio" + studio
        bundle = f"com.{studio}.{gid}"

    website = re.sub(r"^https?://", "", a.website.strip()).rstrip("/")
    if re.search(r'[\s"\\<>]', website):
        sys.exit(f"--website {a.website!r}: a bare address, e.g. yourstudio.com")

    return {
        "name": name, "namespace": ns, "id": gid, "company": a.company.strip(), "owner": a.owner.strip(),
        "bundle": bundle, "website": website, "landscape": bool(a.landscape), "sample": not a.no_sample,
    }


def code_pairs(c):
    ns, name = c["namespace"], c["name"]
    menu = name.replace('"', "").replace("\\", "").replace("/", "-")  # a slash would start a submenu
    return [
        ("namespace GameScaffold.Editor", f"namespace {ns}.Editor"),
        ("namespace GameScaffold.Tests", f"namespace {ns}.Tests"),
        ("namespace GameScaffold", f"namespace {ns}"),
        ("using GameScaffold.Editor;", f"using {ns}.Editor;"),
        ("using GameScaffold;", f"using {ns};"),
        ("GameScaffold.Editor.", f"{ns}.Editor."),
        ('"rootNamespace": "GameScaffold.Editor"', f'"rootNamespace": "{ns}.Editor"'),
        ('"rootNamespace": "GameScaffold.Tests"', f'"rootNamespace": "{ns}.Tests"'),
        ('"rootNamespace": "GameScaffold"', f'"rootNamespace": "{ns}"'),
        ('public const string Root = "Game/";', f'public const string Root = "{menu}/";'),
        ('public const string Id = "gamescaffold";', f'public const string Id = "{c["id"]}";'),
        ('public const string Title = "Game Scaffold";', f'public const string Title = "{cs_string(name)}";'),
        ('public const string Credit = "A game by Your Studio";', f'public const string Credit = "A game by {cs_string(c["company"])}";'),
        ('public const string Website = "";', f'public const string Website = "{c["website"]}";'),
    ]


def doc_pairs(c, folder="", today=""):
    return [
        ("__GAME__", c["name"]), ("__NAMESPACE__", c["namespace"]), ("__OWNER__", c["owner"]), ("__COMPANY__", c["company"]),
        ("__BUNDLE__", c["bundle"]), ("__DATE__", today), ("__FOLDER__", folder.replace("\\", "/")),
    ]


# --landscape: the canvas, the camera fit and the web page on their side. The Shell, the captures and AspectMatch
# read PrototypeUIHelper.ReferenceResolution, so these few lines are the whole switch.
LANDSCAPE_PAIRS = [
    ("public static readonly Vector2 ReferenceResolution = new Vector2(720f, 1280f);",
     "public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);"),
    ("public static float MatchWidthOrHeight = 0f; // width: the sides never clip",
     "public static float MatchWidthOrHeight = 1f; // height: a landscape game gains width at the sides"),
    ("public static float DesignAspect = 9f / 16f;", "public static float DesignAspect = 16f / 9f;"),
    ("--aspect: 9 / 16;", "--aspect: 16 / 9;"),
    ("- Target: **mobile, touch-first, portrait**: canvas 720 × 1280 matched on width (`PrototypeUIHelper`), camera fit\n"
     "  9:16 (`FunTestKit.DesignAspect`), portrait-only Player Settings.",
     "- Target: **mobile, touch-first, landscape**: canvas 1280 × 720 matched on height (`PrototypeUIHelper`), camera fit\n"
     "  16:9 (`FunTestKit.DesignAspect`), landscape-only Player Settings; the `Shell` lays its screens out wide."),
    ("- [ ] **0.4 Portrait.** 720 × 1280 canvas matched on width, 9:16 camera fit, portrait-only Player Settings; the",
     "- [ ] **0.4 Landscape.** 1280 × 720 canvas matched on height, 16:9 camera fit, landscape-only Player Settings (the\n"
     "  aspects below are then on their side: 21.5:9 and 4:3); the"),
]
LANDSCAPE_SETTINGS = [
    ("defaultScreenOrientation: 0", "defaultScreenOrientation: 4"),  # auto-rotation, between the two landscapes
    ("allowedAutorotateToPortrait: 1", "allowedAutorotateToPortrait: 0"),
    ("allowedAutorotateToLandscapeRight: 0", "allowedAutorotateToLandscapeRight: 1"),
    ("allowedAutorotateToLandscapeLeft: 0", "allowedAutorotateToLandscapeLeft: 1"),
    ("defaultScreenWidthWeb: 450", "defaultScreenWidthWeb: 800"),
    ("defaultScreenHeightWeb: 800", "defaultScreenHeightWeb: 450"),
]

# --no-sample: the names the generic files use for the game's own parts
NO_SAMPLE_PAIRS = [
    ("`SampleScene` adds the game's HUD and manager: the sample's until the game's own replaces it", "`GameScene` adds the game's HUD and manager"),
    ("`SampleChecks`: the sample's registration of both, the pattern to copy and then delete", "`GameChecks`: the game's registration of both"),
    ("- [ ] **7.1** Delete the sample game, its tests and `Editor/SampleChecks.cs` once the game's own manager and check are in.",
     "- [ ] **7.1** *(the sample game was left out when the project was made: nothing to delete.)*"),
    ("SampleScene", "GameScene"), ("SampleChecks", "GameChecks"), ("SampleColourTests", "GameColourTests"),
]


def text_pairs(c, folder="", today=""):
    """Every text rewrite a game made with config c gets, in order (tools/sync_kit.py applies the same to compare)."""
    pairs = []
    if not c.get("sample", True):
        pairs += NO_SAMPLE_PAIRS
    if c.get("landscape"):
        pairs += LANDSCAPE_PAIRS
    return pairs + code_pairs(c) + doc_pairs(c, folder, today)


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


def remove(path):
    for p in (path, path + ".meta"):
        if os.path.isdir(p):
            shutil.rmtree(p)
        elif os.path.exists(p):
            os.remove(p)


def strip_sample(folder):
    """The sample game out, the stub from templates/game in (its .meta files are made by Unity on first open)."""
    for rel in SAMPLE_PATHS:
        remove(os.path.join(folder, rel))
    stub = os.path.join(folder, "templates", "game")
    for root, _, files in os.walk(stub):
        out = os.path.join(folder, "Assets", "Game", os.path.relpath(root, stub))
        os.makedirs(out, exist_ok=True)
        for f in files:
            shutil.copy2(os.path.join(root, f), os.path.join(out, f))


def scaffold_commit():
    try:
        head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=SCAFFOLD, text=True, stderr=subprocess.DEVNULL).strip()
        dirty = subprocess.check_output(["git", "status", "--porcelain"], cwd=SCAFFOLD, text=True, stderr=subprocess.DEVNULL).strip()
        return head, bool(dirty)
    except Exception:  # noqa: BLE001
        return None, False


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("name", help='the game\'s name as shown to people, e.g. "Cloak & Crate"')
    ap.add_argument("--folder", help="where to make it (default: a sibling of the scaffold named after the game)")
    ap.add_argument("--namespace", help="C# namespace and asmdef root (default: the name in PascalCase)")
    ap.add_argument("--company", default="Your Studio", help="companyName in Player Settings and the credits line")
    ap.add_argument("--bundle", help="application identifier, e.g. com.yourstudio.cloakandcrate")
    ap.add_argument("--owner", default="the owner", help="who decides (used in TASKS.md / CLAUDE.md placeholders)")
    ap.add_argument("--website", default="", help="the link on the credits card (may be empty)")
    ap.add_argument("--landscape", action="store_true", help="a landscape game: 1280 x 720 canvas, 16:9 camera fit, landscape Player Settings")
    ap.add_argument("--no-sample", action="store_true", help="leave the sample game out; start from a small stub (rules, levels, manager, check, tests)")
    ap.add_argument("--no-git", action="store_true", help="skip git init and the first commit")
    a = ap.parse_args()

    c = make_config(a)
    folder = os.path.abspath(a.folder or os.path.join(os.path.dirname(SCAFFOLD), pascal(c["name"])))
    if os.path.exists(folder) and os.listdir(folder):
        sys.exit(f"{folder} exists and is not empty; pick another --folder")

    print(f"copying scaffold -> {folder}")
    copy_tree(SCAFFOLD, folder)
    if not c["sample"]:
        strip_sample(folder)
        print("sample game left out; the stub is in Assets/Game (Scripts/GameRules.cs, GameLevels.cs, GameManager.cs, ...)")

    today = datetime.date.today().isoformat()
    pairs = text_pairs(c, folder, today)
    changed = 0
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for f in files:
            if not f.endswith(TEXT_SUFFIXES):
                continue
            if f.endswith(".asset") or f == "new_game.py":
                continue  # Unity assets are touched below, by hand; this file holds the patterns themselves
            if rewrite(os.path.join(root, f), pairs):
                changed += 1
    print(f"renamed namespace/menu/placeholders in {changed} files")

    ps = os.path.join(folder, "ProjectSettings", "ProjectSettings.asset")
    with open(ps, encoding="utf-8") as f:
        s = f.read()
    s = re.sub(r"productGUID: [0-9a-f]+", "productGUID: " + uuid.uuid4().hex, s)
    s = re.sub(r"companyName: .*", lambda m: "companyName: " + yaml_scalar(c["company"]), s)
    s = re.sub(r"productName: .*", lambda m: "productName: " + yaml_scalar(c["name"]), s)
    s = s.replace("com.yourstudio.gamescaffold", c["bundle"])
    if c["landscape"]:
        for old, new in LANDSCAPE_SETTINGS:
            if old not in s:
                sys.exit(f"ProjectSettings.asset has no '{old}': --landscape needs updating for this Unity version")
            s = s.replace(old, new)
    with open(ps, "w", encoding="utf-8", newline="\n") as f:
        f.write(s)
    print(f"ProjectSettings: {c['name']} by {c['company']}, {c['bundle']}{', landscape' if c['landscape'] else ''}")

    # the scaffold's own README is about the scaffold; the game gets a short one pointing at CLAUDE.md
    readme = os.path.join(folder, "README.md")
    with open(readme, "w", encoding="utf-8", newline="\n") as f:
        f.write(f"# {c['name']}\n\nA Unity game made from the game scaffold. Start with [`CLAUDE.md`](./CLAUDE.md) and "
                f"[`Assets/docs/TASKS.md`](./Assets/docs/TASKS.md).\n\n"
                f"- Open the folder in Unity Hub (Unity 6000.6), run **{c['name']} > Setup Project (once)**, press Play.\n"
                f"- Headless: `python tools/unity_batch.py setup`, `tests`, `levels`, `build webgl`.\n"
                f"- How the work is done: [`docs/method.md`](./docs/method.md), [`docs/tasks_guide.md`](./docs/tasks_guide.md), "
                f"and the traps already paid for in [`docs/lessons.md`](./docs/lessons.md).\n"
                f"- To bring a prototype in from the idea test bed: `python tools/graduate.py <proto folder> {folder}`.\n"
                f"- Kit fixes travel both ways: `python tools/sync_kit.py {folder}`, run from the scaffold.\n")
    os.remove(os.path.join(folder, "LICENSE")) if os.path.exists(os.path.join(folder, "LICENSE")) else None  # the game picks its own
    # docs/ (the method, the task guide, the lessons) goes with the game: its CLAUDE.md sends the assistant there
    # the game's CLAUDE.md is the template; the scaffold's own is about the scaffold
    tpl = os.path.join(folder, "templates", "CLAUDE.md")
    if os.path.exists(tpl):
        shutil.move(tpl, os.path.join(folder, "CLAUDE.md"))
    shutil.rmtree(os.path.join(folder, "templates"), ignore_errors=True)
    # the tools that only make sense in the scaffold stay there
    for tool in ("new_game.py", "sync_kit.py", "smoke.py"):
        remove(os.path.join(folder, "tools", tool))

    head, dirty = scaffold_commit()
    stamp = dict(c, scaffold_commit=head, scaffold_dirty=dirty, made=today)
    with open(os.path.join(folder, STAMP), "w", encoding="utf-8", newline="\n") as f:
        json.dump(stamp, f, indent=2)
        f.write("\n")

    if not a.no_git:
        try:
            subprocess.check_call(["git", "init", "-q"], cwd=folder)
            subprocess.check_call(["git", "add", "-A"], cwd=folder)
            subprocess.check_call(["git", "commit", "-q", "-m", f"chore(phase0): {c['name']} from the game scaffold"], cwd=folder)
            print("git: initialised with the first commit")
        except Exception as e:  # noqa: BLE001
            print("git skipped:", e)

    if c["sample"]:
        step5 = """  5. Once your own game manager is in, delete Assets/Game/Scripts/Sample/ and Tests/SampleRulesTests.cs; keep the Kit.
     Two editor files are the sample's and yours to replace: Editor/SampleScene.cs (your HUD and manager, called
     from SceneBuilder) and Editor/SampleChecks.cs (your level check and replay). Or start over with --no-sample.
     Then fill in StoreCaptures.Shots with your store poses."""
    else:
        step5 = """  5. The game's own files are a stub to grow: Scripts/GameRules.cs (the rules), GameLevels.cs (the levels as data),
     GameManager.cs (presents them), Editor/GameScene.cs (its part of the scene), Editor/GameChecks.cs (the level
     check and replay), Tests/GameRulesTests.cs. Commit the .meta files Unity makes for them on first open.
     Then fill in StoreCaptures.Shots with your store poses."""
    print(f"""
done: {folder}

next:
  1. Open the folder in Unity Hub (Unity 6000.6), or: python tools/unity_batch.py setup
  2. In the Editor: {c['name']} > Setup Project (once)  (TMP essentials, sound bank, the Main scene), then Play.
  3. Bringing a prototype from the idea test bed? python tools/graduate.py <path to Proto_NN_Name> "{folder}"
  4. Read CLAUDE.md and docs/lessons.md, then fill in Assets/docs/TASKS.md: "Where the game is", the bar, phase 1.
{step5}
""")


if __name__ == "__main__":
    main()
