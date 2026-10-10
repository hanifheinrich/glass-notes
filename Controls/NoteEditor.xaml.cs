using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using GlassNotes.Helpers;
using GlassNotes.Models;

namespace GlassNotes.Controls;

/// <summary>
/// Rich text editor for a note with a Sticky Notes style formatting bar:
/// bold, italic, underline, strikethrough and bullet lists.
/// Writes both the formatted content and the plain text back to the note.
/// </summary>
public partial class NoteEditor : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(nameof(Note), typeof(Note), typeof(NoteEditor),
            new PropertyMetadata(null, (d, e) => ((NoteEditor)d).OnNoteChanged((Note?)e.OldValue, (Note?)e.NewValue)));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(NoteEditor),
            new PropertyMetadata(string.Empty, (d, e) => ((NoteEditor)d).UpdateSearchHighlight()));

    public Note? Note
    {
        get => (Note?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>Raised after the user changes the note text or formatting.</summary>
    public event EventHandler? ContentChanged;

    private bool _loading;
    private bool _writing;
    private string? _lastWritten;
    private SearchHighlightAdorner? _searchAdorner;

    public NoteEditor()
    {
        InitializeComponent();

        Editor.TextChanged += Editor_TextChanged;
        Editor.SelectionChanged += (s, e) => UpdateToolbarState();
        Editor.PreviewKeyDown += Editor_PreviewKeyDown;
        Editor.Loaded += (s, e) => UpdateSearchHighlight();
        DataObject.AddPastingHandler(Editor, Editor_Pasting);

        // Block built-in shortcuts for formatting the note format cannot keep
        // (alignment, font size, sub/superscript), so nothing disappears after a save.
        foreach (var command in new[]
                 {
                     EditingCommands.AlignLeft, EditingCommands.AlignCenter, EditingCommands.AlignRight,
                     EditingCommands.AlignJustify, EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize,
                     EditingCommands.ToggleSubscript, EditingCommands.ToggleSuperscript
                 })
        {
            Editor.CommandBindings.Add(new CommandBinding(command, (s, e) => e.Handled = true,
                (s, e) => { e.CanExecute = false; e.Handled = true; }));
        }

        LoadFromNote();
    }

    public new bool Focus() => Editor.Focus();

    // ── Loading and saving ───────────────────────────────────────────────────

    private void OnNoteChanged(Note? oldNote, Note? newNote)
    {
        if (oldNote != null) oldNote.PropertyChanged -= Note_PropertyChanged;
        if (newNote != null) newNote.PropertyChanged += Note_PropertyChanged;
        LoadFromNote();
    }

    private void Note_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The same note can be open in the main window and in its own window: reload when the other one edits it
        if (e.PropertyName == nameof(Models.Note.RichContent) && !_writing && Note != null && Note.RichContent != _lastWritten)
        {
            LoadFromNote();
        }
    }

    private void LoadFromNote()
    {
        _loading = true;
        try
        {
            Editor.Document = Note == null
                ? new FlowDocument(new Paragraph())
                : RichTextSerializer.ToDocument(Note.RichContent, Note.Content);
            _lastWritten = Note?.RichContent;
        }
        finally
        {
            _loading = false;
        }

        _searchAdorner?.InvalidateVisual();
        UpdateToolbarState();
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || Note == null) return;

        _writing = true;
        try
        {
            _lastWritten = RichTextSerializer.Serialize(Editor.Document);
            Note.RichContent = _lastWritten;
            Note.Content = RichTextSerializer.ToPlainText(Editor.Document);
        }
        finally
        {
            _writing = false;
        }

        _searchAdorner?.InvalidateVisual();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Editor_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        // Paste as plain text so fonts and colors from other apps don't end up in the note
        if (e.FormatToApply != DataFormats.UnicodeText && e.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            e.DataObject = new DataObject(DataFormats.UnicodeText, text);
            e.FormatToApply = DataFormats.UnicodeText;
        }
    }

    // ── Formatting ───────────────────────────────────────────────────────────

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+T is strikethrough in Windows Sticky Notes
        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ToggleStrikethrough();
            e.Handled = true;
        }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e) => ExecuteFormat(EditingCommands.ToggleBold);
    private void ItalicButton_Click(object sender, RoutedEventArgs e) => ExecuteFormat(EditingCommands.ToggleItalic);
    private void UnderlineButton_Click(object sender, RoutedEventArgs e) => ExecuteFormat(EditingCommands.ToggleUnderline);
    private void BulletsButton_Click(object sender, RoutedEventArgs e) => ExecuteFormat(EditingCommands.ToggleBullets);

    private void StrikeButton_Click(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
        ToggleStrikethrough();
    }

    private void ExecuteFormat(RoutedUICommand command)
    {
        Editor.Focus();
        command.Execute(null, Editor);
        UpdateToolbarState();
    }

    private void ToggleStrikethrough()
    {
        var selection = Editor.Selection;
        bool strike = !SelectionHas(TextDecorationLocation.Strikethrough);

        if (selection.IsEmpty)
        {
            // Formatting for the text typed next
            bool underline = SelectionHas(TextDecorationLocation.Underline);
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, RichTextSerializer.Decorations(underline, strike));
        }
        else
        {
            // Apply per run so an existing underline on part of the selection is kept
            var pieces = new List<(TextPointer Start, TextPointer End, bool Underline)>();
            foreach (var run in RunsIn(selection))
            {
                var start = run.ContentStart.CompareTo(selection.Start) < 0 ? selection.Start : run.ContentStart;
                var end = run.ContentEnd.CompareTo(selection.End) > 0 ? selection.End : run.ContentEnd;
                if (start.CompareTo(end) < 0)
                    pieces.Add((start, end, RichTextSerializer.HasDecoration(run, TextDecorationLocation.Underline)));
            }

            Editor.BeginChange();
            foreach (var piece in pieces)
            {
                new TextRange(piece.Start, piece.End).ApplyPropertyValue(
                    Inline.TextDecorationsProperty, RichTextSerializer.Decorations(piece.Underline, strike));
            }
            Editor.EndChange();
        }

        UpdateToolbarState();
    }

    private static IEnumerable<Run> RunsIn(TextRange range)
    {
        var runs = new List<Run>();
        for (var pointer = range.Start; pointer != null && pointer.CompareTo(range.End) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.Parent is Run run && (runs.Count == 0 || runs[^1] != run))
                runs.Add(run);
        }
        return runs;
    }

    private bool SelectionHas(TextDecorationLocation location)
    {
        var selection = Editor.Selection;
        if (selection.IsEmpty)
        {
            // Includes formatting picked for the next typed text
            return selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection decorations
                   && decorations.Any(d => d.Location == location);
        }

        var runs = RunsIn(selection).ToList();
        return runs.Count > 0 && runs.All(r => RichTextSerializer.HasDecoration(r, location));
    }

    private void UpdateToolbarState()
    {
        if (BoldButton == null) return;

        var selection = Editor.Selection;
        BoldButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            selection.GetPropertyValue(TextElement.FontWeightProperty) is FontWeight weight
            && weight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight());
        ItalicButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            selection.GetPropertyValue(TextElement.FontStyleProperty) is FontStyle style && style == FontStyles.Italic);
        UnderlineButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            SelectionHas(TextDecorationLocation.Underline));
        StrikeButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            SelectionHas(TextDecorationLocation.Strikethrough));
        BulletsButton.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            selection.Start.Paragraph?.Parent is ListItem);
    }

    // ── Search highlight ─────────────────────────────────────────────────────

    private void UpdateSearchHighlight()
    {
        var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(Editor);
        if (layer == null) return;

        if (string.IsNullOrEmpty(SearchText))
        {
            if (_searchAdorner != null)
            {
                layer.Remove(_searchAdorner);
                _searchAdorner = null;
            }
            return;
        }

        if (_searchAdorner == null)
        {
            _searchAdorner = new SearchHighlightAdorner(Editor, SearchText);
            layer.Add(_searchAdorner);
        }
        else
        {
            _searchAdorner.UpdateSearchText(SearchText);
        }
    }
}
