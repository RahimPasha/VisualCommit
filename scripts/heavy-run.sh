#!/usr/bin/env bash
# Runs `dotnet test` with the arguments it is given, under a lock that lets only one such run use
# the machine at a time (D245). Whole-project runs and the run at the root are the heavy ones: the
# default run takes about 55 minutes on the laptop and keeps every processor busy, and two at once
# make both slower and fail tests that only fail under load (a git call 10 to 15 times as slow as
# alone, D237). During a phase only the lead starts them, through this script, so that a second
# heavy run waits for the first instead of running beside it. Builders and fixers run the test
# classes they touch with plain `dotnet test`, which needs no lock.
#
# Usage, from anywhere in the repository (Git Bash on Windows, bash on Linux and macOS). The
# arguments go to `dotnet test` unchanged, and it runs at the repository's root:
#   bash scripts/heavy-run.sh                                          the whole default run
#   bash scripts/heavy-run.sh --project tests/VisualCommit.App.Tests
#   VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS=600 bash scripts/heavy-run.sh --project tests/VisualCommit.Git.Tests
# The exit code is dotnet test's own. VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS=N gives up after N
# seconds of waiting (0: at once; N is read as a decimal number, so 08 is 8) with exit code 75;
# without it, the script waits as long as it takes. Exit code 1 means the script itself could not
# run (a value of that variable that is no whole number, no temporary folder), and then dotnet test
# was not started.
#
# The lock is the folder heavy-run.lock in the tests' temporary root, the folder every test folder
# lies in (VisualCommit.Testing's TempDirectory): VisualCommit.Tests in the folder that .NET's
# Path.GetTempPath gives, which is %TMP% (or %TEMP%, or %USERPROFILE%) on Windows and $TMPDIR (or
# /tmp) elsewhere. mkdir makes it, which only one process can do. Its file `holder` says who holds
# it: the script's process, when it took the lock, the command line and the folder it runs in, and
# once the run has started, the dotnet test process. To see who holds the lock in Git Bash:
#   cat "$(cygpath -u "$TMP")/VisualCommit.Tests/heavy-run.lock/holder"
#
# A second run waits: it says once who holds the lock and since when, and looks again every 10
# seconds. It takes over a lock whose holder and dotnet test process both no longer run (on
# Windows, tasklist looks for each by its Windows process number and program; elsewhere kill -0
# looks for the holder and for the test run's process group), and a lock folder without its holder
# file once that folder is more than a minute old. The run that made the folder places its holder
# file there only where there is none yet (ln, which never replaces a file), so that a run that
# stalled for over a minute between its mkdir and its holder file (the laptop asleep, say), and so
# lost its folder to a takeover, cannot write into the folder of the run that took it: it finds
# that run's holder file and waits.
#
# The script gives the lock back when it ends: when dotnet test ends, passed or failed, and on
# Ctrl+C, TERM or HUP, which first stop the test run (on Windows, taskkill /T /F ends dotnet test
# and every process it started; elsewhere its process group gets SIGINT, as from Ctrl+C, and
# SIGKILL after 30 seconds) and then end the script with 128 plus the signal's number. A script
# that is stopped while it waits leaves the holder's lock alone. To stop a run from another shell,
# send TERM to the process the holder file names, from Git Bash on Windows (`kill <pid>`; taskkill
# would end the script without letting it stop dotnet test). Started with & from a script or a shell
# without job control, the script ignores SIGINT, as every such background command does; TERM still
# works. A script ended outright (kill -9, taskkill, or a tool that ends only the top process of a
# command, as Claude Code's TaskStop did on the laptop) cannot give the lock back: its dotnet test
# runs on and the lock stays held, and the next run takes it over once the script and its dotnet
# test process are both gone.
#
# It is not for CI: each CI job has a machine of its own and makes one test run, so there is nothing
# to wait for, and the workflow calls dotnet test itself.
set -euo pipefail

poll_seconds=10
give_up_status=75

say() { printf 'heavy-run: %s\n' "$*" >&2; }
fail() { printf 'heavy-run: %s\n' "$*" >&2; exit 1; }

