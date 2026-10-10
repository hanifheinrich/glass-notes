# Release Notes - GlassNotes v1.0.8

## [v1.0.8] - 2026-10-10

### Added
* **Rich Text Formatting**: Sticky Notes style formatting bar with bold (Ctrl+B), italic (Ctrl+I), underline (Ctrl+U), strikethrough (Ctrl+T) and bullet lists (Ctrl+Shift+L). Formatting is saved with each note; older plain-text notes open unchanged.
* **Dark Mode**: Redesigned Light and Dark themes plus a "Follow Windows" option in Settings → General. Background and text colors now offer "Theme default" so notes always match the theme.

* **Taskbar Button**: Optional GlassNotesBar companion that shows and hides Glass Notes from the Windows taskbar, with a "Show button on taskbar" setting in Settings → General. Release downloads include `GlassNotesBar.exe`.

### Fixed
* **Note Arrangement**: Notes reordered by drag and drop now keep their order after restarting. Dragging shows a drop indicator and scrolls the list near its edges.

### Improved
* **Icons**: Emoji buttons replaced with Segoe MDL2 line icons, an outline logo, and a red hover on close buttons.

---

## [v1.0.7] - 2026-06-10

This release resolves critical data-loss issues related to note auto-saving and introduces static analysis guidelines to prevent future regressions.

### Fixed
* **Note Switching Data Loss**: Fixed a bug where unsaved changes to a note were lost if the user switched to another note before the 3-second auto-save timer fired. Changes are now flushed to disk immediately upon switching.
* **OS Shutdown/Restart Protection**: Handled the `Application.SessionEnding` event. Unsaved note content, window positions, and application settings are now saved cleanly when the operating system shuts down, restarts (e.g., during automatic Windows Updates), or when the user logs off.

### Improved
* **Code Quality & Static Analysis**: Added the `WpfAnalyzers` NuGet Style and Quality Analyzer package to enforce WPF-specific best practices during development.
* **Dependency Property Safety**: Resolved 53 static analysis warnings (`WPF0041`) by converting all direct CLR property assignments to `SetCurrentValue` calls across `MainWindow` and `SettingsWindow`, protecting the application from accidentally overwriting WPF data bindings.
