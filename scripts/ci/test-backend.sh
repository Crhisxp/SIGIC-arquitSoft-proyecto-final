#!/usr/bin/env bash
set -euo pipefail
sigic_repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$sigic_repo_root"
source scripts/ci/env.sh
# Do not combine reports from previous runs. Preserve old evidence elsewhere first.
if [[ -d TestResults ]] && [[ -n $(find TestResults -type f -print -quit) ]]; then
  echo 'TestResults is not empty; archive it before starting a new run.' >&2
  exit 1
fi
for layer in Domain Application Infrastructure WebApi; do
  dotnet test "tests/SIGIC.$layer.Tests/SIGIC.$layer.Tests.csproj" -c Release --no-build \
    --collect:"XPlat Code Coverage" --results-directory "./TestResults/$layer" \
    --logger "trx;LogFileName=$layer.trx"
done
python3 scripts/ci/verify-trx.py TestResults
python3 scripts/ci/coverage.py TestResults
