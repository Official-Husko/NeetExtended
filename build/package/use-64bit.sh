#!/bin/sh
# Switches NEET Demo to Unity's 64-bit player (what the installer sets up), after use-32bit.sh.
# The first time it downloads the player from unity3d.com (about 180 MB).
# Extra arguments go to the tool, e.g. --game "/path/to/NEET Demo". Close the game first.
set -e
here=$(cd "$(dirname "$0")" && pwd)
neet="$here/tool/linux-x64/neet"
chmod +x "$neet" 2>/dev/null || true
"$neet" install --64bit "$@"
