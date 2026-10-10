using CommunityToolkit.Mvvm.ComponentModel;

namespace GlassNotes.Models;

/// <summary>
/// Represents a single note with content and metadata
/// </summary>
public partial class Note : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    private string _title = "Untitled Note";

    [ObservableProperty]
    private string _content = string.Empty;

    /// <summary>
    /// Formatted content (bold, italic, strikethrough, bullets) serialized by RichTextSerializer.
    /// Content keeps the plain text for titles and search; older notes only have Content.
    /// </summary>
    [ObservableProperty]
    private string _richContent = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public Note()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.Now;
        ModifiedAt = DateTime.Now;
    }
}
