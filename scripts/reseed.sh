#!/bin/sh

set -euo pipefail

# Usage:
#   scripts/reseed.sh                # random data
#   scripts/reseed.sh --seed 123     # deterministic data with seed 123

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$REPO_ROOT"

dotnet run --project src/PingPong.Api/PingPong.Api.csproj -- --seed-data --reseed "$@"


