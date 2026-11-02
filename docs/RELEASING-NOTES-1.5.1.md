# MacShot 1.5.1

## What's fixed

**The picker no longer clicks through to the window underneath.** While you
were choosing a window or stretching an area, the mouse still reached the
application below the dimmed overlay: text on a web page got selected, items
in a list highlighted, content in an editor dragged around. The overlays now
keep input to themselves and merely read the cursor position; the window under
the cursor is found by walking the Z-order instead of letting the click land
on it.

Picking works exactly as before otherwise: move the mouse to highlight a
window, click to capture, `Esc` to cancel.

## Two files — pick one

| File | Size | Needs .NET | Use it when |
|---|---|---|---|
| `MacShot-lite.exe` | ~0.9 MB | .NET 8 Desktop Runtime installed | everyday machine with the runtime |
| `MacShot-full.exe` | ~69 MB | nothing | the machine may lack the runtime |

Both contain the same program; the name is the only difference.
