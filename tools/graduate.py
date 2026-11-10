#!/usr/bin/env python3
"""Graduate a prototype from the idea test bed into a game project made from the scaffold.

    python tools/graduate.py D:/Unity/AiGameIdeas/Assets/Prototypes/Proto_03_IsometricStealth D:/Unity/CloakAndCrate
    python tools/graduate.py <proto folder> <project> --row 1.03 --no-wrap

What it does:
  1. Copies the prototype's Scripts/ into <project>/Assets/_mine/Game/Scripts/<ProtoName>/ and its Editor/ into
     <project>/Assets/_mine/Game/Editor/<ProtoName>/ (scenes are not copied: the builder regenerates them).
  2. Wraps every copied file that has no namespace in the project's namespace (from Game.asmdef), so it lands in the
     Game / Game.Editor assemblies beside the kit. (--no-wrap to skip.)
  3. Rewrites the test bed's scene-registration call to the scaffold's, and drops `using` lines for the test bed's
     launcher. Files that still read UnityEngine.Input are listed: the scaffold is Input System only, so each goes
     through GameInput / TouchInputAdapter next.
  4. Writes Assets/docs/initial/fun_test_record.md from the test bed's docs: the TASKS.md row for the prototype
     (--row, or guessed from the folder's NN), every PLAYTEST_NOTES.md line about it, and copies the GDD from
     docs/games/NN_*.md if there is one. That record is the "where this came from" every shipped game kept.
  5. Prints the hand steps left (the builder's menu path, the manager's restart call, the first commit).
"""
import argparse
import glob
import json
import os
import re
import shutil
import sys


def read(p):
    with open(p, encoding="utf-8", errors="replace") as f:
        return f.read()


def write(p, s):
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write(s)


