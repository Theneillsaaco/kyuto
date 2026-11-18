#!/usr/bin/env bash
# Renames the project from "Player" to "Kyuto".
# Run it from the repo with a clean tree, keeping this file OUTSIDE the repo (or untracked):
#   bash ~/Downloads/rename-to-kyuto.sh
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "El arbol no esta limpio: haz commit o stash antes." >&2
  exit 1
fi

# ---- 1. Flatpak: app id and file names ---------------------------------------------------------
old=io.github.Theneillsaaco.MediaPlayer
new=io.github.Theneillsaaco.Kyuto

for f in flatpak/$old.*; do
  git mv "$f" "flatpak/$new.${f##*/$old.}"
done

# ---- 2. Files that can hold a name (not the color oracle, not the MPRIS spec XML) ----------------
mapfile -t files < <(git ls-files -- \
  '*.cs' '*.fs' '*.fsproj' '*.csproj' '*.qml' '*.py' '*.sh' '*.nix' '*.yml' '*.md' '*.targets' '*.desktop' '*.xml' \
  ':(exclude)tests/Kyuto.Tests/Data' ':(exclude)src/Kyuto.Mpris/dbus' ':(exclude,glob)**/rename-to-kyuto.sh')

edit() { sed -E -i "$1" "${files[@]}"; }

# Namespaces and project paths: Player.Core -> Kyuto.Core, src/Player.App/... -> src/Kyuto.App/...
edit 's/\bPlayer\.(Core|App|Audio|Mpris|Providers|Scrobble|Storage|Integration|Tests)\b/Kyuto.\1/g'

# Environment variables: PLAYER_LOG -> KYUTO_LOG ...
edit 's/\bPLAYER_/KYUTO_/g'

# Flatpak app id
edit 's/io\.github\.Theneillsaaco\.MediaPlayer/io.github.Theneillsaaco.Kyuto/g'

# XDG folders, log and database names written as text (docs, comments, strings)
edit 's#(\.config|\.local/share|\.local/state|\.cache)/player\b#\1/kyuto#g'
edit 's#/app/(lib|share)/player\b#/app/\1/kyuto#g'
edit 's/"player", "player\.log"/"kyuto", "kyuto.log"/g'
edit 's/\bplayer\.log\b/kyuto.log/g'
edit 's/\bplayer\.db\b/kyuto.db/g'

# ---- 3. Folder names in code --------------------------------------------------------------------
sed -i 's/"player"/"kyuto"/g' \
  src/Kyuto.Storage/AppPaths.cs \
  src/Kyuto.Storage/Secrets/SecretStore.cs \
  tests/Kyuto.Integration.Tests/AppPathsTests.cs
sed -i 's/--label=Player/--label=Kyuto/' src/Kyuto.Storage/Secrets/SecretStore.cs

# ---- 4. Names the user sees ---------------------------------------------------------------------
sed -i 's/: "Player"$/: "Kyuto"/'                                    src/Kyuto.App/Main.qml
sed -i 's/identity = "Player"/identity = "Kyuto"/'                   src/Kyuto.Mpris/MprisService.cs
sed -i 's/WriteString("Player")/WriteString("Kyuto")/'               src/Kyuto.Mpris/DesktopNotifier.cs
sed -i 's/"audio-client-name", "Player"/"audio-client-name", "Kyuto"/' src/Kyuto.Audio/Mpv/MpvOptions.cs
sed -i 's#"Player/#"Kyuto/#' src/Kyuto.Providers/Lyrics/LrclibClient.cs src/Kyuto.Scrobble/LastFm/Client.fs
sed -i 's/a Player en Last/a Kyuto en Last/g; s/ Player on Last/ Kyuto on Last/g' src/Kyuto.Core/Localization/Translations.fs

# ---- 5. Command name and display name of the package --------------------------------------------
sed -i 's/\bmedia-player\b/kyuto/g' flatpak/*.yml flatpak/*.desktop flatpak/*.xml
sed -i 's/YouTube Music Player/Kyuto/' flatpak/*.desktop flatpak/*.xml
sed -i 's#result/bin/media-player#result/bin/kyuto#' README.md

echo
echo "Listo. Lo que queda con la palabra 'player' (revisa a ojo, mucho es prosa legitima):"
grep -rIn --exclude-dir={.git,bin,obj,.direnv} --exclude='*.json' --exclude=rename-to-kyuto.sh \
  -iE '\bplayer\b|player_' . | grep -v 'MediaPlayer2' || true

echo
echo "Siguiente: dotnet build && dotnet test"
