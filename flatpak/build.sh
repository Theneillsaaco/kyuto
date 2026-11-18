#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

APP=io.github.Theneillsaaco.Kyuto
KDE=6.11
FDO=26.08

flatpak remote-add --user --if-not-exists flathub https://dl.flathub.org/repo/flathub.flatpakrepo
flatpak install -y --noninteractive --user flathub \
  org.kde.Platform//$KDE org.kde.Sdk//$KDE org.freedesktop.Sdk.Extension.dotnet10//$FDO

flatpak-builder --user --install --force-clean --disable-rofiles-fuse --ccache build-dir "$APP.yml"
echo "Listo: flatpak run $APP"