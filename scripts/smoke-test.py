#!/usr/bin/env python3
"""Run the installed Steam game without a window and check the mod's integration tests."""
import os
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--visual", action="store_true", help="Capture menus, cards and event layouts in an isolated test game")
options = parser.parse_args()
game = Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/Slay the Spire 2"
if subprocess.run(["pgrep", "-f", str(game)], capture_output=True).returncode == 0:
    sys.exit("Close Slay the Spire 2 before running this test.")

subprocess.run([str(root / "scripts/deploy.sh")], cwd=root, check=True)
artifacts = root / ".artifacts"
artifacts.mkdir(exist_ok=True)
data_root = Path.home() / "Library/Application Support/SlayTheSpire2"
default_dir = data_root / "default"
marker = default_dir / ".bilingual-spire-test"
if marker.exists():
    sys.exit(f"A previous isolated test was interrupted. Restore the backup recorded in {marker} first.")

def hashes(directory):
    return {str(p.relative_to(directory)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in directory.rglob("*") if p.is_file()} if directory.exists() else {}

steam_before = hashes(data_root / "steam")
default_before = hashes(default_dir)
test_dir = Path(tempfile.mkdtemp(prefix="isolated-test-", dir=artifacts))
saved_default = test_dir / "original-default"
if default_dir.exists():
    default_dir.rename(saved_default)

try:
    settings_dir = default_dir / "1"
    settings_dir.mkdir(parents=True)
    marker.write_text(str(saved_default))
    settings = {
        "schema_version": 8, "language": "jpn", "seen_ea_disclaimer": True,
        "skip_intro_logo": True, "mod_settings": {"mods_enabled": True},
    }
    (settings_dir / "settings.save").write_text(json.dumps(settings))
    env = dict(os.environ, BILINGUAL_SPIRE_SMOKE_TEST="1",
               BILINGUAL_SPIRE_CONFIG_PATH=str(test_dir / "language-settings.json"))
    env.pop("SteamAppId", None)
    env.pop("SteamGameId", None)
    selected_phase = os.environ.get("BILINGUAL_SPIRE_TEST_PHASE", "full")
    phases = ["full", "neow", "readability", "resume-bilingual", "resume-first", "resume-second"] if selected_phase == "full" else [selected_phase]
    for index, phase in enumerate(phases):
        name = "bilingual-smoke" if index == 0 else phase
        log_file = artifacts / f"{name}.log"
        log_file.unlink(missing_ok=True)
        phase_env = dict(env, BILINGUAL_SPIRE_TEST_PHASE=phase)
        phase_env.pop("BILINGUAL_SPIRE_SCREENSHOT", None)
        if phase.startswith("resume-"):
            settings["language"] = "fra"
            (settings_dir / "settings.save").write_text(json.dumps(settings))
        args = [str(game), "--force-steam", "off", "--max-fps", "60",
                "--quit-after", "3600", "--log-file", str(log_file)]
        if options.visual and (index == 0 or phase in ("neow", "readability")):
            args += ["--windowed", "--resolution", "1280x720"]
            phase_env["BILINGUAL_SPIRE_SCREENSHOT"] = str(artifacts / "language-settings.png")
            settings["fullscreen"] = False
            (settings_dir / "settings.save").write_text(json.dumps(settings))
        else:
            args += ["--headless"]
        with (artifacts / f"{name}-console.log").open("w") as console:
            result = subprocess.run(args, cwd=game.parent, env=phase_env, stdout=console,
                                    stderr=subprocess.STDOUT, timeout=60)
        text = log_file.read_text(errors="replace") if log_file.exists() else ""
        print(f"{phase}: game exit code {result.returncode}", flush=True)
        for line in text.splitlines():
            if any(marker in line for marker in ("SMOKE TEST", "STORY PASS", "PARAGRAPH PASS", "GAME LANGUAGE PASS", "NEOW PROGRESSION PASS", "MAP LEGEND PASS", "RELIC TITLE PASS", "READABILITY PASS")):
                print(line, flush=True)
        if (result.returncode != 0 or "SMOKE TEST PASS:" not in text or "[ERROR]" in text or "ERROR: System." in text
                or "Steam initialization skipped" not in text or "Steamworks initialization succeeded" in text):
            raise RuntimeError(f"FAIL {phase}: see {log_file}")
finally:
    if default_dir.exists():
        default_dir.rename(test_dir / "test-default")
    if saved_default.exists():
        saved_default.rename(default_dir)
    if hashes(default_dir) != default_before:
        raise RuntimeError("Default save backup was not restored exactly.")
    if hashes(data_root / "steam") != steam_before:
        raise RuntimeError("Steam save data changed during the isolated test.")

print("PASS: all requested game processes completed successfully.")
print("PASS: Steam disabled; Steam saves unchanged; previous default saves restored exactly.")
