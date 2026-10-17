# MacShot

Window screenshots in the macOS style for Windows.

![Windows](https://img.shields.io/badge/Windows-10%20|%2011-blue) ![.NET](https://img.shields.io/badge/.NET-8-purple) ![License](https://img.shields.io/badge/license-MIT-green)

## Settings

| Option | Default |
|---|---|
| Background | two-color gradient at an angle |
| Padding | 40 |
| Shadow | on: blur 36, offset down 16, density 0.45 |
| Background color | palette of 29 swatches plus the system color dialog |
| Hotkey | `Ctrl+Shift+8` |
| Language | Russian or English |
| Save a copy to Pictures | off |
| Start when I sign in to Windows | off |

Worth knowing:

- **The shadow adapts on its own.** On a dark background it turns light, on a light one it turns dark. Otherwise the window either sticks to the backdrop or disappears into it.
- **Corner radius is not configurable** — it is fixed, like on macOS.
- **Padding is exactly what you set.** At small values the shadow gets cut off by the frame edge.

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