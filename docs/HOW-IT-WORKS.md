# How it works

Technical details that do not belong on the front page.

## Layout of the code

```
App.xaml.cs                 argument parsing, startup, single-instance guard
Interop/Native.cs           P/Invoke
Interop/DibSurface.cs       GDI DIB surface
Capture/WindowFinder.cs     window under the cursor and its bounds
Capture/WindowCapturer.cs   PrintWindow with a timeout, BitBlt as fallback
Capture/PixelTransfer.cs    pixel transfer between GDI and WPF
Render/CornerRounder.cs     corner rounding
Render/ShotRenderer.cs      composition: background + window + shadow
Services/                   hotkey, clipboard, tray, autostart, logging
UI/                         picker overlay, settings, palette, hotkey dialog
Lang/                       interface translations
Themes/                     light and dark palettes
Settings/                   settings model and color parsing
```

## Capture

`PrintWindow` with `PW_RENDERFULLCONTENT` comes first — it draws the window into
the supplied buffer and does not care whether the window is covered.

On DirectComposition windows (Chromium, Electron, some games) `PrintWindow`
blocks forever — verified in practice, the process hung for minutes. So the call
runs on a worker thread with a 2 second timeout, and if the window does not
answer, capture falls back to `BitBlt` from the screen.

**Geometry.** The whole window is printed at `GetWindowRect` size, then the frame
is cropped down to the visible DWM bounds. Drawing straight into a buffer sized
to the visible area shifts the content by the width of the invisible border: a
strip shows up on the left and the right side gets cut off.

## Pixels

In .NET 8 `WriteableBitmap` no longer exposes its back buffer pointer — the only
way to write into it is `WritePixels`. Its third argument is the **row stride**,
not the buffer size; pass the buffer size and WPF computes `stride * height`,
which overflows `int`.

`WriteableBitmap` may only be written from the dispatcher thread. The
`PrintWindow` worker therefore returns a plain `byte[]` and the transfer into
WPF happens on the main thread.

## Repeat captures

The picker overlay is a single window reused for the whole session. Its polling
timer is started from `Begin()` via `Dispatcher.BeginInvoke`, not from
`OnContentRendered` — that event fires only on the very first show. From the
second time on, the overlay appeared but ignored both clicks and `Esc`.

## Overlay

The picker window is transparent (`WS_EX_TRANSPARENT`) and polls the mouse via
`GetCursorPos` / `GetAsyncKeyState` instead of relying on WPF events. At alpha 0
WPF receives no mouse input at all: the event is needed to draw the highlight,
and the highlight is needed for the event to arrive.

## Rendering

The frame is composed in a `RenderTargetBitmap`: background, then the window
with rounded corners and a shadow from `DropShadowEffect`.

Shadow color follows the perceived lightness of the background (Rec. 709). On a
dark background a black shadow is invisible and the window looks glued down, so
there the shadow turns white; on a light background it stays black.

`Direction` in WPF is set in a way that is easy to get wrong: **270 is down, 90
is up**. At 90 the shadow went upward — brightness above the window dropped from
237 to 144 while below it stayed at 230, meaning no shadow at all. Measured on
all four sides.

Gradient direction is computed from the endpoints of the axis rather than by
rotating the brush. `RotateTransform` on a `LinearGradientBrush` pushes the axis
outside the shape, and the brush defaults to `WrapMode.Tile`, so the fill
started repeating: at 90° the background was simply flooded with the first
color.

## Hotkey

`MOD_NOREPEAT` is not used: on the test machine Windows reported `Ctrl+Alt+F12`
as already taken with that flag, while the same registration succeeded without
it.

Two windows cannot own each other until they are shown. That is why
`HotKeyDialog.Owner` is assigned in `OnSourceInitialized` rather than in the
`SettingsWindow` constructor — until the settings window has a handle, WPF throws
`InvalidOperationException`.

A WPF window may be shown with `ShowDialog()` exactly once, so the hotkey
dialog is created fresh on every call.

## Theme

The two palettes live in `Themes/UiTheme.cs` as a dictionary of color keys
rather than as properties: markup and code read the same keys, so one list
instead of two that would drift apart on the next theme edit.

`Themes/Theme.Apply` writes the brushes into `Application.Resources`, and the
markup reads them through `{DynamicResource}`. That is what makes switching
instant — WPF re-resolves the references in the already open window. A
`StaticResource` would have been resolved once at parse time and the window
would keep the colors of the theme it was opened with.

Elements built in code are the exception: they have no implicit style from the
markup, and a brush read at build time outlives the theme. They either get
`SetResourceReference` (list rows, mock window) or are redrawn from the
`Theme.Changed` event (mock content lines, which read the palette color).

`BasedOn` stays `StaticResource` — it is resolved when the style is parsed, so
a theme brush there would never update either.

Dialogs read the palette once in the constructor: `ColorPickerWindow` and
`HotKeyDialog` are recreated on every call and cannot outlive a theme switch.

Two things that a dark-to-light switch does not fix by itself: the accent is
noticeably darker in the light theme (`#2F6FE4` against `#4C8DFF`), because the
same blue looks washed out on white; and the selection ring on the palette
swatches is drawn dark on light swatches, since a white ring was invisible on
the white ones.

