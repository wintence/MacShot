## Buttons stop cutting their own labels

Look at any button in 1.2.3 from close up: the bottom of the text is sheared off flat. "Add text", "Change:", "Done" — all of them were missing the last couple of pixels of every letter, and descenders on letters like "y" disappeared entirely.

The cause was in the button template. The drop shadow sat on the same element that holds the label, and that element also carried a scale transform for the press animation. WPF cannot composite an effect and a transform on one element directly, so it renders the whole button into an off-screen bitmap first — and that bitmap came out a fraction of a pixel short, clipping the content along the bottom edge.

The shadow now lives on its own layer underneath the button, with nothing inside it. The label sits outside the effect and is drawn straight to the screen. The press animation is unchanged.

## Crosses are drawn, not typed

The remove buttons in the per-app title bar list asked the font for the character `✕`. Its ink box does not line up with the text line box, so the crosses sat at slightly different heights from row to row and looked hand-placed.

They are now two drawn lines, centred geometrically. They are identical in every row, at every size, in both themes, and they brighten on hover like the rest of the interface.

## A stray dash on the primary button

The "Готово" button had a short pale line floating above its label, roughly as wide as the word. It was meant to be a full-width highlight along the top edge; it only ever spanned the width of the text. The highlight is gone. The accent fill, the border and the shadow carry the button on their own.

## Settings are tighter

No section moved, and nothing was removed — the same controls, closer together.

- Slider labels sit on the same line as their slider instead of above it. With six sliders that alone removes six rows.
- Card padding, gaps between cards, and the space between radio options are all smaller.
- The window is 1040×740 instead of 1160×820, and the minimum size dropped from 940×620 to 880×560.
- The window title, the watermarks section, and the footer all use the reclaimed room.

## Two files — pick one

| File | Size | Requires |
|---|---|---|
| `MacShot-full.exe` | 68.5 MB | nothing, .NET is inside |
| `MacShot-lite.exe` | 0.9 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

The full file is large almost entirely because of the bundled .NET — the program itself is about 800 KB.
