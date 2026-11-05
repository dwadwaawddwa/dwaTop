using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Miniature vivante (DWM) d'une fenêtre ou d'une zone recadrée, toujours au premier plan.</summary>
    public class PipWindow : Form
    {
        public static bool TestOffscreen;
        readonly ManagedWindow mw;
        readonly IntPtr src;
        IntPtr thumb;
        Rectangle cropRel;                 // zone relative au cadre visible, vide = fenêtre entière
        Rectangle lastSrc, lastDest;
        float s = 1;
        bool interact, hover, dragging, downBody, closing, titleOnHover;
        Point dragStart, formStart;
        int hoverSince;
        byte opacity = 255;
        string title = "";
        Bitmap icon;
        HoverPreview preview;
        DarkOverlay darkOv;
        readonly Timer timer = new Timer();
        int tick;
        string hot;
        Rectangle bInteract, bCrop, bRestore, bClose;

        public PipWindow(ManagedWindow mw, Rectangle cropRel)
        {
            this.mw = mw;
            src = mw.Hwnd;
            this.cropRel = cropRel;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            MaximizeBox = false;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            title = Win.Title(src);
            Text = "dwaTop — " + title;
            interact = Config.S.PipInteractApps.Split(',').Select(x => x.Trim().ToLowerInvariant()).Contains(mw.Process);
            icon = Win.WindowIcon(src, 32);

            Rectangle fr = Win.Frame(src);
            s = Win.ScaleAt(fr);
            var h = Handle;
            Win.SetCorner(h, 2);
            if (N.DwmRegisterThumbnail(h, src, out thumb) != 0) thumb = IntPtr.Zero;
            PlaceInitial();
            timer.Interval = 60;
            timer.Tick += delegate { try { OnTick(); } catch (Exception ex) { Log.Error(ex); } };
            timer.Start();
            SyncDark();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= N.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        int TitleH { get { return (Config.S.PipTitleBar || (titleOnHover && hover)) ? (int)(26 * s) : 0; } }
        int BarReserved { get { return Config.S.PipTitleBar ? (int)(26 * s) : 0; } }

        /// <summary>Zone source dans l'espace "rectangle de fenêtre" (ce qu'attend DWM).</summary>
        Rectangle SourceRect()
        {
            Rectangle wr = Win.WindowRect(src), fr = Win.Frame(src);
            var full = new Rectangle(fr.X - wr.X, fr.Y - wr.Y, fr.Width, fr.Height);
            if (cropRel.IsEmpty) return full;
            var r = new Rectangle(full.X + cropRel.X, full.Y + cropRel.Y, cropRel.Width, cropRel.Height);
            r.Intersect(full);
            return r.Width > 4 && r.Height > 4 ? r : full;
        }

        double Ratio()
        {
            Rectangle r = SourceRect();
            return r.Height > 0 ? r.Width / (double)r.Height : 16.0 / 9;
        }

        Rectangle DestRect()
        {
            int tb = TitleH;
            var area = new Rectangle(0, tb, ClientSize.Width, Math.Max(1, ClientSize.Height - tb));
            double ratio = Ratio();
            int w = area.Width, h = (int)Math.Round(w / ratio);
            if (h > area.Height) { h = area.Height; w = (int)Math.Round(h * ratio); }
            return new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, Math.Max(1, w), Math.Max(1, h));
        }

        public Rectangle ThumbScreenRect()
        {
            Rectangle d = DestRect();
            return new Rectangle(Left + d.X, Top + d.Y, d.Width, d.Height);
        }

        string Key { get { return mw.Process + (cropRel.IsEmpty ? "" : "#crop"); } }

        void PlaceInitial()
        {
            Rectangle fr = Win.Frame(src);
            Rectangle work = Win.MonitorBounds(fr, true);
            double ratio = Ratio();
            int[] saved;
            int w;
            Point pos;
            if (Config.S.PipRemember && Config.S.PipPlacements.TryGetValue(Key, out saved) && saved != null && saved.Length >= 3
                && Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(new Rectangle(saved[0], saved[1], Math.Max(40, saved[2]), 40))))
            {
                w = saved[2];
                pos = new Point(saved[0], saved[1]);
            }
            else
            {
                Rectangle srcR = SourceRect();
                w = (int)Math.Min(Config.S.PipDefaultWidth * s, Math.Max(srcR.Width * 0.5, 200 * s));
                if (!cropRel.IsEmpty) w = (int)Math.Min(Math.Max(srcR.Width, 160 * s), work.Width * 0.4);
                int hh = (int)(w / ratio) + BarReserved;
                int others = Manager.I.Map.Values.Count(m => m.Pip != null && m.Pip != this);
                int m2 = (int)(20 * s);
                pos = new Point(work.Right - w - m2, work.Bottom - hh - m2 - others * (int)(36 * s));
                if (pos.Y < work.Top) pos.Y = work.Top + m2;
            }
            w = Math.Max((int)(140 * s), w);
            if (TestOffscreen) pos = new Point(SystemInformation.VirtualScreen.Right + 400, SystemInformation.VirtualScreen.Top + 50);
            Bounds = new Rectangle(pos, new Size(w, (int)Math.Round(w / ratio) + BarReserved));
        }

        public void SetCrop(Rectangle rel)
        {
            cropRel = rel;
            double ratio = Ratio();
            int w = Width;
            Rectangle work = Win.MonitorBounds(Bounds, true);
            if (w / ratio > work.Height * 0.8) w = (int)(work.Height * 0.8 * ratio);
            Size = new Size(w, (int)Math.Round(w / ratio) + BarReserved);
            UpdateThumb(true);
            Invalidate();
        }

        public void ClearCrop() { SetCrop(Rectangle.Empty); }

        public bool IsCropped { get { return !cropRel.IsEmpty; } }

        void UpdateThumb(bool force)
        {
            if (thumb == IntPtr.Zero) return;
            Rectangle sr = SourceRect(), d = DestRect();
            if (!force && sr == lastSrc && d == lastDest) return;
            lastSrc = sr; lastDest = d;
            var p = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = N.DWM_TNP_RECTDESTINATION | N.DWM_TNP_RECTSOURCE | N.DWM_TNP_VISIBLE | N.DWM_TNP_OPACITY | N.DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = new RECT(d),
                rcSource = new RECT(sr),
                opacity = 255, // l'opacité de la fenêtre PiP (Form.Opacity) s'applique déjà à la miniature
                fVisible = 1,
                fSourceClientAreaOnly = 0
            };
            N.DwmUpdateThumbnailProperties(thumb, ref p);
            if (darkOv != null) darkOv.Sync();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateThumb(false);
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (darkOv != null) darkOv.Sync();
        }

        public void SyncDark()
        {
            bool want = !string.IsNullOrEmpty(mw.Dark) && mw.DarkOv != null;
            if (want && darkOv == null) darkOv = new DarkOverlay(Handle, mw.Dark, ThumbScreenRect, false, mw.DarkOv);
            else if (want) darkOv.SetFilter(mw.Dark);
            else if (darkOv != null) { darkOv.Close(); darkOv = null; }
            if (preview != null) preview.SyncDark(mw);
        }

        void OnTick()
        {
            if (closing) return;
            if (!N.IsWindow(src)) { Manager.I.ClosePip(mw, false); return; }
            tick++;
            if (tick % 4 == 0)
            {
                // la fenêtre source a changé de taille : on garde la largeur et on adapte la hauteur
                Rectangle sr = SourceRect();
                if (sr.Size != lastSrc.Size && !dragging && Control.MouseButtons == MouseButtons.None)
                {
                    double ratio = Ratio();
                    int want = (int)Math.Round(Width / ratio) + BarReserved;
                    if (Math.Abs(want - Height) > 2) Height = want;
                }
                UpdateThumb(false);
            }
            if (tick % 16 == 0)
            {
                string t = Win.Title(src);
                if (t != title) { title = t; Text = "dwaTop — " + t; Invalidate(); }
            }
            // survol
            bool over = Visible && Bounds.Contains(Win.Cursor());
            if (over != hover)
            {
                hover = over;
                hoverSince = Environment.TickCount;
                if (!hover) { hot = null; HidePreview(); }
                if (!Config.S.PipTitleBar) { titleOnHover = true; UpdateThumb(true); }
                Invalidate();
            }
            if (hover && !interact && Config.S.PipHoverPreview && !dragging && Control.MouseButtons == MouseButtons.None
                && Environment.TickCount - hoverSince > 380 && (preview == null || !preview.Visible))
                ShowPreview();
        }

        void ShowPreview()
        {
            if (preview == null || preview.IsDisposed) preview = new HoverPreview(src);
            preview.ShowFor(this, SourceRect(), mw);
        }

        void HidePreview()
        {
            if (preview != null && !preview.IsDisposed) preview.HidePreview();
        }

        // ───────── dessin ─────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HQ(g);
            g.Clear(Theme.Bg);
            int tb = TitleH;
            if (tb > 0)
            {
                using (var b = new SolidBrush(Theme.Panel)) g.FillRectangle(b, 0, 0, Width, tb);
                float ic = 16 * s, pad = 6 * s;
                if (icon != null) g.DrawImage(icon, pad, (tb - ic) / 2, ic, ic);
                float bw = 24 * s;
                bClose = new Rectangle((int)(Width - bw - 2 * s), 0, (int)bw, tb);
                bRestore = new Rectangle(bClose.X - (int)bw, 0, (int)bw, tb);
                bCrop = new Rectangle(bRestore.X - (int)bw, 0, (int)bw, tb);
                bInteract = new Rectangle(bCrop.X - (int)bw, 0, (int)bw, tb);
                DrawBtn(g, bInteract, interact ? "interact" : "click", "interact", interact);
                DrawBtn(g, bCrop, "crop", "crop", !cropRel.IsEmpty);
                DrawBtn(g, bRestore, "restore", "restore", false);
                DrawBtn(g, bClose, "close", "close", false);
                using (var f = new Font("Segoe UI", 8.5f * s, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Theme.Text))
                {
                    var tr = new RectangleF(pad + ic + pad, 0, bInteract.X - (pad + ic + pad) - 2, tb);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString(title.Length > 0 ? title : mw.Process, f, b, tr, sf);
                }
            }
            if (interact && hover)
            {
                using (var pen = new Pen(Color.FromArgb(160, Theme.Accent), Math.Max(1, s)))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        void DrawBtn(Graphics g, Rectangle r, string iconName, string id, bool on)
        {
            if (hot == id)
                using (var b = new SolidBrush(id == "close" ? Color.FromArgb(200, 196, 43, 28) : Theme.TileHover)) g.FillRectangle(b, r);
            float ic = 14 * s;
            Icons.Draw(g, iconName, new RectangleF(r.X + (r.Width - ic) / 2, r.Y + (r.Height - ic) / 2, ic, ic), on ? Theme.Accent : Theme.Text);
        }

        string HitButton(Point p)
        {
            if (TitleH == 0) return null;
            if (bClose.Contains(p)) return "close";
            if (bRestore.Contains(p)) return "restore";
            if (bCrop.Contains(p)) return "crop";
            if (bInteract.Contains(p)) return "interact";
            return null;
        }

        // ───────── souris ─────────

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)N.WM_NCHITTEST)
            {
                long lp = m.LParam.ToInt64();
                var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int e = (int)(6 * s);
                bool l = pt.X < e, r = pt.X >= Width - e, t = pt.Y < e, b = pt.Y >= Height - e;
                if (t && l) { m.Result = new IntPtr(N.HTTOPLEFT); return; }
                if (t && r) { m.Result = new IntPtr(N.HTTOPRIGHT); return; }
                if (b && l) { m.Result = new IntPtr(N.HTBOTTOMLEFT); return; }
                if (b && r) { m.Result = new IntPtr(N.HTBOTTOMRIGHT); return; }
                if (l) { m.Result = new IntPtr(N.HTLEFT); return; }
                if (r) { m.Result = new IntPtr(N.HTRIGHT); return; }
                if (t) { m.Result = new IntPtr(N.HTTOP); return; }
                if (b) { m.Result = new IntPtr(N.HTBOTTOM); return; }
                m.Result = new IntPtr(N.HTCLIENT);
                return;
            }
            if (m.Msg == (int)N.WM_SIZING)
            {
                // garde le ratio pendant le redimensionnement
                var rc = (RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(RECT));
                int edge = m.WParam.ToInt32();
                double ratio = Ratio();
                int bar = BarReserved;
                int minW = (int)(120 * s);
                int w = Math.Max(minW, rc.Width);
                int h = (int)Math.Round(w / ratio) + bar;
                if (edge == 3 || edge == 6) { h = Math.Max(rc.Height, (int)(minW / ratio) + bar); w = (int)Math.Round((h - bar) * ratio); }
                if (edge == 1 || edge == 4 || edge == 7) rc.Left = rc.Right - w; else rc.Right = rc.Left + w;
                if (edge == 3 || edge == 4 || edge == 5) rc.Top = rc.Bottom - h; else rc.Bottom = rc.Top + h;
                System.Runtime.InteropServices.Marshal.StructureToPtr(rc, m.LParam, false);
                m.Result = new IntPtr(1);
                return;
            }
            if (m.Msg == (int)N.WM_DPICHANGED)
            {
                s = (m.WParam.ToInt32() & 0xFFFF) / 96f;
                Invalidate();
                UpdateThumb(true);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        bool InBody(Point p) { return p.Y >= TitleH; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            string btn = HitButton(e.Location);
            if (btn != null) return;
            if (!InBody(e.Location))
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (e.Clicks == 2) { Manager.I.ClosePip(mw, true); return; }
                    downBody = true; dragStart = Win.Cursor(); formStart = Location; dragging = false;
                }
                else if (e.Button == MouseButtons.Right) ShowMenu();
                return;
            }
            if (interact) { ForwardButton(e, true); return; }
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks == 2) { Manager.I.ClosePip(mw, true); return; }
                downBody = true; dragStart = Win.Cursor(); formStart = Location; dragging = false;
                HidePreview();
            }
            else if (e.Button == MouseButtons.Right) ShowMenu();
            else if (e.Button == MouseButtons.Middle) Manager.I.ClosePip(mw, false);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string h = HitButton(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
            if (downBody && Control.MouseButtons.HasFlag(MouseButtons.Left))
            {
                Point c = Win.Cursor();
                if (!dragging && (Math.Abs(c.X - dragStart.X) > 4 || Math.Abs(c.Y - dragStart.Y) > 4)) { dragging = true; HidePreview(); }
                if (dragging) Location = Snap(new Point(formStart.X + c.X - dragStart.X, formStart.Y + c.Y - dragStart.Y));
                return;
            }
            if (interact && InBody(e.Location)) Forward(N.WM_MOUSEMOVE, e.Location, KeyFlags(), 0);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (downBody)
            {
                downBody = false;
                if (dragging) { dragging = false; SavePlacement(); return; }
            }
            string btn = HitButton(e.Location);
            if (btn != null && e.Button == MouseButtons.Left)
            {
                switch (btn)
                {
                    case "close": Manager.I.ClosePip(mw, false); return;
                    case "restore": Manager.I.ClosePip(mw, true); return;
                    case "crop": Manager.I.StartCrop(src); return;
                    case "interact": interact = !interact; HidePreview(); Invalidate(); Osd.Flash("interact", interact ? "Interaction activée" : "Interaction désactivée"); return;
                }
            }
            if (interact && InBody(e.Location)) ForwardButton(e, false);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != null) { hot = null; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (interact && InBody(e.Location) && (ModifierKeys & Keys.Control) == 0)
            {
                Forward(N.WM_MOUSEWHEEL, e.Location, KeyFlags(), e.Delta);
                return;
            }
            if ((ModifierKeys & Keys.Control) != 0)
            {
                int v = Math.Max(20, Math.Min(255, opacity + (e.Delta > 0 ? 25 : -25)));
                SetPipOpacity((byte)v);
                return;
            }
            HidePreview();
            double k = e.Delta > 0 ? 1.12 : 1 / 1.12;
            ResizeAround(Win.Cursor(), (int)(Width * k));
        }

        void ResizeAround(Point anchor, int newW)
        {
            Rectangle work = Win.MonitorBounds(Bounds, true);
            double ratio = Ratio();
            int bar = BarReserved;
            newW = Math.Max((int)(120 * s), Math.Min(newW, (int)(work.Width * 0.98)));
            int newH = (int)Math.Round(newW / ratio) + bar;
            if (newH > work.Height) { newH = work.Height; newW = (int)Math.Round((newH - bar) * ratio); }
            double fx = (anchor.X - Left) / (double)Math.Max(1, Width), fy = (anchor.Y - Top) / (double)Math.Max(1, Height);
            int x = (int)(anchor.X - fx * newW), y = (int)(anchor.Y - fy * newH);
            Bounds = new Rectangle(x, y, newW, newH);
            SavePlacement();
        }

        Point Snap(Point p)
        {
            Rectangle work = Win.MonitorAt(Win.Cursor(), true);
            int d = (int)(14 * s);
            if (Math.Abs(p.X - work.Left) < d) p.X = work.Left;
            if (Math.Abs(p.Y - work.Top) < d) p.Y = work.Top;
            if (Math.Abs(p.X + Width - work.Right) < d) p.X = work.Right - Width;
            if (Math.Abs(p.Y + Height - work.Bottom) < d) p.Y = work.Bottom - Height;
            return p;
        }

        void SetPipOpacity(byte v)
        {
            opacity = v;
            if (v >= 255) Opacity = 1.0; else Opacity = v / 255.0;
            UpdateThumb(true);
        }

        // ───────── interaction (transfert souris / clavier vers la vraie fenêtre) ─────────

        static int KeyFlags()
        {
            int f = 0;
            var mb = Control.MouseButtons;
            if (mb.HasFlag(MouseButtons.Left)) f |= N.MK_LBUTTON;
            if (mb.HasFlag(MouseButtons.Right)) f |= N.MK_RBUTTON;
            if (mb.HasFlag(MouseButtons.Middle)) f |= N.MK_MBUTTON;
            if ((ModifierKeys & Keys.Shift) != 0) f |= N.MK_SHIFT;
            if ((ModifierKeys & Keys.Control) != 0) f |= N.MK_CONTROL;
            return f;
        }

        void ForwardButton(MouseEventArgs e, bool down)
        {
            uint msg;
            if (e.Button == MouseButtons.Left) msg = down ? (e.Clicks == 2 ? N.WM_LBUTTONDBLCLK : N.WM_LBUTTONDOWN) : N.WM_LBUTTONUP;
            else if (e.Button == MouseButtons.Right) msg = down ? N.WM_RBUTTONDOWN : N.WM_RBUTTONUP;
            else if (e.Button == MouseButtons.Middle) msg = down ? N.WM_MBUTTONDOWN : N.WM_MBUTTONUP;
            else return;
            Forward(msg, e.Location, KeyFlags(), 0);
        }

        Point ToSourceScreen(Point client)
        {
            Rectangle d = DestRect(), sr = SourceRect(), wr = Win.WindowRect(src);
            double fx = (client.X - d.X) / (double)Math.Max(1, d.Width), fy = (client.Y - d.Y) / (double)Math.Max(1, d.Height);
            fx = Math.Max(0, Math.Min(0.999, fx)); fy = Math.Max(0, Math.Min(0.999, fy));
            return new Point(wr.X + sr.X + (int)(fx * sr.Width), wr.Y + sr.Y + (int)(fy * sr.Height));
        }

        IntPtr ChildAt(Point screen, out POINT client)
        {
            IntPtr cur = src;
            for (int i = 0; i < 16; i++)
            {
                var p = new POINT(screen.X, screen.Y);
                N.ScreenToClient(cur, ref p);
                IntPtr ch = N.ChildWindowFromPointEx(cur, p, N.CWP_SKIPINVISIBLE | N.CWP_SKIPTRANSPARENT);
                if (ch == IntPtr.Zero || ch == cur) break;
                cur = ch;
            }
            var cp = new POINT(screen.X, screen.Y);
            N.ScreenToClient(cur, ref cp);
            client = cp;
            return cur;
        }

        void Forward(uint msg, Point client, int flags, int wheel)
        {
            Point sp = ToSourceScreen(client);
            POINT cp;
            IntPtr target = ChildAt(sp, out cp);
            if (msg == N.WM_MOUSEWHEEL)
                N.PostMessage(target, msg, new IntPtr((wheel << 16) | (flags & 0xFFFF)), N.MakeLParam(sp.X, sp.Y));
            else
                N.PostMessage(target, msg, new IntPtr(flags), N.MakeLParam(cp.X, cp.Y));
        }

        static bool IsCharKey(Keys k)
        {
            if (k >= Keys.A && k <= Keys.Z) return true;
            if (k >= Keys.D0 && k <= Keys.D9) return true;
            if (k >= Keys.NumPad0 && k <= Keys.Divide) return true;
            if (k == Keys.Space || (k >= Keys.Oem1 && k <= Keys.Oem102) || k == Keys.OemMinus || k == Keys.Oemplus || k == Keys.Oemcomma || k == Keys.OemPeriod) return true;
            return false;
        }

        public override bool PreProcessMessage(ref Message msg)
        {
            if (interact)
            {
                int m = msg.Msg;
                bool key = m == N.WM_KEYDOWN || m == N.WM_KEYUP || m == N.WM_SYSKEYDOWN || m == N.WM_SYSKEYUP;
                if (key || m == N.WM_CHAR)
                {
                    IntPtr target = FocusTarget();
                    if (m == N.WM_CHAR) { N.PostMessage(target, (uint)m, msg.WParam, msg.LParam); return true; }
                    var k = (Keys)(msg.WParam.ToInt32() & 0xFFFF);
                    if (k == Keys.Escape && m == N.WM_KEYDOWN && (ModifierKeys & Keys.Shift) != 0) { interact = false; Invalidate(); return true; }
                    if (IsCharKey(k) && (ModifierKeys & (Keys.Control | Keys.Alt)) == 0) return false; // laisse naître le WM_CHAR
                    N.PostMessage(target, (uint)m, msg.WParam, msg.LParam);
                    return true;
                }
            }
            return base.PreProcessMessage(ref msg);
        }

        IntPtr FocusTarget()
        {
            var gi = new GUITHREADINFO();
            gi.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GUITHREADINFO));
            if (N.GetGUIThreadInfo(Win.Thread(src), ref gi) && gi.hwndFocus != IntPtr.Zero) return gi.hwndFocus;
            return src;
        }

        // ───────── menu ─────────

        void ShowMenu()
        {
            HidePreview();
            var menu = Menus.Create();
            Menus.Add(menu.Items, "Interagir avec la fenêtre", delegate { interact = !interact; Invalidate(); }, interact);
            Menus.Add(menu.Items, "Afficher la fenêtre originale", delegate { Manager.I.SetSourceHidden(mw, !mw.HiddenByPip); }, !mw.HiddenByPip);
            Menus.Add(menu.Items, "Barre de titre", delegate { Config.S.PipTitleBar = !Config.S.PipTitleBar; Config.Save(); RelayoutAll(); }, Config.S.PipTitleBar);
            Menus.Add(menu.Items, "Aperçu agrandi au survol", delegate { Config.S.PipHoverPreview = !Config.S.PipHoverPreview; Config.Save(); }, Config.S.PipHoverPreview);
            Menus.Sep(menu.Items);
            var op = Menus.Sub(menu.Items, "Opacité de la miniature");
            foreach (int v in new[] { 100, 90, 75, 60, 45, 30 })
            {
                int vv = v;
                Menus.Add(op.DropDownItems, v + " %", delegate { SetPipOpacity((byte)(vv * 255 / 100)); }, Math.Abs(opacity * 100 / 255 - v) < 3);
            }
            var sz = Menus.Sub(menu.Items, "Taille");
            foreach (int v in new[] { 20, 30, 50, 75, 100 })
            {
                int vv = v;
                Menus.Add(sz.DropDownItems, v + " % de l'original", delegate
                {
                    Rectangle sr = SourceRect();
                    ResizeAround(new Point(Left + Width / 2, Top + Height / 2), sr.Width * vv / 100);
                });
            }
            Menus.Sep(menu.Items);
            Menus.Add(menu.Items, "Recadrer une zone…", delegate { Manager.I.StartCrop(src); });
            Menus.Add(menu.Items, "Annuler le recadrage", ClearCrop, null, !cropRel.IsEmpty);
            Menus.Add(menu.Items, "Mode sombre", delegate { Manager.I.ToggleDark(src); }, !string.IsNullOrEmpty(mw.Dark));
            Menus.Sep(menu.Items);
            Menus.Add(menu.Items, "Cacher toutes les miniatures", delegate { Manager.I.ToggleAllPips(); });
            Menus.Add(menu.Items, "Restaurer la fenêtre", delegate { Manager.I.ClosePip(mw, true); });
            Menus.Add(menu.Items, "Fermer la miniature", delegate { Manager.I.ClosePip(mw, false); });
            menu.Closed += delegate { BeginInvoke(new MethodInvoker(menu.Dispose)); };
            menu.Show(Win.Cursor());
        }

        static void RelayoutAll()
        {
            foreach (var m in Manager.I.Map.Values) if (m.Pip != null) m.Pip.Relayout();
        }

        public void Relayout()
        {
            Height = (int)Math.Round(Width / Ratio()) + BarReserved;
            UpdateThumb(true);
            Invalidate();
        }

        void SavePlacement()
        {
            if (!Config.S.PipRemember) return;
            Config.S.PipPlacements[Key] = new[] { Left, Top, Width };
        }

        public void HideAll()
        {
            HidePreview();
            Hide();
        }

        /// <summary>Fermeture pilotée par le Manager (ne touche pas à l'état de la source).</summary>
        public void CloseQuiet()
        {
            if (closing) return;
            closing = true;
            timer.Stop();
            SavePlacement();
            Config.Save();
            if (thumb != IntPtr.Zero) { N.DwmUnregisterThumbnail(thumb); thumb = IntPtr.Zero; }
            if (darkOv != null) { darkOv.Close(); darkOv = null; }
            if (preview != null) { preview.Close(); preview = null; }
            if (icon != null) { icon.Dispose(); icon = null; }
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                BeginInvoke(new MethodInvoker(delegate { Manager.I.ClosePip(mw, false); }));
                return;
            }
            base.OnFormClosing(e);
        }
    }

    /// <summary>Grand aperçu affiché à côté d'une miniature pendant le survol.</summary>
    public class HoverPreview : Form
    {
        readonly IntPtr src;
        IntPtr thumb;
        DarkOverlay darkOv;

        public HoverPreview(IntPtr src)
        {
            this.src = src;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Bg;
            var h = Handle;
            N.SetLayeredWindowAttributes(h, 0, 255, N.LWA_ALPHA);
            Win.SetCorner(h, 2);
            if (N.DwmRegisterThumbnail(h, src, out thumb) != 0) thumb = IntPtr.Zero;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= N.WS_EX_TOOLWINDOW | N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT | N.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public Rectangle ScreenRectOfThumb() { return Bounds; }

        public void ShowFor(Form pip, Rectangle sourceRect, ManagedWindow mw)
        {
            if (thumb == IntPtr.Zero || sourceRect.Width < 4) return;
            Rectangle pr = pip.Bounds;
            Rectangle work = Win.MonitorBounds(pr, true);
            double pct = Math.Max(25, Math.Min(90, Config.S.PipHoverPreviewPercent)) / 100.0;
            double k = Math.Min(Math.Min(work.Width * pct / sourceRect.Width, work.Height * pct / sourceRect.Height), 1.0);
            int w = Math.Max(1, (int)(sourceRect.Width * k)), h = Math.Max(1, (int)(sourceRect.Height * k));
            if (w <= pr.Width * 1.15) return; // pas plus grand que la miniature : inutile
            int gap = (int)(10 * Win.ScaleAt(pr));
            Point pos;
            if (pr.Left - work.Left >= w + gap) pos = new Point(pr.Left - w - gap, pr.Top + pr.Height / 2 - h / 2);
            else if (work.Right - pr.Right >= w + gap) pos = new Point(pr.Right + gap, pr.Top + pr.Height / 2 - h / 2);
            else if (pr.Top - work.Top >= h + gap) pos = new Point(pr.Left + pr.Width / 2 - w / 2, pr.Top - h - gap);
            else pos = new Point(pr.Left + pr.Width / 2 - w / 2, pr.Bottom + gap);
            pos.X = Math.Max(work.Left, Math.Min(pos.X, work.Right - w));
            pos.Y = Math.Max(work.Top, Math.Min(pos.Y, work.Bottom - h));
            Bounds = new Rectangle(pos, new Size(w, h));
            var p = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = N.DWM_TNP_RECTDESTINATION | N.DWM_TNP_RECTSOURCE | N.DWM_TNP_VISIBLE | N.DWM_TNP_OPACITY | N.DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = new RECT(0, 0, w, h),
                rcSource = new RECT(sourceRect),
                opacity = 255,
                fVisible = 1
            };
            N.DwmUpdateThumbnailProperties(thumb, ref p);
            if (!Visible) Show();
            N.SetWindowPos(Handle, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            SyncDark(mw);
        }

        public void SyncDark(ManagedWindow mw)
        {
            bool want = Visible && !string.IsNullOrEmpty(mw.Dark) && mw.DarkOv != null;
            if (want && darkOv == null) darkOv = new DarkOverlay(Handle, mw.Dark, ScreenRectOfThumb, false, mw.DarkOv);
            else if (!want && darkOv != null) { darkOv.Close(); darkOv = null; }
            if (darkOv != null) darkOv.Sync();
        }

        public void HidePreview()
        {
            if (darkOv != null) { darkOv.Close(); darkOv = null; }
            if (Visible) Hide();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (darkOv != null) { darkOv.Close(); darkOv = null; }
            if (thumb != IntPtr.Zero) { N.DwmUnregisterThumbnail(thumb); thumb = IntPtr.Zero; }
            base.OnFormClosed(e);
        }
    }
}
