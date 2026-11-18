# Kyuto

A desktop YouTube Music player for Linux, built with **.NET 10**, **Qt Bridge for C#** (custom Material 3 QML) and **libmpv**.

> Unofficial. Not affiliated with YouTube, Google or Last.fm.

## Features

- Search songs, videos, albums, artists and playlists; Home shelves, album / artist / playlist pages
- Your YouTube Music library and likes (session imported from your browser, or a pasted cookie)
- Queue with drag and drop, shuffle, repeat, play next
- Gapless playback: the next tracks are resolved ahead of time and queued in mpv
- Synced lyrics (YouTube Music and [LRCLIB](https://lrclib.net)) with per-track delay and text size
- Music videos in the playback bar or the maximized player
- Last.fm scrobbling with an on-disk queue that survives crashes and being offline
- MPRIS: playerctl, media keys and desktop widgets
- Material 3 interface: light / dark / follow the desktop, accent color, 5 variants, 3 contrast levels, colors from the cover
- Spanish and English
- Listening history and session restore (queue and position)

## Layout

| Project | What it holds |
|---|---|
| `Kyuto.Core` (F#) | Domain, queue, listen tracking, scrobble rules, lyrics (LRC), Material color solver, i18n, logging |
| `Kyuto.Scrobble` (F#) | Last.fm client and request signing |
| `Kyuto.Providers` (C#) | YouTube Music through the Python sidecar, LRCLIB |
| `Kyuto.Audio` (C#) | libmpv engine (P/Invoke) |
| `Kyuto.Storage` (C#) | SQLite database, secrets (keyring or file with mode 600), XDG paths |
| `Kyuto.Mpris` (C#) | `org.mpris.MediaPlayer2` on the session bus |
| `Kyuto.App` (C#) | Services, view models and the QML interface |
| `sidecar/ytm` (Python) | `ytmusicapi` + `yt-dlp` behind a JSON-lines protocol |
| `tests/Kyuto.Tests` (F#) | xUnit tests, including the color solver checked against a reference oracle |

## Run it

### With Nix (recommended)

```sh
nix develop
dotnet run --project src/Kyuto.App
```

Or build the package: `nix build` (the binary is `result/bin/kyuto`).

#### Install (flake)

Add in your inputs
```nix
kyuto = {
  url = "github:Theneillsaaco/kyuto";
  inputs.nixpkgs.follows = "nixpkgs";
};
```

Add in your sistem or home packages like that

```nix
inputs.kyuto.packages.${pkgs.stdenv.hostPlatform.system}.default
```

### Flatpak

```sh
./flatpak/build.sh
flatpak run io.github.Theneillsaaco.Kyuto
```

`build.sh` installs the KDE 6.11 runtime and the .NET 10 extension, generates the offline NuGet and Python dependency lists the first time, then builds and installs the app for your user. The first build compiles ffmpeg, mpv and libplacebo, so it takes a while.

"Import session from browser" needs read access to the browser profile. The manifest grants it for the common browsers; remove what you do not use with `flatpak override --user --nofilesystem=...`.

CI (`.github/workflows/flatpak-build.yml`) only builds the Flatpak to check that it works. It uploads nothing.

### Tests

```sh
dotnet test
```

## Configuration

Environment variables:

| Variable | Purpose |
|---|---|
| `KYUTO_LASTFM_KEY`, `KYUTO_LASTFM_SECRET` | Last.fm API key and shared secret (or enter them in Settings) |
| `KYUTO_SIDECAR` | Path to `server.py` |
| `KYUTO_SIDECAR_DIR` | Folder that holds `ytm/server.py` |
| `KYUTO_PYTHON` | Python interpreter for the sidecar (default `python3`) |
| `KYUTO_LOG` | `debug`, `warn` or `error` (default `info`) |

Files (XDG):

| Path | Content |
|---|---|
| `~/.config/kyuto/lastfm.json` | Last.fm API key and secret (mode 600) |
| `~/.config/kyuto/secrets/` | Fallback for secrets when no keyring is available |
| `~/.local/share/kyuto/kyuto.db` | Settings, history, saved queue, scrobble queue, caches |
| `~/.local/state/kyuto/kyuto.log` | Rotating log (never contains cookies or tokens) |

The YouTube cookie and the Last.fm session key are kept in the Secret Service keyring through `secret-tool`.

## Requirements

`libmpv`, `python3` with `ytmusicapi` and `yt-dlp`, a JS runtime for yt-dlp (`node`), `xdg-utils`, `libsecret` (for `secret-tool`) and Qt 6 with QtQuick, QtQuick Controls, QtQuick Shapes and QtMultimedia (video only). The Nix shell and the Flatpak bring all of it.

## Shortcuts

| Key | Action |
|---|---|
| `Space` | Play / pause |
| `Ctrl+Right` / `Ctrl+Left` | Next / previous |
| `Ctrl+F` | Focus search |
| `Ctrl+L` | Queue panel |
| `Ctrl+Shift+L` | Lyrics panel |
| `Alt+Left` | Back |
| `Esc` | Leave the maximized player |

## About

I use YouTube Music every day, and existing Electron wrappers take forever to start up. On top of that, both YouTube Music and Pear Desktop are still stuck on Material Design 1 or 2. Since Google doesn't seem to be in any rush to modernize it, I decided to do what any developer looking for a side project would do: **build it myself**.

This project started mainly as an excuse to dive deeper into **Qt**, test out **Qt Bridge for C#**, and build my first native desktop application from scratch. I wanted fast startup times, a proper **Material 3** interface, and complete freedom to customize every single feature the way I actually want it.

I'm also a huge **.NET** enthusiast, and honestly, there are barely any active FOSS desktop projects built with it-let alone anything combining **F# and C#** like this, as the few out there stick to plain C# and are either abandoned or overly barebones. So I combined both worlds into a responsive player tailored to my exact needs.

## License

MIT, see [LICENSE](LICENSE).

## Credits

- Color solver ported from [Sung](https://github.com/yappologistic/Sung)'s `m3color.cpp` (MIT); item normalization in the sidecar follows Sung's `helper/catalog.py` (MIT)
- Thanks to [Meiro](https://github.com/elianiva/meiro?tab=contributing-ov-file), which I used as the basis for several ideas, functions and the UI
- [mpv](https://mpv.io), [ytmusicapi](https://github.com/sigma67/ytmusicapi), [yt-dlp](https://github.com/yt-dlp/yt-dlp), [LRCLIB](https://lrclib.net), [Qt Bridge for C#](https://github.com/qt/qtbridge-csharp)
