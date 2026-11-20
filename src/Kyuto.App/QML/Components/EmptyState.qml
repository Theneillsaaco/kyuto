import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic

// A page with nothing to show, as Meiro draws it: a 96dp tile in the secondary container with a glyph (or the
// loading shape while busy), a title, a line of explanation and an optional tonal button. It rises in when it appears.
ColumnLayout {
    id: root

    readonly property var theme: Window.window ? Window.window.theme : null

    property string glyph: "music"
    property string title: ""
    property string body: ""
    property string action: ""
    property bool busy: false

    signal triggered()

    spacing: 12
    opacity: 0
    Component.onCompleted: opacity = 1
    Behavior on opacity { Anim { type: Anim.DefaultEffects } }
    transform: Translate {
        y: root.opacity < 1 ? (1 - root.opacity) * 12 : 0
    }

    Rectangle {
        Layout.alignment: Qt.AlignHCenter
        Layout.preferredWidth: 96
        Layout.preferredHeight: 96
        Layout.bottomMargin: 4
        radius: 28
        color: root.theme ? root.theme.secondaryContainer : "#E8DEF8"

        Icon {
            anchors.centerIn: parent
            name: root.glyph
            size: 40
            tint: root.theme ? root.theme.inkSecondaryContainer : "#4A4458"
            visible: !root.busy
        }
        LoadingIndicator {
            anchors.centerIn: parent
            size: 64
            visible: root.busy
        }
    }

    Label {
        Layout.fillWidth: true
        visible: text.length > 0
        horizontalAlignment: Text.AlignHCenter
        wrapMode: Text.Wrap
        maximumLineCount: 3
        elide: Text.ElideRight
        text: root.title
        color: root.theme ? root.theme.inkSurface : "#1D1B20"
        font.pixelSize: 22
        font.weight: Font.DemiBold
    }

    Label {
        Layout.fillWidth: true
        Layout.maximumWidth: 460
        Layout.alignment: Qt.AlignHCenter
        visible: text.length > 0
        horizontalAlignment: Text.AlignHCenter
        wrapMode: Text.Wrap
        maximumLineCount: 4
        elide: Text.ElideRight
        text: root.body
        color: root.theme ? root.theme.inkSurfaceVariant : "#49454F"
        font.pixelSize: 14
        lineHeight: 1.2
    }

    MButton {
        Layout.alignment: Qt.AlignHCenter
        Layout.topMargin: 4
        visible: root.action.length > 0
        kind: "tonal"
        text: root.action
        onClicked: root.triggered()
    }
}
