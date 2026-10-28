using System.Drawing;
using System.Windows.Forms;

namespace TVCleaner.UI
{
    /// <summary>Shared colors, fonts and button styles (light theme, one accent color, 8 px grid).</summary>
    public static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(0, 120, 140);
        public static readonly Color AccentHover = Color.FromArgb(0, 98, 116);
        public static readonly Color Muted = Color.FromArgb(105, 105, 105);
        public static readonly Color Border = Color.FromArgb(205, 205, 205);
        public static readonly Color Track = Color.FromArgb(228, 228, 228);
        public static readonly Color Good = Color.FromArgb(46, 160, 67);
        public static readonly Color Warn = Color.FromArgb(214, 158, 0);
        public static readonly Color Bad = Color.FromArgb(207, 34, 46);
        public static readonly Color Idle = Color.FromArgb(160, 160, 160);
        public static readonly Color Window = Color.FromArgb(248, 248, 248);

        public static readonly Font Base = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 13f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 9.5f);

        /// <summary>Main action of a screen: filled with the accent color.</summary>
        public static void Primary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = AccentHover;
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.Font = Bold;
            b.Cursor = Cursors.Hand;
        }

        /// <summary>Other actions: white with a thin border.</summary>
        public static void Secondary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(236, 244, 246);
            b.BackColor = Color.White;
            b.ForeColor = Color.Black;
            b.Cursor = Cursors.Hand;
        }

        /// <summary>Green below 70 %, yellow below 90 %, red above.</summary>
        public static Color Load(float fraction)
        {
            return fraction < 0.7f ? Good : fraction < 0.9f ? Warn : Bad;
        }

        public static string Size(long bytes)
        {
            double mb = bytes / 1048576.0;
            return mb >= 1024 ? (mb / 1024).ToString("0.0") + " ГБ" : mb.ToString("0") + " МБ";
        }
    }
}
