import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic

// The lyrics over the whole window, on a background made from the color of the cover. vm is the LyricsViewModel.
// tint is that color (Main.qml finds it with CoverColor). Left: the cover and the controls; right: the lines, big.
// Narrow windows only show the lines. Click a line of timed lyrics to jump there. closeRequested() leaves.
Rectangle {
    id: root

    required property var vm
    required property var playback
    required property var theme
    property color tint: theme.primary
    readonly property var i18n: Window.window ? Window.window.i18n : null
    readonly property bool hasLines: vm.state === "synced" || vm.state === "plain"
    // The cover + controls column squeezed the lines into the right side on big windows, so it is off by default.
    property bool showSide: false
    readonly property bool wide: showSide && width >= 900

    property color topColor: Qt.hsla(tint.hslHue, Math.min(0.65, tint.hslSaturation), 0.27, 1)
    property color bottomColor: Qt.hsla(tint.hslHue, Math.min(0.55, tint.hslSaturation), 0.09, 1)
    Behavior on topColor { ColorAnimation { duration: 600 } }
    Behavior on bottomColor { ColorAnimation { duration: 600 } }

    signal closeRequested()

    function tr(key) { return i18n ? i18n.t(key) : key }

    // The background gradient color at a vertical fraction (0 top, 1 bottom).
    function bgAt(t) {
        const k = Math.max(0, Math.min(1, t))
        return Qt.rgba(topColor.r + (bottomColor.r - topColor.r) * k,
                       topColor.g + (bottomColor.g - topColor.g) * k,
                       topColor.b + (bottomColor.b - topColor.b) * k, 1)
    }

    gradient: Gradient {
        GradientStop { position: 0.0; color: root.topColor }
        GradientStop { position: 1.0; color: root.bottomColor }
    }

    opacity: 0
    Component.onCompleted: opacity = 1
    Behavior on opacity { Anim { type: Anim.DefaultEffects } }

    // Nothing under it receives clicks.
    MouseArea { anchors.fill: parent; acceptedButtons: Qt.AllButtons; onWheel: (wheel) => wheel.accepted = true }

    RowLayout {
        anchors.fill: parent
        anchors.margins: 32
        anchors.topMargin: 72
        spacing: 48

        ColumnLayout {
            visible: root.wide
            Layout.preferredWidth: Math.min(360, root.width * 0.32)
            Layout.fillHeight: true
            spacing: 12

            Item { Layout.fillHeight: true }
            Cover {
                Layout.fillWidth: true
                Layout.preferredHeight: width
                Layout.maximumHeight: root.height - 320
                radius: 28
                source: root.playback.currentArt
                pixels: 544
            }
            Label {
                Layout.fillWidth: true
                Layout.topMargin: 8
                text: root.playback.currentTitle
                color: "#FFFFFF"
                font.pixelSize: 24
                font.weight: Font.DemiBold
                elide: Text.ElideRight
            }
            Label {
                Layout.fillWidth: true
                text: root.playback.currentArtist
                color: "#FFFFFF"
                opacity: 0.75
                font.pixelSize: 16
                elide: Text.ElideRight
            }
            RowLayout {
                spacing: 4
                MIconButton {
                    glyph: "previous"
                    tip: root.tr("tip.previous")
                    cOnSurfaceVariant: "#FFFFFF"
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
                    cOnSurfaceVariant: "#FFFFFF"
                    enabled: root.playback.canNext
                    onClicked: root.playback.next()
                }
            }
            Item { Layout.fillHeight: true }
        }

        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            ListView {
                id: list
                // Lines keep a readable measure on wide windows and sit centred in the free space.
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                anchors.horizontalCenter: parent.horizontalCenter
                width: Math.min(parent.width, 1000)
                visible: root.hasLines
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                model: root.vm.lines
                currentIndex: root.vm.currentIndex
                highlightRangeMode: ListView.ApplyRange
                preferredHighlightBegin: height * 0.30
                preferredHighlightEnd: height * 0.55
                highlightMoveDuration: 450
                highlightFollowsCurrentItem: true
                topMargin: 24
                bottomMargin: height * 0.5
                ScrollBar.vertical: MScrollBar {}

                header: Item {
                    width: list.width
                    height: root.vm.countdown > 0 ? 56 : 0
                    visible: height > 0

                    Row {
                        anchors.verticalCenter: parent.verticalCenter
                        spacing: 12

                        Repeater {
                            model: 3
                            Rectangle {
                                required property int index
                                readonly property bool lit: root.vm.countdown <= 3 - index
                                width: 12
                                height: 12
                                radius: 6
                                color: "#FFFFFF"
                                opacity: lit ? 1 : 0.3
                                scale: lit ? 1.15 : 0.85
                                Behavior on opacity { Anim { type: Anim.FastEffects } }
                                Behavior on scale { Anim { type: Anim.FastSpatial } }
                            }
                        }
                    }
                }

                delegate: Item {
                    id: row

                    required property int index
                    required property var model
                    readonly property bool current: root.vm.synced && index === root.vm.currentIndex
                    readonly property int distance: Math.abs(index - root.vm.currentIndex)

                    width: ListView.view.width
                    height: textColumn.implicitHeight + 18

                    Column {
                        id: textColumn
                        width: parent.width
                        anchors.verticalCenter: parent.verticalCenter
                        spacing: 4
                        opacity: row.current || !root.vm.synced ? 1 : Math.max(0.16, 0.5 - 0.09 * (row.distance - 1))
                        scale: row.current || !root.vm.synced ? 1 : 0.95
                        transformOrigin: Item.Left
                        Behavior on opacity { Anim { type: Anim.DefaultEffects } }
                        Behavior on scale { Anim { type: Anim.DefaultSpatial } }

                        Label {
                            width: parent.width
                            text: row.model.text
                            wrapMode: Text.WrapAtWordBoundaryOrAnywhere
                            font.pixelSize: root.vm.fontSize + 14
                            font.weight: Font.Bold
                            color: "#FFFFFF"
                        }
                        Label {
                            visible: text.length > 0
                            width: parent.width
                            text: row.model.sub
                            wrapMode: Text.WrapAtWordBoundaryOrAnywhere
                            font.pixelSize: Math.round(root.vm.fontSize * 0.85)
                            color: "#FFFFFF"
                            opacity: 0.7
                        }
                    }
                    MouseArea {
                        anchors.fill: parent
                        enabled: root.vm.synced
                        cursorShape: enabled ? Qt.PointingHandCursor : Qt.ArrowCursor
                        onClicked: root.vm.seekToLine(row.index)
                    }
                }
            }

            // Soft edges: overlays take the background color at their own height, so they blend with the gradient.
            Rectangle {
                id: topFade
                anchors.top: parent.top
                anchors.left: parent.left
                anchors.right: parent.right
                height: 72
                visible: root.hasLines
                readonly property color c: root.bgAt((parent.mapToItem(root, 0, 0).y) / root.height)
                gradient: Gradient {
                    GradientStop { position: 0.0; color: topFade.c }
                    GradientStop { position: 0.35; color: Qt.rgba(topFade.c.r, topFade.c.g, topFade.c.b, 0.7) }
                    GradientStop { position: 1.0; color: Qt.rgba(topFade.c.r, topFade.c.g, topFade.c.b, 0) }
                }
            }
            Rectangle {
                id: bottomFade
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                height: 120
                visible: root.hasLines
                readonly property color c: root.bgAt((parent.mapToItem(root, 0, parent.height).y) / root.height)
                gradient: Gradient {
                    GradientStop { position: 0.0; color: Qt.rgba(bottomFade.c.r, bottomFade.c.g, bottomFade.c.b, 0) }
                    GradientStop { position: 0.65; color: Qt.rgba(bottomFade.c.r, bottomFade.c.g, bottomFade.c.b, 0.7) }
                    GradientStop { position: 1.0; color: bottomFade.c }
                }
            }

            ColumnLayout {
                anchors.centerIn: parent
                width: parent.width
                visible: !root.hasLines
                spacing: 16

                LoadingIndicator {
                    Layout.alignment: Qt.AlignHCenter
                    visible: root.vm.loading
                    color: "#FFFFFF"
                }
                Label {
                    Layout.fillWidth: true
                    horizontalAlignment: Text.AlignHCenter
                    wrapMode: Text.Wrap
                    text: root.vm.message
                    color: "#FFFFFF"
                    opacity: 0.8
                    font.pixelSize: 18
                }
                MButton {
                    Layout.alignment: Qt.AlignHCenter
                    visible: root.vm.state === "error"
                    kind: "tonal"
                    text: root.tr("action.retry")
                    onClicked: root.vm.retry()
                }
            }
        }
    }

    // Top bar: where the lyrics come from on the left; text size, extra lines and leaving on the right.
    RowLayout {
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.margins: 16
        spacing: 4

        Label {
            Layout.fillWidth: true
            Layout.leftMargin: 8
            text: root.vm.source.length > 0 ? root.tr("lyrics.title") + " · " + root.vm.source : root.tr("lyrics.title")
            color: "#FFFFFF"
            opacity: 0.85
            font.pixelSize: 16
            font.weight: Font.Medium
            elide: Text.ElideRight
        }
        MButton { visible: root.hasLines; kind: "text"; text: "A−"; cPrimary: "#FFFFFF"; onClicked: root.vm.shrinkText() }
        MButton { visible: root.hasLines; kind: "text"; text: "A+"; cPrimary: "#FFFFFF"; onClicked: root.vm.growText() }
        MButton { visible: root.hasLines; kind: "text"; text: root.tr("lyrics.extra." + root.vm.extraMode); cPrimary: "#FFFFFF"; onClicked: root.vm.cycleExtra() }
        MIconButton {
            glyph: "fullscreen_exit"
            tip: root.tr("lyrics.leaveFull")
            cOnSurfaceVariant: "#FFFFFF"
            onClicked: root.closeRequested()
        }
    }
}
