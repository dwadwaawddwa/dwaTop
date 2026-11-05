using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DwaTop
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(20, 20, 22);
        public static readonly Color Panel = Color.FromArgb(28, 28, 31);
        public static readonly Color Tile = Color.FromArgb(40, 40, 44);
        public static readonly Color TileHover = Color.FromArgb(52, 52, 58);
        public static readonly Color TileOn = Color.FromArgb(32, 72, 96);
        public static readonly Color Accent = Color.FromArgb(76, 194, 255);
        public static readonly Color Text = Color.FromArgb(240, 240, 240);
        public static readonly Color Sub = Color.FromArgb(150, 150, 156);
        public static readonly Color Line = Color.FromArgb(55, 55, 60);
        public static readonly Color Danger = Color.FromArgb(255, 110, 100);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void HQ(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static Color Parse(string hex, Color fallback)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return fallback; }
        }
    }

    /// <summary>Icônes vectorielles dessinées à la main (grille 24×24).</summary>
    public static class Icons
    {
        public static void Draw(Graphics g, string name, RectangleF r, Color c)
        {
            var st = g.Save();
            g.TranslateTransform(r.X, r.Y);
            float k = Math.Min(r.Width, r.Height) / 24f;
            g.ScaleTransform(k, k);
            using (var pen = new Pen(c, 1.8f))
            using (var br = new SolidBrush(c))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                switch (name)
                {
                    case "pin":
                    case "pinOn":
                        {
                            var s2 = g.Save();
                            g.TranslateTransform(12, 12); g.RotateTransform(45); g.TranslateTransform(-12, -12);
                            var head = new RectangleF(8, 2.5f, 8, 9);
                            using (var p = Theme.Round(head, 2))
                            {
                                if (name == "pinOn") g.FillPath(br, p); else g.DrawPath(pen, p);
                            }
                            g.DrawLine(pen, 5.5f, 12, 18.5f, 12);
                            g.DrawLine(pen, 12, 12, 12, 21.5f);
                            g.Restore(s2);
                            break;
                        }
                    case "opacity":
                        g.DrawEllipse(pen, 4, 4, 16, 16);
                        using (var p = new GraphicsPath()) { p.AddPie(4, 4, 16, 16, 90, 180); g.FillPath(br, p); }
                        break;
                    case "click":
                        {
                            var pts = new[] { new PointF(6, 4), new PointF(6, 19), new PointF(10, 15), new PointF(13, 21), new PointF(15.5f, 20), new PointF(12.5f, 14), new PointF(18, 14) };
                            g.DrawPolygon(pen, pts);
                            using (var dp = new Pen(Color.FromArgb(c.A / 2, c), 1.4f)) { dp.DashStyle = DashStyle.Dot; g.DrawArc(dp, 1, 0, 10, 10, 180, 270); }
                            break;
                        }
                    case "interact":
                        {
                            var pts = new[] { new PointF(6, 3), new PointF(6, 19), new PointF(10, 15), new PointF(13, 21), new PointF(15.5f, 20), new PointF(12.5f, 14), new PointF(18, 14) };
                            g.FillPolygon(br, pts);
                            break;
                        }
                    case "dark":
                        using (var reg = new Region(new RectangleF(4, 4, 16, 16)))
                        {
                            using (var p = new GraphicsPath()) { p.AddEllipse(4, 4, 16, 16); reg.Intersect(p); }
                            using (var p2 = new GraphicsPath()) { p2.AddEllipse(9, 1, 15, 15); reg.Exclude(p2); }
                            g.FillRegion(br, reg);
                        }
                        break;
                    case "pip":
                        using (var p = Theme.Round(new RectangleF(3, 5, 18, 14), 2.5f)) g.DrawPath(pen, p);
                        using (var p = Theme.Round(new RectangleF(11.5f, 11.5f, 7, 5), 1)) g.FillPath(br, p);
                        break;
                    case "crop":
                        g.DrawLines(pen, new[] { new PointF(7, 2.5f), new PointF(7, 17), new PointF(21.5f, 17) });
                        g.DrawLines(pen, new[] { new PointF(2.5f, 7), new PointF(17, 7), new PointF(17, 21.5f) });
                        break;
                    case "anchor":
                        g.DrawEllipse(pen, 10, 2.5f, 4, 4);
                        g.DrawLine(pen, 12, 6.5f, 12, 20.5f);
                        g.DrawLine(pen, 8, 10, 16, 10);
                        g.DrawArc(pen, 4.5f, 6.5f, 15, 14, 20, 140);
                        break;
                    case "blur":
                        using (var b1 = new SolidBrush(Color.FromArgb(c.A / 3, c))) g.FillEllipse(b1, 3, 3, 18, 18);
                        using (var b2 = new SolidBrush(Color.FromArgb(c.A * 2 / 3, c))) g.FillEllipse(b2, 6.5f, 6.5f, 11, 11);
                        g.FillEllipse(br, 9.5f, 9.5f, 5, 5);
                        break;
                    case "glass":
                        using (var p = Theme.Round(new RectangleF(3.5f, 3.5f, 17, 17), 3)) g.DrawPath(pen, p);
                        g.DrawLine(pen, 7, 13, 13, 7);
                        g.DrawLine(pen, 9.5f, 17, 17, 9.5f);
                        break;
                    case "gear":
                        using (var tp = new Pen(c, 3.6f)) { tp.DashPattern = new float[] { 0.75f, 0.75f }; g.DrawEllipse(tp, 5, 5, 14, 14); }
                        g.DrawEllipse(pen, 7, 7, 10, 10);
                        g.DrawEllipse(pen, 10, 10, 4, 4);
                        break;
                    case "save":
                        g.DrawPolygon(pen, new[] { new PointF(6.5f, 3), new PointF(17.5f, 3), new PointF(17.5f, 21), new PointF(12, 16.5f), new PointF(6.5f, 21) });
                        break;
                    case "reset":
                        g.DrawArc(pen, 4, 4, 16, 16, -60, 300);
                        g.FillPolygon(br, new[] { new PointF(17.5f, 1.8f), new PointF(19.5f, 8.5f), new PointF(12.8f, 7.2f) });
                        break;
                    case "close":
                        g.DrawLine(pen, 6, 6, 18, 18);
                        g.DrawLine(pen, 18, 6, 6, 18);
                        break;
                    case "restore":
                        using (var p = Theme.Round(new RectangleF(3, 7, 14, 14), 2)) g.DrawPath(pen, p);
                        g.DrawLine(pen, 11, 13, 20.5f, 3.5f);
                        g.DrawLines(pen, new[] { new PointF(14, 3.5f), new PointF(20.5f, 3.5f), new PointF(20.5f, 10) });
                        break;
                    case "eye":
                        using (var p = new GraphicsPath())
                        {
                            p.AddBezier(2, 12, 7, 4.5f, 17, 4.5f, 22, 12);
                            p.AddBezier(22, 12, 17, 19.5f, 7, 19.5f, 2, 12);
                            g.DrawPath(pen, p);
                        }
                        g.FillEllipse(br, 9, 9, 6, 6);
                        break;
                    case "hide":
                        using (var p = new GraphicsPath())
                        {
                            p.AddBezier(2, 12, 7, 4.5f, 17, 4.5f, 22, 12);
                            p.AddBezier(22, 12, 17, 19.5f, 7, 19.5f, 2, 12);
                            g.DrawPath(pen, p);
                        }
                        g.DrawLine(pen, 4, 20, 20, 4);
                        break;
                    case "chevron":
                        g.DrawLines(pen, new[] { new PointF(6, 9.5f), new PointF(12, 15.5f), new PointF(18, 9.5f) });
                        break;
                    case "logo":
                        using (var p = Theme.Round(new RectangleF(2.5f, 4, 19, 15), 2.5f)) g.DrawPath(pen, p);
                        using (var p = Theme.Round(new RectangleF(11.5f, 11, 7.5f, 5.5f), 1.2f)) g.FillPath(br, p);
                        g.DrawLine(pen, 2.5f, 8, 21.5f, 8);
                        break;
                    default:
                        g.DrawEllipse(pen, 5, 5, 14, 14);
                        break;
                }
            }
            g.Restore(st);
        }

        /// <summary>Icône de l'application (barre des tâches, zone de notification).</summary>
        public static Bitmap AppBitmap(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Theme.HQ(g);
                float pad = size * 0.04f;
                using (var p = Theme.Round(new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad), size * 0.22f))
                using (var b = new SolidBrush(Color.FromArgb(18, 18, 20))) g.FillPath(b, p);
                float s = size;
                using (var pen = new Pen(Color.White, Math.Max(1.2f, s * 0.07f)))
                using (var p = Theme.Round(new RectangleF(s * 0.17f, s * 0.22f, s * 0.66f, s * 0.54f), s * 0.08f))
                    g.DrawPath(pen, p);
                using (var p = Theme.Round(new RectangleF(s * 0.47f, s * 0.47f, s * 0.30f, s * 0.22f), s * 0.04f))
                using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
            }
            return bmp;
        }

        public static Icon AppIcon(int size)
        {
            using (var b = AppBitmap(size))
            {
                IntPtr h = b.GetHicon();
                var ic = (Icon)Icon.FromHandle(h).Clone();
                N.DestroyIcon(h);
                return ic;
            }
        }

        /// <summary>Écrit un .ico multi-tailles (PNG) — utilisé par build.bat.</summary>
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var pngs = new System.Collections.Generic.List<byte[]>();
            foreach (int s in sizes)
                using (var b = AppBitmap(s))
                using (var ms = new System.IO.MemoryStream())
                {
                    if (s >= 256) b.Save(ms, ImageFormat.Png);
                    else WriteDib(b, ms);
                    pngs.Add(ms.ToArray());
                }
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }

        /// <summary>Entrée d'icône classique (DIB 32 bits + masque), lisible partout.</summary>
        static void WriteDib(Bitmap b, System.IO.Stream st)
        {
            int w = b.Width, h = b.Height;
            int maskStride = ((w + 31) / 32) * 4;
            var bw = new System.IO.BinaryWriter(st);
            bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((short)1); bw.Write((short)32);
            bw.Write(0); bw.Write(w * h * 4 + maskStride * h); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    Color c = b.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            for (int y = h - 1; y >= 0; y--)
            {
                var row = new byte[maskStride];
                for (int x = 0; x < w; x++) if (b.GetPixel(x, y).A == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
                bw.Write(row);
            }
            bw.Flush();
        }
    }

    /// <summary>Fenêtre à transparence par pixel (UpdateLayeredWindow).</summary>
    public class LayeredForm : Form
    {
        readonly bool clickThrough, noActivate, topmost;

        public LayeredForm(bool clickThrough, bool topmost, bool noActivate = true)
        {
            this.clickThrough = clickThrough;
            this.topmost = topmost;
            this.noActivate = noActivate;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(1, 1);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= N.WS_EX_LAYERED | N.WS_EX_TOOLWINDOW;
                if (noActivate) cp.ExStyle |= N.WS_EX_NOACTIVATE;
                if (clickThrough) cp.ExStyle |= N.WS_EX_TRANSPARENT;
                if (topmost) cp.ExStyle |= N.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (noActivate && m.Msg == (int)N.WM_MOUSEACTIVATE) { m.Result = new IntPtr(N.MA_NOACTIVATE); return; }
            base.WndProc(ref m);
        }

        public void SetBitmap(Bitmap bmp, Point pos, byte alpha)
        {
            if (IsDisposed) return;
            IntPtr screenDc = N.GetDC(IntPtr.Zero);
            IntPtr memDc = N.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                old = N.SelectObject(memDc, hBmp);
                var size = new SIZE(bmp.Width, bmp.Height);
                var src = new POINT(0, 0);
                var dst = new POINT(pos.X, pos.Y);
                var bf = new BLENDFUNCTION { BlendOp = N.AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = N.AC_SRC_ALPHA };
                N.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref bf, N.ULW_ALPHA);
            }
            finally
            {
                N.ReleaseDC(IntPtr.Zero, screenDc);
                if (hBmp != IntPtr.Zero) { N.SelectObject(memDc, old); N.DeleteObject(hBmp); }
                N.DeleteDC(memDc);
            }
        }

        public void ShowNoActivate()
        {
            if (!Visible) Show();
        }
    }

    /// <summary>Petite bulle d'information en bas de l'écran (retour visuel des raccourcis).</summary>
    public class Osd : LayeredForm
    {
        static Osd inst;
        readonly Timer timer = new Timer();
        int life; byte alpha = 255;
        Bitmap bmp; Point pos;

        Osd() : base(true, true)
        {
            timer.Interval = 30;
            timer.Tick += delegate
            {
                life -= timer.Interval;
                if (life > 0) return;
                int a = alpha - 40;
                if (a <= 0) { timer.Stop(); Hide(); return; }
                alpha = (byte)a;
                SetBitmap(bmp, pos, alpha);
            };
        }

        public static bool Muted;

        public static void Flash(string icon, string text, bool force = false)
        {
            if (!force && (!Config.S.Osd || Muted)) return;
            if (inst == null || inst.IsDisposed) { inst = new Osd(); var h = inst.Handle; }
            inst.Render(icon, text);
        }

        void Render(string icon, string text)
        {
            Point c = Win.Cursor();
            Rectangle mon = Win.MonitorAt(c, true);
            float s = Win.ScaleAt(c);
            using (var font = new Font("Segoe UI Semibold", 11f * s, GraphicsUnit.Pixel))
            {
                Size ts;
                using (var tmp = new Bitmap(1, 1)) using (var g0 = Graphics.FromImage(tmp)) ts = Size.Ceiling(g0.MeasureString(text, font));
                int ic = icon != null ? (int)(18 * s) : 0;
                int padX = (int)(14 * s), padY = (int)(9 * s), gap = icon != null ? (int)(8 * s) : 0;
                int w = padX * 2 + ic + gap + ts.Width, h = Math.Max(ts.Height, ic) + padY * 2;
                if (bmp != null) bmp.Dispose();
                bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    Theme.HQ(g);
                    using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, w - 1, h - 1), h / 2f))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(225, 18, 18, 20))) g.FillPath(b, p);
                        using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1)) g.DrawPath(pen, p);
                    }
                    if (icon != null) Icons.Draw(g, icon, new RectangleF(padX, (h - ic) / 2f, ic, ic), Theme.Accent);
                    using (var b = new SolidBrush(Theme.Text)) g.DrawString(text, font, b, padX + ic + gap, (h - ts.Height) / 2f);
                }
                pos = new Point(mon.Left + (mon.Width - w) / 2, mon.Bottom - h - (int)(70 * s));
            }
            alpha = 255; life = 1100;
            SetBitmap(bmp, pos, 255);
            ShowNoActivate();
            N.SetWindowPos(Handle, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            timer.Stop(); timer.Start();
        }
    }
}
