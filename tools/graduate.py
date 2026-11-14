#!/usr/bin/env python3
"""Graduate a prototype from the idea test bed into a game project made from the scaffold.

    python tools/graduate.py D:/Unity/IdeaTestBed/Assets/Prototypes/Proto_03_IsometricStealth D:/Unity/MyGame
    python tools/graduate.py <proto folder> <project> --row 1.03 --no-wrap
    python tools/graduate.py D:/Unity/MyGame/Assets/Prototypes/Proto_53_MyGame D:/Unity/MyGame --testbed D:/Unity/IdeaTestBed

What it does:
  1. Copies the prototype's Scripts/ (with each script's .meta, so its GUID is kept) into <project>/Assets/Game/Scripts/<ProtoName>/ and its Editor/ into
     <project>/Assets/Game/Editor/<ProtoName>/ (scenes are not copied: the builder regenerates them).
  2. Wraps every copied file that has no namespace in the project's namespace (from Game.asmdef), so it lands in the
     Game / Game.Editor assemblies beside the kit. (--no-wrap to skip.) The test bed's root namespace becomes the
     project's; a prototype named like the game (MobilePrototypes.MyGame in project MyGame) lands in MyGame, not
     MyGame.MyGame.
  3. Rewrites the test bed's scene-registration call to the scaffold's, and drops `using` lines for the test bed's
     launcher. Files that still read UnityEngine.Input are listed (the scaffold is Input System only), and so are
     files that read a device directly (Keyboard.current, Mouse.current, ...): every input is an action on GameInput,
     the pointer goes through TouchInputAdapter.
  4. Writes Assets/docs/initial/fun_test_record.md from the test bed's docs: the TASKS.md row for the prototype
     (--row, or guessed from the folder's NN), every PLAYTEST_NOTES.md line about it, and copies the GDD from
     docs/games/NN_*.md if there is one. That record is the "where this came from" every shipped game kept. A record
     the project already has is never replaced: what was found goes to fun_test_record.new.md beside it, and nothing
     is written when nothing was found. The test bed is the project the prototype folder sits in; --testbed names it
     when the prototype has already left (it was copied into its own project before that project had the scaffold).
  5. Copies the prototype's README.md (its rules, as played) to Assets/docs/rules.md, unless one is there.
  6. Prints the hand steps left (the builder's body into the game's scene part, the manager, the first commit).
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
    ap.add_argument("--testbed", help="the test bed project whose Assets/docs has the prototype's row and notes "
                                      "(default: the project the prototype folder is in)")
    a = ap.parse_args()

    proto = os.path.abspath(a.proto)
    project = os.path.abspath(a.project)
    proto_name = os.path.basename(proto)
    m = re.match(r"Proto_(\d+)_(\w+)", proto_name)
    nn = m.group(1) if m else None
    short = m.group(2) if m else proto_name
    home = os.path.dirname(os.path.dirname(os.path.dirname(proto)))  # .../Assets/Prototypes/Proto -> project root
    testbed = os.path.abspath(a.testbed) if a.testbed else home
    in_place = os.path.normcase(home) == os.path.normcase(project)  # the prototype already lives in the project
    if not os.path.isdir(proto):
        sys.exit(f"{proto} is not a folder")
    asmdef = os.path.join(project, "Assets", "Game", "Scripts", "Game.asmdef")
    if not os.path.exists(asmdef):
        sys.exit(f"{project} does not look like a scaffold project (no {asmdef})")
    ns = json.load(open(asmdef, encoding="utf-8")).get("rootNamespace", "GameScaffold")

    # The test bed's namespaces: its kit's (e.g. MobilePrototypes.Common) becomes the project's, and its root prefix
    # (MobilePrototypes.) becomes the project's, so prototype code lands in a child namespace that sees the kit.
    common_ns, testbed_root = None, None
    kit = os.path.join(os.path.dirname(proto), "Common", "FunTestKit.cs")
    if os.path.exists(kit):
        mm_ns = re.search(r"^\s*namespace\s+([\w.]+)", read(kit), re.M)
        if mm_ns:
            common_ns = mm_ns.group(1)
            testbed_root = common_ns.rsplit(".", 1)[0] if "." in common_ns else common_ns
    print(f"test bed namespace: {common_ns or '(none)'} -> {ns}")

    copied, wrapped, legacy_input, device_reads = [], [], [], []
    doubled = ns + "." + ns.split(".")[-1]  # MobilePrototypes.MyGame -> MyGame.MyGame when the prototype is named like the game
    for sub, dest_root, dest_ns in (("Scripts", "Scripts", ns), ("Editor", "Editor", ns + ".Editor")):
        src_dir = os.path.join(proto, sub)
        if not os.path.isdir(src_dir):
            continue
        for p in glob.glob(os.path.join(src_dir, "**", "*.cs"), recursive=True):
            rel = os.path.relpath(p, src_dir)
            dst = os.path.join(project, "Assets", "Game", dest_root, short, rel)
            s = read(p)
            s = s.replace("MasterPrototypesBuilder.RegisterScenesInBuildSettings();",
                          "FunTestKitBuilder.RegisterScene(ScenePath); // TODO: const string ScenePath = \"Assets/Game/Scenes/Main.unity\"")
            s = re.sub(r"^\s*using\s+\w*PrototypeLauncher\w*;\s*$", "", s, flags=re.M)
            if common_ns:
                s = s.replace(f"using {common_ns};", f"using {ns};")
                s = s.replace(common_ns + ".", ns + ".")  # a fully qualified kit type
                s = re.sub(r"\b" + re.escape(testbed_root) + r"\.", ns + ".", s)
                s = re.sub(r"^(\s*namespace\s+)" + re.escape(testbed_root) + r"\s*$", r"\g<1>" + ns, s, flags=re.M)
                s = re.sub(r"\b" + re.escape(doubled) + r"\b", ns, s)
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
            devices = sorted(set(re.findall(r"\b(Keyboard|Mouse|Touchscreen|Gamepad|Joystick|Pen|Pointer)\.current\b", s)))
            if devices and dest_root == "Scripts":
                device_reads.append(os.path.join(dest_root, short, rel) + "  (" + ", ".join(d + ".current" for d in devices) + ")")
            write(dst, s)
            if os.path.exists(p + ".meta") and not os.path.exists(dst + ".meta"):
                shutil.copy2(p + ".meta", dst + ".meta")  # the same GUID: a scene, prefab or asset that names the script still finds it
            copied.append(dst)
    print(f"copied {len(copied)} scripts ({len(wrapped)} wrapped in namespace {ns})")

    # the fun-test record from the test bed's docs
    docs = os.path.join(testbed, "Assets", "docs")
    row = a.row or (f"1.{nn}" if nn else None)
    record = [f"# {short} — fun-test record", "",
              f"Where this project came from: `{proto_name}` of the idea test bed (`{testbed}`), copied by tools/graduate.py. "
              "Reference only: `../TASKS.md` is the plan.", ""]
    found = []  # what the test bed's docs actually had about this prototype
    tasks = os.path.join(docs, "TASKS.md")
    if row and os.path.exists(tasks):
        t = read(tasks)
        for mm in re.finditer(rf"^### {re.escape(row)}\b.*?(?=^### |\Z)", t, re.M | re.S):
            record += ["## How the prototype was built (test bed TASKS.md)", "", mm.group(0).rstrip(), ""]
            found.append(f"the TASKS.md row {row}")
        if not found:
            record += [f"(No `### {row}` section found in the test bed's TASKS.md; copy it by hand.)", ""]
    notes = os.path.join(docs, "PLAYTEST_NOTES.md")
    if nn and os.path.exists(notes):
        lines = [l for l in read(notes).split("\n") if re.match(rf"^\|\s*{nn}\s*\|", l) or re.match(rf"^- \*\*{nn}\b", l)]
        if lines:
            record += ["## The owner's playtest notes (PLAYTEST_NOTES.md)", "", "| # | Prototype | Note | Read | Fixed after |", "|---|---|---|---|---|"]
            record += [l for l in lines if l.startswith("|")]
            record += [""] + [l for l in lines if l.startswith("-")] + [""]
            found.append(f"{len(lines)} playtest note(s)")
    initial = os.path.join(project, "Assets", "docs", "initial")
    record_path = os.path.join(initial, "fun_test_record.md")
    has_record = os.path.exists(record_path) and "copy it by hand" not in read(record_path) and len(read(record_path).strip().splitlines()) > 6
    where = f"{docs} (row {row})"
    if has_record and not found:
        print(f"fun-test record: kept the project's; nothing about this prototype in {where}"
              + ("" if a.testbed else ". If the test bed is another project, pass --testbed <its folder>"))
    elif has_record:
        write(os.path.join(initial, "fun_test_record.new.md"), "\n".join(record))
        print(f"fun-test record: kept the project's; the test bed had {' and '.join(found)}: written to fun_test_record.new.md, to merge by hand")
    else:
        write(record_path, "\n".join(record))
        if found:
            print(f"wrote Assets/docs/initial/fun_test_record.md ({' and '.join(found)})")
        else:
            print(f"wrote Assets/docs/initial/fun_test_record.md as a STUB: nothing about this prototype in {where}. "
                  "Fill it in by hand" + ("" if a.testbed else ", or pass --testbed <the test bed's folder> and run again"))
    gdd = glob.glob(os.path.join(docs, "games", f"{nn}_*.md")) if nn else []
    for g in gdd:
        if not os.path.exists(os.path.join(initial, os.path.basename(g))):
            os.makedirs(initial, exist_ok=True)
            shutil.copy2(g, os.path.join(initial, os.path.basename(g)))
            print(f"copied the GDD: Assets/docs/initial/{os.path.basename(g)}")

    # the prototype's README is the rules as played: the game keeps it current
    readme = os.path.join(proto, "README.md")
    rules = os.path.join(project, "Assets", "docs", "rules.md")
    if os.path.exists(readme):
        if os.path.exists(rules):
            print("Assets/docs/rules.md is already there: the prototype's README.md was not copied")
        else:
            os.makedirs(os.path.dirname(rules), exist_ok=True)
            shutil.copy2(readme, rules)
            print("copied the prototype's README.md -> Assets/docs/rules.md (the rules as played; keep it current)")

    stub = os.path.exists(os.path.join(project, "Assets", "Game", "Editor", "GameScene.cs"))  # made with --no-sample
    editor_names = sorted({os.path.splitext(os.path.basename(c))[0] for c in copied if os.sep + "Editor" + os.sep in c})
    print("\nleft to do by hand:")
    if stub:
        print("  - the scene: move the body of the prototype's builder into Editor/GameScene.cs (the one call SceneBuilder makes;")
        print("    SceneBuilder adds the kit and registers Scenes/Main.unity), then delete the copied builder")
        print("  - the manager: the prototype's MonoBehaviour takes GameManager's place (restart through FunTestKit.Restart(), the")
        print("    current level in Progress.Current.LastLevel, levels through Shell.Catalog); delete the stub files it replaces")
    else:
        print("  - the scene: move the body of the prototype's builder into the game's scene part (Editor/SampleScene.cs is the")
        print("    sample's: the one call SceneBuilder makes), then delete the copied builder")
        print("  - the manager: restart through FunTestKit.Restart(); the current level in Progress.Current.LastLevel")
        print("  - delete Assets/Game/Scripts/Sample, Editor/SampleScene.cs, Editor/SampleChecks.cs and Tests/SampleRulesTests.cs; keep the Kit")
    print("  - statics: Enter Play Mode has no domain reload here; every static field needs a")
    print("    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] reset")
    if editor_names:
        print(f"  - CLAUDE.md: name {', '.join(editor_names)} in the layout table while it exists (DocsTests fails on an editor script it does not name)")
    if legacy_input:
        print("  - Input System only: these still read UnityEngine.Input; move each to GameInput / TouchInputAdapter:")
        for f in legacy_input:
            print("      " + f)
    if device_reads:
        print("  - every input is an action: these read a device directly; move each to an action on GameInput (dev keys to DevMenu):")
        for f in device_reads:
            print("      " + f)
    if in_place:
        print(f"  - the prototype was copied, not moved: delete {os.path.relpath(os.path.dirname(proto), project)} (the prototype, the old kit, its")
        print("    scene) before compiling, or its menu items and scene sit beside the new ones")
    print("  - compile (python tools/unity_batch.py compile), build the scene, play a win and a lose, commit: chore(phase0): graduated from the test bed")


if __name__ == "__main__":
    main()
