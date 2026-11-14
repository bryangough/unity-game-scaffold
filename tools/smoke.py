#!/usr/bin/env python3
"""The smoke test of the whole scaffold: make fresh games from it and run each headless.

    python tools/smoke.py            # two games (the defaults, and --no-sample --landscape) and the adoption
    python tools/smoke.py --quick    # the default game only
    python tools/smoke.py --adopt    # the adoption only
    python tools/smoke.py --keep     # leave the copies in place (their paths are printed)

For each game: new_game.py into a temporary folder, sync_kit.py --check (a fresh game must match the scaffold: the
rename rules and the sync rules agree), then unity_batch.py setup, tests and levels inside the copy. Exit 1 when any
step fails. The adoption is the other way in: a small project that already exists (a prototype beside its old kit,
its own task list and fun-test record, in git) takes the scaffold with `new_game.py --into`, then `graduate.py` on the
prototype inside it; the project's plan, record and productGUID must come through untouched, the prototype must land
in the game's namespace (not doubled), and the result must set up and pass its tests. Each game takes a few minutes (a first import). Run it before handing off a change to the scaffold; the
scaffold itself does not need to be closed in the Editor, only the copies are opened.
"""
import os
import shutil
import subprocess
import sys
import tempfile
import time

TOOLS = os.path.dirname(os.path.abspath(__file__))
VARIANTS = [
    ("default", "Smoke Game", []),
    ("no-sample, landscape", "Smoke: \"Wide\" Game", ["--no-sample", "--landscape", "--company", "Test: Studio & Co"]),
]


def step(label, cmd, cwd=None):
    t0 = time.time()
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    ok = p.returncode == 0
    print(f"    {'ok  ' if ok else 'FAIL'} {label} ({time.time() - t0:.0f} s)")
    if not ok:
        tail = (p.stdout + p.stderr).strip().splitlines()[-25:]
        print("\n".join("      " + line for line in tail))
    return ok


PLAN = "# Smoke Game: the project's own plan\n"
RECORD = "# The project's own record\n\n" + "\n".join(f"- line {i}: what the owner said" for i in range(8)) + "\n"
GUID = "0123456789abcdef0123456789abcdef"
PROTO = """using MobilePrototypes.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MobilePrototypes.SmokeGame
{
    public class SmokeProto : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) FunTestKit.Restart();
        }
    }
}
"""
OLD_KIT = """namespace MobilePrototypes.Common
{
    public static class FunTestKit
    {
        public static void Restart() { }
    }
}
"""


