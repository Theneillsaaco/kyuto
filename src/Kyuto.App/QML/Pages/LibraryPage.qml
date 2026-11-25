import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic
import "../Components"

// The user's YouTube Music library: the playlists ("Me gusta" first) and, once one is open,
// its tracks with Play / Shuffle. Everything comes from the view model.
Item {
    id: root

    required property var vm
    readonly property var theme: Window.window ? Window.window.theme : null
    readonly property var i18n: Window.window ? Window.window.i18n : null
    signal settingsRequested()

    readonly property bool signedIn: vm.accountState === "signedIn"
    readonly property bool inList: signedIn && vm.openListId !== ""

    function tr(key) { return i18n ? i18n.t(key) : key }

    PickState { id: pick }
    Connections {
        target: root.vm.tracks
        function onModelReset() { pick.clear() }
    }

    MDialog {
        id: confirmDelete
        title: root.tr("playlist.delete")

        Label {
            Layout.fillWidth: true
            text: root.tr("playlist.deleteAsk").replace("{0}", root.vm.openListTitle)
            color: root.theme ? root.theme.inkSurface : "#1D1B20"
            font.pixelSize: 14
            wrapMode: Text.Wrap
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 8

            Item { Layout.fillWidth: true }

            MButton {
                kind: "text"
                text: root.tr("action.cancel")
                onClicked: confirmDelete.close()
            }
            MButton {
                text: root.tr("playlist.delete")
                onClicked: {
                    root.vm.deletePlaylist(root.vm.openListId)
                    confirmDelete.close()
                }
            }
        }
    }

    // No account connected
    EmptyState {
        anchors.centerIn: parent
        width: parent.width - 48
        visible: !root.signedIn
        glyph: "library"
        busy: root.vm.accountBusy
        title: root.vm.accountBusy || root.vm.accountState === "unavailable" ? root.vm.accountStatus
            : root.vm.accountState === "expired" ? root.tr("library.expired")
                : root.tr("library.connect")
        action: root.vm.accountState !== "unavailable" && !root.vm.accountBusy ? root.tr("action.openSettings") : ""
        onTriggered: root.settingsRequested()
    }

    // Playlists
    ListView {
        id: listsView
        anchors.fill: parent
        visible: root.signedIn && !root.inList
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        model: root.vm.library
        ScrollBar.vertical: MScrollBar {}
        delegate: TrackItem {
            required property int index
            required property var model
            width: ListView.view.width
            heading: model.title
            supporting: model.artist
            artUrl: model.artUrl
            onActivated: root.vm.openPlaylist(index)
        }
    }
    LoadingIndicator {
        anchors.centerIn: parent
        anchors.verticalCenterOffset: -44
        visible: root.signedIn && !root.inList && listsView.count === 0 && root.vm.working
    }
    Label {
        anchors.centerIn: parent
        anchors.verticalCenterOffset: root.vm.working ? 44 : 0
        width: parent.width - 48
        horizontalAlignment: Text.AlignHCenter
        wrapMode: Text.Wrap
        visible: root.signedIn && !root.inList && listsView.count === 0
        text: root.vm.libraryStatus
        color: root.theme ? root.theme.inkSurfaceVariant : "#49454F"
        font.pixelSize: 14
    }

    // One playlist
    ColumnLayout {
        anchors.fill: parent
        visible: root.inList
        spacing: 0

        RowLayout {
            Layout.fillWidth: true
            Layout.margins: 8
            spacing: 8

            MIconButton {
                glyph: "back"
                tip: root.tr("tip.back")
                onClicked: root.vm.closeList()
            }
            Label {
                Layout.fillWidth: true
                text: root.vm.openListTitle + (root.vm.listCount > 0 ? " · " + root.vm.listCount : "")
                color: root.theme ? root.theme.inkSurface : "#1D1B20"
                font.pixelSize: 16
                font.weight: Font.Medium
                elide: Text.ElideRight
            }
            MButton {
                kind: "tonal"
                text: root.tr("action.shuffle")
                enabled: root.vm.listCount > 0
                onClicked: root.vm.playList(true)
            }
            MIconButton {
                glyph: "delete"
                tip: root.tr("playlist.delete")
                visible: root.vm.openListId !== "LM"
                onClicked: confirmDelete.open()
            }
            MButton {
                text: root.tr("action.play")
                enabled: root.vm.listCount > 0
                onClicked: root.vm.playList(false)
            }
        }

        Label {
            Layout.leftMargin: 16
            Layout.bottomMargin: 4
            visible: root.vm.listStatus !== "" && tracksView.count > 0
            text: root.vm.listStatus
            color: root.theme ? root.theme.inkSurfaceVariant : "#49454F"
            font.pixelSize: 12
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.leftMargin: 16
            Layout.rightMargin: 16
            Layout.bottomMargin: 8
            spacing: 8
            visible: root.vm.listCount > 1

            MSearchField {
                id: filterField
                Layout.fillWidth: true
                implicitHeight: 44
                placeholderText: root.tr("list.filter")
                clearTip: root.tr("tip.clearSearch")
                onTextChanged: root.vm.chooseListFilter(text)
            }
            Repeater {
                model: ["title", "artist", "duration"]
                MChip {
                    required property string modelData
                    text: root.tr("sort." + modelData)
                    selected: root.vm.listSort === modelData
                    onClicked: root.vm.chooseListSort(root.vm.listSort === modelData ? "default" : modelData)
                }
            }
        }

        Connections {
            target: root.vm
            function onOpenListIdChanged() { filterField.clear() }
        }
        
        ListView {
            id: tracksView
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            boundsBehavior: Flickable.StopAtBounds
            model: root.vm.tracks
            ScrollBar.vertical: MScrollBar {}
            delegate: TrackItem {
                required property int index
                required property var model
                width: ListView.view.width
                heading: model.title
                supporting: model.artist
                trailing: model.duration
                artUrl: model.artUrl
                videoId: model.videoId
                showAdd: true
                pickable: true
                selecting: pick.active
                picked: pick.has(index)
                onPickToggled: pick.toggle(index)
                onActivated: root.vm.playListItem(index)
                onAddRequested: root.vm.addListItem(index)
                onPlayNextRequested: root.vm.playNextListItem(index)
            }
            LoadingIndicator {
                anchors.centerIn: parent
                anchors.verticalCenterOffset: -44
                visible: tracksView.count === 0 && root.vm.working
            }
            Label {
                anchors.centerIn: parent
                anchors.verticalCenterOffset: root.vm.working ? 44 : 0
                width: parent.width - 48
                horizontalAlignment: Text.AlignHCenter
                wrapMode: Text.Wrap
                visible: tracksView.count === 0
                text: root.vm.listStatus
                color: root.theme ? root.theme.inkSurfaceVariant : "#49454F"
                font.pixelSize: 14
            }
        }
    }

    SelectionBar {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 16
        visible: root.vm.openListId !== ""
        count: pick.count
        onQueueRequested: { root.vm.addSelected(pick.json()); Window.window.toast(root.tr("queue.added")); pick.clear() }
        onNextRequested: { root.vm.playNextSelected(pick.json()); Window.window.toast(root.tr("queue.addedNext")); pick.clear() }
        onPlaylistRequested: { root.vm.stageSelected(pick.json()); Window.window.pickPlaylistStaged(); pick.clear() }
        onCleared: pick.clear()
    }
}
