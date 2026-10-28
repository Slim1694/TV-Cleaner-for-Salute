using System.Drawing;
using System.Windows.Forms;

namespace TVCleaner.UI
{
    /// <summary>Caption, value and a thin colored bar (the standard ProgressBar cannot be recolored).</summary>
    public sealed class Meter : Control
    {
        private float fraction = -1;
        private string value = "…";

        public Meter(string caption)
        {
            Text = caption;
            DoubleBuffered = true;
            Font = Theme.Small;
            Size = new Size(170, 36);
        }

        /// <param name="fraction">0..1, or a negative value when unknown (empty bar).</param>
        public void SetValue(float fraction, string value)
        {
            this.fraction = fraction;
            this.value = value;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Window);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Point(0, 0), Theme.Muted);
            TextRenderer.DrawText(e.Graphics, value, Theme.Bold, new Rectangle(0, 0, Width, 18), Color.Black,
                TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.SingleLine);
            var track = new Rectangle(0, Height - 10, Width - 1, 8);
            using (var b = new SolidBrush(Theme.Track)) e.Graphics.FillRectangle(b, track);
            if (fraction >= 0)
            {
                int w = (int)(track.Width * System.Math.Min(1f, fraction));
                using (var b = new SolidBrush(Theme.Load(fraction))) e.Graphics.FillRectangle(b, track.X, track.Y, w, track.Height);
            }
        }
    }
}
