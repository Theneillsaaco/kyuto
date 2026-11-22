import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic

// Material 3 two-line list item with a cover. Click plays or opens; right click or the ⋮ button opens a menu.
// The menu is built the first time it is opened (a Popup with five items per row is heavy while scrolling).
// showAdd: the item can be added to the queue (a + button and "play next" / "add to queue" in the menu).
// showRemove: the menu also offers "remove from queue". draggable: a grip on the left that reorders (the queue).
// With a videoId the second line is a link to the artist's page. round: circular cover (artists).
// The current row shows dancing bars over its cover; they move while Window.window.playingNow is true.
Item {
    id: row

    readonly property var theme: Window.window ? Window.window.theme : null
    readonly property var i18n: Window.window ? Window.window.i18n : null

    property string heading: ""
    property string supporting: ""
    property string trailing: ""
    property string artUrl: ""
    property string videoId: ""
    property bool current: false
    property bool showAdd: false
    property bool showRemove: false
    property bool draggable: false
    property bool dimmed: false
    property bool round: false
    property color backdrop: theme ? theme.surfaceContainer : "#F2ECF4"
    property color cPrimary: theme ? theme.primary : "#6750A4"
    property color cOnSurface: theme ? theme.inkSurface : "#1D1B20"
    property color cOnSurfaceVariant: theme ? theme.inkSurfaceVariant : "#49454F"
    property color cSecondaryContainer: theme ? theme.secondaryContainer : "#E8DEF8"
    property color cOnSecondaryContainer: theme ? theme.inkSecondaryContainer : "#1D192B"

    readonly property bool playing: current && Window.window && Window.window.playingNow === true
    readonly property bool linkable: videoId !== "" && supporting !== ""
    readonly property bool hasId: /^[A-Za-z0-9_-]{11}$/.test(videoId)
    readonly property bool hasMenu: showAdd || showRemove || canLike || hasId
    // A real YouTube id and a signed-in account (Main.qml decides). `liked` is read when the menu opens.
    readonly property bool canLike: {
        const w = row.Window.window
        return videoId !== "" && w && w.canLikeId ? w.canLikeId(videoId) : false
    }
    property bool liked: false
    
    signal activated()
    signal addRequested()
    signal playNextRequested()
    signal removeRequested()
    // The grip of a draggable item: positions are in window coordinates.
    signal gripPressed()
    signal gripMoved(real sceneX, real sceneY)
    signal gripReleased(bool dropped)

    function tr(key) { return i18n ? i18n.t(key) : key }
    function toast(key) {
        if (Window.window && Window.window.toast)
            Window.window.toast(row.tr(key))
    }

    // Opens the menu next to `target` (the ⋮ button) or at the pointer when there is none.
    function openMenu(target, x, y) {
        if (!menuHost.item)
            menuHost.sourceComponent = menuComp
        if (target)
            menuHost.item.popup(target, x, y)
        else
            menuHost.item.popup()
    }

    function moveFocus(delta) {
        const v = ListView.view
        if (!v || !v.indexAt)
            return
        const i = v.indexAt(row.x + 1, row.y + 1) + delta
        if (i < 0 || i >= v.count)
            return
        v.positionViewAtIndex(i, ListView.Contain)
        const target = v.itemAtIndex(i)
        if (target)
            target.forceActiveFocus(Qt.TabFocusReason)
    }

    implicitHeight: 64
    opacity: dimmed ? 0.4 : 1
    activeFocusOnTab: true
    Accessible.role: Accessible.ListItem
    Accessible.name: heading + (supporting !== "" ? ", " + supporting : "")
    Accessible.onPressAction: row.activated()

    Keys.onReturnPressed: row.activated()
    Keys.onEnterPressed: row.activated()
    Keys.onDownPressed: moveFocus(1)
    Keys.onUpPressed: moveFocus(-1)
    Keys.onMenuPressed: if (hasMenu) row.openMenu(more, 0, more.height)
    Keys.onPressed: (event) => {
        if (event.key === Qt.Key_Delete && row.showRemove) {
            row.removeRequested()
            event.accepted = true
        } else if (event.key === Qt.Key_F10 && (event.modifiers & Qt.ShiftModifier) && row.hasMenu) {
            row.openMenu(more, 0, more.height)
            event.accepted = true
        }
    }
    
    Rectangle {
        anchors.fill: parent
        anchors.leftMargin: 8
        anchors.rightMargin: 8
        anchors.topMargin: 2
        anchors.bottomMargin: 2
        radius: 16
        color: row.current ? row.cSecondaryContainer : "transparent"
        Behavior on color { ColorAnimation { duration: 200 } }

        Rectangle { // state layer
            anchors.fill: parent
            radius: parent.radius
            color: row.current ? row.cOnSecondaryContainer : row.cOnSurface
            opacity: area.pressed ? 0.10 : area.containsMouse ? 0.08 : 0
            Behavior on opacity { NumberAnimation { duration: 100 } }
        }

        Rectangle { // focus ring
            anchors.fill: parent
            anchors.leftMargin: 8
            anchors.rightMargin: 8
            anchors.topMargin: 2
            anchors.bottomMargin: 2
            radius: 16
            color: "transparent"
            border.width: 3
            border.color: row.cPrimary
            visible: row.activeFocus
        }
    }

    MouseArea {
        id: area
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton | Qt.RightButton
        cursorShape: Qt.PointingHandCursor
        onClicked: (mouse) => {
            if (mouse.button === Qt.RightButton && row.hasMenu)
                row.openMenu(null, 0, 0)
            else if (mouse.button === Qt.LeftButton)
                row.activated()
        }
    }

    RowLayout {
        anchors.fill: parent
        anchors.leftMargin: row.draggable ? 8 : 16
        anchors.rightMargin: 16
        spacing: 12

        Item { // grip
            visible: row.draggable
            Layout.preferredWidth: 28
            Layout.fillHeight: true

            Icon {
                anchors.centerIn: parent
                name: "drag"
                size: 20
                tint: row.cOnSurfaceVariant
            }
            MouseArea {
                id: grip
                anchors.fill: parent
                cursorShape: pressed ? Qt.ClosedHandCursor : Qt.OpenHandCursor
                preventStealing: true
                hoverEnabled: true
                ToolTip.visible: containsMouse && !pressed
                ToolTip.text: row.tr("tip.drag")
                ToolTip.delay: 600
                onPressed: row.gripPressed()
                onPositionChanged: (mouse) => {
                    if (!pressed) return
                    const p = grip.mapToItem(null, mouse.x, mouse.y)
                    row.gripMoved(p.x, p.y)
                }
                onReleased: row.gripReleased(true)
                onCanceled: row.gripReleased(false)
            }
        }

        Cover {
            id: coverArt
            Layout.preferredWidth: 48
            Layout.preferredHeight: 48
            radius: row.round ? 24 : 12
            source: row.artUrl

            // The current row: a scrim with dancing bars over the cover.
            Rectangle {
                anchors.fill: parent
                radius: coverArt.radius
                color: Qt.rgba(0, 0, 0, 0.45)
                opacity: row.current ? 1 : 0
                visible: opacity > 0
                Behavior on opacity { Anim { type: Anim.FastEffects } }

                EqualizerBars {
                    anchors.centerIn: parent
                    running: row.playing
                    color: "#FFFFFF"
                }
            }
        }

        ColumnLayout {
            id: texts
            Layout.fillWidth: true
            spacing: 2

            Label {
                Layout.fillWidth: true
                text: row.heading
                color: row.current ? row.cOnSecondaryContainer : row.cOnSurface
                font.pixelSize: 16
                font.weight: row.current ? Font.Medium : Font.Normal
                elide: Text.ElideRight
            }

            Label {
                id: sub
                Layout.fillWidth: true
                text: row.supporting
                color: row.linkable && link.containsMouse ? row.cPrimary
                    : row.current ? row.cOnSecondaryContainer : row.cOnSurfaceVariant
                font.pixelSize: 14
                font.underline: row.linkable && link.containsMouse
                elide: Text.ElideRight

                MouseArea {
                    id: link
                    width: Math.min(sub.implicitWidth, sub.width)
                    height: sub.height
                    enabled: row.linkable
                    hoverEnabled: true
                    cursorShape: row.linkable ? Qt.PointingHandCursor : Qt.ArrowCursor
                    onClicked: {
                        if (Window.window && Window.window.openArtistOf)
                            Window.window.openArtistOf(row.videoId)
                    }
                }
            }
        }

        Label {
            visible: row.trailing !== ""
            Layout.preferredWidth: 44
            horizontalAlignment: Text.AlignRight
            text: row.trailing
            color: row.current ? row.cOnSecondaryContainer : row.cOnSurfaceVariant
            font.pixelSize: 12
        }

        MIconButton {
            visible: row.showAdd
            glyph: "add"
            tip: row.tr("tip.add")
            onClicked: { row.addRequested(); row.toast("queue.added") }
        }

        MIconButton {
            id: more
            visible: row.hasMenu
            glyph: "more"
            tip: row.tr("tip.more")
            onClicked: row.openMenu(more, 0, more.height)
        }
    }

    // The menu only exists once someone opens it.
    Loader { id: menuHost }

    Component {
        id: menuComp

        MMenu {
            onAboutToShow: row.liked = row.Window.window && row.Window.window.isLiked ? row.Window.window.isLiked(row.videoId) : false

            MMenuItem {
                visible: row.showAdd
                glyph: "playlist_add"
                text: row.tr("menu.playNext")
                onTriggered: { row.playNextRequested(); row.toast("queue.addedNext") }
            }
            MMenuItem {
                visible: row.showAdd
                glyph: "add"
                text: row.tr("menu.addToQueue")
                onTriggered: { row.addRequested(); row.toast("queue.added") }
            }
            MMenuItem {
                visible: row.canLike
                glyph: row.liked ? "favorite" : "favorite_border"
                text: row.liked ? row.tr("menu.unlike") : row.tr("menu.like")
                onTriggered: {
                    row.toast(row.liked ? "like.removed" : "like.added")
                    const w = row.Window.window
                    if (w) w.toggleLikeOf(row.videoId)
                }
            }
            MMenuItem {
                visible: row.canLike
                glyph: "playlist_add"
                text: row.tr("menu.addToPlaylist")
                onTriggered: {
                    const w = row.Window.window
                    if (w && w.pickPlaylist) w.pickPlaylist(row.videoId)
                }
            }
            MMenuItem {
                visible: row.showRemove
                glyph: "delete"
                text: row.tr("menu.removeFromQueue")
                onTriggered: row.removeRequested()
            }
            MMenuItem {
                visible: row.hasId
                glyph: "album"
                text: row.tr("menu.goAlbum")
                onTriggered: if (row.Window.window) row.Window.window.openAlbumOf(row.videoId)
            }
            MMenuItem {
                visible: row.hasId
                glyph: "person"
                text: row.tr("menu.goArtist")
                onTriggered: if (row.Window.window) row.Window.window.openArtistOf(row.videoId)
            }
            MMenuItem {
                visible: row.hasId
                glyph: "radio"
                text: row.tr("menu.radio")
                onTriggered: if (row.Window.window) row.Window.window.startRadio(row.videoId)
            }
            MMenuItem {
                visible: row.hasId
                glyph: "link"
                text: row.tr("menu.copyLink")
                onTriggered: {
                    row.Window.window.copyText("https://music.youtube.com/watch?v=" + row.videoId)
                    row.toast("link.copied")
                }
            }
        }
    }
}
