import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic

// The small window: only the cover and the controls. Main.qml shrinks the window to it and puts it over everything.
// pinned: the window stays above the others (not every compositor lets it). expandRequested(): back to the full window.
Rectangle {
    id: root

    required property var playback
    required property var library
    required property var theme
    readonly property var i18n: Window.window ? Window.window.i18n : null

    property bool pinned: false

    signal expandRequested()
    signal pinToggled()

    function tr(key) { return i18n ? i18n.t(key) : key }

    function clock(seconds) {
        const total = Math.max(0, Math.floor(seconds))
        const rest = total % 60
        return Math.floor(total / 60) + ":" + (rest < 10 ? "0" : "") + rest
    }

    color: theme.surfaceContainerLow

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 12
        spacing: 6

        RowLayout {
            Layout.fillWidth: true
            spacing: 4

            MIconButton {
                glyph: "push_pin"
                tip: root.tr("mini.pin")
                selected: root.pinned
                onClicked: root.pinToggled()
            }
            Item { Layout.fillWidth: true }
            MIconButton {
                glyph: "open_in_full"
                tip: root.tr("mini.expand")
                onClicked: root.expandRequested()
            }
        }

        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true
            Layout.minimumHeight: 64

            Cover {
                anchors.centerIn: parent
                width: Math.max(64, Math.min(parent.width, parent.height))
                height: width
                radius: 24
                source: root.playback.currentArt
                pixels: 544
            }
            MouseArea {
                anchors.fill: parent
                cursorShape: Qt.PointingHandCursor
                onClicked: root.playback.toggle()
            }
        }

        Label {
            Layout.fillWidth: true
            horizontalAlignment: Text.AlignHCenter
            text: root.playback.currentTitle
            color: root.theme.inkSurface
            font.pixelSize: 18
            font.weight: Font.Medium
            elide: Text.ElideRight
        }
        Label {
            Layout.fillWidth: true
            horizontalAlignment: Text.AlignHCenter
            text: root.playback.currentArtist
            color: root.theme.inkSurfaceVariant
            font.pixelSize: 13
            elide: Text.ElideRight
        }

        RowLayout {
            Layout.fillWidth: true
            spacing: 8

            Label { text: root.playback.positionText; color: root.theme.inkSurfaceVariant; font.pixelSize: 12; font.features: ({ "tnum": 1 }) }
            MSlider {
                Layout.fillWidth: true
                from: 0
                to: Math.max(1, root.playback.duration)
                value: root.playback.position
                enabled: root.playback.isLoaded && root.playback.duration > 0
                expressive: true
                playing: root.playback.isLoaded && !root.playback.paused && !root.playback.audioWaiting
                valueLabel: root.clock(value)
                hoverFormat: (v) => root.clock(v)
                onChangedByUser: (v) => root.playback.seek(v)
            }
            Label { text: root.playback.durationText; color: root.theme.inkSurfaceVariant; font.pixelSize: 12; font.features: ({ "tnum": 1 }) }
        }

        RowLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 4

            MIconButton {
                glyph: "shuffle"
                tip: root.tr("tip.shuffle")
                selected: root.playback.shuffle
                onClicked: root.playback.toggleShuffle()
            }
            MIconButton {
                glyph: "previous"
                tip: root.tr("tip.previous")
                enabled: root.playback.canPrevious
                onClicked: root.playback.previous()
            }
            MIconButton {
                glyph: root.playback.isLoaded && !root.playback.paused ? "pause" : "play"
                tip: root.playback.isLoaded && !root.playback.paused ? root.tr("tip.pause") : root.tr("tip.play")
                filled: true
                containerSize: 48
                onClicked: root.playback.toggle()
            }
            MIconButton {
                glyph: "next"
                tip: root.tr("tip.next")
                enabled: root.playback.canNext
                onClicked: root.playback.next()
            }
            MIconButton {
                glyph: root.playback.repeatMode === "one" ? "repeat_one" : "repeat"
                tip: root.tr("tip.repeat")
                selected: root.playback.repeatMode !== "off"
                onClicked: root.playback.cycleRepeat()
            }
            MIconButton {
                visible: root.library.canLike
                glyph: root.library.liked ? "favorite" : "favorite_border"
                tip: root.tr("tip.like")
                selected: root.library.liked
                onClicked: root.library.toggleLike()
            }
        }
    }
}
