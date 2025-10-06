#!/usr/bin/env bash
set -euo pipefail

# Normalize audio loudness across your asset files using ffmpeg's EBU R128 loudnorm filter.
#
# Defaults:
# - Target loudness: -14 LUFS (good for games/streams)
# - True peak limit: -1.5 dBTP
# - Loudness range: 11 LU
#
# By default, processes audio files in assets/sounds and writes normalized copies
# next to originals using a ".norm" suffix (e.g., gun.wav -> gun.norm.wav).
# Use --in-place to overwrite originals safely (temp file + move).
#
# Requirements: ffmpeg must be installed and available on PATH.

TARGET_LUFS="-14"
PEAK_DBTP="-1.5"
LRA_LU="11"
SUFFIX=".norm"
IN_PLACE="false"
DRY_RUN="false"
VERBOSE="false"
declare -a DIRS
declare -a FILES

usage() {
  cat <<EOF
Normalize audio files to consistent loudness using ffmpeg loudnorm.

Usage:
  tools/normalize_audio.sh [options]

Options:
  -d, --dir PATH         Add a directory to scan (default: assets/sounds)
  -f, --file PATH        Add a specific file to process (repeatable)
      --target N         Target LUFS (default: -14)
      --peak N           True peak limit dBTP (default: -1.5)
      --lra N            Loudness range LU (default: 11)
      --suffix STR       Suffix for output files (default: .norm)
      --in-place         Overwrite the original file in-place
      --dry-run          Show what would be processed, don’t modify files
  -v, --verbose          Print ffmpeg command lines
  -h, --help             Show this help

Notes:
  - Single-pass loudnorm is used for speed and simplicity. For SFX this is
    typically sufficient. If you later want two-pass, we can extend this script.
  - Supported extensions: wav, ogg, mp3, flac, aiff
EOF
}

have_ffmpeg() {
  command -v ffmpeg >/dev/null 2>&1
}

log() { printf "%s\n" "$*"; }
logv() { [ "$VERBOSE" = "true" ] && printf "[cmd] %s\n" "$*" || true; }

add_dir() { DIRS+=("$1"); }
add_file() { FILES+=("$1"); }

# Parse args
while (( "$#" )); do
  case "$1" in
    -d|--dir)           add_dir "$2"; shift 2 ;;
    -f|--file)          add_file "$2"; shift 2 ;;
    --target)           TARGET_LUFS="$2"; shift 2 ;;
    --peak)             PEAK_DBTP="$2"; shift 2 ;;
    --lra)              LRA_LU="$2"; shift 2 ;;
    --suffix)           SUFFIX="$2"; shift 2 ;;
    --in-place)         IN_PLACE="true"; shift 1 ;;
    --dry-run)          DRY_RUN="true"; shift 1 ;;
    -v|--verbose)       VERBOSE="true"; shift 1 ;;
    -h|--help)          usage; exit 0 ;;
    --)                 shift; break ;;
    -*)                 echo "Unknown option: $1" >&2; usage; exit 2 ;;
    *)                  add_file "$1"; shift 1 ;;
  esac
done

if ! have_ffmpeg; then
  echo "Error: ffmpeg not found on PATH. Please install ffmpeg." >&2
  exit 1
fi

# Default directory if none provided and no explicit files.
if [ ${#DIRS[@]} -eq 0 ] && [ ${#FILES[@]} -eq 0 ]; then
  add_dir "assets/sounds"
fi

# Expand directories into files
for d in "${DIRS[@]}"; do
  if [ -d "$d" ]; then
    while IFS= read -r -d '' f; do
      FILES+=("$f")
    done < <(find "$d" -type f \( \
      -iname '*.wav' -o -iname '*.ogg' -o -iname '*.mp3' -o -iname '*.flac' -o -iname '*.aiff' \
    \) -print0)
  else
    echo "Warn: directory not found: $d" >&2
  fi
done

if [ ${#FILES[@]} -eq 0 ]; then
  echo "No audio files found to process." >&2
  exit 0
fi

filter="loudnorm=I=${TARGET_LUFS}:TP=${PEAK_DBTP}:LRA=${LRA_LU}"

process_one() {
  local src="$1"
  local dir base ext dst tmp
  dir="$(dirname -- "$src")"
  base="$(basename -- "$src")"
  ext="${base##*.}"
  base="${base%.*}"

  if [ "$IN_PLACE" = "true" ]; then
    # Write to temp file alongside source, then move over original.
    tmp="${dir}/${base}${SUFFIX}.${ext}"
    dst="$tmp"
  else
    dst="${dir}/${base}${SUFFIX}.${ext}"
  fi

  if [ "$DRY_RUN" = "true" ]; then
    log "Would normalize: $src -> $dst"
    return 0
  fi

  mkdir -p -- "$dir"
  # -hide_banner to reduce noise, -loglevel error to only surface errors
  local cmd=(ffmpeg -y -hide_banner -loglevel error -i "$src" -filter:a "$filter" "$dst")
  log "Normalizing: $src"
  logv "${cmd[*]}"
  if ! "${cmd[@]}"; then
    echo "Error: ffmpeg failed on $src" >&2
    return 1
  fi

  if [ "$IN_PLACE" = "true" ]; then
    # Move over original atomically if possible.
    mv -f -- "$dst" "$src"
    log "Updated in-place: $src"
  else
    log "Wrote: $dst"
  fi
}

processed=0
for f in "${FILES[@]}"; do
  # Skip Godot .import sidecars
  case "$f" in *.import) continue ;; esac
  if [ ! -f "$f" ]; then
    echo "Warn: not a file: $f" >&2
    continue
  fi
  if process_one "$f"; then
    processed=$((processed+1))
  fi
done

log "Done. Processed $processed file(s)."