def make_old_project(folder):
    """A project as a prototype leaves a test bed by hand: the prototype, the old kit, a plan, a record, in git."""
    files = {
        "Assets/Prototypes/Proto_07_SmokeGame/Scripts/SmokeProto.cs": PROTO,
        "Assets/Prototypes/Proto_07_SmokeGame/README.md": "# 07 Smoke Game\n\nThe rules as played.\n",
        "Assets/Prototypes/Common/FunTestKit.cs": OLD_KIT,
        "Assets/docs/TASKS.md": PLAN,
        "Assets/docs/initial/fun_test_record.md": RECORD,
        "ProjectSettings/ProjectSettings.asset": f"PlayerSettings:\n  productGUID: {GUID}\n  productName: Old\n",
        "Packages/manifest.json": '{\n  "dependencies": {\n    "com.unity.ugui": "2.6.0"\n  }\n}\n',
        ".gitignore": "/Library/\n",
    }
    for rel, text in files.items():
        path = os.path.join(folder, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    git = ["git", "-c", "user.name=smoke", "-c", "user.email=smoke@example.invalid"]
    for cmd in (["git", "init", "-q"], ["git", "add", "-A"], git + ["commit", "-q", "-m", "the project before the scaffold"]):
        subprocess.check_call(cmd, cwd=folder)


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def check_adopted(folder):
    """What --into and graduate.py must leave alone, and what they must have done. Returns the failures."""
    bad = []
    if read(os.path.join(folder, "Assets/docs/TASKS.md")) != PLAN:
        bad.append("the project's TASKS.md was changed")
    if read(os.path.join(folder, "Assets/docs/initial/fun_test_record.md")) != RECORD:
        bad.append("the project's fun-test record was changed")
    if f"productGUID: {GUID}" not in read(os.path.join(folder, "ProjectSettings/ProjectSettings.asset")):
        bad.append("the project's productGUID was not kept")
    if not os.path.exists(os.path.join(folder, ".scaffold.json")):
        bad.append("no .scaffold.json")
    manifest = read(os.path.join(folder, "Packages/manifest.json"))
    if "com.unity.inputsystem" not in manifest or '"com.unity.ugui": "2.6.0"' not in manifest:
        bad.append("the manifest was not merged (the scaffold's packages added, the project's version kept)")
    moved = os.path.join(folder, "Assets/Game/Scripts/SmokeGame/SmokeProto.cs")
    if not os.path.exists(moved):
        bad.append("the prototype's script was not copied into Assets/Game/Scripts")
    else:
        code = read(moved)
        if "namespace SmokeGame\n" not in code or "SmokeGame.SmokeGame" in code or "MobilePrototypes" in code:
            bad.append("the prototype did not land in namespace SmokeGame")
    if not os.path.exists(os.path.join(folder, "Assets/docs/rules.md")):
        bad.append("the prototype's README.md was not copied to Assets/docs/rules.md")
    return bad


def adoption(keep):
    folder = os.path.join(tempfile.mkdtemp(prefix="scaffold_smoke_"), "Game")
    print(f"\nadoption: {folder}")
    make_old_project(folder)
    proto = os.path.join(folder, "Assets", "Prototypes", "Proto_07_SmokeGame")
    batch = [sys.executable, os.path.join(folder, "tools", "unity_batch.py")]
    failed = None
    for what, cmd in (
        ("new_game --into", [sys.executable, os.path.join(TOOLS, "new_game.py"), "Smoke Game", "--into", folder, "--no-sample"]),
        ("sync_kit --check", [sys.executable, os.path.join(TOOLS, "sync_kit.py"), folder, "--check"]),
        ("graduate (in place)", [sys.executable, os.path.join(TOOLS, "graduate.py"), proto, folder]),
    ):
        if not step(what, cmd):
            failed = what
            break
    if not failed:
        bad = check_adopted(folder)
        print(f"    {'ok  ' if not bad else 'FAIL'} the project's own files kept, the prototype moved")
        for line in bad:
            print("      " + line)
        failed = "kept and moved" if bad else None
    if not failed:
        shutil.rmtree(os.path.join(folder, "Assets", "Prototypes"))  # graduate.py's first hand step
        for what in ("setup", "tests"):
            if not step(what, batch + [what]):
                failed = what
                break
    if keep:
        print(f"    kept: {folder}")
    else:
        shutil.rmtree(os.path.dirname(folder), ignore_errors=True)
    return failed


def main():
    quick, keep, only_adopt = "--quick" in sys.argv, "--keep" in sys.argv, "--adopt" in sys.argv
    failed = []
    for label, name, flags in [] if only_adopt else VARIANTS[:1] if quick else VARIANTS:
        folder = os.path.join(tempfile.mkdtemp(prefix="scaffold_smoke_"), "Game")
        print(f"\n{label}: {folder}")
        batch = [sys.executable, os.path.join(folder, "tools", "unity_batch.py")]
        steps = [
            ("new_game", [sys.executable, os.path.join(TOOLS, "new_game.py"), name, "--folder", folder, "--no-git"] + flags),
            ("sync_kit --check", [sys.executable, os.path.join(TOOLS, "sync_kit.py"), folder, "--check"]),
            ("setup", batch + ["setup"]),
            ("tests", batch + ["tests"]),
            ("levels", batch + ["levels"]),
        ]
        for what, cmd in steps:
            if not step(what, cmd):
                failed.append(f"{label}: {what}")
                break
        if keep:
            print(f"    kept: {folder}")
        else:
            shutil.rmtree(os.path.dirname(folder), ignore_errors=True)
    if not quick:
        what = adoption(keep)
        if what:
            failed.append(f"adoption: {what}")
    print("\nSMOKE OK" if not failed else "\nSMOKE FAILED: " + "; ".join(failed))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
