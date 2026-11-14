#!/bin/bash
set -euo pipefail

project_dir="$(cd "$(dirname "$0")/.." && pwd)"
cd "$project_dir"
dotnet build BilingualSpire.csproj -t:DeployMod "$@"
