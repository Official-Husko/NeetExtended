#!/bin/sh
# Takes NEET Mods out of NEET Demo again: the loader, the 64-bit player and the boot setting.
# The Mods folder stays unless you pass --remove-mods.
set -e
here=$(cd "$(dirname "$0")" && pwd)
neet="$here/tool/linux-x64/neet"
chmod +x "$neet" 2>/dev/null || true
"$neet" uninstall "$@"
