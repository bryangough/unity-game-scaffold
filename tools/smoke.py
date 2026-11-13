#!/usr/bin/env python3
"""The smoke test of the whole scaffold: make fresh games from it and run each headless.

    python tools/smoke.py            # two games: the defaults, and --no-sample --landscape
    python tools/smoke.py --quick    # the default game only
    python tools/smoke.py --keep     # leave the copies in place (their paths are printed)

For each game: new_game.py into a temporary folder, sync_kit.py --check (a fresh game must match the scaffold: the
rename rules and the sync rules agree), then unity_batch.py setup, tests and levels inside the copy. Exit 1 when any
step fails. Each game takes a few minutes (a first import). Run it before handing off a change to the scaffold; the
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


def main():
    quick, keep = "--quick" in sys.argv, "--keep" in sys.argv
    failed = []
    for label, name, flags in VARIANTS[:1] if quick else VARIANTS:
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
    print("\nSMOKE OK" if not failed else "\nSMOKE FAILED: " + "; ".join(failed))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
