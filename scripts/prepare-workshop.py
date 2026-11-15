#!/usr/bin/env python3
"""Prepare the official STS2 uploader workspace from a verified release ZIP."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("release_zip", type=Path)
parser.add_argument("--visibility", choices=["private", "public", "unlisted", "friends_only"], default="private")
parser.add_argument("--change-note", default="Initial Steam Workshop release")
options = parser.parse_args()
source = root / "workshop"
workspace = root / ".artifacts/workshop/BilingualSpire"
content = workspace / "content"
image = source / "image.png"
if not image.exists() or image.stat().st_size >= 1_000_000:
    parser.error("workshop/image.png must exist and be smaller than 1 MB")
if image.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
    parser.error("The preview must be a PNG")

checksum_file = options.release_zip.with_suffix(".zip.sha256")
expected = checksum_file.read_text().split()[0]
if hashlib.sha256(options.release_zip.read_bytes()).hexdigest() != expected:
    parser.error("Release ZIP checksum does not match")
names = ["BilingualSpire.dll", "BilingualSpire.json"]
with zipfile.ZipFile(options.release_zip) as archive:
    if sorted(archive.namelist()) != sorted("BilingualSpire/" + name for name in names):
        parser.error("Release ZIP must contain only the mod DLL and JSON")
    files = {name: archive.read("BilingualSpire/" + name) for name in names}
manifest = json.loads(files["BilingualSpire.json"])
if manifest["id"] != "BilingualSpire":
    parser.error("Wrong mod in release ZIP")
if content.exists() and any(p.name not in names or not p.is_file() for p in content.iterdir()):
    parser.error("Unexpected files in Workshop content; inspect them before uploading")
content.mkdir(parents=True, exist_ok=True)
item_id = (source / "item-id.txt").read_text().strip()
if not item_id.isdecimal():
    parser.error("Invalid Workshop item ID")
saved_id = workspace / "mod_id.txt"
if saved_id.exists() and saved_id.read_text().strip() != item_id:
    parser.error("Workspace belongs to a different Workshop item")
for name, data in files.items():
    (content / name).write_bytes(data)
shutil.copy2(image, workspace / "image.png")
previews = workspace / "previews"
previews.mkdir(exist_ok=True)
for preview in (source / "previews").iterdir():
    if preview.suffix.lower() not in {".png", ".jpg", ".jpeg"} or preview.stat().st_size >= 1_000_000:
        parser.error("Previews must be PNG/JPEG files smaller than 1 MB")
    shutil.copy2(preview, previews / preview.name)
metadata = json.loads((source / "metadata.json").read_text())
metadata.update(description=(source / "description.bbcode").read_text().replace("@MOD_VERSION@", manifest["version"].removeprefix("v")),
                visibility=options.visibility, changeNote=options.change_note)
(workspace / "workshop.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n")
saved_id.write_text(item_id + "\n")
print(f"Prepared {manifest['version']} ({options.visibility}): {workspace}")
print("Content: BilingualSpire.dll and BilingualSpire.json only; existing item ID preserved")