**The position squares in the preview are not a theme color.** They sit on the
frame background, and that background is whatever the user picked — a theme
cannot know whether it will be light or dark. With a single border the square
was readable on one background and gone on the other: a light border vanished
on a light frame, a dark one vanished on a dark frame. So the square carries
its own double border, dark outside and light inside, identical in both
themes. That is why `SpotFill`, `SpotLine` and `SpotHalo` are the same in
`Dark` and `Light` — they answer to the frame, not to the theme.

This needed alpha in the palette, so `ThemePalette` accepts `#AARRGGBB`
alongside `#RRGGBB`.

## Settings

Colors are written into the settings from the input fields on `TextChanged`.
Without that handler the preview read the fields directly while the old values
were persisted, and the color reverted when the window closed.

Changing the language recreates the window: strings in markup are read through
`x:Static` at XAML parse time and cannot be refreshed in place.

Switching the theme does **not** recreate the window the way switching the
language does — the colors come from `{DynamicResource}` and update in place.

Translations live in `Lang/I18n.cs` as a plain dictionary rather than in
`.resx`: there are few strings, and XAML would need `x:Static` either way. The
class is named `I18n` rather than `Localization` because
`System.Windows.Localization` exists as well and collides by name. The dictionary
will stop being enough past roughly ten languages, or as soon as plural forms
are needed.

## Icons

Two files: `Resources/MacShotIconNew.ico` for the application and
`Resources/MacShotTray.ico` for the tray. Both are drawn by the same script and
differ only in element thickness (0.100 versus 0.135 of the size): the tray icon
lives at 16–24 px, and at normal thickness the brackets merge into a blob.

Entries in the `.ico` are written as **DIB** (`BITMAPINFOHEADER` + BGRA + AND
mask), not PNG. Windows Vista and later understand PNG inside `.ico`, but
`System.Drawing.Icon` in .NET Framework and .NET 8 does not — `Icon.ToBitmap()`
returns garbage. The tray goes through `System.Drawing.Icon` exactly, so PNG
entries would break the icon there. Only the 256 size is left as PNG, because a
DIB of that size runs to half a megabyte and `Icon` never asks for it anyway.

When building the AND mask the byte number is computed with `Floor`, not
`[int]`: PowerShell rounds 7.875 to 8, and at size 64 the index lands exactly one
past the end of the array.

## Why the full file is 64 MB

The program itself is about 800 KB. The rest is the .NET runtime, bundled in so
the application runs on any machine with nothing installed.

| Component | Roughly |
|---|---|
| .NET 8, WPF, Windows Forms | ~62 MB |
| Compression | takes roughly half off |
| Program and icons | ~1 MB |

Compression is already enabled. The file cannot be made much smaller:

- **`PublishTrimmed` does not apply to WPF.** Trimming knows nothing about XAML
  reflection or which constructors the markup invokes. Windows still open, then
  fall over the first time markup touches a type — which is the worst possible
  moment.
- **`PublishReadyToRun` makes the file bigger**, not smaller: it precompiles IL
  to machine code for faster startup.
- **Language packs** are dropped except Russian.

To get a file 20 times smaller, give up self-containment and install the .NET 8
Desktop Runtime on the machine. That build is about 0.8 MB, but it needs an extra
install step, and on a clean Windows without internet there is nothing to
install it from.

```powershell
# self-contained, ~64 MB
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=ru `
  -o release-full

# compact, ~0.8 MB
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:SatelliteResourceLanguages=ru `
  -o release
```

## Known limitations

- Capture runs at the logical scale of the window (`GetDpiForWindow` →
  `GetScale`). On a mixed-DPI setup the result may differ from what is on screen.
- When `PrintWindow` times out, the worker thread stays stuck along with its GDI
  buffer. It is released when the process exits.
- Covered windows are captured through `PrintWindow`. If an application refuses
  to hand over its content (protected DRM material), the frame comes out empty.
- On a mixed background (a gradient from dark to light) the shadow is chosen
  from the darker edge.

## Verified on a live machine

- capture through `PrintWindow`, three shots in a row in the same process
- styling: rounded corners, shadow below, gradient — measured by pixel brightness
- automatic shadow color: on a dark background 112 below the window against a
  background of 29; on a light one 150 against 237
- padding: 10 → 1174×969, 40 → 1234×1029, 80 → 1314×1109
- full cycle: click a window → screenshot in the clipboard (the image was read
  back out)
- `WM_HOTKEY` delivery on a real key press
- language switching in both directions, with the choice persisted
- theme switching in both directions on a live window: palette, cards, inputs,
  buttons, checkboxes, sliders, scroll bar, list rows, and the mock window in
  the preview all change without reopening it
- both dialogs (palette, hotkey) in the light theme; the choice persisted and
  was picked up on the next start
- a `settings.json` written before the theme existed loads as dark
- the preview position squares on a light and a dark frame background, in both
  themes — all four combinations measured on screen

Key presses cannot be reproduced automatically: neither `SendInput` nor
`keybd_event` wakes a global hotkey, in this application or in a third-party
probe.