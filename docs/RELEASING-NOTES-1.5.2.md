# MacShot 1.5.2

## What's new

**The screen freezes while the overlay is open.** When a capture starts,
the whole screen (all monitors, physical pixels) is captured once up
front, and the overlay shows that static frame. Animations, video and
moving windows stand still while you pick a window or stretch a region —
you select on a frozen picture, not on a moving desktop. The final shot
is cropped out of that same frame, so it always matches what you saw and
selected at pick time.

**Full-screen capture.** A new mode takes the entire screen in one
keystroke, without the picker: `Ctrl+Shift+0` by default, with its own
tray menu item and a settings row to change or clear the hotkey. Clearing
it disables the hotkey; the tray item keeps working either way.

**A toast after the shot.** A small toast slides up at the bottom of the
screen when a capture is taken, so a missed click or a dismissed window
is visible immediately.

**MacShot's own windows are pickable.** Window picking used to skip the
app's own overlays; they now appear in the picker like any other window.

## Two files — pick one

| File | Size | Needs .NET | Use it when |
|---|---|---|---|
| `MacShot-lite.exe` | ~0.9 MB | .NET 8 Desktop Runtime installed | everyday machine with the runtime |
| `MacShot-full.exe` | ~69 MB | nothing | the machine may lack the runtime |

Both contain the same program; the name is the only difference.
