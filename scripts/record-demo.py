#!/usr/bin/env python3
"""Record real game rendering with automated input and isolated saves, then export a short MP4."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", type=Path, default=root / ".artifacts/demo/BilingualSpire-demo.mp4")
options = parser.parse_args()
game_dir = Path.home() / "Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS"
if subprocess.run(["pgrep", "-f", str(game_dir / "Slay the Spire 2")], capture_output=True).returncode == 0:
    sys.exit("Close Slay the Spire 2 before recording.")
for tool in ("ffmpeg", "ffprobe"):
    if not shutil.which(tool):
        sys.exit(f"{tool} is required.")
artifacts = root / ".artifacts"
artifacts.mkdir(exist_ok=True)
work = Path(tempfile.mkdtemp(prefix="demo-recording-", dir=artifacts))
manual_mod = game_dir / "mods/BilingualSpire"
backup = work / "original-manual-mod"
if manual_mod.exists():
    manual_mod.rename(backup)
movie = work / "game.avi"
print(f"Recording artifacts: {work}", flush=True)
try:
    subprocess.run([sys.executable, str(root / "scripts/smoke-test.py"), "--record-demo", str(movie)], cwd=root, check=True)
finally:
    if manual_mod.exists():
        manual_mod.rename(work / "recording-debug-mod")
    if backup.exists():
        backup.rename(manual_mod)

timing = json.loads(movie.with_suffix(".json").read_text())
start = timing["markers"]["start"] / timing["fps"]
duration = (timing["markers"]["end"] - timing["markers"]["start"]) / timing["fps"]
output = options.output.resolve()
output.parent.mkdir(parents=True, exist_ok=True)
subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "warning", "-y", "-ss", str(start), "-i", str(movie),
                "-t", str(duration), "-c:v", "libx264", "-crf", "19", "-preset", "medium", "-pix_fmt", "yuv420p",
                "-an", "-movflags", "+faststart", str(output)], check=True)
subprocess.run(["ffmpeg", "-v", "error", "-i", str(output), "-f", "null", "-"], check=True)
probe = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "stream=codec_name,width,height,r_frame_rate:format=duration,size",
                        "-of", "json", str(output)], check=True, capture_output=True, text=True)
print(probe.stdout)
print(f"PASS: recorded and decoded {output}")
