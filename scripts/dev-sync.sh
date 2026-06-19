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
#   ./scripts/dev-sync.sh [DEST_PACKAGE_DIR]
#     DEST defaults to $UNIMANCER_UNITY_DEST, else the Mobile Mayhem project path.
#     Poll interval: $UNIMANCER_SYNC_INTERVAL seconds (default 2).
#   Run it in a spare terminal while you work; Ctrl-C to stop.
#
# NOTES
#   * One-directional (repo -> embedded). Never edit the embedded copy directly;
#     it gets overwritten.
#   * *.meta files are excluded so Unity's generated GUIDs/references survive.
#   * No --delete: files you remove/rename here are not auto-removed from the
#     embedded copy (rare; clean up by hand if needed).

set -euo pipefail

# Source = the UPM package root in this repo (the unity/ folder, trailing slash
# so rsync copies its *contents* into DEST).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$SCRIPT_DIR/../unity/"

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
RSYNC=(rsync -rlt --checksum --exclude='*.meta' --out-format='  ↳ %n')

echo "▶ dev-sync: mirroring (Ctrl-C to stop)"
echo "  from: $SRC"
echo "  to:   $DEST"
echo "  poll: every ${INTERVAL}s"
echo

trap 'echo; echo "■ dev-sync stopped."; exit 0' INT TERM

# Initial full pass.
"${RSYNC[@]}" "$SRC" "$DEST" || true

while true; do
  sleep "$INTERVAL"
  out="$("${RSYNC[@]}" "$SRC" "$DEST" 2>&1 || true)"
  if [[ -n "$out" ]]; then
    echo "[$(date +%H:%M:%S)] synced:"
    echo "$out"
  fi
done
