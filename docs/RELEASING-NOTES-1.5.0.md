# MacShot 1.5.0

## What's new

**Area capture, next to the window shot.** Until now every screenshot was
a whole window. The tray gets a second item — "Capture area" — and its own
hotkey (`Ctrl+Shift+9` by default): the screen dims, you stretch a rectangle
with the mouse, a small chip shows the size, and on release the fragment
is captured with pixel-perfect resolution. `Esc` cancels at any moment;
a plain click without stretching is treated as a cancel too.

The area goes through the same styling as a window screenshot: padding,
rounded corners, shadow and your background or watermark, per your settings.
Two differences by design: nothing is cut from the top (a screen fragment
has no title bar) and no app name caption is drawn (the area belongs to no
window).

The crop is taken against the DPI scale of the monitor the selection lies
on, so padding and shadow come out the same physical size as on a window
shot when displays run at 150–200%.

Everything is settable: "Settings → General" has a second hotkey row,
"Area selection", changeable like the window one.

## Two files — pick one

| File | Size | Needs .NET | Use it when |
|---|---|---|---|
| `MacShot-lite.exe` | ~0.9 MB | .NET 8 Desktop Runtime installed | everyday machine with the runtime |
| `MacShot-full.exe` | ~69 MB | nothing | the machine may lack the runtime |

Both contain the same program; the name is the only difference.
