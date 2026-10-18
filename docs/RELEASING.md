# How to release

MacShot releases are published with two files. Lite is the one used on
day-to-day machines and is the one that gets copied to `E:\Soft\MacShot`,
where it is started from.

## Steps

1. Update the version in both places. They are separate on purpose: one is
   what the window title shows, the other is what the executable reports.

   - `Lang/L.cs` — `public const string Version`
   - `MacShot.csproj` — `<Version>`

2. Build with zero warnings. A build with warnings means something was
   left half-done; do not publish it.

3. Stop any running copy first. `MacShot-lite.exe` on this machine is
   usually open, and the publish will fail on the file lock — with ten
   retries and a stack of MSB3027 errors that say nothing useful.

4. Publish both files:

   ```
   dotnet publish -c Release -r win-x64 --self-contained false `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
     -p:DebugType=none -o release

   dotnet publish -c Release -r win-x64 --self-contained true `
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
     -p:EnableCompressionInSingleFile=true -p:DebugType=none -o release-full
   ```

   Both produce `MacShot.exe`. Rename them to `MacShot-lite.exe` and
   `MacShot-full.exe` — the name is the only thing telling them apart, and
   two identical `MacShot.exe` files in two folders is a trap.

5. Copy the **lite** build to `E:\Soft\MacShot\MacShot-lite.exe`. This is
   the installed copy, the one launched from there, and it must match the
   release. Do not skip it: the user runs from that path, and a release
   published without it leaves the machine on the previous version.

6. Commit, push, then create the release:

   ```
   gh release create v1.2.3 --repo wintence/MacShot `
     --title "MacShot 1.2.3 — …" `
     --notes <file> `
     "release-full\MacShot-full.exe#MacShot-full.exe" `
     "release\MacShot-lite.exe#MacShot-lite.exe"
   ```

## Release notes

Two sections: what the release adds, and `Two files — pick one` with the
real sizes. Anything explaining a non-obvious decision belongs there too —
the reader cannot see the code, so a note that saves them from repeating
a mistake is worth more than a feature list.

Descriptions go in English. The program ships both languages, but the
release notes have always been English and changing that is its own
decision.

## Before publishing anything visual

Screenshots are the only honest check. The helpers live outside the repo
in `.tools` — `wincap.ps1` grabs a window by title, `burst.ps1` takes a
run of frames to catch an animation mid-flight, `pickswitch.ps1` clicks a
radio button through UI Automation.

Synthetic mouse clicks do not reach WPF controls on this machine. A
window will take focus and the click will be swallowed. Reach for UI
Automation instead — `InvokePattern` and `SelectionItemPattern` work
reliably. Two things this cost real time:

- A `Border` with a `MouseLeftButtonUp` handler has no automation peer,
  so the colour picker swatch cannot be opened from a script at all.
  Check such dialogs by reading the code and asking the user.
- `Add-Type` compiles with C# 5. `out var`, `using var` and lambdas fail
  with confusing errors. Write it the old way.