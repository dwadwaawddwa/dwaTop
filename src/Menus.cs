using System;
using System.Drawing;
using System.Windows.Forms;

namespace DwaTop
{
    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.TileHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.TileHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.TileHover; } }
        public override Color MenuItemBorder { get { return Theme.TileHover; } }
        public override Color MenuBorder { get { return Theme.Line; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color SeparatorDark { get { return Theme.Line; } }
        public override Color SeparatorLight { get { return Theme.Line; } }
        public override Color CheckBackground { get { return Theme.TileOn; } }
        public override Color CheckSelectedBackground { get { return Theme.TileOn; } }
        public override Color CheckPressedBackground { get { return Theme.TileOn; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Tile; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Tile; } }
    }

    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Sub;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.Sub;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            r.Inflate(-1, -1);
            using (var b = new SolidBrush(Theme.TileOn)) e.Graphics.FillRectangle(b, r);
            using (var pen = new Pen(Theme.Accent, 2f))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                e.Graphics.DrawLines(pen, new[] { new PointF(x + w * 0.22f, y + h * 0.52f), new PointF(x + w * 0.42f, y + h * 0.72f), new PointF(x + w * 0.78f, y + h * 0.3f) });
            }
        }
    }

    public static class Menus
    {
        public static ContextMenuStrip Create()
        {
            var m = new ContextMenuStrip();
            m.Renderer = new DarkRenderer();
            m.ShowImageMargin = true;
            m.BackColor = Theme.Panel;
            m.ForeColor = Theme.Text;
            m.Font = new Font("Segoe UI", 9f);
            return m;
        }

        public static ToolStripMenuItem Add(ToolStripItemCollection items, string text, Action click, bool? check = null, bool enabled = true)
        {
            var it = new ToolStripMenuItem(text);
            it.ForeColor = Theme.Text;
            if (check.HasValue) it.Checked = check.Value;
            it.Enabled = enabled;
            if (click != null) it.Click += delegate { try { click(); } catch (Exception ex) { Log.Error(ex); } };
            items.Add(it);
            return it;
        }

        public static ToolStripMenuItem Sub(ToolStripItemCollection items, string text)
        {
            var it = new ToolStripMenuItem(text);
            it.ForeColor = Theme.Text;
            it.DropDown.BackColor = Theme.Panel;
            ((ToolStripDropDownMenu)it.DropDown).Renderer = new DarkRenderer();
            items.Add(it);
            return it;
        }

        public static void Sep(ToolStripItemCollection items) { items.Add(new ToolStripSeparator()); }
    }
}
