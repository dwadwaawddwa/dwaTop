using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>
    /// Ancres : une icône flottante pour chaque fenêtre entièrement recouverte par d'autres.
    /// Clic = la ramener devant, clic droit = réduire, clic molette = fermer, glisser un fichier dessus = l'amener devant.
    /// </summary>
    public class AnchorManager
    {
        readonly Timer timer = new Timer();
        readonly Dictionary<IntPtr, AnchorIcon> icons = new Dictionary<IntPtr, AnchorIcon>();
        readonly Bitmap probe = new Bitmap(1, 1);
        readonly Graphics probeG;

        public AnchorManager()
        {
            probeG = Graphics.FromImage(probe);
            timer.Interval = 600;
            timer.Tick += delegate { try { Update(); } catch (Exception ex) { Log.Error(ex); } };
            timer.Start();
        }

        static bool Wanted(IntPtr h)
        {
            if (Config.S.AnchorsAll) return true;
            var mw = Manager.I.Get(h, false);
            return mw != null && mw.Anchor;
        }

        static bool IsOccluder(IntPtr h, bool altTab)
        {
            if (Manager.I.IsHiddenSource(h)) return false;
            string cls = Win.Class(h);
            if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
            if (!altTab && !Win.IsEligible(h)) return false;
            int ex = N.GetExStyle(h);
            if ((ex & N.WS_EX_TRANSPARENT) != 0) return false;
            if ((ex & N.WS_EX_LAYERED) != 0)
            {
                uint k, fl; byte a;
                if (N.GetLayeredWindowAttributes(h, out k, out a, out fl) && (fl & N.LWA_ALPHA) != 0 && a < 200) return false;
            }
            return true;
        }

        static double Area(Region r)
        {
            double a = 0;
            using (var m = new Matrix())
                foreach (var rc in r.GetRegionScans(m)) a += rc.Width * (double)rc.Height;
            return a;
        }

        public void Update()
        {
            bool any = Config.S.AnchorsAll || Manager.I.Map.Values.Any(m => m.Anchor);
            if (!any) { Clear(); return; }
            var covered = new List<IntPtr>();
            using (var screens = new Region(Rectangle.Empty))
            using (var occ = new Region(Rectangle.Empty))
            {
                foreach (var sc in Screen.AllScreens) screens.Union(sc.Bounds);
                foreach (var h in Win.TopLevelWindows())
                {
                    if (!N.IsWindowVisible(h) || N.IsIconic(h) || Win.IsCloaked(h) || Win.IsOurs(h)) continue;
                    bool alt = Win.IsAltTab(h);
                    Rectangle f = Win.Frame(h);
                    if (f.Width <= 0 || f.Height <= 0) continue;
                    if (alt && Wanted(h) && !Manager.I.IsHiddenSource(h))
                    {
                        using (var reg = new Region(f))
                        {
                            reg.Intersect(screens);
                            reg.Exclude(occ);
                            double vis = reg.IsEmpty(probeG) ? 0 : Area(reg);
                            if (vis < Math.Max(400, f.Width * (double)f.Height * 0.004)) covered.Add(h);
                        }
                    }
                    if (IsOccluder(h, alt)) occ.Union(f);
                }
            }
            // ordre : la plus récemment utilisée d'abord
            var mru = Manager.I.Mru;
            covered = covered.OrderBy(h => { int i = mru.IndexOf(h); return i < 0 ? 999 : i; }).ToList();
            if (Config.S.AnchorMax > 0 && covered.Count > Config.S.AnchorMax) covered = covered.Take(Config.S.AnchorMax).ToList();

            foreach (var h in icons.Keys.ToList())
                if (!covered.Contains(h)) { icons[h].Close(); icons.Remove(h); }

            var placed = new List<Rectangle>();
            var procCount = covered.GroupBy(h => Win.ProcessName(h)).ToDictionary(g => g.Key, g => g.Count());
            for (int i = 0; i < covered.Count; i++)
            {
                IntPtr h = covered[i];
                AnchorIcon ic;
                if (!icons.TryGetValue(h, out ic)) { ic = new AnchorIcon(h); icons[h] = ic; }
                Rectangle f = Win.Frame(h);
                float s = Win.ScaleAt(f);
                double scale = 1;
                if (Config.S.AnchorEmphasisSize && i < Config.S.AnchorEmphasisCount && Config.S.AnchorEmphasisCount > 0)
                    scale = Config.S.AnchorMaxScale - (Config.S.AnchorMaxScale - 1) * i / Config.S.AnchorEmphasisCount;
                int size = (int)(Config.S.AnchorSize * s * scale);
                int alpha = 255;
                if (Config.S.AnchorEmphasisOpacity && covered.Count > 1)
                    alpha = (int)(255 - (255 - Config.S.AnchorMinOpacity * 2.55) * i / (covered.Count - 1));
                string proc = Win.ProcessName(h);
                bool showTitle = !Config.S.AnchorTitleDuplicatesOnly || (procCount.ContainsKey(proc) && procCount[proc] > 1);
                Rectangle work = Win.MonitorBounds(f, true);
                var vis = Rectangle.Intersect(f, work);
                if (vis.IsEmpty) vis = work;
                int m = (int)(10 * s);
                int x;
                switch (Config.S.AnchorSide)
                {
                    case "left": x = vis.Left + m; break;
                    case "center": x = vis.Left + vis.Width / 2 - size / 2; break;
                    default: x = vis.Right - size - m; break;
                }
                int y = vis.Top + vis.Height / 2 - size / 2;
                int fullH = size + (showTitle ? (int)(18 * s) : 0);
                var r = new Rectangle(x, y, size, fullH);
                int guard = 0;
                while (placed.Any(p => p.IntersectsWith(r)) && guard++ < 40)
                {
                    r.Y += fullH / 2 + (int)(4 * s);
                    if (r.Bottom > work.Bottom) { r.Y = work.Top + m; r.X += (Config.S.AnchorSide == "left" ? 1 : -1) * (size + m); }
                }
                placed.Add(r);
                ic.Place(r.Location, size, s, showTitle, (byte)Math.Max(30, Math.Min(255, alpha)));
            }
        }

        public void Clear()
        {
            foreach (var ic in icons.Values) ic.Close();
            icons.Clear();
        }
    }

    public class AnchorIcon : LayeredForm
    {
        readonly IntPtr target;
        Bitmap icon;
        bool hot, showTitle;
        int size; float s; byte alpha;
        Point pos;
        readonly ToolTip tip = new ToolTip();
        string title = "";

        public AnchorIcon(IntPtr target) : base(false, true)
        {
            this.target = target;
            Text = "dwaTop anchor";
            AllowDrop = true;
            icon = Win.WindowIcon(target, 48);
            var h = Handle;
        }

        public void Place(Point p, int size, float s, bool showTitle, byte alpha)
        {
            string t = Win.Title(target);
            bool changed = p != pos || size != this.size || showTitle != this.showTitle || alpha != this.alpha || t != title || !Visible;
            pos = p; this.size = size; this.s = s; this.showTitle = showTitle; this.alpha = alpha;
            if (t != title) { title = t; tip.SetToolTip(this, t); }
            if (changed) Render();
            ShowNoActivate();
        }

        void Render()
        {
            int th = showTitle ? (int)(18 * s) : 0;
            int w = Math.Max(size, showTitle ? (int)(size * 2.2) : size);
            using (var bmp = new Bitmap(w, size + th, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Theme.HQ(g);
                    float cx = (w - size) / 2f;
                    var circle = new RectangleF(cx + 1, 1, size - 2, size - 2);
                    using (var b = new SolidBrush(hot ? Color.FromArgb(245, 50, 50, 56) : Color.FromArgb(225, 24, 24, 27))) g.FillEllipse(b, circle);
                    using (var pen = new Pen(hot ? Theme.Accent : Color.FromArgb(90, 255, 255, 255), Math.Max(1, s))) g.DrawEllipse(pen, circle);
                    float isz = size * 0.56f;
                    var ir = new RectangleF(cx + (size - isz) / 2, (size - isz) / 2, isz, isz);
                    if (icon != null) g.DrawImage(icon, ir);
                    else Icons.Draw(g, "logo", ir, Theme.Text);
                    if (showTitle)
                        using (var f = new Font("Segoe UI", 8.5f * s, GraphicsUnit.Pixel))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                            var tr = new RectangleF(0, size + 1, w, th);
                            using (var p = Theme.Round(new RectangleF(2, size + 1, w - 4, th - 2), (th - 2) / 2f))
                            using (var b = new SolidBrush(Color.FromArgb(215, 20, 20, 22))) g.FillPath(b, p);
                            using (var b = new SolidBrush(Theme.Text)) g.DrawString(title, f, b, new RectangleF(4, size + 2, w - 8, th - 2), sf);
                        }
                }
                SetBitmap(bmp, new Point(pos.X - (w - size) / 2, pos.Y), hot ? (byte)255 : alpha);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; Render(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; Render(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!N.IsWindow(target)) return;
            if (e.Button == MouseButtons.Left) { Win.ForceForeground(target); Hide(); }
            else if (e.Button == MouseButtons.Right) DoAction(Config.S.AnchorRightClick);
            else if (e.Button == MouseButtons.Middle) DoAction(Config.S.AnchorMiddleClick);
        }

        void DoAction(string a)
        {
            switch (a)
            {
                case "minimize": N.ShowWindow(target, N.SW_MINIMIZE); Hide(); break;
                case "close": N.PostMessage(target, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); Hide(); break;
                case "menu":
                    var menu = Menus.Create();
                    Menus.Add(menu.Items, "Afficher", delegate { Win.ForceForeground(target); });
                    Menus.Add(menu.Items, "Réduire", delegate { N.ShowWindow(target, N.SW_MINIMIZE); });
                    Menus.Add(menu.Items, "Panneau dwaTop", delegate { QuickPanel.ShowFor(target, Bounds); });
                    Menus.Add(menu.Items, "Fermer la fenêtre", delegate { N.PostMessage(target, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); });
                    menu.Closed += delegate { BeginInvoke(new MethodInvoker(menu.Dispose)); };
                    menu.Show(Win.Cursor());
                    break;
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            e.Effect = DragDropEffects.None;
            if (N.IsWindow(target)) Win.ForceForeground(target);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tip.Dispose();
            if (icon != null) { icon.Dispose(); icon = null; }
            base.OnFormClosed(e);
        }
    }
}
