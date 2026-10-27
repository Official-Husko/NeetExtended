#!/usr/bin/env bash
# Publishes a release of NeetExtended on GitHub.
#
#   build/release.sh --suggest                      what changed since the last release, and a suggested version
#   build/release.sh <X.Y.Z|major|minor|patch> [--dry-run] [--yes]
#
# Checks that the tree is clean and in sync with GitHub, that gh is logged in and that the installed
# game is the build the mods were tested with. Then it sets the version, writes changelog.json with
# that version released, commits, builds the package from that commit, tags it, pushes, and creates
# the GitHub release with the zip and notes from the changelog. If something fails before the push,
# the release commit and tag are taken back. --dry-run shows the notes and stops before changing
# anything; --yes skips the confirmation.
set -euo pipefail

repo=$(cd "$(dirname "$0")/.." && pwd)
cd "$repo"

die() {
    echo "release: $*" >&2
    exit 1
}

dotnet build tools/NeetTool -c Release -v quiet -nologo > /dev/null
neet() { "$repo/tools/NeetTool/bin/Release/net10.0/neet" "$@"; }

if [ "${1:-}" = "--suggest" ]; then
    neet changelog --suggest
    exit 0
fi

target=${1:-}
[ -n "$target" ] || die "usage: build/release.sh <X.Y.Z|major|minor|patch> [--dry-run] [--yes], or --suggest"
shift
dry_run=false
assume_yes=false
for arg in "$@"; do
    case $arg in
        --dry-run) dry_run=true ;;
        --yes) assume_yes=true ;;
        *) die "unknown option $arg" ;;
    esac
done

# Where things stand.
[ -z "$(git status --porcelain)" ] || die "the working tree has uncommitted changes"
[ "$(git branch --show-current)" = main ] || die "releases are made from main"
git fetch -q origin
# Local commits not on GitHub yet are fine (the release pushes main), missing or different ones aren't.
git merge-base --is-ancestor origin/main HEAD || die "main is behind or has diverged from origin/main; pull first"
gh auth status > /dev/null 2>&1 || die "gh isn't logged in (gh auth login)"
github=$(gh repo view --json nameWithOwner -q .nameWithOwner)
neet compat --check --offline > /dev/null || die "the installed game isn't the tested build: test it, then run neet compat --record"

# The version.
last=$(git tag --list 'v*' --sort=-v:refname | head -n 1)
last=${last#v}
case $target in
    major | minor | patch)
        [ -n "$last" ] || die "there's no release yet; give the first version as X.Y.Z"
        IFS=. read -r major minor patch <<< "$last"
        case $target in
            major) version="$((major + 1)).0.0" ;;
            minor) version="$major.$((minor + 1)).0" ;;
            patch) version="$major.$minor.$((patch + 1))" ;;
        esac
        ;;
    *) version=$target ;;
esac
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "$version isn't a version (Major.Minor.Patch)"
! git rev-parse -q --verify "refs/tags/v$version" > /dev/null || die "v$version already exists"
if [ -n "$last" ]; then
    newest=$(printf '%s\n%s\n' "$last" "$version" | sort -V | tail -n 1)
    [ "$newest" = "$version" ] && [ "$version" != "$last" ] || die "$version isn't newer than the last release ($last)"
fi

# The notes: the changelog for this version, plus the tested game build and how to install.
notes=$(mktemp)
trap 'rm -f "$notes"' EXIT
build_id=$(sed -n 's/.*"buildId": *"\([^"]*\)".*/\1/p' compat/game.json)
unity=$(sed -n 's/.*"unity": *"\([^"]*\)".*/\1/p' compat/game.json)
{
    neet changelog --release "$version" --notes "$version"
    echo
    echo "---"
    echo
    echo "Tested with NEET Demo build $build_id (Unity $unity)."
    echo
    echo "**Installing:** download \`NeetExtended-$version.zip\`, unpack it, close the game and run \`install.bat\` (Windows) or \`install.sh\` (Linux). [FEATURES.md](https://github.com/$github/blob/v$version/FEATURES.md) lists everything the mods do."
} > "$notes"

echo
neet changelog --suggest
neet compat || true
echo
echo "Releasing $version (last release: ${last:-none}) to $github, with $(git rev-list --count origin/main..HEAD) commit(s) not pushed yet"
echo "----- notes -----"
cat "$notes"
echo "-----------------"

if $dry_run; then
    echo "Dry run: nothing was changed."
    exit 0
fi
if ! $assume_yes; then
    read -r -p "Publish v$version? [y/N] " answer
    [[ $answer == [yY]* ]] || die "stopped, nothing was changed"
fi

# Local part: undone if anything fails before the push.
start=$(git rev-parse HEAD)
rollback() {
    echo "release: failed, taking back the release commit and tag" >&2
    git tag -d "v$version" > /dev/null 2>&1 || true
    git reset -q --hard "$start"
}
trap 'rollback' ERR

sed -i "s|<Version>[^<]*</Version>|<Version>$version</Version>|" Directory.Build.props
neet changelog --write --release "$version" > /dev/null
neet compat --record > /dev/null
git add Directory.Build.props changelog.json compat/game.json
git commit -q -m "chore(release): $version" \
    -m "Sets the version to $version, marks it released in changelog.json and records the game build it was tested with (NEET Demo build $build_id)."

# Built from the release commit, so the game shows its hash without "-dirty".
build/package.sh
zip="dist/NeetExtended-$version.zip"
[ -f "$zip" ] || false

git tag -a "v$version" -m "NeetExtended $version" -m "$(cat "$notes")"
trap - ERR

# Public part.
git push -q origin main "v$version"
gh release create "v$version" "$zip" --repo "$github" --title "NeetExtended $version" --notes-file "$notes"
echo "Released: https://github.com/$github/releases/tag/v$version"
