## The window title now belongs to the app

The system title bar was white on a dark window. Windows 11 can be asked to colour it (`DWMWA_CAPTION_COLOR`), and it worked — when the call came from another process. From inside the app the same call did nothing: Windhawk's `win11-power-buttons` mod is injected into the process and redraws the caption, and it wins. Repeating the call every 700 ms did not help either.

So the title bar is drawn by MacShot now. The app icon, the name and the three dots are its own, the colour is the theme's, and the window changes with the theme like everything else. The dots keep the same colours and the same order as the system ones: green maximises, yellow minimises, red closes. The hotkey and colour dialogs got the same treatment, so there is no light stripe left anywhere.

## Buttons stop cutting their own labels

Look at any button in 1.2.3 from close up: the bottom of the text is sheared off flat. "Add text", "Change…", "Done" — all of them were missing the last couple of pixels of every letter, and descenders on letters like "y" disappeared entirely.

The cause was in the button template. The drop shadow sat on the same element that holds the label, and that element also carried a scale transform for the press animation. WPF cannot composite an effect and a transform on one element directly, so it renders the whole button into an off-screen bitmap first — and that bitmap came out a fraction of a pixel short, clipping the content along the bottom edge.

The shadow now lives on its own empty layer underneath the button. The label sits outside the effect and is drawn straight to the screen. The press animation is unchanged.

## Hover covers the whole button

The hover tint was drawn inside the button's padding, so it coloured a rectangle around the label and left the button's edges untouched. It now sits over the whole button, under the frame.

## Crosses are drawn, not typed

The remove buttons in the per-app title bar list asked the font for the character `✕`. Its ink box does not line up with the text line box, so the crosses sat at slightly different heights from row to row and looked hand-placed.

They are now two drawn lines, centred geometrically. They are identical in every row, at every size, in both themes, and they brighten on hover like the rest of the interface.

## A stray dash on the primary button

The "Done" button had a short pale line floating above its label, roughly as wide as the word. It was meant to be a full-width highlight along the top edge; it only ever spanned the width of the text. The highlight is gone. The accent fill, the border and the shadow carry the button on their own.

## The settings window is smaller and quieter

No setting was removed, and nothing moved to another place.

- The window is 940×700 instead of 1160×820, and the minimum size dropped to 780×520.
- Slider labels sit on the same line as their slider instead of above it. With six sliders that alone removes six rows.
- The hotkey field is 170 pixels wide instead of filling the card.
- The "WATERMARK" heading above the watermark buttons, the "nothing added yet" line under them, and the two explanations under the storage checkboxes are gone — the checkboxes say what they do.
- The version and the repository link no longer sit in a card; they are plain text next to the "Done" button, which now stands on its own instead of leaning on the card's corner.
- The app list rows lost 30 pixels of slider, so file names are not cut off any more.

## Two files — pick one

| File | Size | Requires |
|---|---|---|
| `MacShot-full.exe` | 68.5 MB | nothing, .NET is inside |
| `MacShot-lite.exe` | 0.9 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

The full file is large almost entirely because of the bundled .NET — the program itself is about 800 KB.
