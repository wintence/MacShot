![MacShot settings](https://raw.githubusercontent.com/wintence/MacShot/main/docs/screenshot.png)

## Settings, rebuilt

The settings window now looks and behaves like macOS System Settings: sections on the left, the chosen section's controls on the right, the preview always in place.

- **Four sections** — Background, Window, Watermarks, General — replace one long scroll. Only the right column changes when a section is picked; the preview stays where it is, so the watermark remains in sight while its padding and corners are adjusted.
- **Search** finds a setting by its name in either language. Results replace the section list, Enter opens the first one, Esc clears the field.
- **Cards** group related settings under a heading. Switches are iOS-style pills, sliders are thin lines with a round handle, separators are hairlines.
- **Traffic lights** moved to the top-left corner of the sidebar and do what they do everywhere else: green maximises, yellow minimises, red closes.
- Appearance is picked from two thumbnails, and the language from two rows, instead of dropdowns.
- The window is 960×600 — the old one left a lot of empty space.
- Version and the GitHub link sit at the bottom of the sidebar. The build stamp is gone: it existed to tell two local builds apart, which is a developer's need, not a user's.

## Marks moved into the card

Adding a watermark mark and editing it used to happen under the preview. Both are on the Watermarks page now: the add buttons, the list and the editor live in one card, and the separator disappears while the list is empty.

## Monochrome section icons

The icons next to Background, Window, Watermarks and General are drawn outlines — a photo, a window, a "T", a gear — instead of coloured squares with symbols. They take the colour of their label, white on the selected row, so one set works in both themes.

## Dialogs

The hotkey dialog and the colour picker got the same treatment: drawn title bar with a red dot, system-rounded corners, theme colours, no light stripe left anywhere. The hotkey is written the way it is printed on the key: `Ctrl+=`, not `Ctrl+OemPlus`.

## Unchanged

Capture, the hotkey, the tray and the clipboard work as before, and a settings file from 1.2.4 loads as it is.

## Two files — pick one

| File | Size | Requires |
|---|---|---|
| `MacShot-full.exe` | 68.5 MB | nothing, .NET is inside |
| `MacShot-lite.exe` | 0.9 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

The full file is large almost entirely because of the bundled .NET — the program itself is under 1 MB.
