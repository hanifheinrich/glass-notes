using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GlassNotes.Helpers
{
    /// <summary>
    /// Draws a line above or below a list item to show where a dragged note will land.
    /// </summary>
    public class DropIndicatorAdorner : Adorner
    {
        private readonly Pen _pen;
        private readonly Brush _brush;
        private bool _below;

        public DropIndicatorAdorner(UIElement item, bool below) : base(item)
        {
            _below = below;
            IsHitTestVisible = false;

            _brush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            _brush.Freeze();
            _pen = new Pen(_brush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            _pen.Freeze();
        }

        public bool Below
        {
            get => _below;
            set
            {
                if (_below == value) return;
                _below = value;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var size = AdornedElement.RenderSize;
            double y = _below ? size.Height - 1 : 1;
            drawingContext.DrawLine(_pen, new Point(6, y), new Point(size.Width - 6, y));
            drawingContext.DrawEllipse(_brush, null, new Point(6, y), 3.5, 3.5);
        }
    }
}
