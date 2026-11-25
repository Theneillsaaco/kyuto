import QtQuick

// Placeholder for Home: two shelves, each a title and a strip of cards.
Column {
    id: root

    Repeater {
        model: 2

        Item {
            width: root.width
            height: 64 + 148 + 56 + 20

            Skeleton { x: 24; y: 22; width: 220; height: 24 }

            Row {
                x: 24
                y: 64
                spacing: 16

                Repeater {
                    model: Math.max(2, Math.floor((root.width - 24) / 164))

                    Column {
                        spacing: 8

                        Skeleton { width: 148; height: 148; radius: 20 }
                        Skeleton { width: 110; height: 14 }
                        Skeleton { width: 70; height: 12 }
                    }
                }
            }
        }
    }
}
