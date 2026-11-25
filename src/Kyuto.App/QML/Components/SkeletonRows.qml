import QtQuick

// Placeholder song rows (cover and two lines), optionally under a placeholder page header (cover, title, buttons).
// round: the header cover is a circle (artists).
Column {
    id: root

    property int rows: 8
    property bool header: false
    property bool round: false

    Item {
        visible: root.header
        width: root.width
        height: root.header ? 248 : 0

        Skeleton {
            x: 24
            y: 32
            width: root.round ? 136 : 184
            height: width
            radius: root.round ? width / 2 : 20
        }
        Skeleton { x: (root.round ? 136 : 184) + 48; y: 70; width: 260; height: 28 }
        Skeleton { x: (root.round ? 136 : 184) + 48; y: 112; width: 160; height: 16 }
        Skeleton { x: (root.round ? 136 : 184) + 48; y: 148; width: 220; height: 40; radius: 20 }
    }

    Repeater {
        model: root.rows

        Item {
            required property int index

            width: root.width
            height: 64

            Skeleton { x: 16; y: 8; width: 48; height: 48; radius: 12 }
            Skeleton { x: 76; y: 15; width: Math.min(root.width * 0.4, 180 + (index % 3) * 50); height: 14 }
            Skeleton { x: 76; y: 38; width: Math.min(root.width * 0.25, 110 + (index % 2) * 40); height: 12 }
        }
    }
}
