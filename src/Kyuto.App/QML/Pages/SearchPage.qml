import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic
import "../Components"

// Search results. vm is the SearchViewModel. The chips choose what is searched for; songs and videos play,
// albums, artists and playlists open their page (entityActivated). The status line shows while the list is empty.
Item {
    id: root

    required property var vm
    readonly property var theme: Window.window ? Window.window.theme : null
    readonly property var i18n: Window.window ? Window.window.i18n : null

    signal entityActivated(string kind, string entityId)

    function tr(key) { return i18n ? i18n.t(key) : key }

    // The best result, big, above the lists. A song plays; an album, artist or playlist opens.
    component TopResult: Item {
        property real full: 0

        width: full
        height: root.vm.hasTop ? 156 : 0
        visible: height > 0

        Rectangle {
            x: 16
            y: 8
            width: parent.width - 32
            height: 140
            radius: 24
            color: root.theme ? root.theme.surfaceContainerHigh : "#ECE6F0"

            Rectangle { // state layer
                anchors.fill: parent
                radius: parent.radius
                color: root.theme ? root.theme.inkSurface : "#1D1B20"
                opacity: topArea.pressed ? 0.10 : topArea.containsMouse ? 0.08 : 0
            }

            MouseArea {
                id: topArea
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: root.openTop()
            }

            RowLayout {
                anchors.fill: parent
                anchors.margins: 16
                spacing: 16

                Cover {
                    Layout.preferredWidth: 108
                    Layout.preferredHeight: 108
                    radius: root.vm.topKind === "artist" ? 54 : 16
                    source: root.vm.topArt
                    pixels: 216
                }
                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 4

                    Label {
                        text: root.tr("search.topResult").toUpperCase()
                        color: root.theme ? root.theme.primary : "#6750A4"
                        font.pixelSize: 12
                        font.weight: Font.Medium
                        font.letterSpacing: 1.4
                    }
                    Label {
                        Layout.fillWidth: true
                        text: root.vm.topTitle
                        color: root.theme ? root.theme.inkSurface : "#1D1B20"
                        font.pixelSize: 24
                        font.weight: Font.Medium
                        elide: Text.ElideRight
                    }
                    Label {
                        Layout.fillWidth: true
                        text: root.vm.topSubtitle
                        color: root.theme ? root.theme.inkSurfaceVariant : "#49454F"
                        font.pixelSize: 14
                        elide: Text.ElideRight
                    }
                }
                MIconButton {
                    Layout.alignment: Qt.AlignVCenter
                    visible: root.vm.topKind === "track"
                    glyph: "play"
                    filled: true
                    containerSize: 56
                    tip: root.tr("action.play")
                    onClicked: root.openTop()
                }
            }
        }
    }

    function openTop() {
        if (root.vm.topKind === "track")
            root.vm.playResult(0)
        else
            root.entityActivated(root.vm.topKind, root.vm.topId)
    }

    PickState { id: pick }
    Connections {
        target: root.vm.results
        function onModelReset() { pick.clear(); tracksView.positionViewAtBeginning() }
    }
    Connections {
        target: root.vm.entities
        function onModelReset() { entitiesView.positionViewAtBeginning() }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Flow {
            Layout.fillWidth: true
            Layout.margins: 16
            Layout.bottomMargin: 8
            spacing: 8

            Repeater {
                model: [
                    { key: "songs", label: "filter.songs" },
                    { key: "videos", label: "filter.videos" },
                    { key: "albums", label: "filter.albums" },
                    { key: "artists", label: "filter.artists" },
                    { key: "playlists", label: "filter.playlists" }
                ]
                MChip {
                    required property var modelData
                    text: root.tr(modelData.label)
                    selected: root.vm.filter === modelData.key
                    onClicked: root.vm.chooseFilter(modelData.key)
                }
            }
        }

        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            ListView {
                id: tracksView
                anchors.fill: parent
                visible: !root.vm.entityMode
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                model: root.vm.results
                header: TopResult { full: tracksView.width }
                // With no filter, the albums, artists and playlists of the search come after the songs.
                footer: Column {
                    width: tracksView.width
                    visible: root.vm.allMode && moreEntities.count > 0
                    height: visible ? implicitHeight : 0

                    Label {
                        x: 16
                        topPadding: 16
                        bottomPadding: 4
                        text: root.tr("search.more")
                        color: root.theme ? root.theme.inkSurface : "#1D1B20"
                        font.pixelSize: 16
                        font.weight: Font.Medium
                    }
                    Repeater {
                        id: moreEntities
                        model: root.vm.allMode ? root.vm.entities : null
                        delegate: TrackItem {
                            required property var model
                            width: tracksView.width
                            heading: model.title
                            supporting: model.subtitle
                            artUrl: model.artUrl
                            round: model.kind === "artist"
                            onActivated: root.entityActivated(model.kind, model.entityId)
                        }
                    }
                }
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
                    onActivated: root.vm.playResult(index)
                    onAddRequested: root.vm.addResult(index)
                    onPlayNextRequested: root.vm.playNextResult(index)
                }
            }

            ListView {
                id: entitiesView
                anchors.fill: parent
                visible: root.vm.entityMode
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                model: root.vm.entities
                header: TopResult { full: entitiesView.width }
                ScrollBar.vertical: MScrollBar {}
                delegate: TrackItem {
                    required property int index
                    required property var model
                    width: ListView.view.width
                    heading: model.title
                    supporting: model.subtitle
                    artUrl: model.artUrl
                    round: model.kind === "artist"
                    onActivated: root.entityActivated(model.kind, model.entityId)
                }
            }

            EmptyState {
                anchors.centerIn: parent
                width: parent.width - 48
                visible: (root.vm.entityMode ? entitiesView.count
                          : root.vm.allMode ? tracksView.count + entitiesView.count
                          : tracksView.count) === 0
                glyph: "search"
                busy: root.vm.searching
                title: root.vm.status
            }
        }
    }

    SelectionBar {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 16
        count: pick.count
        onQueueRequested: { root.vm.addSelected(pick.json()); Window.window.toast(root.tr("queue.added")); pick.clear() }
        onNextRequested: { root.vm.playNextSelected(pick.json()); Window.window.toast(root.tr("queue.addedNext")); pick.clear() }
        onPlaylistRequested: { root.vm.stageSelected(pick.json()); Window.window.pickPlaylistStaged(); pick.clear() }
        onCleared: pick.clear()
    }
}
