using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Petit bouton posé dans la barre de titre de la fenêtre active (à gauche de "réduire").</summary>
    public class TitleButton : LayeredForm
    {
        IntPtr target;
        bool hot, shown, lastHot;
        Rectangle lastRect;
        int lastEffects = -1;
        float lastScale;
        readonly Timer timer = new Timer();

        public TitleButton() : base(false, true)
        {
            Text = "dwaTop button";
            var h = Handle;
            timer.Interval = 200;
            timer.Tick += delegate { try { Update(); } catch (Exception ex) { Log.Error(ex); } };
            timer.Start();
        }

        public IntPtr Target { get { return target; } }

        public void Track(IntPtr fg)
        {
            if (fg == IntPtr.Zero || Win.IsOurs(fg)) return;
            target = fg;
            Update();
        }

        public new void Update()
        {
            if (IsDisposed) return;
            if (QuickPanel.IsOpen) { if (shown) Position(); return; }
            IntPtr fg = N.GetForegroundWindow();
            if (fg != IntPtr.Zero && !Win.IsOurs(fg)) target = fg;
            bool ok = Config.S.TitleButton && Win.IsEligible(target) && !N.IsIconic(target) && !Win.IsFullscreen(target)
                && !Manager.I.IsHiddenSource(target);
            if (ok && Config.S.TitleButtonHoverOnly)
            {
                Rectangle f = Win.Frame(target);
                float s = Win.ScaleAt(f);
                var strip = new Rectangle(f.X, f.Y, f.Width, (int)(44 * s));
                if (!strip.Contains(Win.Cursor())) ok = false;
            }
            if (!ok) { HideButton(); return; }
            Position();
        }

        void Position()
        {
            Rectangle r = Compute(target);
            if (r.IsEmpty) { HideButton(); return; }
            var mw = Manager.I.Get(target, false);
            int eff = mw != null ? mw.EffectCount : 0;
            float s = Win.ScaleAt(r);
            if (r.Size != lastRect.Size || eff != lastEffects || hot != lastHot || s != lastScale || !shown)
            {
                Render(r, eff, s);
                lastEffects = eff; lastHot = hot; lastScale = s;
            }
            else if (r.Location != lastRect.Location)
                N.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, 0, 0, N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE);
            lastRect = r;
            if (!shown) { ShowNoActivate(); shown = true; }
            Follow.PlaceAbove(Handle, Manager.I.TopFollower(target));
        }

        void HideButton()
        {
            if (!shown) return;
            Hide();
            shown = false;
            hot = false;
        }

        /// <summary>Position à gauche du bouton "réduire" (ou estimation si l'appli dessine sa propre barre).</summary>
        static Rectangle Compute(IntPtr h)
        {
            Rectangle frame = Win.Frame(h);
            float s = Win.ScaleAt(frame);
            int w = (int)Math.Round(30 * s), hh = (int)Math.Round(20 * s);
            var ti = new TITLEBARINFOEX { rgstate = new int[6], rgrect = new RECT[6] };
            ti.cbSize = Marshal.SizeOf(typeof(TITLEBARINFOEX));
            IntPtr res;
            bool ok = N.SendMessageTimeout(h, N.WM_GETTITLEBARINFOEX, IntPtr.Zero, ref ti, N.SMTO_ABORTIFHUNG, 40, out res) != IntPtr.Zero;
            Rectangle btn = Rectangle.Empty;
            if (ok && ti.rgrect != null)
            {
                for (int i = 2; i <= 5; i++)
                {
                    if ((ti.rgstate[i] & 0x8000) != 0) continue;
                    var r = ti.rgrect[i].ToRectangle();
                    if (r.Width <= 0 || r.Height <= 0) continue;
                    if (btn.IsEmpty || r.Left < btn.Left) btn = r;
                }
            }
            bool valid = !btn.IsEmpty && btn.Left > frame.Left + frame.Width / 3 && btn.Right <= frame.Right + 2
                && btn.Top >= frame.Top - 2 && btn.Top < frame.Top + 60 * s && btn.Height < 70 * s;
            int x, y;
            if (valid)
            {
                x = btn.Left - w - (int)(6 * s);
                y = btn.Top + (btn.Height - hh) / 2;
                if (y < frame.Top + 2) y = frame.Top + (int)(5 * s);
            }
            else
            {
                x = frame.Right - (int)(3 * 46 * s) - w - (int)(8 * s);
                y = frame.Top + (int)(5 * s);
            }
            if (x < frame.Left + 4) return Rectangle.Empty;
            return new Rectangle(x, y, w, hh);
        }

        void Render(Rectangle r, int effects, float s)
        {
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Theme.HQ(g);
                    var rect = new RectangleF(0.5f, 0.5f, r.Width - 1, r.Height - 1);
                    using (var p = Theme.Round(rect, r.Height / 2f))
                    {
                        using (var b = new SolidBrush(hot ? Color.FromArgb(240, 62, 62, 70) : Color.FromArgb(205, 30, 30, 34))) g.FillPath(b, p);
                        using (var pen = new Pen(effects > 0 ? Color.FromArgb(200, Theme.Accent) : Color.FromArgb(70, 255, 255, 255), 1)) g.DrawPath(pen, p);
                    }
                    float ic = r.Height * 0.8f;
                    Icons.Draw(g, "chevron", new RectangleF((r.Width - ic) / 2, (r.Height - ic) / 2 + 0.5f, ic, ic), effects > 0 ? Theme.Accent : Color.White);
                }
                SetBitmap(bmp, r.Location, 255);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hot = true; if (shown) Position(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hot = false; if (shown) Position(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (target == IntPtr.Zero || !N.IsWindow(target)) return;
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                QuickPanel.ShowFor(target, Bounds);
            else if (e.Button == MouseButtons.Middle)
                Manager.I.TogglePin(target);
        }
    }

    /// <summary>Panneau d'actions rapides (épingler, opacité, sombre, miniature…).</summary>
    public class QuickPanel : Form
    {
        static QuickPanel inst;
        public static bool IsOpen { get { return inst != null && !inst.IsDisposed && inst.Visible; } }

        class Tile { public string Id, Icon, Label; public Rectangle R; }

        readonly IntPtr target;
        readonly float s;
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<KeyValuePair<string, Rectangle>> chips = new List<KeyValuePair<string, Rectangle>>();
        Rectangle rGear, rSlider, rHover, rReset, rSave, rHeader;
        string hot, title, proc;
        bool sliderDrag, refocus;
        Bitmap icon;

        public static void ShowFor(IntPtr target, Rectangle anchor)
        {
            if (inst != null && !inst.IsDisposed) inst.Close();
            inst = new QuickPanel(target, anchor);
            inst.Show();
            inst.Activate();
            N.SetForegroundWindow(inst.Handle);
        }

        public static void Close2() { if (inst != null && !inst.IsDisposed) inst.Close(); }

        QuickPanel(IntPtr target, Rectangle anchor)
        {
            this.target = target;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Panel;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "dwaTop";
            s = Win.ScaleAt(anchor);
            icon = Win.WindowIcon(target, 32);
            title = Win.Title(target);
            proc = Win.ProcessName(target);
            var h = Handle;
            Win.SetDarkFrame(h);
            DoLayout();
            Rectangle work = Win.MonitorBounds(anchor, true);
            int x = anchor.Right - Width, y = anchor.Bottom + (int)(6 * s);
            if (y + Height > work.Bottom) y = anchor.Top - Height - (int)(6 * s);
            x = Math.Max(work.Left + 4, Math.Min(x, work.Right - Width - 4));
            y = Math.Max(work.Top + 4, Math.Min(y, work.Bottom - Height - 4));
            Location = new Point(x, y);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= N.WS_EX_TOOLWINDOW; return cp; }
        }

        ManagedWindow MW { get { return Manager.I.Get(target, false); } }

        int S(float v) { return (int)Math.Round(v * s); }

        void DoLayout()
        {
            int W = S(328), pad = S(12);
            int y = pad;
            rHeader = new Rectangle(pad, y, W - 2 * pad, S(36));
            rGear = new Rectangle(W - pad - S(30), y + S(3), S(30), S(30));
            y += S(44);
            tiles.Clear();
            string[][] defs = {
                new[] { "pin", "pin", "Épingler" }, new[] { "click", "click", "Click-through" },
                new[] { "dark", "dark", "Sombre" }, new[] { "pip", "pip", "Miniature" },
                new[] { "crop", "crop", "Recadrer" }, new[] { "anchor", "anchor", "Ancre" },
                new[] { "blur", "blur", "Flou" }, new[] { "glass", "glass", "Verre" } };
            int gap = S(6), cols = 4;
            int tw = (W - 2 * pad - gap * (cols - 1)) / cols, th = S(62);
            for (int i = 0; i < defs.Length; i++)
            {
                int c = i % cols, r = i / cols;
                tiles.Add(new Tile { Id = defs[i][0], Icon = defs[i][1], Label = defs[i][2], R = new Rectangle(pad + c * (tw + gap), y + r * (th + gap), tw, th) });
            }
            y += 2 * th + gap + S(14);
            rSlider = new Rectangle(pad, y, W - 2 * pad, S(44));
            y += S(48);
            rHover = new Rectangle(pad, y, W - 2 * pad, S(26));
            y += S(32);
            chips.Clear();
            var mw = MW;
            if (mw != null && !string.IsNullOrEmpty(mw.Dark))
            {
                int cw = (W - 2 * pad - gap * 3) / 4, ch = S(26);
                for (int i = 0; i < Filters.All.Length; i++)
                {
                    int c = i % 4, r = i / 4;
                    chips.Add(new KeyValuePair<string, Rectangle>(Filters.All[i][0], new Rectangle(pad + c * (cw + gap), y + S(18) + r * (ch + gap), cw, ch)));
                }
                y += S(18) + 2 * (S(26) + gap) + S(6);
            }
            int fw = (W - 2 * pad - gap) / 2;
            rReset = new Rectangle(pad, y, fw, S(32));
            rSave = new Rectangle(pad + fw + gap, y, fw, S(32));
            y += S(32) + pad;
            Size = new Size(W, y);
        }

        bool IsOn(string id)
        {
            var mw = MW;
            if (mw == null) return false;
            switch (id)
            {
                case "pin": return mw.Pinned;
                case "click": return mw.ClickThrough;
                case "dark": return !string.IsNullOrEmpty(mw.Dark);
                case "pip": return mw.Pip != null;
                case "crop": return mw.Pip != null && mw.Pip.IsCropped;
                case "anchor": return mw.Anchor || Config.S.AnchorsAll;
                case "blur": return mw.Blur;
                case "glass": return mw.Glass;
            }
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HQ(g);
            g.Clear(Theme.Panel);
            using (var pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            var mw = MW;
            using (var fTitle = new Font("Segoe UI Semibold", 10.5f * s, GraphicsUnit.Pixel))
            using (var fSub = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (var fTile = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (var bText = new SolidBrush(Theme.Text))
            using (var bSub = new SolidBrush(Theme.Sub))
            {
                var sfL = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
                var sfC = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                // en-tête
                float ic = S(22);
                if (icon != null) g.DrawImage(icon, rHeader.X, rHeader.Y + (rHeader.Height - ic) / 2, ic, ic);
                else Icons.Draw(g, "logo", new RectangleF(rHeader.X, rHeader.Y + (rHeader.Height - ic) / 2, ic, ic), Theme.Text);
                float tx = rHeader.X + ic + S(10), tw = rGear.X - tx - S(6);
                g.DrawString(title.Length > 0 ? title : proc, fTitle, bText, new RectangleF(tx, rHeader.Y, tw, rHeader.Height * 0.55f), sfL);
                g.DrawString(proc + (mw != null && mw.DarkOv != null && mw.DarkOv.Paused ? "  ·  sombre en veille (déjà sombre)" : ""), fSub, bSub, new RectangleF(tx, rHeader.Y + rHeader.Height * 0.5f, tw, rHeader.Height * 0.5f), sfL);
                if (hot == "gear") using (var b = new SolidBrush(Theme.TileHover)) using (var p = Theme.Round(rGear, S(6))) g.FillPath(b, p);
                Icons.Draw(g, "gear", Inset(rGear, S(6)), Theme.Text);
                // tuiles
                foreach (var t in tiles)
                {
                    bool on = IsOn(t.Id);
                    Color bg = on ? Theme.TileOn : (hot == t.Id ? Theme.TileHover : Theme.Tile);
                    using (var p = Theme.Round(t.R, S(8)))
                    {
                        using (var b = new SolidBrush(bg)) g.FillPath(b, p);
                        if (on) using (var pen = new Pen(Color.FromArgb(150, Theme.Accent), Math.Max(1, s))) g.DrawPath(pen, p);
                    }
                    float isz = S(22);
                    string iconName = t.Id == "pin" && on ? "pinOn" : t.Icon;
                    Icons.Draw(g, iconName, new RectangleF(t.R.X + (t.R.Width - isz) / 2, t.R.Y + S(9), isz, isz), on ? Theme.Accent : Theme.Text);
                    g.DrawString(t.Label, fTile, on ? new SolidBrush(Theme.Text) : bSub, new RectangleF(t.R.X, t.R.Bottom - S(22), t.R.Width, S(18)), sfC);
                }
                // opacité
                int op = mw != null ? mw.Opacity : 100;
                g.DrawString("Opacité", fTile, bText, new RectangleF(rSlider.X, rSlider.Y, rSlider.Width / 2, S(18)), sfL);
                var sfR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(op + " %", fTile, op < 100 ? new SolidBrush(Theme.Accent) : bSub, new RectangleF(rSlider.X + rSlider.Width / 2, rSlider.Y, rSlider.Width / 2, S(18)), sfR);
                Rectangle track = TrackRect();
                using (var p = Theme.Round(track, track.Height / 2f)) using (var b = new SolidBrush(Theme.Tile)) g.FillPath(b, p);
                float fx = track.X + (op - 10) / 90f * track.Width;
                using (var p = Theme.Round(new RectangleF(track.X, track.Y, Math.Max(track.Height, fx - track.X), track.Height), track.Height / 2f))
                using (var b = new SolidBrush(Color.FromArgb(op < 100 ? 255 : 120, Theme.Accent))) g.FillPath(b, p);
                float kn = S(16);
                using (var b = new SolidBrush(hot == "slider" || sliderDrag ? Color.White : Color.FromArgb(230, 230, 230)))
                    g.FillEllipse(b, fx - kn / 2, track.Y + track.Height / 2f - kn / 2, kn, kn);
                // opaque au survol
                bool hov = mw != null && mw.HoverOpaque;
                DrawSwitch(g, new Rectangle(rHover.X, rHover.Y + (rHover.Height - S(16)) / 2, S(30), S(16)), hov);
                g.DrawString("Opaque au survol de la souris", fTile, hot == "hover" ? bText : bSub, new RectangleF(rHover.X + S(40), rHover.Y, rHover.Width - S(40), rHover.Height), sfL);
                // filtres du mode sombre
                if (chips.Count > 0)
                {
                    g.DrawString("Filtre du mode sombre", fSub, bSub, new RectangleF(chips[0].Value.X, chips[0].Value.Y - S(18), rSlider.Width, S(16)), sfL);
                    foreach (var c in chips)
                    {
                        bool on = mw != null && mw.Dark == c.Key;
                        using (var p = Theme.Round(c.Value, S(6)))
                        {
                            using (var b = new SolidBrush(on ? Theme.TileOn : (hot == "chip:" + c.Key ? Theme.TileHover : Theme.Tile))) g.FillPath(b, p);
                            if (on) using (var pen = new Pen(Theme.Accent)) g.DrawPath(pen, p);
                        }
                        g.DrawString(Filters.Label(c.Key), fSub, on ? bText : bSub, c.Value, sfC);
                    }
                }
                // pied
                FooterBtn(g, rReset, "reset", "Réinitialiser", fTile);
                FooterBtn(g, rSave, "save", "Mémoriser (" + Short(proc) + ")", fTile);
            }
        }

        static string Short(string p)
        {
            if (string.IsNullOrEmpty(p)) return "appli";
            if (p.EndsWith(".exe")) p = p.Substring(0, p.Length - 4);
            return p.Length > 12 ? p.Substring(0, 11) + "…" : p;
        }

        void FooterBtn(Graphics g, Rectangle r, string id, string text, Font f)
        {
            using (var p = Theme.Round(r, S(7)))
            using (var b = new SolidBrush(hot == id ? Theme.TileHover : Theme.Tile)) g.FillPath(b, p);
            float isz = S(15);
            using (var b = new SolidBrush(Theme.Text))
            {
                var ts = g.MeasureString(text, f);
                float total = isz + S(6) + ts.Width;
                float x = r.X + (r.Width - total) / 2;
                Icons.Draw(g, id, new RectangleF(x, r.Y + (r.Height - isz) / 2, isz, isz), Theme.Text);
                g.DrawString(text, f, b, x + isz + S(6), r.Y + (r.Height - ts.Height) / 2);
            }
        }

        void DrawSwitch(Graphics g, Rectangle r, bool on)
        {
            using (var p = Theme.Round(r, r.Height / 2f))
            using (var b = new SolidBrush(on ? Theme.Accent : Theme.Tile)) g.FillPath(b, p);
            float d = r.Height - S(4);
            float x = on ? r.Right - d - S(2) : r.X + S(2);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + S(2), d, d);
        }

        static RectangleF Inset(Rectangle r, int d) { return new RectangleF(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d); }

        Rectangle TrackRect() { return new Rectangle(rSlider.X + S(8), rSlider.Y + S(28), rSlider.Width - S(16), S(6)); }

        string Hit(Point p)
        {
            if (rGear.Contains(p)) return "gear";
            foreach (var t in tiles) if (t.R.Contains(p)) return t.Id;
            var tr = TrackRect(); tr.Inflate(S(6), S(10));
            if (tr.Contains(p)) return "slider";
            if (rHover.Contains(p)) return "hover";
            foreach (var c in chips) if (c.Value.Contains(p)) return "chip:" + c.Key;
            if (rReset.Contains(p)) return "reset";
            if (rSave.Contains(p)) return "save";
            return null;
        }

        void SetSlider(int x)
        {
            Rectangle tr = TrackRect();
            int v = 10 + (int)Math.Round((x - tr.X) / (double)tr.Width * 90);
            v = Math.Max(10, Math.Min(100, (int)(Math.Round(v / 5.0) * 5)));
            var mw = MW;
            if (mw != null && mw.Opacity == v) return;
            if (mw == null && v == 100) return;
            Manager.I.SetOpacity(target, v, false);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (sliderDrag) { SetSlider(e.X); return; }
            string h = Hit(e.Location);
            if (h != hot) { hot = h; Cursor = h != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != null) { hot = null; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (Hit(e.Location) == "slider") { sliderDrag = true; Capture = true; SetSlider(e.X); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (sliderDrag) { sliderDrag = false; Capture = false; Invalidate(); return; }
            if (e.Button != MouseButtons.Left) return;
            string h = Hit(e.Location);
            if (h == null || !N.IsWindow(target)) return;
            Osd.Muted = true;
            try
            {
                switch (h)
                {
                    case "gear": Close(); App.ShowSettings(); return;
                    case "pin": Manager.I.TogglePin(target); break;
                    case "click": Manager.I.ToggleClickThrough(target); break;
                    case "dark": Manager.I.ToggleDark(target); DoLayout(); break;
                    case "pip": refocus = false; Close(); Manager.I.TogglePip(target); return;
                    case "crop": refocus = false; Close(); Manager.I.StartCrop(target); return;
                    case "anchor": Manager.I.ToggleAnchor(target); break;
                    case "blur": Manager.I.ToggleBlur(target); break;
                    case "glass": Manager.I.ToggleGlass(target); break;
                    case "hover": { var mw = MW; Manager.I.SetHoverOpaque(target, mw == null || !mw.HoverOpaque); break; }
                    case "reset": Manager.I.Reset(target, false); DoLayout(); break;
                    case "save": Osd.Muted = false; Manager.I.RememberRule(target); break;
                    default:
                        if (h.StartsWith("chip:"))
                        {
                            string f = h.Substring(5);
                            Config.S.DarkFilter = f;
                            Config.Save();
                            Manager.I.SetDark(target, f);
                        }
                        break;
                }
            }
            finally { Osd.Muted = false; }
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { refocus = true; Close(); }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!IsDisposed) BeginInvoke(new MethodInvoker(delegate { if (!IsDisposed && !sliderDrag) Close(); }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (inst == this) inst = null;
            if (refocus && N.IsWindow(target) && !Manager.I.IsHiddenSource(target)) Win.ForceForeground(target);
            if (icon != null) { icon.Dispose(); icon = null; }
            base.OnFormClosed(e);
        }
    }
}
