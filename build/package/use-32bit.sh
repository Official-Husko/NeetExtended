#!/bin/sh
# Switches NEET Demo back to its original 32-bit player, in case the 64-bit one gives you trouble.
# Installing again keeps it that way; use-64bit.sh switches back to 64-bit.
# Extra arguments go to the tool, e.g. --game "/path/to/NEET Demo". Close the game first.
set -e
here=$(cd "$(dirname "$0")" && pwd)
neet="$here/tool/linux-x64/neet"
chmod +x "$neet" 2>/dev/null || true
"$neet" install --32bit "$@"
