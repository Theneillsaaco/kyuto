import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic

// The bar that floats at the bottom of a list while rows are ticked: how many, then what to do with them.
// Put it in the page, anchored at the bottom centre. The page answers the signals with its view model.
Rectangle {
    id: root

    readonly property var theme: Window.window ? Window.window.theme : null
    readonly property var i18n: Window.window ? Window.window.i18n : null

    property int count: 0
    // Adding to a playlist needs a signed-in account.
    readonly property bool canPlaylist: Window.window && Window.window.canLikeId ? Window.window.canLikeId("aaaaaaaaaaa") : false

    signal queueRequested()
    signal nextRequested()
    signal playlistRequested()
    signal cleared()

    function tr(key) { return i18n ? i18n.t(key) : key }

    width: Math.min(row.implicitWidth + 40, parent ? parent.width - 32 : row.implicitWidth + 40)
    height: 56
    radius: 28
    color: theme ? theme.inverseSurface : "#313033"
    opacity: count > 0 ? 1 : 0
    visible: opacity > 0
    z: 30
    Behavior on opacity { Anim { type: Anim.FastEffects } }

    // Clicks on the bar must not reach the rows behind it.
    MouseArea { anchors.fill: parent }

    RowLayout {
        id: row
        anchors.verticalCenter: parent.verticalCenter
        anchors.left: parent.left
        anchors.leftMargin: 20
        spacing: 4

        Label {
            Layout.rightMargin: 8
            text: root.tr("select.count").replace("{0}", root.count)
            color: root.theme ? root.theme.inverseOnSurface : "#F4EFF4"
            font.pixelSize: 14
            font.weight: Font.Medium
        }
        MButton {
            kind: "text"
            text: root.tr("select.queue")
            cPrimary: root.theme ? root.theme.inversePrimary : "#D0BCFF"
            onClicked: root.queueRequested()
        }
        MButton {
            kind: "text"
            text: root.tr("select.next")
            cPrimary: root.theme ? root.theme.inversePrimary : "#D0BCFF"
            onClicked: root.nextRequested()
        }
        MButton {
            visible: root.canPlaylist
            kind: "text"
            text: root.tr("select.playlist")
            cPrimary: root.theme ? root.theme.inversePrimary : "#D0BCFF"
            onClicked: root.playlistRequested()
        }
        MIconButton {
            glyph: "close"
            tip: root.tr("select.clear")
            cOnSurfaceVariant: root.theme ? root.theme.inverseOnSurface : "#F4EFF4"
            onClicked: root.cleared()
        }
    }
}
