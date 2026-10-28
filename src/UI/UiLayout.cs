using System;
using System.Windows.Forms;

namespace TVCleaner.UI
{
    /// <summary>Layout helpers that do not rely on Anchor (anchors break when a container is briefly tiny).</summary>
    public static class UiLayout
    {
        /// <summary>Keeps a control at the right edge of a docked bar.</summary>
        public static void KeepRight(Control bar, Control c, int margin = 12)
        {
            EventHandler place = (s, e) => c.Left = bar.ClientSize.Width - c.Width - margin;
            bar.Resize += place;
            place(null, EventArgs.Empty);
        }
    }
}
