import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic
import QtQuick.Window
import "../QML/Components"
import "../QML/Pages"
import "../QML/Dialogs"

ApplicationWindow {
    id: win
    width: 1100
    height: 700
    minimumWidth: miniWindow ? 300 : 720
    minimumHeight: miniWindow ? 420 : 480
    flags: Qt.Window | (miniWindow && pinned ? Qt.WindowStaysOnTopHint : 0)
    visible: true
    title: playback.currentTitle !== ""
        ? playback.currentTitle + (playback.currentArtist !== "" ? " · " + playback.currentArtist : "")
        : "Kyuto"

    // Meiro's frame: the window is a tinted surface and the pages are a rounded sheet laid on it.
    color: appTheme.surfaceContainerLow

    // Google Sans is the font of the whole interface. Labels and controls inherit it from the window.
    font.family: appTheme.fontFamily
    font.pixelSize: 14

    // The Basic style's own pieces (scroll bars, text cursors, selections) read the palette, which is light
    // unless told otherwise: that is what made the dark mode look wrong.
    palette.window: appTheme.surface
    palette.windowText: appTheme.inkSurface
    palette.base: appTheme.surfaceContainer
    palette.text: appTheme.inkSurface
    palette.button: appTheme.surfaceContainerHigh
    palette.buttonText: appTheme.inkSurface
    palette.highlight: appTheme.primary
    palette.highlightedText: appTheme.inkPrimary
    palette.light: appTheme.surfaceContainerHighest
    palette.mid: appTheme.outlineVariant
    palette.dark: appTheme.outline
    palette.placeholderText: appTheme.inkSurfaceVariant

    // The four weights of Google Sans that ship in Fonts/ (Regular, Medium, SemiBold, Bold).
    FontLoader { source: themeVm.fontDir !== "" ? themeVm.fontDir + "/GoogleSans-Regular.ttf" : "" }
    FontLoader { source: themeVm.fontDir !== "" ? themeVm.fontDir + "/GoogleSans-Medium.ttf" : "" }
    FontLoader { source: themeVm.fontDir !== "" ? themeVm.fontDir + "/GoogleSans-SemiBold.ttf" : "" }
    FontLoader { source: themeVm.fontDir !== "" ? themeVm.fontDir + "/GoogleSans-Bold.ttf" : "" }

    // Leaf components find the theme and the texts through the window: Window.window.theme, Window.window.i18n.
    readonly property var theme: Theme
    readonly property var appTheme: Theme
    readonly property var i18n: appTr

    function tr(key) { return appTr.t(key) }

    // A short note in the snackbar.
    property var undoAction: null
    property var scrollMemory: ({})

    function toast(text) { undoAction = null; navVm.notify(text) }
    function toastUndo(text, action) { undoAction = action; navVm.notify(text) }
    function goBack() {
        if (playerOpen) playerMode = "mini"
        else navVm.goBack()
    }
    function copyText(text) {
        clipboardProxy.text = text
        clipboardProxy.selectAll()
        clipboardProxy.copy()
    }
    function startRadio(id) { navVm.startRadio(id) }

    TextEdit { id: clipboardProxy; visible: false }
    TapHandler { acceptedButtons: Qt.BackButton; onTapped: win.goBack() }   // botón atrás del ratón

    // The heading of the top bar: where the user is.
    function pageTitle() {
        if (playerOpen) return tr("tip.player")
        switch (navVm.page) {
        case "home": return tr("nav.home")
        case "search": return tr("nav.search")
        case "library": return tr("nav.library")
        case "history": return tr("nav.history")
        default: return navVm.title
        }
    }

    // Likes from the lists (the ⋮ menu and the right click of a song). The ids come as a JSON array.
    readonly property var likedIds: {
        const map = ({})
        try { for (const id of JSON.parse(libraryVm.likedJson)) map[id] = true } catch (e) { }
        return map
    }
    function isLiked(id) { return likedIds[id] === true }
    function canLikeId(id) { return libraryVm.accountState === "signedIn" && id !== "" && !/^(https?:\/\/|\/)/i.test(id) }
    function toggleLikeOf(id) { libraryVm.toggleLikeOf(id) }
    function pickPlaylist(id) { playlistPicker.show(id) }
    
    // Comes from Program.cs as an initial property of Qml.Load (`kyuto --mini` opens the small window).
    property bool startMini: false
    Component.onCompleted: if (startMini) enterMini()
    
    property int lastVolume: 60
    function toggleMute() {
        if (playback.volume > 0) {
            lastVolume = playback.volume
            playback.setVolume(0)
        } else {
            playback.setVolume(lastVolume > 0 ? lastVolume : 60)
        }
    }

    property bool queueOpen: true
    property bool lyricsOpen: false

    // The lyrics over the whole window, on the color of the cover.
    property bool lyricsFull: false
    property color lyricsTint: appTheme.primary

    // The small window (cover and controls only). The size of the big one is kept to come back to it.
    property bool miniWindow: false
    property bool pinned: false
    property int savedWidth: 1100
    property int savedHeight: 700

    function enterMini() {
        if (miniWindow) return
        settings.close()
        lyricsFull = false
        playerMode = "mini"
        savedWidth = width
        savedHeight = height
        miniWindow = true
        width = 380
        height = 560
    }
    function leaveMini() {
        if (!miniWindow) return
        miniWindow = false
        pinned = false
        width = savedWidth
        height = savedHeight
    }
    function toggleMini() { if (miniWindow) leaveMini(); else enterMini() }

    // The tray: closing hides the window while the option is on, the icon brings it back.
    readonly property bool trayReady: trayLoader.status === Loader.Ready
    property var keepAlive: null
    function toggleWindow() {
        if (visible && visibility !== Window.Minimized) {
            hide()
        } else {
            show()
            raise()
            requestActivate()
        }
    }
    onClosing: (close) => {
        if (settingsVm.closeToTray && win.trayReady) {
            close.accepted = false
            win.hide()
        }
    }

    // A song dragged sideways from a list onto the queue or the playback bar. TrackItem calls it with
    // "begin", "move" and "end" (which answers whether it landed on a target).
    property bool trackDragging: false
    property bool trackDropOver: false
    property string trackDragTitle: ""
    property real trackDragX: 0
    property real trackDragY: 0
    function overItem(item, x, y) {
        if (!item || !item.visible) return false
        const p = item.mapFromItem(null, x, y)
        return p.x >= 0 && p.y >= 0 && p.x <= item.width && p.y <= item.height
    }
    function trackDrag(phase, title, x, y) {
        trackDragX = x
        trackDragY = y
        trackDropOver = overItem(queuePanel, x, y) || overItem(bar, x, y)
        if (phase === "begin") {
            trackDragTitle = title
            trackDragging = true
            return false
        }
        if (phase === "end") {
            const landed = trackDropOver
            trackDragging = false
            trackDropOver = false
            return landed
        }
        return false
    }
    function pickPlaylistStaged() { playlistPicker.showStaged() }
    readonly property bool playingNow: playback.isLoaded && !playback.paused

    // The player: "mini" (the picture lives in the playback bar and the pages show), "page" (the player takes the
    // left area) or "full" (maximized: the playback bar floats over the picture).
    property string playerMode: "mini"
    readonly property bool playerOpen: playerMode !== "mini"

    // Width of the queue and lyrics side panels: they give way on narrow windows.
    readonly property int sideWidth: Math.round(Math.min(380, Math.max(280, width * 0.28)))

    // Under this width the queue and the lyrics go below the picture instead of beside it.
    readonly property bool compact: width < 1000

    // A text field has the keyboard: the single-key shortcuts (space) must not take it.
    readonly property bool typing: activeFocusItem !== null && activeFocusItem.selectByMouse !== undefined

    PlaybackViewModel { id: playback }
    QueueViewModel { id: queueVm }
    SearchViewModel { id: searchVm }
    LibraryViewModel { id: libraryVm }
    SettingsViewModel { id: settingsVm }
    ThemeViewModel { id: themeVm }
    LocaleViewModel { id: localeVm }
    LyricsViewModel { id: lyricsVm }
    VideoViewModel { id: videoVm }
    NavigationViewModel { id: navVm }
    HistoryViewModel { id: historyVm }
    // Theme is a singleton (pragma Singleton): the view model only feeds it.
    Binding { target: Theme; property: "tokensJson"; value: themeVm.tokensJson }
    Tr { id: appTr; json: localeVm.stringsJson }

    // The queue and lyrics panels are built once and only moved and faded. Creating and destroying them on every
    // click is what made the player crash after many quick clicks. compactHost, wideQueueHost and wideLyricsHost
    // are the places they live in (below the picture on a narrow window, on the right on a wide one).
    QueuePanel {
        id: queuePanel
        parent: win.compact ? compactHost : wideQueueHost
        anchors.fill: parent
        radius: 28
        color: appTheme.surface
        clip: true
        vm: queueVm
        opacity: win.queueOpen ? 1 : 0
        visible: opacity > 0
        Behavior on opacity { Anim { type: Anim.FastEffects } }

        Rectangle { // drop target
            anchors.fill: parent
            radius: parent.radius
            color: "transparent"
            border.width: 3
            border.color: appTheme.primary
            visible: win.trackDragging
            opacity: win.trackDropOver ? 1 : 0.35
            z: 40
        }
    }

    LyricsPanel {
        id: lyricsPanel
        parent: win.compact ? compactHost : wideLyricsHost
        anchors.fill: parent
        radius: 28
        color: appTheme.surface
        clip: true
        vm: lyricsVm
        opacity: win.lyricsOpen ? 1 : 0
        visible: opacity > 0
        Behavior on opacity { Anim { type: Anim.FastEffects } }
    }

    // The colors follow the cover that plays while the option is on (Settings > Appearance).
    CoverColor {
        id: coverColor
        source: playback.currentArt
        active: themeVm.dynamic
        onFound: (hex) => themeVm.useCoverColor(hex)
    }

    SearchSuggestions {
        id: suggestions
        field: input
        recent: input.text.trim().length === 0
        entries: input.text.trim().length === 0
            ? (searchVm.recentJson.length > 0 ? JSON.parse(searchVm.recentJson) : [])
            : (searchVm.suggestionsJson.length > 0 ? JSON.parse(searchVm.suggestionsJson) : [])
        onClearRecent: searchVm.clearRecent()
        onRemoveRecent: (text) => searchVm.removeRecent(text)
        onChosen: (text) => {
            input.text = text
            close()
            searchVm.clearSuggestions()
            win.playerMode = "mini"
            navVm.goRoot("search")
            searchVm.search(text)
        }
    }

    Timer {
        id: suggestTimer
        interval: 220
        onTriggered: searchVm.suggest(input.text)
    }

    SettingsPage { id: settings; anchors.fill: parent; z: 90; vm: settingsVm; appearanceVm: themeVm; videoVm: videoVm; localeVm: localeVm; lyricsVm: lyricsVm; theme: appTheme }

    PlaylistPicker { id: playlistPicker; vm: libraryVm; theme: appTheme }

    ShortcutsDialog { id: shortcutsDialog }
    
    // Called by the lists and the playback bar: a name opens its page, from wherever the user is.
    function openEntity(kind, entityId) {
        playerMode = "mini"
        navVm.openEntity(kind, entityId)
    }
    function openArtistOf(videoId) {
        playerMode = "mini"
        navVm.openTrackArtist(videoId)
    }
    function openAlbumOf(videoId) {
        playerMode = "mini"
        navVm.openTrackAlbum(videoId)
    }

    function pollAll() {
        settingsVm.pollLight()
        searchVm.poll()
        libraryVm.poll()
        lyricsVm.poll()
        videoVm.poll()
        navVm.poll()
        playback.poll()
    }

    // Polls only while something is playing, searching or working in the background,
    // so an idle player wakes nothing. It ticks faster while the lyrics are open, to follow the line.
    Timer {
        interval: win.lyricsOpen || win.lyricsFull ? 120 : 250
        repeat: true
        running: (playback.isLoaded && !playback.paused) || searchVm.searching || searchVm.suggesting || playback.statsPending || libraryVm.working || settingsVm.busy || lyricsVm.loading || videoVm.loading || navVm.working
        onTriggered: win.pollAll()
    }

    // Playlist actions say how they went in the snackbar.
    Connections {
        target: libraryVm
        function onNoticeChanged() { if (libraryVm.notice !== "") win.toast(libraryVm.notice) }
    }

    // Follows the desktop's light/dark setting, only while the mode is "system".
    Timer {
        interval: 1000
        repeat: true
        running: themeVm.mode === "system"
        onTriggered: themeVm.poll()
    }

    // Runs what the desktop asked for (playerctl, media keys). Cheap: it only looks at a queue.
    Timer {
        interval: 250
        repeat: true
        running: true
        onTriggered: playback.pump()
    }

    // The snackbar goes away by itself.
    Timer {
        interval: win.undoAction !== null ? 7000 : 4000
        running: navVm.message.length > 0
        onTriggered: { navVm.dismissMessage(); win.undoAction = null }
    }

    // Opening the library, or signing in while it is open, loads the playlists.
    property string currentPage: navVm.page
    onCurrentPageChanged: {
        if (currentPage === "library") libraryVm.loadLibrary()
        else if (currentPage === "history") historyVm.load()
    }
    property string accountState: libraryVm.accountState
    onAccountStateChanged: if (currentPage === "library") libraryVm.loadLibrary()

    // The queue and the lyrics share the right-hand side: opening one closes the other.
    onQueueOpenChanged: if (queueOpen) lyricsOpen = false
    onLyricsOpenChanged: {
        lyricsVm.panelShown(lyricsOpen || lyricsFull)
        if (lyricsOpen) queueOpen = false
    }
    onLyricsFullChanged: {
        lyricsVm.panelShown(lyricsOpen || lyricsFull)
        if (lyricsFull) leaveMini()
    }

    // ---- keyboard ----

    // Escape leaves the maximized player.
    Shortcut {
        sequence: "Esc"
        enabled: win.lyricsFull && !settings.opened
        onActivated: win.lyricsFull = false
    }
    Shortcut {
        sequence: "Esc"
        enabled: win.playerMode === "full" && !settings.opened && !win.lyricsFull
        onActivated: win.playerMode = "page"
    }
    Shortcut {
        sequence: "Alt+Left"
        enabled: !win.miniWindow
        onActivated: win.goBack()
    }
    Shortcut {
        sequence: "Ctrl+M"
        onActivated: win.toggleMini()
    }
    Shortcut {
        sequence: "Ctrl+Shift+F"
        enabled: !win.miniWindow
        onActivated: win.lyricsFull = !win.lyricsFull
    }
    Shortcut {
        sequence: "Space"
        enabled: !win.typing && !settings.visible
        onActivated: playback.toggle()
    }
    Shortcut {
        sequence: "Ctrl+Right"
        onActivated: playback.next()
    }
    Shortcut {
        sequence: "Ctrl+Left"
        onActivated: playback.previous()
    }
    Shortcut {
        sequence: "Ctrl+F"
        onActivated: { input.forceActiveFocus(); input.selectAll() }
    }
    Shortcut {
        sequence: "Ctrl+L"
        onActivated: win.queueOpen = !win.queueOpen
    }
    Shortcut {
        sequence: "Ctrl+Shift+L"
        onActivated: win.lyricsOpen = !win.lyricsOpen
    }
    Shortcut {
        sequence: "Ctrl+,"
        enabled: !win.miniWindow
        onActivated: settings.open()
    }

    Shortcut {
        sequence: "Left"
        enabled: !win.typing && !settings.visible && playback.isLoaded
        onActivated: playback.seek(Math.max(0, playback.position - 5))
    }
    Shortcut {
        sequence: "Right"
        enabled: !win.typing && !settings.visible && playback.isLoaded
        onActivated: playback.seek(Math.min(playback.duration, playback.position + 5))
    }
    Shortcut {
        sequence: "Ctrl+Up"
        onActivated: playback.setVolume(Math.min(100, playback.volume + 5))
    }
    Shortcut {
        sequence: "Ctrl+Down"
        onActivated: playback.setVolume(Math.max(0, playback.volume - 5))
    }
    Shortcut {
        sequence: "M"
        enabled: !win.typing && !settings.visible
        onActivated: win.toggleMute()
    }
    // Reload the page on screen (Home, an album, an artist...).
    Shortcut {
        sequence: "F5"
        onActivated: navVm.retry()
    }
    Shortcut {
        sequences: ["?", "Shift+/"]
        enabled: !win.typing && !settings.visible
        onActivated: shortcutsDialog.open()
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0
        visible: !win.miniWindow

        // Top bar: back and where you are on the left, search on the right, then the panels.
        RowLayout {
            Layout.fillWidth: true
            Layout.leftMargin: 12
            Layout.rightMargin: 12
            Layout.topMargin: 8
            Layout.bottomMargin: 4
            spacing: 8

            MIconButton {
                glyph: "back"
                tip: win.tr("tip.back")
                enabled: win.playerOpen || navVm.canBack
                onClicked: {
                    if (win.playerOpen) win.playerMode = "mini"
                    else navVm.goBack()
                }
            }

            Label {
                Layout.fillWidth: true
                Layout.minimumWidth: 0
                Layout.leftMargin: 8
                visible: win.width >= 900
                text: win.pageTitle()
                elide: Text.ElideRight
                maximumLineCount: 1
                color: appTheme.inkSurface
                font.pixelSize: 24
                font.weight: Font.DemiBold
            }

            Item {
                Layout.fillWidth: true
                visible: win.width < 900
            }

            MSearchField {
                id: input
                Layout.fillWidth: win.width < 900
                Layout.preferredWidth: 520
                Layout.maximumWidth: 560
                Layout.minimumWidth: 160
                placeholderText: win.tr("search.placeholder")
                clearTip: win.tr("tip.clearSearch")
                onTextEdited: suggestTimer.restart()
                onCleared: { suggestTimer.stop(); searchVm.clearSuggestions() }
                Keys.onDownPressed: suggestions.step(1)
                Keys.onUpPressed: suggestions.step(-1)
                Keys.onEscapePressed: { suggestions.close(); searchVm.clearSuggestions() }
                onAccepted: {
                    const query = suggestions.pick() !== "" ? suggestions.pick() : text
                    suggestions.close()
                    searchVm.clearSuggestions()
                    if (query !== text) text = query
                    win.playerMode = "mini"
                    navVm.goRoot("search")
                    searchVm.search(query)
                }
            }
            MIconButton {
                glyph: "album"
                tip: win.tr("tip.player")
                selected: win.playerOpen
                onClicked: win.playerMode = win.playerOpen ? "mini" : "page"
            }
            MIconButton {
                glyph: "lyrics"
                tip: win.tr("tip.lyrics")
                selected: win.lyricsOpen
                onClicked: win.lyricsOpen = !win.lyricsOpen
            }
            MIconButton {
                glyph: "queue"
                tip: win.tr("tip.queue")
                selected: win.queueOpen
                onClicked: win.queueOpen = !win.queueOpen
            }
            MIconButton {
                glyph: "mini"
                tip: win.tr("tip.miniPlayer")
                onClicked: win.enterMini()
            }
            MIconButton {
                glyph: "settings"
                tip: win.tr("tip.settings")
                onClicked: settings.open()
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            Layout.leftMargin: 4
            Layout.rightMargin: 12
            spacing: 8

            NavRail {
                Layout.fillHeight: true
                Layout.preferredWidth: 80
                current: navVm.section
                onChosen: (key) => {
                    win.playerMode = "mini"
                    navVm.goRoot(key)
                }
            }

            ColumnLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                spacing: 8

                // The pages and the player share one rounded sheet.
                Rectangle {
                    id: stageCard
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    radius: win.playerMode === "full" ? 0 : 28
                    clip: true
                    color: win.playerMode === "full" ? "#000000" : appTheme.surface

                    // Pages cross-fade (Material "fade through": the old one out, then the new one in).
                    FadePage {
                        shown: !win.playerOpen && navVm.page === "search"
                        SearchPage {
                            anchors.fill: parent
                            vm: searchVm
                            onEntityActivated: (kind, entityId) => win.openEntity(kind, entityId)
                        }
                    }

                    FadePage {
                        shown: !win.playerOpen && navVm.page === "home"
                        HomePage {
                            anchors.fill: parent
                            nav: navVm
                            onEntityActivated: (kind, entityId) => win.openEntity(kind, entityId)
                        }
                    }

                    FadePage {
                        shown: !win.playerOpen && navVm.page === "library"
                        LibraryPage {
                            anchors.fill: parent
                            vm: libraryVm
                            onSettingsRequested: settings.open()
                        }
                    }

                    FadePage {
                        shown: !win.playerOpen && navVm.page === "history"
                        HistoryPage {
                            anchors.fill: parent
                            vm: historyVm
                        }
                    }

                    FadePage {
                        shown: !win.playerOpen && (navVm.page === "album" || navVm.page === "collection")
                        CollectionPage {
                            anchors.fill: parent
                            nav: navVm
                        }
                    }

                    FadePage {
                        shown: !win.playerOpen && navVm.page === "artist"
                        ArtistPage {
                            anchors.fill: parent
                            nav: navVm
                            onEntityActivated: (kind, entityId) => win.openEntity(kind, entityId)
                        }
                    }

                    PlayerPage {
                        id: playerPage
                        anchors.fill: parent
                        visible: win.playerOpen
                        mode: win.playerMode
                        playback: playback
                        video: videoVm
                        theme: appTheme
                        videoReady: videoLayer.ready
                        videoWaiting: videoLayer.waiting
                        videoUnavailable: videoLayer.failed
                        onMinimizeRequested: win.playerMode = "mini"
                        onMaximizeRequested: win.playerMode = "full"
                        onRestoreRequested: win.playerMode = "page"
                    }

                    CornerMask {
                        anchors.fill: parent
                        z: 50
                        radius: stageCard.radius
                        color: appTheme.surfaceContainerLow
                        visible: win.playerMode !== "full"
                    }
                }

                // Narrow window: the queue or the lyrics go under the picture, as YouTube Music does.
                Item {
                    id: compactHost
                    Layout.fillWidth: true
                    Layout.preferredHeight: Math.max(180, Math.round(win.height * 0.38))
                    visible: win.compact && win.playerMode !== "full" && (win.queueOpen || win.lyricsOpen)
                }
            }

            // Wide window: they stay on the right.
            Item {
                id: wideQueueHost
                Layout.preferredWidth: win.sideWidth
                Layout.fillHeight: true
                visible: !win.compact && win.queueOpen
            }

            Item {
                id: wideLyricsHost
                Layout.preferredWidth: win.sideWidth + 20
                Layout.fillHeight: true
                visible: !win.compact && win.lyricsOpen
            }
        }

        // Where the playback bar sits in the window (it moves onto the video in the maximized player).
        Item {
            id: barHost
            Layout.fillWidth: true
            Layout.leftMargin: 12
            Layout.rightMargin: 12
            Layout.topMargin: 8
            Layout.bottomMargin: 12
            Layout.preferredHeight: 96
            visible: win.playerMode !== "full"
        }
    }

    PlaybackBar {
        id: bar
        parent: win.playerMode === "full" ? playerPage.dock : barHost
        anchors.fill: parent
        playback: playback
        queue: queueVm
        library: libraryVm
        video: videoVm
        theme: appTheme
        docked: win.playerMode === "full"
        videoInSlot: win.playerMode === "mini" && videoVm.showing
        onCoverClicked: win.playerMode = win.playerOpen ? "mini" : "page"
    }

    // The one video of the app, moved into whichever slot is on screen so it never restarts.
    VideoLayer {
        id: videoLayer
        parent: win.playerOpen ? playerPage.stage : bar.miniSlot
        anchors.fill: parent
        vm: videoVm
        playback: playback
        fit: win.playerMode === "full"
        suspended: win.miniWindow
    }

    MiniPlayer {
        id: miniPlayer
        anchors.fill: parent
        z: 70
        visible: win.miniWindow
        playback: playback
        library: libraryVm
        theme: appTheme
        pinned: win.pinned
        onPinToggled: win.pinned = !win.pinned
        onExpandRequested: win.leaveMini()
    }

    // Color of the cover for the full-screen lyrics (independent of the "dynamic color" setting).
    CoverColor {
        source: playback.currentArt
        active: win.lyricsFull
        onFound: (hex) => win.lyricsTint = hex !== "" ? Qt.color(hex) : appTheme.primary
    }

    Loader {
        id: lyricsFullLoader
        anchors.fill: parent
        z: 80
        active: win.lyricsFull

        sourceComponent: LyricsFullscreen {
            vm: lyricsVm
            playback: playback
            theme: appTheme
            tint: win.lyricsTint
            onCloseRequested: win.lyricsFull = false
        }
    }

    // A song on its way to the queue: it follows the pointer; the targets light up when it is over them.
    Rectangle {
        visible: win.trackDragging
        z: 150
        x: Math.min(win.width - width - 8, win.trackDragX + 12)
        y: win.trackDragY + 12
        width: Math.min(260, dragLabel.implicitWidth + 32)
        height: 40
        radius: 20
        color: win.trackDropOver ? appTheme.primary : appTheme.inverseSurface
        opacity: 0.94

        Label {
            id: dragLabel
            anchors.fill: parent
            anchors.leftMargin: 16
            anchors.rightMargin: 16
            verticalAlignment: Text.AlignVCenter
            text: win.trackDropOver ? win.tr("queue.added") : win.trackDragTitle
            color: win.trackDropOver ? appTheme.inkPrimary : appTheme.inverseOnSurface
            font.pixelSize: 14
            elide: Text.ElideRight
        }
    }

    // The tray icon, only while the option is on.
    Loader {
        id: trayLoader
        active: settingsVm.closeToTray && settingsVm.trayAvailable
        source: "../QML/Components/TrayHost.qml"
        onLoaded: {
            item.host = win
            item.playback = playback
        }
    }

    // Hiding the only window would end the app. While the tray keeps it alive, a 1x1 transparent window of its own
    // stays on screen.
    Component {
        id: keepAliveComponent

        Window {
            width: 1
            height: 1
            x: 0
            y: 0
            color: "transparent"
            flags: Qt.Tool | Qt.FramelessWindowHint | Qt.WindowDoesNotAcceptFocus | Qt.WindowTransparentForInput
            visible: true
        }
    }
    function syncKeepAlive() {
        const wanted = settingsVm.closeToTray && trayReady
        const needWindow = wanted && !settingsVm.holdWithoutWindows(wanted)
        if (!wanted)
            settingsVm.holdWithoutWindows(false)
        if (needWindow && keepAlive === null) {
            keepAlive = keepAliveComponent.createObject(null)
        } else if (!needWindow && keepAlive !== null) {
            keepAlive.destroy()
            keepAlive = null
        }
    }
    onTrayReadyChanged: syncKeepAlive()
    Connections {
        target: settingsVm
        function onCloseToTrayChanged() { win.syncKeepAlive() }
    }

    // Snackbar: short messages ("added to the queue", "this track has no album"...).
    Rectangle {
        id: snack
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: navVm.message.length > 0 ? 124 : 108
        z: 100
        width: Math.min(snackText.implicitWidth + 40 + (undoBtn.visible ? undoBtn.width : 0), parent.width - 48)
        height: 48
        radius: 12
        color: appTheme.inverseSurface
        opacity: navVm.message.length > 0 ? 1 : 0
        visible: opacity > 0
        Behavior on opacity { NumberAnimation { duration: appTheme.durationShort3; easing.type: Easing.BezierSpline; easing.bezierCurve: appTheme.easingStandard } }
        Behavior on anchors.bottomMargin { NumberAnimation { duration: appTheme.durationMedium1; easing.type: Easing.BezierSpline; easing.bezierCurve: appTheme.easingEmphasizedDecelerate } }

        Label {
            id: snackText
            anchors.left: parent.left
            anchors.leftMargin: 20
            anchors.right: undoBtn.visible ? undoBtn.left : parent.right
            anchors.rightMargin: undoBtn.visible ? 4 : 20
            anchors.verticalCenter: parent.verticalCenter
            text: navVm.message
            color: appTheme.inverseOnSurface
            font.pixelSize: 14
            elide: Text.ElideRight
        }
        MButton {
            id: undoBtn
            visible: win.undoAction !== null
            anchors.right: parent.right
            anchors.rightMargin: 8
            anchors.verticalCenter: parent.verticalCenter
            kind: "text"
            cPrimary: appTheme.inversePrimary
            text: win.tr("action.undo")
            onClicked: {
                const a = win.undoAction
                win.undoAction = null
                navVm.dismissMessage()
                if (a) a()
            }
        }
    }
}