def wrap_namespace(src, ns):
    if re.search(r"^\s*namespace\s+\w", src, re.M):
        return src, False
    lines = src.split("\n")
    # the namespace opens after the last top-of-file using (or preprocessor line that precedes code)
    last_using = -1
    for i, l in enumerate(lines):
        t = l.strip()
        if t.startswith("using ") and t.endswith(";"):
            last_using = i
        elif t and not t.startswith("//") and not t.startswith("#") and not t.startswith("/*") and not t.startswith("*"):
            break
    head = lines[: last_using + 1]
    body = lines[last_using + 1 :]
    out = head + ["", f"namespace {ns}", "{"] + ["    " + l if l.strip() else l for l in body] + ["}", ""]
    return "\n".join(out), True


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("proto", help="the prototype folder in the test bed (Assets/Prototypes/Proto_NN_Name)")
    ap.add_argument("project", help="the game project made by new_game.py")
    ap.add_argument("--row", help="the prototype's TASKS.md row id in the test bed, e.g. 1.03 (default: from the folder's NN)")
    ap.add_argument("--no-wrap", action="store_true", help="do not wrap files in the project's namespace")
    a = ap.parse_args()

    proto = os.path.abspath(a.proto)
    project = os.path.abspath(a.project)
    proto_name = os.path.basename(proto)
    m = re.match(r"Proto_(\d+)_(\w+)", proto_name)
    nn = m.group(1) if m else None
    short = m.group(2) if m else proto_name
    testbed = os.path.dirname(os.path.dirname(os.path.dirname(proto)))  # .../Assets/Prototypes/Proto -> project root
    asmdef = os.path.join(project, "Assets", "_mine", "Game", "Scripts", "Game.asmdef")
    if not os.path.exists(asmdef):
        sys.exit(f"{project} does not look like a scaffold project (no {asmdef})")
    ns = json.load(open(asmdef, encoding="utf-8")).get("rootNamespace", "GameScaffold")

    # The test bed's namespaces: its kit's (e.g. MobilePrototypes.Common) becomes the project's, and its root prefix
    # (MobilePrototypes.) becomes the project's, so prototype code lands in a child namespace that sees the kit.
    common_ns, testbed_root = None, None
    kit = os.path.join(testbed, "Assets", "Prototypes", "Common", "FunTestKit.cs")
    if os.path.exists(kit):
        mm_ns = re.search(r"^\s*namespace\s+([\w.]+)", read(kit), re.M)
        if mm_ns:
            common_ns = mm_ns.group(1)
            testbed_root = common_ns.rsplit(".", 1)[0] if "." in common_ns else common_ns
    print(f"test bed namespace: {common_ns or '(none)'} -> {ns}")

    copied, wrapped, legacy_input = [], [], []
    for sub, dest_root, dest_ns in (("Scripts", "Scripts", ns), ("Editor", "Editor", ns + ".Editor")):
        src_dir = os.path.join(proto, sub)
        if not os.path.isdir(src_dir):
            continue
        for p in glob.glob(os.path.join(src_dir, "**", "*.cs"), recursive=True):
            rel = os.path.relpath(p, src_dir)
            dst = os.path.join(project, "Assets", "_mine", "Game", dest_root, short, rel)
            s = read(p)
            s = s.replace("MasterPrototypesBuilder.RegisterScenesInBuildSettings();",
                          "FunTestKitBuilder.RegisterScene(ScenePath); // TODO: const string ScenePath = \"Assets/_mine/Game/Scenes/Main.unity\"")
            s = re.sub(r"^\s*using\s+\w*PrototypeLauncher\w*;\s*$", "", s, flags=re.M)
            if common_ns:
                s = s.replace(f"using {common_ns};", f"using {ns};")
                s = s.replace(common_ns + ".", ns + ".")  # a fully qualified kit type
                s = re.sub(r"\b" + re.escape(testbed_root) + r"\.", ns + ".", s)
                s = re.sub(r"^(\s*namespace\s+)" + re.escape(testbed_root) + r"\s*$", r"\g<1>" + ns, s, flags=re.M)
            if dest_root == "Editor" and f"using {ns};" not in s:
                s = f"using {ns};\n" + s
            # one `using` of the project's namespace is enough
            seen, kept = False, []
            for l in s.split("\n"):
                if l.strip() == f"using {ns};":
                    if seen:
                        continue
                    seen = True
                kept.append(l)
            s = "\n".join(kept)
            if not a.no_wrap:
                s, did = wrap_namespace(s, dest_ns)
                if did:
                    wrapped.append(rel)
            if re.search(r"\bInput\.(GetKey|GetMouse|mousePosition|touch|GetAxis|GetButton)", s):
                legacy_input.append(os.path.join(dest_root, short, rel))
            write(dst, s)
            copied.append(dst)
    print(f"copied {len(copied)} scripts ({len(wrapped)} wrapped in namespace {ns})")

    # the fun-test record from the test bed's docs
    docs = os.path.join(testbed, "Assets", "docs")
    row = a.row or (f"1.{nn}" if nn else None)
    record = [f"# {short} — fun-test record", "",
              f"Where this project came from: `{proto_name}` of the idea test bed (`{testbed}`), copied by tools/graduate.py. "
              "Reference only: `../TASKS.md` is the plan.", ""]
    tasks = os.path.join(docs, "TASKS.md")
    if row and os.path.exists(tasks):
        t = read(tasks)
        found = False
        for mm in re.finditer(rf"^### {re.escape(row)}\b.*?(?=^### |\Z)", t, re.M | re.S):
            record += ["## How the prototype was built (test bed TASKS.md)", "", mm.group(0).rstrip(), ""]
            found = True
        if not found:
            record += [f"(No `### {row}` section found in the test bed's TASKS.md; copy it by hand.)", ""]
    notes = os.path.join(docs, "PLAYTEST_NOTES.md")
    if nn and os.path.exists(notes):
        lines = [l for l in read(notes).split("\n") if re.match(rf"^\|\s*{nn}\s*\|", l) or re.match(rf"^- \*\*{nn}\b", l)]
        if lines:
            record += ["## The owner's playtest notes (PLAYTEST_NOTES.md)", "", "| # | Prototype | Note | Read | Fixed after |", "|---|---|---|---|---|"]
            record += [l for l in lines if l.startswith("|")]
            record += [""] + [l for l in lines if l.startswith("-")] + [""]
    write(os.path.join(project, "Assets", "docs", "initial", "fun_test_record.md"), "\n".join(record))
    gdd = glob.glob(os.path.join(docs, "games", f"{nn}_*.md")) if nn else []
    for g in gdd:
        shutil.copy2(g, os.path.join(project, "Assets", "docs", "initial", os.path.basename(g)))
    print(f"wrote Assets/docs/initial/fun_test_record.md{' and copied the GDD' if gdd else ''}")

    print("\nleft to do by hand:")
    print(f"  - the scene builder: a [MenuItem(Menus.Root + \"Build Main Scene\")] that calls FunTestKitBuilder.AddKit() and RegisterScene; retire the sample's SceneBuilder.cs")
    print("  - the game manager: restart through FunTestKit.Restart(); the current level in Progress.Current.LastLevel")
    if legacy_input:
        print("  - Input System only: these still read UnityEngine.Input; move each to GameInput / TouchInputAdapter:")
        for f in legacy_input:
            print("      " + f)
    print("  - delete Assets/_mine/Game/Scripts/Sample and Tests/SampleRulesTests.cs; keep the Kit")
    print("  - compile (python tools/unity_batch.py compile), build the scene, play a win and a lose, commit: chore(phase0): graduated from the test bed")


if __name__ == "__main__":
    main()
