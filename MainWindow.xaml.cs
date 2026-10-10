using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using GlassNotes.Helpers;
using GlassNotes.ViewModels;

namespace GlassNotes;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        DataContext = new MainViewModel();

        // Apply the theme before the window is built so it never flashes the wrong colors
        ThemeHelper.ApplyTheme(ViewModel.Settings.Theme);
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        InitializeComponent();

        // Load saved window position and size
        LoadWindowState();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Enable full edge/corner resizing for transparent windows via WM_NCHITTEST hook
        ResizeHelper.Attach(this);

        // Apply Windows 10/11 dark mode title bar
        try
        {
            // Dark mode is handled by Windows 11 Fluent Design automatically now.
            // WindowBlurHelper was removed as it is no longer used.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying dark mode: {ex.Message}");
        }

        // Register global hotkey Ctrl + Shift + Z
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(HwndHook);
            GlobalHotkeyHelper.RegisterHotKey(source, HOTKEY_ID, ModifierKeys.Control | ModifierKeys.Shift, Key.Z);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyHelper.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ViewModel.ToggleAllNotesVisibilityCommand.Execute(null);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set always on top
        SetCurrentValue(TopmostProperty, ViewModel.Settings.AlwaysOnTop);

        // Set initial opacity handled in InitializeComboBoxes -> ApplyBackgroundColor

        // Initialize ComboBoxes
        InitializeComboBoxes();
    }

    private void InitializeComboBoxes()
    {
        // Apply initial colors
        ApplyBackgroundColor(ViewModel.Settings.BackgroundColor);
        ApplyTextColor(ViewModel.Settings.TextColor);
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save window state
        SaveWindowState();

        // Cleanup ViewModel
        ViewModel.Cleanup();

        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

        // Unregister global hotkey
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            GlobalHotkeyHelper.UnregisterHotKey(source, HOTKEY_ID);
            source.RemoveHook(HwndHook);
        }
    }

    private void LoadWindowState()
    {
        const double minW = 480, minH = 360;

        if (ViewModel.Settings.WindowWidth >= minW && ViewModel.Settings.WindowHeight >= minH)
        {
            SetCurrentValue(WidthProperty, ViewModel.Settings.WindowWidth);
            SetCurrentValue(HeightProperty, ViewModel.Settings.WindowHeight);
        }
        else
        {
            SetCurrentValue(WidthProperty, 680.0);
            SetCurrentValue(HeightProperty, 520.0);
        }

        // Restore position only if it's plausibly on-screen
        if (ViewModel.Settings.WindowLeft >= 0 && ViewModel.Settings.WindowTop >= 0)
        {
            SetCurrentValue(LeftProperty, ViewModel.Settings.WindowLeft);
            SetCurrentValue(TopProperty, ViewModel.Settings.WindowTop);
        }
    }

    private void SaveWindowState()
    {
        ViewModel.Settings.WindowWidth = Width;
        ViewModel.Settings.WindowHeight = Height;
        ViewModel.Settings.WindowLeft = Left;
        ViewModel.Settings.WindowTop = Top;
        ViewModel.SaveSettings();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to maximize/restore (optional)
            SetCurrentValue(WindowStateProperty, WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        }
        else
        {
            // Drag to move window
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(WindowStateProperty, WindowState.Minimized);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(WindowStateProperty, WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (MaximizeButton != null)
        {
            MaximizeButton.SetCurrentValue(ContentProperty, WindowState == WindowState.Maximized ? "" : "");
            MaximizeButton.SetCurrentValue(ToolTipProperty, WindowState == WindowState.Maximized ? "Restore" : "Maximize");
        }
    }

    // ── Settings Overlay ─────────────────────────────────────────────────────
    private string _soActiveTab = "General";
    private bool _soInitialized = false;

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsOverlay.Visibility == Visibility.Visible)
        {
            SettingsOverlay.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        }
        else
        {
            if (!_soInitialized)
            {
                SO_LoadSettings();
                SO_AttachEventHandlers();
                _soInitialized = true;
            }
            SettingsOverlay.SetCurrentValue(VisibilityProperty, Visibility.Visible);
            SO_SelectTab("General");
        }
    }

    private void SettingsHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Dragging the settings header moves the MAIN window
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void SettingsCloseButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
    }

    private void SOTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tabName)
            SO_SelectTab(tabName);
    }

    private void SO_SelectTab(string tabName)
    {
        _soActiveTab = tabName;

        SO_PanelGeneral.SetCurrentValue(VisibilityProperty, tabName == "General" ? Visibility.Visible : Visibility.Collapsed);
        SO_PanelColors.SetCurrentValue(VisibilityProperty, tabName == "Colors" ? Visibility.Visible : Visibility.Collapsed);
        SO_PanelFont.SetCurrentValue(VisibilityProperty, tabName == "Font" ? Visibility.Visible : Visibility.Collapsed);
        SO_PanelAbout.SetCurrentValue(VisibilityProperty, tabName == "About" ? Visibility.Visible : Visibility.Collapsed);

        // Resource references so the tab colors follow a theme change
        SO_TabGeneral.SetResourceReference(ForegroundProperty, tabName == "General" ? "TextPrimaryBrush" : "TextSecondaryBrush");
        SO_TabColors.SetResourceReference(ForegroundProperty, tabName == "Colors" ? "TextPrimaryBrush" : "TextSecondaryBrush");
        SO_TabFont.SetResourceReference(ForegroundProperty, tabName == "Font" ? "TextPrimaryBrush" : "TextSecondaryBrush");
        SO_TabAbout.SetResourceReference(ForegroundProperty, tabName == "About" ? "TextPrimaryBrush" : "TextSecondaryBrush");

        // Move underline
        Button? targetButton = tabName switch
        {
            "General" => SO_TabGeneral,
            "Colors" => SO_TabColors,
            "Font" => SO_TabFont,
            "About" => SO_TabAbout,
            _ => null
        };

        if (targetButton != null)
        {
            // Ensure layout is up to date to get accurate position
            if (SettingsOverlay.Visibility == Visibility.Visible)
            {
                SettingsOverlay.UpdateLayout();
            }

            // Calculate position relative to the first tab (General) to determine left offset
            // The tabs are in a StackPanel, so we can just use TranslatePoint relative to the first tab
            // Offset of General is 0.

            try
            {
                // We want the position relative to the container (StackPanel)
                // The underline is in a generic Grid below, which shares the same left alignment as the StackPanel
                // So the X position of the button within the StackPanel is the Left Margin for the underline.

                // Fallback for initial layout if needed
                if (targetButton.ActualWidth == 0 && tabName == "General")
                {
                    SO_TabUnderline.SetCurrentValue(MarginProperty, new Thickness(0, 0, 0, 0));
                    SO_TabUnderline.SetCurrentValue(WidthProperty, 60.0); // Default estimate
                }
                else
                {
                    // Get parent stackpanel
                    var parent = System.Windows.Media.VisualTreeHelper.GetParent(targetButton) as UIElement;
                    if (parent != null)
                    {
                        var offset = targetButton.TranslatePoint(new Point(0, 0), parent);
                        SO_TabUnderline.SetCurrentValue(MarginProperty, new Thickness(offset.X, 0, 0, 0));
                        SO_TabUnderline.SetCurrentValue(WidthProperty, targetButton.ActualWidth > 0 ? targetButton.ActualWidth : 60.0);
                    }
                }
            }
            catch
            {
                // Fallback if visual tree is not ready
            }
        }
    }

    private void SO_LoadSettings()
    {
        SO_TransparencySlider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, ViewModel.Settings.Opacity);
        SO_TransparencyLabel.SetCurrentValue(TextBlock.TextProperty, $"{(int)(ViewModel.Settings.Opacity * 100)}%");
        SO_AlwaysOnTopCheckBox.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, ViewModel.Settings.AlwaysOnTop);
        SO_FontSizeSlider.SetCurrentValue(System.Windows.Controls.Primitives.RangeBase.ValueProperty, ViewModel.Settings.FontSize);
        SO_FontSizeLabel.SetCurrentValue(TextBlock.TextProperty, $"{(int)ViewModel.Settings.FontSize}px");
        SelectComboByTag(SO_BackgroundColorComboBox, ViewModel.Settings.BackgroundColor);
        SelectComboByTag(SO_TextColorComboBox, ViewModel.Settings.TextColor);
        SelectComboByTag(SO_FontFamilyComboBox, ViewModel.Settings.FontFamily);
        SelectComboByTag(SO_ThemeComboBox, ViewModel.Settings.Theme);
    }

    private static void SelectComboByTag(ComboBox box, string? value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (item.Tag?.ToString() == value) { box.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, item); return; }
    }

    private void SO_AttachEventHandlers()
    {
        SO_ThemeComboBox.SelectionChanged += (s, e) =>
        {
            if (SO_ThemeComboBox.SelectedItem is ComboBoxItem item)
            {
                ViewModel.Settings.Theme = item.Tag?.ToString() ?? "System";
                ApplyThemeAndColors();
            }
        };

        SO_TransparencySlider.ValueChanged += (s, e) =>
        {
            ViewModel.Settings.Opacity = e.NewValue;
            SO_TransparencyLabel.SetCurrentValue(TextBlock.TextProperty, $"{(int)(e.NewValue * 100)}%");
            ApplyBackgroundColor(ViewModel.Settings.BackgroundColor);
            ViewModel.SaveSettings();
        };

        SO_AlwaysOnTopCheckBox.Checked += (s, e) => { SetCurrentValue(TopmostProperty, true); ViewModel.Settings.AlwaysOnTop = true; ViewModel.SaveSettings(); };
        SO_AlwaysOnTopCheckBox.Unchecked += (s, e) => { SetCurrentValue(TopmostProperty, false); ViewModel.Settings.AlwaysOnTop = false; ViewModel.SaveSettings(); };

        SO_FontSizeSlider.ValueChanged += (s, e) =>
        {
            ViewModel.Settings.FontSize = e.NewValue;
            SO_FontSizeLabel.SetCurrentValue(TextBlock.TextProperty, $"{(int)e.NewValue}px");
            ViewModel.SaveSettings();
        };

        SO_BackgroundColorComboBox.SelectionChanged += (s, e) =>
        {
            if (SO_BackgroundColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag?.ToString() ?? "White";
                ViewModel.Settings.BackgroundColor = color;
                ApplyBackgroundColor(color);
                ViewModel.SaveSettings();
            }
        };

        SO_TextColorComboBox.SelectionChanged += (s, e) =>
        {
            if (SO_TextColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag?.ToString() ?? "Black";
                ViewModel.Settings.TextColor = color;
                ApplyTextColor(color);
                ViewModel.SaveSettings();
            }
        };

        SO_FontFamilyComboBox.SelectionChanged += (s, e) =>
        {
            if (SO_FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                ViewModel.Settings.FontFamily = item.Tag?.ToString() ?? "Segoe UI";
                ViewModel.SaveSettings();
            }
        };
    }

    private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        // Windows switched between light and dark app mode
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && ViewModel.Settings.Theme == "System")
            Dispatcher.BeginInvoke(ApplyThemeAndColors);
    }

    /// <summary>
    /// Switches the theme, then recomputes the colors set from code ("Theme default" depends on the theme)
    /// and passes them on to open note windows through SaveSettings.
    /// </summary>
    private void ApplyThemeAndColors()
    {
        ThemeHelper.ApplyTheme(ViewModel.Settings.Theme);
        ApplyBackgroundColor(ViewModel.Settings.BackgroundColor);
        ApplyTextColor(ViewModel.Settings.TextColor);
        ViewModel.SaveSettings();
    }

    public void UpdateOpacity(double opacity)
    {
        ViewModel.Settings.Opacity = opacity;
        ApplyBackgroundColor(ViewModel.Settings.BackgroundColor);
    }

    public void UpdateBackgroundColor(string colorName)
    {
        ApplyBackgroundColor(colorName);
    }

    public void UpdateTextColor(string colorName)
    {
        ApplyTextColor(colorName);
    }

    private void ApplyBackgroundColor(string colorName)
    {
        var brush = Helpers.ColorHelper.GetBackgroundBrush(colorName);
        brush.Opacity = ViewModel.Settings.Opacity;

        // Apply to the main content grid
        var grid = this.FindName("ContentGrid") as Grid;
        if (grid != null)
        {
            grid.Background = brush;
        }

        // Apply to sidebar
        var notesSidebar = this.FindName("NotesSidebar") as Border;
        if (notesSidebar != null)
        {
            notesSidebar.Background = brush;
        }

        // Keep text editor transparent
        if (NoteEditor != null)
        {
            NoteEditor.SetCurrentValue(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        }
    }

    private void ApplyTextColor(string colorName)
    {
        if (NoteEditor != null)
        {
            NoteEditor.SetCurrentValue(Control.ForegroundProperty, Helpers.ColorHelper.GetTextBrush(colorName));
        }
    }

    // ── Drag and drop to arrange notes ───────────────────────────────────────
    private Models.Note? _dragCandidate;
    private Point _dragStartPoint;
    private DropIndicatorAdorner? _dropIndicator;
    private ListBoxItem? _dropIndicatorItem;

    private void NotesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only a press on a note can start a drag (not the scrollbar or empty space)
        _dragCandidate = GetItemFromPoint(e.GetPosition(NotesListBox))?.DataContext as Models.Note;
        _dragStartPoint = e.GetPosition(NotesListBox);
    }

    private void NotesListBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate == null) return;

        var delta = e.GetPosition(NotesListBox) - _dragStartPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var note = _dragCandidate;
        _dragCandidate = null;
        try
        {
            DragDrop.DoDragDrop(NotesListBox, note, DragDropEffects.Move);
        }
        finally
        {
            HideDropIndicator();
        }
    }

    private void NotesListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(Models.Note)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var point = e.GetPosition(NotesListBox);
        AutoScrollNotesList(point.Y);

        var (item, below) = GetDropTarget(point);
        if (item == null) HideDropIndicator();
        else ShowDropIndicator(item, below);
    }

    private void NotesListBox_DragLeave(object sender, DragEventArgs e)
    {
        // DragLeave also fires when moving between items; only hide when the pointer left the list
        var point = e.GetPosition(NotesListBox);
        if (point.X < 0 || point.Y < 0 || point.X > NotesListBox.ActualWidth || point.Y > NotesListBox.ActualHeight)
            HideDropIndicator();
    }

    private void NotesListBox_Drop(object sender, DragEventArgs e)
    {
        HideDropIndicator();
        if (e.Data.GetData(typeof(Models.Note)) is not Models.Note dragged) return;

        var (item, below) = GetDropTarget(e.GetPosition(NotesListBox));
        if (item?.DataContext is not Models.Note target || target == dragged) return;

        int oldIndex = ViewModel.Notes.IndexOf(dragged);
        int insertAt = ViewModel.Notes.IndexOf(target) + (below ? 1 : 0);
        if (oldIndex == -1 || insertAt < 0) return;

        // Removing the note first shifts everything after it up by one
        ViewModel.MoveNote(dragged, oldIndex < insertAt ? insertAt - 1 : insertAt);
        NotesListBox.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, dragged);
        e.Handled = true;
    }

    /// <summary>
    /// The note under the pointer, and whether the drop goes below it (lower half of the item).
    /// Below the last note counts as dropping after it.
    /// </summary>
    private (ListBoxItem? Item, bool Below) GetDropTarget(Point point)
    {
        // Measure by position instead of hit testing, so the gaps between notes still count
        ListBoxItem? last = null;
        for (int i = 0; i < NotesListBox.Items.Count; i++)
        {
            if (NotesListBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item || !item.IsVisible)
                continue;

            double top = item.TranslatePoint(new Point(), NotesListBox).Y;
            if (point.Y < top + item.ActualHeight)
                return (item, point.Y > top + item.ActualHeight / 2);
            last = item;
        }

        return last != null ? (last, true) : (null, false);
    }

    private ListBoxItem? GetItemFromPoint(Point point)
    {
        var element = NotesListBox.InputHitTest(point) as DependencyObject;
        while (element != null && element != NotesListBox)
        {
            if (element is ListBoxItem item) return item;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private void ShowDropIndicator(ListBoxItem item, bool below)
    {
        if (_dropIndicatorItem == item && _dropIndicator != null)
        {
            _dropIndicator.Below = below;
            return;
        }

        HideDropIndicator();
        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(item);
        if (layer == null) return;

        _dropIndicator = new DropIndicatorAdorner(item, below);
        _dropIndicatorItem = item;
        layer.Add(_dropIndicator);
    }

    private void HideDropIndicator()
    {
        if (_dropIndicator != null && _dropIndicatorItem != null)
        {
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(_dropIndicatorItem)?.Remove(_dropIndicator);
        }
        _dropIndicator = null;
        _dropIndicatorItem = null;
    }

    private void AutoScrollNotesList(double y)
    {
        const double edge = 24;
        var scrollViewer = FindChild<ScrollViewer>(NotesListBox);
        if (scrollViewer == null) return;

        if (y < edge) scrollViewer.LineUp();
        else if (y > NotesListBox.ActualHeight - edge) scrollViewer.LineDown();
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is T nested) return nested;
        }
        return null;
    }

    private void NotesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (NotesListBox.SelectedItem is Models.Note note)
        {
            ViewModel.OpenNoteInNewWindowCommand.Execute(note);
        }
    }
}
