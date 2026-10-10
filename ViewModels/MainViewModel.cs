using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassNotes.Models;
using GlassNotes.Services;

namespace GlassNotes.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly NoteService _noteService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly System.Collections.Generic.Dictionary<Guid, NoteWindow> _openNoteWindows = new();

    [ObservableProperty]
    private ObservableCollection<Note> _notes = new();

    [ObservableProperty]
    private Note? _currentNote;

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ICollectionView FilteredNotes { get; }

    [ObservableProperty]
    private bool _isSettingsPanelOpen;

    [ObservableProperty]
    private bool _areNotesVisible = true;

    public MainViewModel()
    {
        _noteService = new NoteService();
        _settingsService = new SettingsService();

        // Setup auto-save timer (save every 3 seconds after changes)
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;

        // Load settings and notes
        LoadSettings();
        LoadNotes();

        FilteredNotes = CollectionViewSource.GetDefaultView(Notes);
        FilteredNotes.Filter = FilterNotes;
    }

    private bool FilterNotes(object obj)
    {
        if (obj is not Note note) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        return note.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               note.Content.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredNotes.Refresh();
    }

    private void LoadSettings()
    {
        Settings = _settingsService.LoadSettings();
    }

    private void LoadNotes()
    {
        var loadedNotes = _noteService.LoadAllNotes();

        // Sort notes based on settings
        var sortedNotes = Settings.NoteSortOrder == "OldestFirst"
            ? loadedNotes.OrderBy(n => n.ModifiedAt).ToList()
            : loadedNotes.OrderByDescending(n => n.ModifiedAt).ToList();

        // Notes arranged by drag and drop keep their order; notes missing from it
        // go where a new note would (top for newest first, bottom for oldest first)
        if (Settings.CustomNoteOrder.Count > 0)
        {
            var position = Settings.CustomNoteOrder
                .Select((id, index) => (id, index))
                .GroupBy(p => p.id)
                .ToDictionary(g => g.Key, g => g.First().index);
            var arranged = sortedNotes.Where(n => position.ContainsKey(n.Id)).OrderBy(n => position[n.Id]);
            var unarranged = sortedNotes.Where(n => !position.ContainsKey(n.Id));

            sortedNotes = Settings.NoteSortOrder == "OldestFirst"
                ? arranged.Concat(unarranged).ToList()
                : unarranged.Concat(arranged).ToList();
        }

        Notes = new ObservableCollection<Note>(sortedNotes);

        // Select the last opened note or the first note
        if (Settings.LastOpenedNoteId.HasValue)
        {
            CurrentNote = Notes.FirstOrDefault(n => n.Id == Settings.LastOpenedNoteId.Value);
        }

        CurrentNote ??= Notes.FirstOrDefault();
    }

    private Note? _previousNote;
    partial void OnCurrentNoteChanged(Note? value)
    {
        if (_previousNote != null)
        {
            if (_autoSaveTimer.IsEnabled)
            {
                _autoSaveTimer.Stop();
                _noteService.SaveNote(_previousNote);
            }
            _previousNote.PropertyChanged -= CurrentNote_PropertyChanged;
        }

        if (value != null)
        {
            value.PropertyChanged += CurrentNote_PropertyChanged;
            Settings.LastOpenedNoteId = value.Id;
            SaveSettings();
        }
        _previousNote = value;
    }

    private void CurrentNote_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Note.Content))
        {
            if (CurrentNote != null)
            {
                var firstLine = CurrentNote.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    CurrentNote.Title = firstLine.Length > 30 ? firstLine.Substring(0, 30) + "..." : firstLine;
                }
                else
                {
                    CurrentNote.Title = "Untitled Note";
                }

                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }
        else if (e.PropertyName == nameof(Note.RichContent) && CurrentNote != null)
        {
            // Formatting-only changes (e.g. making a word bold) also need saving
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        _autoSaveTimer.Stop();
        SaveCurrentNote();
    }

    [RelayCommand]
    private void SaveCurrentNote()
    {
        if (CurrentNote != null)
        {
            _noteService.SaveNote(CurrentNote);
        }
    }

    [RelayCommand]
    private void CreateNewNote()
    {
        var newNote = new Note
        {
            Title = $"Note {Notes.Count + 1}",
            Content = string.Empty
        };

        // Add note based on sort order
        if (Settings.NoteSortOrder == "OldestFirst")
        {
            Notes.Add(newNote); // Add at bottom for oldest first
        }
        else
        {
            Notes.Insert(0, newNote); // Add at top for newest first
        }

        CurrentNote = newNote;
        _noteService.SaveNote(newNote);
        SaveNoteOrder();
    }

    /// <summary>
    /// Moves a note to a new position in the list (drag and drop) and remembers the order.
    /// </summary>
    public void MoveNote(Note note, int newIndex)
    {
        int oldIndex = Notes.IndexOf(note);
        if (oldIndex == -1) return;

        newIndex = Math.Clamp(newIndex, 0, Notes.Count - 1);
        if (newIndex != oldIndex)
        {
            Notes.Move(oldIndex, newIndex);
            SaveNoteOrder();
        }
    }

    private void SaveNoteOrder()
    {
        Settings.CustomNoteOrder = Notes.Select(n => n.Id).ToList();
        SaveSettings();
    }

    [RelayCommand]
    private void DeleteCurrentNote()
    {
        if (CurrentNote != null && Notes.Count > 1)
        {
            var noteToDelete = CurrentNote;
            var index = Notes.IndexOf(noteToDelete);

            // Remove the note first
            Notes.Remove(noteToDelete);

            // Select another note - if we deleted the last one, select the new last one
            // Otherwise select the note at the same index (which is now the next note)
            if (index >= Notes.Count)
            {
                CurrentNote = Notes[Notes.Count - 1];
            }
            else
            {
                CurrentNote = Notes[index];
            }

            _noteService.DeleteNote(noteToDelete.Id);
            SaveNoteOrder();
        }
    }

    [RelayCommand]
    private void OpenNoteInNewWindow(Note? note)
    {
        if (note == null) return;

        if (_openNoteWindows.TryGetValue(note.Id, out var existingWindow))
        {
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        var noteWindow = new NoteWindow(note, Settings);
        _openNoteWindows[note.Id] = noteWindow;

        noteWindow.Closed += (s, e) => _openNoteWindows.Remove(note.Id);
        noteWindow.Show();
    }

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        IsSettingsPanelOpen = !IsSettingsPanelOpen;
    }

    [RelayCommand]
    public void ToggleAllNotesVisibility()
    {
        AreNotesVisible = !AreNotesVisible;

        foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
        {
            if (window is MainWindow || window is NoteWindow)
            {
                if (AreNotesVisible)
                {
                    window.Show();
                    // Optional: Bring to front when unhiding
                    if (window.Topmost) window.Topmost = true;
                }
                else
                {
                    window.Hide();
                }
            }
        }
    }

    public void SaveSettings()
    {
        _settingsService.SaveSettings(Settings);
        foreach (var window in _openNoteWindows.Values)
        {
            window.ApplySettings(Settings);
        }
    }

    public void Cleanup()
    {
        _autoSaveTimer.Stop();
        SaveCurrentNote();
        SaveSettings();

        foreach (var window in _openNoteWindows.Values.ToList())
        {
            window.Close();
        }
    }
}