wait_limit="${VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS:-}"
case "$wait_limit" in
  *[!0-9]*) fail "VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS must be a whole number of seconds, not \"$wait_limit\"" ;;
esac
# Read as decimal: bash's arithmetic takes a leading 0 for octal, and 08 or 09 would end the
# waiting loop with an error.
[ -z "$wait_limit" ] || wait_limit=$((10#$wait_limit))

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) windows=true ;;
  *) windows=false ;;
esac

if [ "$windows" = true ]; then
  temp="${TMP:-${TEMP:-${USERPROFILE:-}}}"
  [ -n "$temp" ] || fail "none of TMP, TEMP and USERPROFILE is set, so the tests' temporary folder is unknown"
  temp="$(cygpath -u "$temp")"
else
  temp="${TMPDIR:-/tmp}"
fi
lock_root="${temp%/}/VisualCommit.Tests"
lock="$lock_root/heavy-run.lock"
guard="$lock_root/heavy-run.takeover"
mkdir -p "$lock_root" || fail "cannot make $lock_root"
# The lock as messages name it: in Windows' own form on Windows, where Git Bash calls the folder /tmp.
shown="$lock"
[ "$windows" = false ] || shown="$(cygpath -w "$lock")"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

command_line="dotnet test"
if [ "$#" -gt 0 ]; then
  command_line="$command_line$(printf ' %q' "$@")"
fi

# --- Processes -----------------------------------------------------------------------------------

# The Windows process number of a Git Bash process.
windows_pid() { cat "/proc/$1/winpid" 2>/dev/null || true; }

# The program a Windows process runs (bash.exe, dotnet.exe), as tasklist names it.
windows_program() {
  [ -n "$1" ] || return 0
  tasklist //FI "PID eq $1" //FO CSV //NH 2>/dev/null | tr -d '\r' |
    awk -F'","' -v pid="$1" '$2 == pid { sub(/^"/, "", $1); print $1; exit }' || true
}

# Whether the Windows process $1 still runs program $2 (any program when $2 is empty). A check
# that cannot be made counts as running: a lock must never be taken from a run that may be going.
windows_process_runs() {
  [ -n "$1" ] || return 1
  local listing
  listing="$(tasklist //FI "PID eq $1" //FO CSV //NH 2>/dev/null)" || return 0
  listing="$(printf '%s\n' "$listing" | tr -d '\r')"
  if [ -n "$2" ]; then
    printf '%s\n' "$listing" | grep -Fqi -- "\"$2\",\"$1\","
  else
    printf '%s\n' "$listing" | grep -Fq -- "\",\"$1\","
  fi
}

my_windows_pid=""
my_program=""
if [ "$windows" = true ]; then
  my_windows_pid="$(windows_pid $$)"
  my_program="$(windows_program "$my_windows_pid")"
fi

# --- The lock ------------------------------------------------------------------------------------

token="$$.$(date +%s).$RANDOM"
owned=false
guarded=false
child=""
child_windows_pid=""
child_program=""
sleeper=""
holder_text=""
started_text=""

# A field of the holder file last read into holder_text.
value_of() { awk -v key="$1" 'index($0, key "=") == 1 { print substr($0, length(key) + 2); exit }' <<<"$holder_text"; }

# Writes the holder file in one step (a reader never sees half of it). Fails when the lock folder
# is gone. With "first", right after this run's mkdir, it places the file only where there is none
# yet, and fails when another run's holder file is there: this run stalled after its mkdir, its
# empty folder was taken over, and the folder is that run's now. ln never replaces a file; where
# hard links cannot be made, a file made with noclobber (which also never replaces one) stands in.
write_holder() {
  local new="$lock/holder.$$"
  {
    printf 'token=%s\n' "$token"
    printf 'pid=%s\n' "$$"
    printf 'windows_pid=%s\n' "$my_windows_pid"
    printf 'program=%s\n' "$my_program"
    printf 'started=%s\n' "$started_text"
    printf 'started_epoch=%s\n' "$started_epoch"
    printf 'command=%s\n' "$command_line"
    printf 'directory=%s\n' "$root"
    printf 'test_pid=%s\n' "$child"
    printf 'test_windows_pid=%s\n' "$child_windows_pid"
    printf 'test_program=%s\n' "$child_program"
  } >"$new" 2>/dev/null || return 1
  if [ "${1:-}" = first ]; then
    if ln "$new" "$lock/holder" 2>/dev/null ||
      { [ ! -e "$lock/holder" ] && (set -C; cat "$new" >"$lock/holder") 2>/dev/null; }; then
      rm -f "$new"
      return 0
    fi
    rm -f "$new" 2>/dev/null || true
    return 1
  fi
  mv -f "$new" "$lock/holder" 2>/dev/null
}

