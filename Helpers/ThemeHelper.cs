using System.Windows;
using System.Windows.Media;

namespace GlassNotes.Helpers;

/// <summary>
/// Helper for managing application themes
/// </summary>
public static class ThemeHelper
{
    /// <summary>
    /// Apply a theme to the application
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;

        // Remove existing theme dictionaries
        var existingTheme = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/") == true);

        if (existingTheme != null)
        {
            app.Resources.MergedDictionaries.Remove(existingTheme);
        }

        // Add new theme
        var themeUri = ResolveTheme(theme) == "Light"
            ? new Uri("Resources/Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Resources/Themes/DarkTheme.xaml", UriKind.Relative);

        try
        {
            var themeDict = new ResourceDictionary { Source = themeUri };
            app.Resources.MergedDictionaries.Add(themeDict);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
        }
    }

    /// <summary>
    /// "Light" or "Dark" for a theme setting; "System" follows the Windows app mode.
    /// </summary>
    public static string ResolveTheme(string theme)
    {
        return theme.ToLower() switch
        {
            "light" => "Light",
            "system" => IsWindowsInLightMode() ? "Light" : "Dark",
            _ => "Dark"
        };
    }

    private static bool IsWindowsInLightMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Get a color from hex string
    /// </summary>
    public static Color GetColorFromHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Blue;
        }
    }

    /// <summary>
    /// Apply accent color to the application
    /// </summary>
    public static void ApplyAccentColor(string hexColor)
    {
        var color = GetColorFromHex(hexColor);
        var brush = new SolidColorBrush(color);

        Application.Current.Resources["AccentColor"] = color;
        Application.Current.Resources["AccentBrush"] = brush;
    }
}
