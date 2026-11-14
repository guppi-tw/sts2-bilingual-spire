#!/bin/bash
set -euo pipefail

steam="$HOME/Library/Application Support/Steam/Steam.AppBundle/Steam/Contents/MacOS/steam_osx"
if [[ ! -x "$steam" ]]; then
  echo "Steam executable not found: $steam" >&2
  exit 1
fi
"$steam" -applaunch 2868840
