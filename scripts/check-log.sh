#!/bin/bash
set -euo pipefail

log_file="${1:-$HOME/Library/Application Support/SlayTheSpire2/logs/godot.log}"
if [[ ! -f "$log_file" ]]; then
  echo "Log not found: $log_file" >&2
  exit 1
fi

if ! grep -E 'BilingualSpire v[0-9.]+ loaded successfully' "$log_file"; then
  echo "BilingualSpire has not initialized in this log: $log_file" >&2
  exit 1
fi
