#!/usr/bin/env python3
"""Drive this project's Unity Editor headless: compile, set up, test, build.

    python tools/unity_batch.py compile            # import + compile, report `error CS` lines
    python tools/unity_batch.py setup              # first-time setup (TMP essentials, sound bank, main scene)
    python tools/unity_batch.py tests              # EditMode tests -> Builds/Checks/tests.xml, summary printed
    python tools/unity_batch.py levels             # the level check -> Builds/Checks/levels.md; exit 1 when a level fails
    python tools/unity_batch.py build webgl        # a player build through Editor/PhoneBuilds.cs (webgl|android|ios)
    python tools/unity_batch.py capture            # Play mode for a moment, a 720x1280 shot -> Builds/Checks/views/batch.png
    python tools/unity_batch.py capture Levels     # the same with one of the shell's screens up (Title, Levels, Play,
                                                   # Result, Pause, Settings, Confirm) -> batch_Levels.png

Why: an AI assistant (or CI) can verify a change without a human at the Editor. The Unity version comes from
ProjectSettings/ProjectVersion.txt and the Hub's default install folder; set UNITY_PATH to override. Logs land in
Logs/batch_<command>.log. The project must not be open in another Editor at the same time (Temp/UnityLockfile).
"""
import json
import os
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def unity_exe():
    env = os.environ.get("UNITY_PATH")
    if env and os.path.exists(env):
        return env
    with open(os.path.join(ROOT, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as f:
        version = re.search(r"m_EditorVersion:\s*(\S+)", f.read()).group(1)
    candidates = [
        rf"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe",
        f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity",
        os.path.expanduser(f"~/Unity/Hub/Editor/{version}/Editor/Unity"),
    ]
    for c in candidates:
        if os.path.exists(c):
            return c
    sys.exit(f"Unity {version} not found; set UNITY_PATH. Looked in: {candidates}")


def editor_namespace():
    """The Editor assembly's root namespace (new_game.py renames it), for -executeMethod."""
    path = os.path.join(ROOT, "Assets", "Game", "Editor", "Game.Editor.asmdef")
    with open(path, encoding="utf-8") as f:
        return json.load(f).get("rootNamespace", "GameScaffold.Editor")


def project_is_open():
    """True when a Unity process has this project open. A lockfile alone is not proof: a run that died leaves one."""
    lock = os.path.join(ROOT, "Temp", "UnityLockfile")
    if not os.path.exists(lock):
        return False
    try:
        if sys.platform == "win32":
            out = subprocess.run(["powershell", "-NoProfile", "-Command",
                                  "Get-CimInstance Win32_Process -Filter \"name='Unity.exe'\" | ForEach-Object { $_.CommandLine }"],
                                 capture_output=True, text=True, timeout=30).stdout
        else:
            out = subprocess.run(["ps", "-eo", "args"], capture_output=True, text=True, timeout=30).stdout
        mine = os.path.normcase(os.path.normpath(ROOT))
        for line in out.splitlines():
            m = re.search(r'-projectPath\s+"?([^"\s]+(?:\s[^"\s]+)*)"?', line, re.I)
            if m and os.path.normcase(os.path.normpath(m.group(1))) == mine:
                return True
    except Exception:  # noqa: BLE001
        return True  # cannot tell: be careful
    print("removing a stale Temp/UnityLockfile (no Unity has this project open)")
    os.remove(lock)
    return False


def run(args, log_name, timeout=3600, graphics=False):
    if project_is_open():
        sys.exit("The project is open in another Unity Editor (Temp/UnityLockfile). Close it, or use that Editor's menus.")
    os.makedirs(os.path.join(ROOT, "Logs"), exist_ok=True)
    log = os.path.join(ROOT, "Logs", f"batch_{log_name}.log")
    if os.path.exists(log):
        try:
            os.remove(log)
        except PermissionError:  # a crash handler can hold the last log for a while: write beside it
            log = os.path.join(ROOT, "Logs", f"batch_{log_name}_{time.strftime('%H%M%S')}.log")
    # -nographics is faster and needs no GPU, but a capture has to render, so it keeps the graphics device
    cmd = [unity_exe(), "-batchmode"] + ([] if graphics else ["-nographics"]) + ["-projectPath", ROOT, "-logFile", log] + args
    print("running:", " ".join(f'"{a}"' if " " in a else a for a in cmd[1:]))
    t0 = time.time()
    code = subprocess.call(cmd, timeout=timeout)
    print(f"unity exited {code} after {(time.time() - t0) / 60:.1f} min; log: {log}")
    errors = []
    if os.path.exists(log):
        with open(log, encoding="utf-8", errors="replace") as f:
            for line in f:
                if re.search(r"error CS\d+|\berror\b.*\.cs\(\d+", line) and "warning" not in line:
                    errors.append(line.rstrip())
    if errors:
        print(f"{len(errors)} compile error line(s):")
        for e in errors[:30]:
            print("  " + e)
    return code, log, errors


def cmd_compile():
    code, _, errors = run(["-quit"], "compile")
    print("COMPILE OK" if code == 0 and not errors else "COMPILE FAILED")
    return 0 if code == 0 and not errors else 1


def cmd_setup():
    code, _, errors = run(["-executeMethod", editor_namespace() + ".ProjectSetup.Batch"], "setup")
    scene = os.path.join(ROOT, "Assets", "Game", "Scenes", "Main.unity")
    tmp = os.path.join(ROOT, "Assets", "TextMesh Pro", "Resources", "TMP Settings.asset")
    print("main scene:", "built" if os.path.exists(scene) else "MISSING")
    print("TMP essentials:", "in" if os.path.exists(tmp) else "MISSING (run setup again, or Window > TextMeshPro > Import TMP Essential Resources)")
    ok = code == 0 and not errors and os.path.exists(scene)
    print("SETUP OK" if ok else "SETUP FAILED")
    return 0 if ok else 1


def cmd_tests():
    results = os.path.join(ROOT, "Builds", "Checks", "tests.xml")
    os.makedirs(os.path.dirname(results), exist_ok=True)
    if os.path.exists(results):
        os.remove(results)
    code, _, errors = run(["-runTests", "-testPlatform", "EditMode", "-testResults", results], "tests")
    if not os.path.exists(results):
        print("no results file (a compile error?)")
        return 1
    root = ET.parse(results).getroot()
    total, passed, failed = root.get("total"), root.get("passed"), root.get("failed")
    print(f"tests: {passed} passed, {failed} failed of {total}")
    for case in root.iter("test-case"):
        if case.get("result") != "Passed":
            msg = case.find(".//message")
            print(f"  FAIL {case.get('fullname')}: {(msg.text or '').strip()[:300] if msg is not None else ''}")
    return 0 if failed == "0" and code == 0 else 1


def cmd_levels():
    report = os.path.join(ROOT, "Builds", "Checks", "levels.md")
    if os.path.exists(report):
        os.remove(report)  # a stale report must not pass for a new one
    code, _, errors = run(["-executeMethod", editor_namespace() + ".LevelCheck.Batch"], "levels")
    if os.path.exists(report):
        with open(report, encoding="utf-8") as f:
            print(f.read())
    else:
        print("no report (a compile error, or no check registered: see LevelCheck.Game)")
    ok = code == 0 and not errors and os.path.exists(report)
    print("LEVELS OK" if ok else "LEVELS FAILED")
    return 0 if ok else 1


def cmd_build(target):
    targets = {"webgl": "WebGL", "android": "Android", "ios": "iOS"}
    if target not in targets:
        sys.exit("build target: webgl | android | ios")
    code, _, errors = run(["-buildTarget", targets[target], "-executeMethod", editor_namespace() + ".PhoneBuilds.Batch"], f"build_{target}", timeout=7200)
    log = os.path.join(ROOT, "Builds", "build_log.txt")
    if os.path.exists(log):
        with open(log, encoding="utf-8") as f:
            print("".join(f.readlines()[-4:]))
    return 0 if code == 0 and not errors else 1


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    what = sys.argv[1]
    if what == "compile":
        sys.exit(cmd_compile())
    if what == "setup":
        sys.exit(cmd_setup())
    if what == "tests":
        sys.exit(cmd_tests())
    if what == "levels":
        sys.exit(cmd_levels())
    if what == "build":
        sys.exit(cmd_build(sys.argv[2] if len(sys.argv) > 2 else ""))
    if what == "capture":
        screen = sys.argv[2] if len(sys.argv) > 2 else None
        shot = os.path.join(ROOT, "Builds", "Checks", "views", f"batch_{screen}.png" if screen else "batch.png")
        if os.path.exists(shot):
            os.remove(shot)  # a stale picture must not pass for a new one
        code, _, errors = run(["-executeMethod", editor_namespace() + ".BatchCapture.Batch"] + (["-shellScreen", screen] if screen else []),
                              "capture", graphics=True)
        print("capture:", shot if os.path.exists(shot) else "MISSING")
        sys.exit(0 if code == 0 and os.path.exists(shot) else 1)
    sys.exit(__doc__)
