# Glass Notes (fork) - Transparent Notes App for Windows

> **This is a fork of [psipher/GlassNotes](https://github.com/psipher/GlassNotes)** (based on upstream v1.0.7).
> It adds rich text formatting in the style of Windows Sticky Notes, drag-and-drop note ordering that persists,
> and a redesigned dark mode. All credit for the original app goes to [psipher](https://github.com/psipher).

Glass Notes is a lightweight note-taking app for Windows 10/11 with a transparent, always-on-top window.
For the original app, the Microsoft Store release and upstream development, see the
[original repository](https://github.com/psipher/GlassNotes).

## What this fork adds (v1.0.8)

### Rich text formatting
A formatting bar under every note, like Windows Sticky Notes:

| Format | Button | Shortcut |
|---|---|---|
| **Bold** | **B** | `Ctrl + B` |
| *Italic* | *I* | `Ctrl + I` |
| Underline | U | `Ctrl + U` |
| ~~Strikethrough~~ | ~~ab~~ | `Ctrl + T` |
| Bullet list | ☰ | `Ctrl + Shift + L` |

- Works in the main window and in notes opened in their own window.
- Formatting is saved with the note. Notes from the original app open unchanged, and lines starting with `•` become a bullet list.
- Pasting from other apps inserts plain text, so outside fonts and colors don't end up in your notes.

### Arrange notes by drag and drop
- Drag a note up or down in the list; a line shows where it will land.
- The order is kept after restarting the app (upstream saved it but did not restore it).
- The list scrolls when you drag near its top or bottom edge.

### Dark mode
- Redesigned Light and Dark themes: neutral graphite surfaces and off-white text in dark mode, a gold accent taken from the app icon.
- **Settings → General → Theme**: *Follow Windows*, *Light* or *Dark*. Switching applies instantly; *Follow Windows* also follows Windows when it changes mode.
- Background and text colors have a **Theme default** option so notes always match the theme. The original custom colors are still available.
- Every part of the app follows the theme: notes list, settings, dropdowns, context menus, scrollbars, sliders and checkboxes.

### Taskbar button (GlassNotesBar)
A small companion program that puts the Glass Notes logo on the Windows taskbar, next to the system tray (similar to TrafficMonitor).
- **Left-click** shows or hides Glass Notes (and starts it if it isn't running). **Right-click** opens a menu.
- Turn it on or off in **Settings → General → Show button on taskbar**. When on, it starts with Windows.
- Put `GlassNotesBar.exe` in the same folder as `GlassNotes.exe` (both are attached to each release), or install it to `%LOCALAPPDATA%\Programs\GlassNotesBar\`. Without it the setting is greyed out.
- Made for the Windows 10 taskbar (horizontal; vertical taskbars are not supported). The Windows 11 taskbar is built differently and has not been tested.

### Look and feel
- Emoji buttons replaced with Windows line icons (Segoe MDL2 Assets), an outline logo in the title bar, and a red hover on close buttons.

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for the full list of changes.

## Compatibility with the original app

Notes and settings use the same location as the original app (`%APPDATA%\GlassNotes\`), so this fork picks up existing notes.
Formatting is stored in a new `RichContent` field. **If a note is saved by the original app, that field is dropped and the note's formatting is lost** (the text itself stays). Avoid running both versions at the same time.

## Requirements

- Windows 10 (version 1809 or later) or Windows 11
- .NET 10.0 Desktop Runtime, or use the self-contained build below

## Building from source

Requires the [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/hanifheinrich/glass-notes.git
cd glass-notes
dotnet build -c Release
dotnet run
```

Self-contained single-file build (no runtime needed on the target PC), the same as the upstream release workflow:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o ./publish
```

The app is then in `publish\GlassNotes.exe`.

The taskbar button is a small .NET Framework 4 program built with the C# compiler included in Windows (no SDK needed):

```powershell
GlassNotesBar\build.cmd publish
```

This puts `GlassNotesBar.exe` next to `GlassNotes.exe` in `publish\`. Without an argument it builds to `GlassNotesBar\bin\`.

## Usage

- **Drag the title bar** to move the window
- **Hide/Show notes** with the eye icon or `Ctrl + Shift + Z`
- **Gear icon** opens Settings (theme, transparency, always on top, colors, font)
- **Plus icon** creates a new note; right-click a note to open it in a new window or delete it
- **Double-click a note** to open it in its own window
- Changes are saved automatically

## Data storage

```
%APPDATA%\GlassNotes\
├── Notes\           # One .json file per note (plain text + formatting)
└── settings.json    # App settings, including theme and note order
```

## Project structure

```
GlassNotes/
├── Controls/            # NoteEditor: rich text editor with formatting bar (fork)
├── Models/              # Data models (Note, AppSettings)
├── ViewModels/          # MVVM view models
├── Services/            # NoteService, SettingsService
├── Helpers/             # Theme, colors, rich text serializer, adorners
├── Resources/
│   ├── Themes/          # Light and Dark theme colors
│   └── Styles/          # Control and button styles
├── Assets/              # Icons and images
└── GlassNotesBar/       # Taskbar button, separate program (fork)
```

## Technology stack

- WPF on .NET 10
- CommunityToolkit.Mvvm
- Newtonsoft.Json

## License

MIT License, see [LICENSE](LICENSE). The original copyright notice from psipher is kept as the license requires.

## Credits

- Original app: [Glass Notes by psipher](https://github.com/psipher/GlassNotes)
- Fork changes: rich text, note ordering, dark mode, taskbar button and UI refresh
