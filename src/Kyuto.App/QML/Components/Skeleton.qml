import QtQuick

// A grey block that pulses softly: the shape of something that is still loading.
Rectangle {
    id: root

    readonly property var theme: Window.window ? Window.window.theme : null

    color: theme ? theme.surfaceContainerHighest : "#E6E0E9"
    radius: 8

    SequentialAnimation on opacity {
        running: root.visible
        loops: Animation.Infinite
        NumberAnimation { to: 0.45; duration: 700; easing.type: Easing.InOutSine }
        NumberAnimation { to: 1; duration: 700; easing.type: Easing.InOutSine }
    }
}
