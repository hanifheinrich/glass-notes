using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GlassNotes.Helpers
{
    public class SearchHighlightAdorner : Adorner
    {
        private readonly RichTextBox _textBox;
        private string _searchText;
        private readonly Brush _highlightBrush;
        private readonly Pen _highlightPen;

        public SearchHighlightAdorner(RichTextBox textBox, string searchText) : base(textBox)
        {
            _textBox = textBox;
            _searchText = searchText;
            ClipToBounds = true;
            IsHitTestVisible = false;

            // Semi-transparent yellow highlight with rounded corners
            _highlightBrush = new SolidColorBrush(Color.FromArgb(120, 255, 215, 0));
            _highlightPen = new Pen(_highlightBrush, 0);

            // Re-render when size, text, or scroll position changes
            _textBox.TextChanged += (s, e) => InvalidateVisual();
            _textBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrollChanged));
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            InvalidateVisual();
        }

        public void UpdateSearchText(string newText)
        {
            if (_searchText != newText)
            {
                _searchText = newText;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (string.IsNullOrEmpty(_searchText))
            {
                return;
            }

            // Explicitly clip the adornment to the bounds of the editor
            // This prevents highlights from rendering over toolbars or scrollbars when scrolled out of view.
            drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, _textBox.RenderSize.Width, _textBox.RenderSize.Height)));

            // Search inside each run of text (matches are not joined across formatting changes)
            var document = _textBox.Document;
            for (var pointer = document.ContentStart; pointer != null && pointer.CompareTo(document.ContentEnd) < 0;
                 pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
            {
                if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text) continue;

                string text = pointer.GetTextInRun(LogicalDirection.Forward);
                int index = 0;
                while ((index = text.IndexOf(_searchText, index, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    var start = pointer.GetPositionAtOffset(index);
                    var end = pointer.GetPositionAtOffset(index + _searchText.Length);
                    if (start != null && end != null)
                    {
                        Rect startRect = start.GetCharacterRect(LogicalDirection.Forward);
                        Rect endRect = end.GetCharacterRect(LogicalDirection.Backward);

                        if (!startRect.IsEmpty && !endRect.IsEmpty)
                        {
                            // For single line, or simple word wrapping, create a bounding box
                            Rect highlightRect = new Rect(startRect.TopLeft, endRect.BottomRight);
                            highlightRect.Inflate(2, 2);

                            drawingContext.DrawRoundedRectangle(_highlightBrush, _highlightPen, highlightRect, 3, 3);
                        }
                    }

                    index += _searchText.Length;
                }
            }

            // Remove the clip
            drawingContext.Pop();

            base.OnRender(drawingContext);
        }
    }
}
