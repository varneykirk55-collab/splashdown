#!/usr/bin/env bash
# Rebuild, validate and re-render every staged Splashdown asset (Blender 5.2, background mode).
# Usage (Git Bash):  ./build_all.sh
set -u
cd "$(dirname "$0")"
B="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.2/blender.exe}"
for a in $("$B" -b -P scripts/build_assets.py 2>&1 | grep '^ASSETS' | sed 's/ASSETS //' | tr ',' ' '); do
  "$B" -b -P scripts/build_assets.py -- "$a" 2>&1 | grep -E '^BUILT|Error' || echo "FAILED $a"
done
"$B" -b -P scripts/validate_assets.py 2>&1 | grep -E '^SD_|^asset'
for a in $(ls src/*.blend | xargs -n1 basename | sed 's/.blend//'); do "$B" -b -P scripts/render_previews.py -- "$a" >/dev/null 2>&1; done
"$B" -b -P scripts/render_previews.py -- assembled 2>&1 | grep -E '^RENDERED'
