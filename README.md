# NoteZero

A minimal plain-text editor for Windows 11. The window holds nothing but your text.

## Install

1. Download **[NoteZero.exe](https://github.com/gyeozai/NoteZero/releases/latest/download/NoteZero.exe)** and move it somewhere permanent, e.g. `%LOCALAPPDATA%\Programs\NoteZero\`.
2. Right-click a `.txt` file → **Open with** → **Choose another app** → **Choose an app on your PC** → select `NoteZero.exe` → **Always**.
3. Repeat step 2 for other file types (`.md`, `.log`, `.csv`, …).

The exe is unsigned, so SmartScreen may warn on first run: click **More info → Run anyway**.
Requires Windows 11 (also runs on Windows 10, without rounded corners).

## File handling

- **Encoding:** only valid UTF-8 opens. Other encodings (Big5, GBK, UTF-16, …) are refused with a message, so an auto-save can never corrupt them.
- **BOM:** kept if the file had one; never added.
- **Line endings:** the file's own style (CRLF, LF or CR) is used for new lines too. Files without any line break default to CRLF.
- **Save errors** (read-only, no permission, file locked by another app): an error is shown and the window stays open, so nothing is lost.

## Project structure

```
NoteZero/
├── assets/
│   └── NoteZero.ico        App icon (embedded into the exe)
├── src/
│   ├── Program.cs          Entry point, command line, save on logoff
│   ├── EditorWindow.cs     Window, title bar, editor, save/close logic
│   ├── TextFile.cs         UTF-8 validation, BOM/line-ending detection, safe write
│   ├── WindowSettings.cs   Remembers window size, position and zoom
│   ├── NativeMethods.cs    Win32 / DWM interop
│   ├── Theme.xaml          Caption buttons, overlay scroll bar, editor style
│   └── app.manifest        Per-monitor DPI, Windows 10/11 compatibility
├── build.ps1               Build script → bin\NoteZero.exe
├── .gitattributes
├── .gitignore
└── README.md
```
