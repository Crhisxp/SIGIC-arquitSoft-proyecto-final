#!/usr/bin/env bash
set -euo pipefail
sigic_repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$sigic_repo_root"
source scripts/ci/env.sh
if [[ $# -gt 1 || ($# -eq 1 && $1 != --html) ]]; then
  echo 'Usage: test-backend.sh [--html]' >&2
  exit 1
fi
sigic_reportgenerator=${REPORTGENERATOR:-reportgenerator}
if [[ $# -eq 1 ]] && ! command -v "$sigic_reportgenerator" >/dev/null; then
  echo 'Install ReportGenerator or set REPORTGENERATOR to its executable path.' >&2
  exit 1
fi
# Start with clean evidence and preserve previous artifacts, including diagnostics.
if [[ -e TestResults || -L TestResults ]]; then
  sigic_archive=$(mktemp -d "${TMPDIR:-/tmp}/sigic-testresults.XXXXXXXX")
  mv -- TestResults "$sigic_archive/"
  printf 'Previous evidence archived in %s/TestResults\n' "$sigic_archive"
fi
mkdir TestResults
for layer in Domain Application Infrastructure WebApi; do
  dotnet test "tests/SIGIC.$layer.Tests/SIGIC.$layer.Tests.csproj" -c Release --no-build \
    --collect:"XPlat Code Coverage" --results-directory "./TestResults/$layer" \
    --logger "trx;LogFileName=$layer.trx"
done
python3 scripts/ci/verify-trx.py TestResults
python3 scripts/ci/coverage.py TestResults
if [[ $# -eq 1 ]]; then
  "$sigic_reportgenerator" '-reports:TestResults/**/coverage.cobertura.xml' \
    '-targetdir:TestResults/coverage-html' '-reporttypes:Html'
fi
