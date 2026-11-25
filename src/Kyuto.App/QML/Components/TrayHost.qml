import QtQuick
import Qt.labs.platform as Labs

// The system tray icon (StatusNotifierItem). Loaded on demand by Main.qml (Loader), so a Qt without Qt.labs.platform
// only loses the tray. host is the window: it knows how to show and hide itself and has tr(). playback is the
// PlaybackViewModel. Click the icon to show or hide the window.
Labs.SystemTrayIcon {
    id: tray

    property var host: null
    property var playback: null
    readonly property bool playing: playback !== null && playback.isLoaded && !playback.paused

    visible: true
    icon.name: "io.github.Theneillsaaco.Kyuto"
    tooltip: playback && playback.currentTitle !== ""
        ? playback.currentTitle + (playback.currentArtist !== "" ? " · " + playback.currentArtist : "")
        : "Kyuto"

    onActivated: (reason) => {
        if (reason === Labs.SystemTrayIcon.Trigger && host)
            host.toggleWindow()
    }

    menu: Labs.Menu {
        Labs.MenuItem {
            text: tray.host ? tray.host.tr("tray.show") : "Show"
            onTriggered: if (tray.host) tray.host.toggleWindow()
        }
        Labs.MenuItem {
            text: tray.playing ? (tray.host ? tray.host.tr("tip.pause") : "Pause") : (tray.host ? tray.host.tr("tip.play") : "Play")
            onTriggered: if (tray.playback) tray.playback.toggle()
        }
        Labs.MenuItem {
            text: tray.host ? tray.host.tr("tip.next") : "Next"
            enabled: tray.playback ? tray.playback.canNext : false
            onTriggered: if (tray.playback) tray.playback.next()
        }
        Labs.MenuItem {
            text: tray.host ? tray.host.tr("tip.previous") : "Previous"
            enabled: tray.playback ? tray.playback.canPrevious : false
            onTriggered: if (tray.playback) tray.playback.previous()
        }
        Labs.MenuSeparator {}
        Labs.MenuItem {
            text: tray.host ? tray.host.tr("tray.quit") : "Quit"
            onTriggered: Qt.quit()
        }
    }
}
