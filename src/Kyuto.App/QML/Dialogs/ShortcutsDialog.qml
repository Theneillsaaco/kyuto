import QtQuick
import QtQuick.Layouts
import QtQuick.Controls.Basic
import "../Components"

// The keyboard shortcuts of the window, opened with "?".
MDialog {
    id: dialog

    title: dialog.tr("shortcuts.title")

    readonly property var entries: [
        { keys: "Space", label: "shortcut.playPause" },
        { keys: "← / →", label: "shortcut.seek" },
        { keys: "Ctrl + → / ←", label: "shortcut.nextPrev" },
        { keys: "Ctrl + ↑ / ↓", label: "shortcut.volume" },
        { keys: "M", label: "shortcut.mute" },
        { keys: "Ctrl + F", label: "shortcut.search" },
        { keys: "Ctrl + L", label: "shortcut.queue" },
        { keys: "Ctrl + Shift + L", label: "shortcut.lyrics" },
        { keys: "Ctrl + ,", label: "shortcut.settings" },
        { keys: "Alt + ←", label: "shortcut.back" },
        { keys: "F5", label: "shortcut.reload" },
        { keys: "Esc", label: "shortcut.leaveFull" },
        { keys: "Ctrl + M", label: "shortcut.mini" },
        { keys: "Ctrl + Shift + F", label: "shortcut.lyricsFull" },
        { keys: "Ctrl + clic", label: "shortcut.select" },
        { keys: "?", label: "shortcut.help" }
    ]

    Repeater {
        model: dialog.entries

        RowLayout {
            required property var modelData

            Layout.fillWidth: true
            spacing: 16

            Label {
                Layout.fillWidth: true
                text: dialog.tr(modelData.label)
                color: dialog.cOnSurface
                font.pixelSize: 14
                elide: Text.ElideRight
            }
            Label {
                text: modelData.keys
                color: dialog.theme ? dialog.theme.inkSurfaceVariant : "#49454F"
                font.pixelSize: 13
                font.weight: Font.Medium
            }
        }
    }

    RowLayout {
        Layout.fillWidth: true
        Item { Layout.fillWidth: true }
        MButton {
            kind: "text"
            text: dialog.tr("action.close")
            onClicked: dialog.close()
        }
    }
}
