#!/usr/bin/env bash
# Batch-peak-normalize audio assets using ffmpeg.
#
# Default behavior:
# - Targets peak at -3 dBFS (configurable via -t <dB>, e.g., -1 or -6)
# - Writes normalized copies to assets/sounds_normalized with "_norm" suffix
# - Preserves file extension; OGG encoded with libvorbis (q=5), WAV as 16-bit PCM
# - Safe: does not overwrite originals
#
# Usage examples:
#   tools/normalize_audio_ffmpeg.sh                        # normalize common sound dirs
#   tools/normalize_audio_ffmpeg.sh -t -6 file1.ogg a/b.wav
#   TARGET_PEAK_DB=-1 tools/normalize_audio_ffmpeg.sh      # env var override
#
set -euo pipefail

TARGET_PEAK_DB=${TARGET_PEAK_DB:--3}
OUTDIR=${OUTDIR:-assets/sounds_normalized}

log() { printf "[norm] %s\n" "$*"; }

normalize_one() {
  local src="$1"
  [[ -f "$src" ]] || { log "skip (not a file): $src"; return 0; }
  local base ext name dst gain
  base=$(basename -- "$src")
  ext=${base##*.}
  name=${base%.*}
  mkdir -p "$OUTDIR"
  dst="$OUTDIR/${name}_norm.${ext}"

  # Measure current peak using volumedetect
  local info max
  info=$(ffmpeg -hide_banner -nostats -i "$src" -af volumedetect -f null - 2>&1 || true)
  max=$(printf "%s\n" "$info" | awk -F': ' '/max_volume:/ {print $2}' | sed 's/ dB//; s/ //g' | tail -n1)
  if [[ -z "${max:-}" || "$max" == "N/A" ]]; then
    log "warn: could not detect max_volume for $src; copying without change"
    cp -f "$src" "$dst"
    return 0
  fi

  # Compute gain to reach target peak: gain = target - current
  gain=$(awk -v tgt="$TARGET_PEAK_DB" -v cur="$max" 'BEGIN { printf "%.3f", (tgt - cur) }')

  # Choose codec per extension (case-insensitive)
  local codec_args=( )
  local ext_lc
  ext_lc=$(printf '%s' "$ext" | tr '[:upper:]' '[:lower:]')
  case "$ext_lc" in
    ogg)
      codec_args=( -c:a libvorbis -q:a 5 )
      ;;
    wav)
      codec_args=( -c:a pcm_s16le )
      ;;
    mp3)
      codec_args=( -c:a libmp3lame -q:a 2 )
      ;;
    flac)
      codec_args=( -c:a flac )
      ;;
    *)
      codec_args=( -c:a aac -q:a 2 )
      ;;
  esac

  log "normalizing: $src (max=$max dB, gain=${gain} dB) -> $dst"
  ffmpeg -y -hide_banner -i "$src" \
    -af "volume=${gain}dB,alimiter=limit=${TARGET_PEAK_DB}dB" \
    "${codec_args[@]}" \
    "$dst" >/dev/null 2>&1
}

main() {
  local -a files
  if [[ $# -gt 0 ]]; then
    files=( "$@" )
  else
    files=()
    # Common project locations
    for d in assets/sounds reference/balatro/resources/sounds; do
      [[ -d "$d" ]] || continue
      while IFS= read -r -d '' f; do files+=("$f"); done < <(find "$d" -type f \( -iname '*.wav' -o -iname '*.ogg' -o -iname '*.mp3' -o -iname '*.flac' \) -print0)
    done
  fi

  if [[ ${#files[@]} -eq 0 ]]; then
    log "no input files found; pass files or ensure assets/sounds exists"
    exit 0
  fi

  log "target peak: ${TARGET_PEAK_DB} dBFS"
  log "output dir:  $OUTDIR"
  for f in "${files[@]}"; do
    normalize_one "$f"
  done
  log "done. Switch your Godot SFX paths to the *_norm files in $OUTDIR."
}

main "$@"
