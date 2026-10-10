using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace GlassNotes.Helpers;

/// <summary>
/// Controls the optional GlassNotesBar companion: a small button on the Windows taskbar
/// that shows and hides Glass Notes. It is a separate program; when it is not installed
/// the setting is unavailable. Whether it is shown is read from its autostart entry.
/// </summary>
public static class TaskbarButtonHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "GlassNotesBar";
    private const string ProcessName = "GlassNotesBar";
    private const string ExitSignalName = "GlassNotesBar_Exit";

    // GlassNotesBar.exe next to GlassNotes.exe (as in the release download), otherwise the per-user install folder
    private static readonly string BarExe = FindBarExe();

    private static string FindBarExe()
    {
        var sideBySide = Path.Combine(AppContext.BaseDirectory, "GlassNotesBar.exe");
        if (File.Exists(sideBySide)) return sideBySide;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Programs\GlassNotesBar\GlassNotesBar.exe");
    }

    public static bool IsInstalled => File.Exists(BarExe);

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(RunValueName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>Starts the taskbar button now and at every sign-in.</summary>
    public static void Enable()
    {
        if (!IsInstalled) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(RunValueName, $"\"{BarExe}\"");

            if (Process.GetProcessesByName(ProcessName).Length == 0)
                Process.Start(new ProcessStartInfo(BarExe) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error enabling taskbar button: {ex.Message}");
        }
    }

    /// <summary>Closes the taskbar button and stops it from starting at sign-in.</summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(RunValueName, throwOnMissingValue: false);

            // Ask it to quit so it gives its space on the taskbar back
            if (EventWaitHandle.TryOpenExisting(ExitSignalName, out var exitSignal))
            {
                using (exitSignal) exitSignal.Set();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disabling taskbar button: {ex.Message}");
        }
    }
}