holder_runs() {
  if [ "$windows" = true ] && [ -n "$(value_of windows_pid)" ]; then
    windows_process_runs "$(value_of windows_pid)" "$(value_of program)"
  else
    local pid
    pid="$(value_of pid)"
    [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null
  fi
}

# Whether the dotnet test process of the holder still runs (on Linux and macOS, anything in its
# process group: the test applications it started too).
holder_tests_run() {
  if [ "$windows" = true ]; then
    windows_process_runs "$(value_of test_windows_pid)" "$(value_of test_program)"
  else
    local pid
    pid="$(value_of test_pid)"
    [ -n "$pid" ] && kill -0 -- "-$pid" 2>/dev/null
  fi
}

older_than_a_minute() { [ -n "$(find "$1" -maxdepth 0 -mmin +1 2>/dev/null || true)" ]; }

lock_is_stale() {
  if [ -z "$holder_text" ]; then
    # Made by mkdir a moment ago and about to get its holder file, or left by a run that died
    # before writing it.
    older_than_a_minute "$lock"
    return
  fi
  if holder_runs || holder_tests_run; then
    return 1
  fi
  return 0
}

describe_holder() {
  if [ -z "$holder_text" ]; then
    printf 'a run that is just taking it'
    return
  fi
  local pid
  pid="$(value_of pid)"
  [ -z "$(value_of windows_pid)" ] || pid="$pid, Windows process $(value_of windows_pid)"
  printf '%s, held since %s (process %s, in %s)' \
    "$(value_of command)" "$(value_of started)" "$pid" "$(value_of directory)"
}

# Removes a stale lock. A second folder guards the removal, so that of two runs that both find the
# lock stale, only one removes it, and only while it still holds what that run read.
take_over() {
  if ! mkdir "$guard" 2>/dev/null; then
    # Another run is taking the lock over; a guard over a minute old was left by one that died.
    if older_than_a_minute "$guard"; then
      rmdir "$guard" 2>/dev/null || true
    fi
    return 1
  fi
  guarded=true
  local now
  now="$(cat "$lock/holder" 2>/dev/null || true)"
  if [ "$now" = "$holder_text" ] && { [ -n "$now" ] || older_than_a_minute "$lock"; }; then
    if [ -n "$now" ]; then
      say "taking over a stale lock: no process of its holder runs any more: $(describe_holder)"
    else
      say "taking over a stale lock: its folder has had no holder file for over a minute"
    fi
    rm -rf "$lock"
  fi
  rmdir "$guard" 2>/dev/null || true
  guarded=false
}

release() {
  if [ -n "$sleeper" ]; then
    kill "$sleeper" 2>/dev/null || true
    sleeper=""
  fi
  if [ "$guarded" = true ]; then
    rmdir "$guard" 2>/dev/null || true
    guarded=false
  fi
  if [ "$owned" = true ]; then
    # Only the lock this script took: one taken over from it (judged stale) is another run's.
    case "$(cat "$lock/holder" 2>/dev/null || true)" in
      "" | "token=$token"*) rm -rf "$lock" ;;
    esac
    owned=false
  fi
}

stop_tests() {
  [ -n "$child" ] || return 0
  if [ "$windows" = true ]; then
    child_windows_pid="$(windows_pid "$child")"
    if [ -n "$child_windows_pid" ]; then
      taskkill //F //T //PID "$child_windows_pid" >/dev/null 2>&1 || true
    fi
  else
    kill -INT -- "-$child" 2>/dev/null || true
    local waited=0
    while kill -0 -- "-$child" 2>/dev/null && [ "$waited" -lt 30 ]; do
      sleep 1
      waited=$((waited + 1))
    done
    kill -KILL -- "-$child" 2>/dev/null || true
  fi
  wait "$child" 2>/dev/null || true
  child=""
}

