#!/usr/bin/env bash
# Builds a release package: dist/NEET-Mods-<version>.zip with the mods, the neet tool for Linux and
# Windows, and the install scripts from build/package. The dev bridge stays out (it opens a port
# and is only for development).
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo/Directory.Build.props" | head -n 1)
name="NEET-Mods-$version"
out="$repo/dist/$name"
excluded=(DevBridge)

rm -rf "$out" "$repo/dist/$name.zip"
mkdir -p "$out/Mods" "$out/tool"

echo "Building $version..."
dotnet build "$repo/NeetMods.sln" -c Release -p:NeetDeploy=false -v quiet -nologo

for project in "$repo"/src/NeetModKit "$repo"/mods/*/; do
    project=${project%/}
    [ -f "$project/mod.json" ] || continue
    id=$(sed -n 's:.*"id"[[:space:]]*\:[[:space:]]*"\([^"]*\)".*:\1:p' "$project/mod.json" | head -n 1)
    if [[ " ${excluded[*]} " == *" $id "* ]]; then
        continue
    fi
    mkdir -p "$out/Mods/$id"
    (cd "$project/bin/Release" && find . -type f ! -name '*.deps.json' -exec cp --parents {} "$out/Mods/$id/" \;)
    echo "  $id"
done

for rid in linux-x64 win-x64; do
    echo "Publishing neet for $rid..."
    dotnet publish "$repo/tools/NeetTool" -c Release -r "$rid" --self-contained true \
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:NeetDeploy=false \
        -o "$out/tool/$rid" -v quiet -nologo
    rm -f "$out/tool/$rid/"*.pdb
done

cp "$repo/build/package/"{install.sh,install.bat,uninstall.sh,uninstall.bat,README.txt} "$out/"
chmod +x "$out/install.sh" "$out/uninstall.sh" "$out/tool/linux-x64/neet"

(cd "$repo/dist" && zip -qr "$name.zip" "$name")
echo "Done: dist/$name.zip ($(du -h "$repo/dist/$name.zip" | cut -f1))"
