# MacShot

Window screenshots in the macOS style for Windows.

![Windows](https://img.shields.io/badge/Windows-10%20|%2011-blue) ![.NET](https://img.shields.io/badge/.NET-8-purple) ![License](https://img.shields.io/badge/license-MIT-green)

![MacShot settings](docs/screenshot.jpg)

## Watermarks

Add a signature, a logo, or both — as many as you like. Everything is drawn onto the finished frame, so the result is a single flat PNG.

- **Any number of items.** Texts and pictures live in one list; add and remove them as needed.
- **Position on the preview itself.** Nine squares sit on the frame — click one to move the selected item there, or just drag the item. Occupied squares disappear so they never lie on top of your content.
- **They step aside for each other.** If a text and a picture are both at bottom center, they line up side by side instead of covering one another.
- **Text:** font, size, weight, italics, color, opacity, margin from the edge, and a shadow with its own color, blur and opacity.
- **Picture:** height (width follows the proportions), opacity, margin from the edge.

## Settings

| Option | Default |
|---|---|
| Background | two-color gradient at an angle |
| Padding | 40 |
| Shadow | on: blur 36, offset down 16, density 0.45 |
| Background color | palette of 29 swatches plus the system color dialog |
| Watermark | off |
| Hotkey | `Ctrl+Shift+8` |
| Language | English or Russian |
| Save a copy to Pictures | off |
| Start when I sign in to Windows | off |

The settings window shows a live preview on the left and the controls on the right, so every change is visible before you take a screenshot. The preview uses the same rendering code as the real capture — it cannot show you something different from what you get.

Worth knowing:

- **The shadow adapts on its own.** On a dark background it turns light, on a light one it turns dark. Otherwise the window either sticks to the backdrop or disappears into it.
- **Corner radius is not configurable** — it is fixed, like on macOS.
- **Padding is exactly what you set.** At small values the shadow gets cut off by the frame edge.
- **Watermark margins are measured from the frame edge**, not from the window. With a text in a corner the margin therefore decides whether the signature lands on the backdrop or on the window itself.

## Installation

Download from [Releases](https://github.com/wintence/MacShot/releases) and run it with a double click. Nothing to install.

| File | Size | Requires |
|---|---|---|
| `MacShot-full.exe` | 64 MB | nothing, .NET is inside |
| `MacShot-lite.exe` | 0.8 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |

The full file is large almost entirely because of the bundled .NET — the program itself is about 800 KB. Details in [docs/HOW-IT-WORKS.md](docs/HOW-IT-WORKS.md).

## Usage

1. Press the hotkey — `Ctrl+Shift+8` by default.
2. The screen dims and the window under the cursor is highlighted.
3. Click the window you want — the screenshot goes to the clipboard.
4. `Esc` cancels.

Right-click the tray icon: **Capture window**, **Settings**, **Exit**.

## Where files live

| What | Path |
|---|---|
| Settings | `%AppData%\MacShot\settings.json` |
| Log | `%AppData%\MacShot\MacShot.log` |
| Screenshots | `%Pictures%\MacShot` |

## Building from source

```powershell
git clone https://github.com/wintence/MacShot.git
cd MacShot
dotnet build
dotnet run
```

Requires the .NET 8 SDK.

## License

MIT — see [LICENSE](LICENSE).
