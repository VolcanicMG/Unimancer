#!/usr/bin/env bash
# dev-sync.sh — continuously mirror this repo's Unity package into the embedded
# copy that the Windows-side Unity Editor actually compiles.
#
# WHY THIS EXISTS
#   Unity runs on Windows and compiles an *embedded copy* of the package inside
#   the host project's Packages/ folder. It cannot reliably watch files on the
#   WSL ext4 filesystem (the \\wsl.localhost\... bridge), so edits made in this
#   repo never reach the Editor on their own. This script polls and rsyncs so you
#   only ever edit here — Unity picks the changes up on its next focus/recompile.
#
# USAGE
#   ./scripts/dev-sync.sh [--once] [--delete] [DEST_PACKAGE_DIR]
#     DEST defaults to $UNIMANCER_UNITY_DEST, else the Mobile Mayhem project path.
#     Poll interval: $UNIMANCER_SYNC_INTERVAL seconds (default 2).
#     --once:   one sync pass (+ recompile request), then exit — no background loop.
#     --delete: also remove files from DEST that no longer exist here (*.meta kept).
#               Use after deleting/renaming C# files, or a stale copy keeps compiling.
#   Run it in a spare terminal while you work; Ctrl-C to stop.
#
# NOTES
#   * One-directional (repo -> embedded). Never edit the embedded copy directly;
#     it gets overwritten.
#   * *.meta files are excluded so Unity's generated GUIDs/references survive.
#   * Deletion is opt-in (--delete): without it, files you remove/rename here
#     stay in the embedded copy.

set -euo pipefail

# Source = the UPM package root in this repo (the unity/ folder, trailing slash
# so rsync copies its *contents* into DEST).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$SCRIPT_DIR/../unity/"

DELETE=(); ONCE=0
while [[ "${1:-}" == --* ]]; do
  case "$1" in
    --once) ONCE=1 ;;
    --delete) DELETE=(--delete) ;;
    *) echo "unknown flag: $1" >&2; exit 2 ;;
  esac
  shift
done

# Dest = the embedded copy Unity compiles. Override via arg 1 or env var.
DEST="${1:-${UNIMANCER_UNITY_DEST:-/mnt/c/Users/ethan/UnityGames/Mobile Mayhem/Packages/com.unimancer.mcp/}}"
INTERVAL="${UNIMANCER_SYNC_INTERVAL:-2}"

if [[ ! -d "$SRC" ]]; then echo "✗ source not found: $SRC" >&2; exit 1; fi
if [[ ! -d "$DEST" ]]; then
  echo "✗ dest not found: $DEST" >&2
  echo "  Pass the embedded package dir as arg 1, or set UNIMANCER_UNITY_DEST." >&2
  exit 1
fi

# -rlt : recurse, copy symlinks, set mtimes on files we do transfer.
# --checksum : compare by content hash, NOT mtime. Essential here — the Windows
#   drvfs mount (/mnt/c) rounds timestamps, so an mtime comparison sees every
#   file as "changed" and re-copies the whole tree each pass, which would make
#   Unity recompile constantly. Checksum only transfers files whose bytes differ.
# We omit perms/owner/group (-pog): meaningless on drvfs and cause churn.
# The tree is ~100 small files, so hashing every couple seconds is negligible.
RSYNC=(rsync -rlt --checksum "${DELETE[@]}" --exclude='*.meta' --out-format='  ↳ %n')

echo "▶ dev-sync: mirroring$([[ $ONCE == 1 ]] && echo ' once' || echo ' (Ctrl-C to stop)')"
echo "  from: $SRC"
echo "  to:   $DEST"
echo "  poll: every ${INTERVAL}s"
echo

trap 'echo; echo "■ dev-sync stopped."; exit 0' INT TERM

# After a changed pass, ask the Editor to recompile via the Unity CLI (no focus needed).
# Project root = two levels above the embedded package dir; Windows form for unity.exe.
PROJECT="$(cd "$DEST/../.." && pwd)"
command -v wslpath >/dev/null && PROJECT="$(wslpath -w "$PROJECT")"
recompile() {
  command -v unity >/dev/null || return 0
  UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 unity command recompile --project-path "$PROJECT" --format json >/dev/null 2>&1 \
    && echo "  ⟳ recompile requested" || echo "  ⚠ recompile request failed (Editor not reachable?)"
}

# Initial full pass.
out="$("${RSYNC[@]}" "$SRC" "$DEST" 2>&1 || true)"
[[ -n "$out" ]] && { echo "$out"; recompile; }
[[ $ONCE == 1 ]] && { echo "■ done."; exit 0; }

while true; do
  sleep "$INTERVAL"
  out="$("${RSYNC[@]}" "$SRC" "$DEST" 2>&1 || true)"
  if [[ -n "$out" ]]; then
    echo "[$(date +%H:%M:%S)] synced:"
    echo "$out"
    recompile
  fi
done
