import QtQuick

// Remembers where a page was scrolled, per page key, and puts it back when that page is shown again.
// view: the Flickable of the page. key: nav.pageKey. Call contentReady() each time the rows of a page have just
// been put in place; the position is restored a moment later, once the layout has its final height.
Item {
    id: mem

    required property Flickable view
    required property string key

    readonly property var store: Window.window ? Window.window.scrollMemory : null
    property string tracked: ""
    property real wanted: -1

    function contentReady() { restoreTimer.restart() }

    onKeyChanged: {
        tracked = ""
        wanted = store && store[key] !== undefined ? store[key] : -1
    }

    Connections {
        target: mem.view
        function onContentYChanged() {
            if (mem.store && mem.tracked !== "" && mem.tracked === mem.key)
                mem.store[mem.tracked] = mem.view.contentY - mem.view.originY
        }
    }

    Timer {
        id: restoreTimer
        interval: 40
        onTriggered: {
            if (mem.view.contentHeight <= 1)
                return // still the empty page of a load in progress
            mem.tracked = mem.key
            if (mem.wanted >= 0) {
                mem.view.contentY = mem.view.originY + mem.wanted
                mem.wanted = -1
            }
        }
    }
}
