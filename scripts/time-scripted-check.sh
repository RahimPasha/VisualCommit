#!/usr/bin/env bash
# Times scripted visual checks, for the time budget of docs/phase-3-design.md 7.7: runs the
# walk-through's tests whose method names match a filter, alone, and prints how long each test
# took (as the test platform measured it) and the time of each step the checks noted (every
# picture a phase 3 check saves through Shot is a step: artifacts/visual/phase-N/timings/).
# It never runs the real-window pass.
#
# Usage, from anywhere in the repository (Git Bash on Windows, bash elsewhere):
#   bash scripts/time-scripted-check.sh "*Check_01_commit_menu"
#   bash scripts/time-scripted-check.sh "*Phase3ChecksD*" 3
# The second argument is the phase whose step times to print (3 when left out).
#
# Like every partial run, it empties the phase's picture folder first (VisualRun.Save), so the
# folder then holds only the pictures of the checks it ran; run `dotnet test` at the repo root
# again before the real-window pass. Under load (another build on the machine, a hot laptop) the
# times grow: time a check alone and more than once before writing a budget.
set -euo pipefail

filter="${1:?usage: bash scripts/time-scripted-check.sh <method filter, such as \"*Check_01_commit_menu\"> [phase]}"
phase="${2:-3}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

dotnet build tests/VisualCommit.VisualTests --nologo -v quiet

timings="artifacts/visual/phase-${phase}/timings"
mkdir -p "$timings"
marker="$(mktemp)"
trap 'rm -f "$marker"' EXIT

start=$(date +%s)
status=0
dotnet test --project tests/VisualCommit.VisualTests --no-build --filter-method "$filter" --output Detailed || status=$?
end=$(date +%s)

echo
echo "Wall time of the run (the test host's start included): $((end - start)) s"
echo "Steps the checks noted (time since the app started, time since the step before):"
found=0
for file in "$timings"/*.txt; do
  if [ -f "$file" ] && [ "$file" -nt "$marker" ]; then
    cat "$file"
    found=1
  fi
done
if [ "$found" -eq 0 ]; then
  echo "  (none: the tests that ran saved no phase ${phase} picture)"
fi

exit "$status"
