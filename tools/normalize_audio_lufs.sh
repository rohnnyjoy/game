#!/usr/bin/env bash
# Loudness-normalize audio assets to a target LUFS using ffmpeg loudnorm (EBU R128).
#
# Two-pass mode per file:
#  1) Analyze to get measured values
#  2) Render with measured_* and offset for stable, deterministic loudness
#
# Defaults:
#  - Integrated loudness: -16 LUFS (good SFX baseline; tweak to taste)
#  - True-peak limit: -1.0 dBTP
#  - LRA: 11 LU
#  - Output dir: assets/sounds_lufs
#
# Usage:
#  tools/normalize_audio_lufs.sh                 # normalize common sound dirs
#  tools/normalize_audio_lufs.sh -i -18 file.ogg # set target I to -18 LUFS
#  I=-14 TP=-1 OUTDIR=custom tools/normalize_audio_lufs.sh  # env overrides

set -euo pipefail

I=${I:--16}
TP=${TP:--1.0}
LRA=${LRA:-11}
OUTDIR=${OUTDIR:-assets/sounds_lufs}

while getopts ":i:p:r:o:" opt; do
  case $opt in
    i) I="$OPTARG";;
    p) TP="$OPTARG";;
    r) LRA="$OPTARG";;
    o) OUTDIR="$OPTARG";;
    \?) echo "Unknown option -$OPTARG" >&2; exit 2;;
  esac
done
shift $((OPTIND - 1))

log(){ printf "[lufs] %s\n" "$*"; }

codec_for_ext(){
  local ext_lc
  ext_lc=$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')
  case "$ext_lc" in
    ogg) echo "-c:a libvorbis -q:a 5";;
    wav) echo "-c:a pcm_s16le";;
    mp3) echo "-c:a libmp3lame -q:a 2";;
    flac) echo "-c:a flac";;
    *) echo "-c:a aac -q:a 2";;
  esac
}

analyze(){
  # Prints 5 space-separated values: I TP LRA THRESH OFFSET
  local src="$1"
  local out Ival TPval LRAval THval OFFval
  out=$(ffmpeg -hide_banner -nostats -i "$src" -af "loudnorm=I=$I:TP=$TP:LRA=$LRA:print_format=summary" -f null - 2>&1 || true)
  Ival=$(printf '%s\n' "$out" | awk -F': ' '/Input Integrated:/ {print $2}' | awk '{print $1}' | tail -n1)
  TPval=$(printf '%s\n' "$out" | awk -F': ' '/Input True Peak:/ {print $2}' | awk '{print $1}' | tail -n1)
  LRAval=$(printf '%s\n' "$out" | awk -F': ' '/Input LRA:/ {print $2}' | awk '{print $1}' | tail -n1)
  THval=$(printf '%s\n' "$out" | awk -F': ' '/Input Threshold:/ {print $2}' | awk '{print $1}' | tail -n1)
  OFFval=$(printf '%s\n' "$out" | awk -F': ' '/Target Offset:/ {print $2}' | awk '{print $1}' | tail -n1)
  echo "$Ival $TPval $LRAval $THval $OFFval"
}

normalize_one(){
  local src="$1"
  [[ -f "$src" ]] || { log "skip (not a file): $src"; return 0; }
  local base ext name dst Ival TPval LRAval THval OFFval codecs
  base=$(basename -- "$src")
  ext=${base##*.}
  name=${base%.*}
  mkdir -p "$OUTDIR"
  dst="$OUTDIR/${name}_lufs.${ext}"

  read Ival TPval LRAval THval OFFval < <(analyze "$src")
  if [[ -z "${Ival:-}" || "$Ival" == "N/A" ]]; then
    log "warn: could not analyze $src; applying single-pass loudnorm"
    codecs=$(codec_for_ext "$ext")
    ffmpeg -y -hide_banner -i "$src" \
      -af "loudnorm=I=$I:TP=$TP:LRA=$LRA:print_format=summary" \
      $codecs "$dst" >/dev/null 2>&1
    return 0
  fi

  codecs=$(codec_for_ext "$ext")
  log "normalizing: $src (I=${Ival} LUFS, TP=${TPval} dBTP) -> $dst"
  ffmpeg -y -hide_banner -i "$src" \
    -af "loudnorm=I=$I:TP=$TP:LRA=$LRA:measured_I=${Ival}:measured_TP=${TPval}:measured_LRA=${LRAval}:measured_thresh=${THval}:offset=${OFFval}:linear=true:print_format=summary" \
    $codecs "$dst" >/dev/null 2>&1
}

main(){
  local -a files
  if [[ $# -gt 0 ]]; then
    files=("$@")
  else
    files=()
    for d in assets/sounds reference/balatro/resources/sounds; do
      [[ -d "$d" ]] || continue
      while IFS= read -r -d '' f; do files+=("$f"); done < <(find "$d" -type f \( -iname '*.wav' -o -iname '*.ogg' -o -iname '*.mp3' -o -iname '*.flac' \) -print0)
    done
  fi
  if [[ ${#files[@]} -eq 0 ]]; then
    log "no input files found"
    exit 0
  fi
  log "target: I=${I} LUFS, TP=${TP} dBTP, LRA=${LRA} LU"
  log "output dir: $OUTDIR"
  for f in "${files[@]}"; do
    normalize_one "$f"
  done
  log "done. Update your sound paths to *_lufs files in $OUTDIR."
}

main "$@"

