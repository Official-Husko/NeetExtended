#!/bin/sh
# Installs NeetExtended into NEET Demo (Steam, Linux with Proton).
# Run it from the unpacked folder. Extra arguments go to the installer, e.g. --game "/path/to/NEET Demo".
set -e
here=$(cd "$(dirname "$0")" && pwd)
neet="$here/tool/linux-x64/neet"
chmod +x "$neet" 2>/dev/null || true

echo "NeetExtended installer"
echo
echo "This adds the mod loader and the mods to NEET Demo, and switches the game to Unity's 64-bit player"
echo "(downloaded from unity3d.com the first time, about 180 MB). If you went back to 32-bit with"
echo "use-32bit.sh, that stays. Close the game first."
echo

flags=""
printf "Clean up memory in small steps instead of freezing the game for a moment every so often\n"
printf "(incremental garbage collection)? Recommended. [Y/n] "
read -r answer
case "$answer" in
    [nN]*) ;;
    *) flags="$flags --incremental-gc" ;;
esac

printf "\nCo-op over Steam needs two files from Valve (steam_api.dll and steam_api64.dll, about 560 KB)\n"
printf "that this pack can't include. Download them now from github.com (the Steamworks.NET release)? [y/N] "
read -r answer
steam=no
case "$answer" in
    [yY]*) steam=yes ;;
esac

echo
# shellcheck disable=SC2086 # flags are separate words on purpose
"$neet" install --mods "$here/Mods" $flags "$@"
if [ "$steam" = yes ]; then
    "$neet" steam "$@" || echo "Couldn't fetch Steam's files. Try again later with: neet steam"
fi
echo
echo "Done. Start NEET Demo from Steam; F1 opens the mod menu."
echo "If the game doesn't start or keeps crashing on 64-bit, run use-32bit.sh to go back to its original"
echo "32-bit player."
