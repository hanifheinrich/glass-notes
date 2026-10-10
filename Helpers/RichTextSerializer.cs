using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Xml.Linq;

namespace GlassNotes.Helpers;

/// <summary>
/// Converts a FlowDocument to and from a small XML format that only keeps what the
/// editor supports: paragraphs, bullet/numbered lists, line breaks, and bold, italic,
/// underline and strikethrough text. Fonts and colors always come from the app settings.
///
/// &lt;doc&gt;&lt;p&gt;&lt;r b="1" s="1"&gt;text&lt;/r&gt;&lt;br/&gt;&lt;/p&gt;&lt;ul&gt;&lt;li&gt;&lt;p&gt;...&lt;/p&gt;&lt;/li&gt;&lt;/ul&gt;&lt;/doc&gt;
/// </summary>
public static class RichTextSerializer
{
    public static string Serialize(FlowDocument document)
    {
        var root = new XElement("doc");
        WriteBlocks(document.Blocks, root);
        return root.ToString(SaveOptions.DisableFormatting);
    }

    public static string ToPlainText(FlowDocument document)
    {
        return new TextRange(document.ContentStart, document.ContentEnd).Text.TrimEnd('\r', '\n');
    }

    /// <summary>
    /// Builds the document for a note: from its formatted content when present,
    /// otherwise from the plain text of notes saved before formatting existed.
    /// </summary>
    public static FlowDocument ToDocument(string? richContent, string? plainText)
    {
        if (!string.IsNullOrEmpty(richContent))
        {
            try
            {
                var document = new FlowDocument();
                ReadBlocks(XElement.Parse(richContent), document.Blocks);
                if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph());
                return document;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading formatted note, using plain text: {ex.Message}");
            }
        }

        return FromPlainText(plainText ?? string.Empty);
    }

    private static FlowDocument FromPlainText(string text)
    {
        var document = new FlowDocument();
        List? bullets = null;

        foreach (var line in Regex.Split(text, "\r\n|\r|\n"))
        {
            // Turn old "• item" lines into a real bullet list
            if (line.StartsWith("• ") || line.StartsWith("•"))
            {
                bullets ??= new List { MarkerStyle = TextMarkerStyle.Disc };
                bullets.ListItems.Add(new ListItem(new Paragraph(new Run(line.TrimStart('•').TrimStart()))));
                if (bullets.Parent == null) document.Blocks.Add(bullets);
                continue;
            }

            bullets = null;
            document.Blocks.Add(new Paragraph(new Run(line)));
        }

        return document;
    }

    // ── Writing ──────────────────────────────────────────────────────────────

    private static void WriteBlocks(BlockCollection blocks, XElement parent)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    var p = new XElement("p");
                    WriteInlines(paragraph.Inlines, p);
                    parent.Add(p);
                    break;

                case List list:
                    var listElement = new XElement(IsNumbered(list.MarkerStyle) ? "ol" : "ul");
                    foreach (var item in list.ListItems)
                    {
                        var li = new XElement("li");
                        WriteBlocks(item.Blocks, li);
                        listElement.Add(li);
                    }
                    parent.Add(listElement);
                    break;

                case Section section:
                    WriteBlocks(section.Blocks, parent);
                    break;

                default:
                    // Tables or embedded UI: keep their text only
                    parent.Add(new XElement("p", new XElement("r", new TextRange(block.ContentStart, block.ContentEnd).Text)));
                    break;
            }
        }
    }

    private static void WriteInlines(InlineCollection inlines, XElement parent)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when run.Text.Length > 0:
                    var r = new XElement("r", run.Text);
                    if (run.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight()) r.SetAttributeValue("b", 1);
                    if (run.FontStyle == FontStyles.Italic || run.FontStyle == FontStyles.Oblique) r.SetAttributeValue("i", 1);
                    if (HasDecoration(run, TextDecorationLocation.Underline)) r.SetAttributeValue("u", 1);
                    if (HasDecoration(run, TextDecorationLocation.Strikethrough)) r.SetAttributeValue("s", 1);
                    parent.Add(r);
                    break;

                case LineBreak:
                    parent.Add(new XElement("br"));
                    break;

                case Span span:
                    WriteInlines(span.Inlines, parent);
                    break;
            }
        }
    }

    /// <summary>
    /// TextDecorations is not inherited, so look at the run and the spans that contain it.
    /// </summary>
    public static bool HasDecoration(Inline inline, TextDecorationLocation location)
    {
        for (DependencyObject? element = inline; element is Inline current; element = current.Parent)
        {
            if (current.TextDecorations != null && current.TextDecorations.Any(d => d.Location == location))
                return true;
        }
        return false;
    }

    private static bool IsNumbered(TextMarkerStyle style)
    {
        return style is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin
            or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;
    }

    // ── Reading ──────────────────────────────────────────────────────────────

    private static void ReadBlocks(XElement parent, BlockCollection blocks)
    {
        foreach (var element in parent.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "p":
                    var paragraph = new Paragraph();
                    ReadInlines(element, paragraph.Inlines);
                    blocks.Add(paragraph);
                    break;

                case "ul":
                case "ol":
                    var list = new List
                    {
                        MarkerStyle = element.Name.LocalName == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc
                    };
                    foreach (var li in element.Elements("li"))
                    {
                        var item = new ListItem();
                        ReadBlocks(li, item.Blocks);
                        if (item.Blocks.Count == 0) item.Blocks.Add(new Paragraph());
                        list.ListItems.Add(item);
                    }
                    blocks.Add(list);
                    break;
            }
        }
    }

    private static void ReadInlines(XElement parent, InlineCollection inlines)
    {
        foreach (var element in parent.Elements())
        {
            if (element.Name.LocalName == "br")
            {
                inlines.Add(new LineBreak());
                continue;
            }
            if (element.Name.LocalName != "r") continue;

            var run = new Run(element.Value);
            if (IsSet(element, "b")) run.FontWeight = FontWeights.Bold;
            if (IsSet(element, "i")) run.FontStyle = FontStyles.Italic;

            bool underline = IsSet(element, "u"), strike = IsSet(element, "s");
            if (underline || strike) run.TextDecorations = Decorations(underline, strike);

            inlines.Add(run);
        }
    }

    private static bool IsSet(XElement element, string attribute)
    {
        return (string?)element.Attribute(attribute) == "1";
    }

    public static TextDecorationCollection? Decorations(bool underline, bool strike)
    {
        if (!underline && !strike) return null;
        var decorations = new TextDecorationCollection();
        if (underline) decorations.Add(TextDecorations.Underline);
        if (strike) decorations.Add(TextDecorations.Strikethrough);
        decorations.Freeze();
        return decorations;
    }
}
