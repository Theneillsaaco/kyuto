import QtQuick

// The rows ticked in one list. Row indexes are the ones of the model the list shows. active: something is ticked,
// so the list is in selection mode (a plain click ticks instead of playing). json() is what the view models take.
QtObject {
    id: root

    property var picked: ({})
    readonly property int count: Object.keys(picked).length
    readonly property bool active: count > 0

    function has(index) { return picked[index] === true }

    function toggle(index) {
        const next = Object.assign({}, picked)
        if (next[index]) delete next[index]
        else next[index] = true
        picked = next
    }

    function clear() { picked = ({}) }

    function json() { return JSON.stringify(Object.keys(picked).map(Number)) }
}
