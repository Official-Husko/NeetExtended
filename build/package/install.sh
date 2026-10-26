#!/bin/sh
# Installs NEET Mods into NEET Demo (Steam, Linux with Proton).
# Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "/path/to/NEET Demo".
set -e
here=$(cd "$(dirname "$0")" && pwd)
neet="$here/tool/linux-x64/neet"
chmod +x "$neet" 2>/dev/null || true

echo "NEET Mods installer"
echo
echo "This adds the mod loader and the mods to NEET Demo. Close the game first."
echo

flags=""
printf "Switch the game to 64-bit? It runs a little faster and can use more memory. This downloads\n"
printf "Unity's 64-bit player (about 180 MB) from unity3d.com. You can switch back later. [y/N] "
read -r answer
case "$answer" in
    [yY]*) flags="$flags --64bit" ;;
esac

printf "Clean up memory in small steps instead of freezing the game for a moment every so often\n"
printf "(incremental garbage collection)? Recommended. [Y/n] "
read -r answer
case "$answer" in
    [nN]*) ;;
    *) flags="$flags --incremental-gc" ;;
esac

echo
# shellcheck disable=SC2086 # flags are separate words on purpose
"$neet" install --mods "$here/Mods" $flags "$@"
echo
echo "Done. Start NEET Demo from Steam; F1 opens the mod menu."