on_signal() {
  trap '' INT TERM HUP
  if [ -n "$child" ]; then
    say "$1: stopping the test run"
    stop_tests
    say "the test run is stopped; giving the lock back"
  fi
  exit "$2"
}

trap release EXIT
trap 'on_signal INT 130' INT
trap 'on_signal TERM 143' TERM
trap 'on_signal HUP 129' HUP

# Sleeps in the background, so that a signal is handled at once.
nap() {
  sleep "$1" &
  sleeper=$!
  wait "$sleeper" || true
  sleeper=""
}

# --- Waiting for the lock ------------------------------------------------------------------------

wait_start=$(date +%s)
announced="none"
while :; do
  started_epoch=$(date +%s)
  started_text="$(date '+%Y-%m-%d %H:%M:%S %z')"
  # Made again if someone cleared the temporary folder while this run waited.
  mkdir -p "$lock_root" 2>/dev/null || true
  if mkdir "$lock" 2>/dev/null; then
    # Owned only once its holder file is in place: until then, a script that ends leaves the empty
    # folder to another run's takeover a minute later, rather than remove a folder that may be
    # another run's by then.
    if write_holder first; then
      owned=true
      break
    fi
    # Between mkdir and the holder file, the folder went, or another run took it over as stale and
    # placed its own holder file: wait for that run like any other.
    continue
  fi

  holder_text="$(cat "$lock/holder" 2>/dev/null || true)"
  next=$poll_seconds
  if lock_is_stale; then
    take_over || true
    # Tried again at once when the stale lock is gone. Otherwise (another run is taking it over, or
    # it could not be removed, such as a file held open on Windows) after a pause, not in a busy
    # loop, and within the wait limit below like any other wait.
    [ -e "$lock" ] || continue
    next=1
  elif [ "$(value_of token)" != "$announced" ]; then
    announced="$(value_of token)"
    say "waiting for the lock $shown: $(describe_holder); looking again every $poll_seconds s"
  fi

  waited=$(($(date +%s) - wait_start))
  if [ -n "$wait_limit" ]; then
    if [ "$waited" -ge "$wait_limit" ]; then
      say "giving up after $waited s of waiting (VISUALCOMMIT_HEAVY_RUN_WAIT_SECONDS=$wait_limit): the lock is still held by $(describe_holder)"
      exit "$give_up_status"
    fi
    if [ $((wait_limit - waited)) -lt "$next" ]; then
      next=$((wait_limit - waited))
    fi
  fi
  nap "$next"
done

# Only a loop that ended with the lock leads here; anything else (an error that ended the loop
# early) must not start dotnet test without it.
[ "$owned" = true ] || fail "the lock $shown was not taken, so dotnet test was not started"

if [ "$announced" = "none" ]; then
  say "took the lock $shown"
else
  say "took the lock $shown after $((started_epoch - wait_start)) s of waiting"
fi

# --- The run -------------------------------------------------------------------------------------

# On Linux and macOS the run gets a process group of its own (set -m), so that a signal reaches the
# test applications it starts too, and Ctrl+C is not ignored by it as by other background commands.
# Windows has no such groups for its programs: taskkill /T finds them by their parent instead.
[ "$windows" = true ] || set -m
dotnet test ${1+"$@"} </dev/null &
child=$!
[ "$windows" = true ] || set +m

if [ "$windows" = true ]; then
  # Read once the forked shell has become dotnet.exe.
  nap 1
  child_windows_pid="$(windows_pid "$child")"
  child_program="$(windows_program "$child_windows_pid")"
fi
write_holder || say "warning: could not note the test run's process in $shown/holder"

status=0
wait "$child" || status=$?
child=""
release
say "dotnet test ended with exit code $status after $(($(date +%s) - started_epoch)) s; the lock is given back"
exit "$status"
