using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Filtres de couleur du mode sombre (matrices 5×5, convention vecteur-ligne).</summary>
    public static class Filters
    {
        public static readonly string[][] All = {
            new[] { "smart", "Intelligent" },
            new[] { "invert", "Inversé" },
            new[] { "invertGray", "Inversé gris" },
            new[] { "gray", "Gris" },
            new[] { "sepia", "Sépia" },
            new[] { "night", "Nuit (chaud)" },
            new[] { "dim", "Assombri" },
        };

        public static string Label(string id)
        {
            foreach (var f in All) if (f[0] == id) return f[1];
            return id;
        }

        static float[,] Id()
        {
            var m = new float[5, 5];
            for (int i = 0; i < 5; i++) m[i, i] = 1;
            return m;
        }

        static float[,] Mul(float[,] a, float[,] b)
        {
            var r = new float[5, 5];
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    float s = 0;
                    for (int k = 0; k < 5; k++) s += a[i, k] * b[k, j];
                    r[i, j] = s;
                }
            return r;
        }

        /// <summary>Matrice 3×3 "colonne" (out = M·in) → 5×5 "ligne".</summary>
        static float[,] FromColumn(float[,] c)
        {
            var m = Id();
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) m[i, j] = c[j, i];
            return m;
        }

        static float[,] Invert()
        {
            var m = Id();
            m[0, 0] = m[1, 1] = m[2, 2] = -1;
            m[4, 0] = m[4, 1] = m[4, 2] = 1;
            return m;
        }

        static float[,] Hue180()
        {
            return FromColumn(new float[,] {
                { -0.574f, 1.430f, 0.144f },
                { 0.426f, 0.430f, 0.144f },
                { 0.426f, 1.430f, -0.856f } });
        }

        static float[,] Gray()
        {
            return FromColumn(new float[,] {
                { 0.2126f, 0.7152f, 0.0722f },
                { 0.2126f, 0.7152f, 0.0722f },
                { 0.2126f, 0.7152f, 0.0722f } });
        }

        static float[,] Scale(float r, float g, float b)
        {
            var m = Id();
            m[0, 0] = r; m[1, 1] = g; m[2, 2] = b;
            return m;
        }

        public static float[] Matrix(string id)
        {
            float[,] m;
            switch (id)
            {
                case "invert": m = Invert(); break;
                case "invertGray": m = Mul(Gray(), Invert()); break;
                case "gray": m = Gray(); break;
                case "sepia":
                    m = FromColumn(new float[,] { { .393f, .769f, .189f }, { .349f, .686f, .168f }, { .272f, .534f, .131f } });
                    break;
                case "night": m = Scale(1f, 0.82f, 0.58f); break;
                case "dim": m = Scale(0.6f, 0.6f, 0.6f); break;
                default: m = Mul(Mul(Invert(), Hue180()), Scale(0.92f, 0.92f, 0.92f)); break;
            }
            var flat = new float[25];
            for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++) flat[i * 5 + j] = m[i, j];
            return flat;
        }
    }

    /// <summary>Garde une fenêtre de dwaTop juste au-dessus d'une autre dans l'ordre Z.</summary>
    public static class Follow
    {
        const uint F = N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE | N.SWP_NOOWNERZORDER;

        public static void PlaceAbove(IntPtr self, IntPtr below)
        {
            IntPtr prev = N.GetWindow(below, N.GW_HWNDPREV);
            if (prev == self) return;
            bool tmBelow = Win.IsTopmost(below);
            if (Win.IsTopmost(self) != tmBelow)
            {
                N.SetWindowPos(self, tmBelow ? N.HWND_TOPMOST : N.HWND_NOTOPMOST, 0, 0, 0, 0, F);
                prev = N.GetWindow(below, N.GW_HWNDPREV);
                if (prev == self) return;
            }
            IntPtr after;
            if (prev == IntPtr.Zero) after = tmBelow ? N.HWND_TOPMOST : N.HWND_TOP;
            else if (!tmBelow && Win.IsTopmost(prev)) after = N.HWND_TOP;
            else after = prev;
            N.SetWindowPos(self, after, 0, 0, 0, 0, F);
        }
    }

    /// <summary>
    /// Mode sombre : une loupe Windows (Magnification API) à 100 % posée sur la fenêtre,
    /// qui affiche son contenu avec une matrice de couleur. Click-through, juste au-dessus de la cible.
    /// </summary>
    public class DarkOverlay : Form
    {
        public static bool Available;
        static readonly List<DarkOverlay> all = new List<DarkOverlay>();
        static System.Windows.Forms.Timer timer;
        static int ticks, lastSample;
        static bool sampling;

        public readonly IntPtr Target;
        readonly Func<Rectangle> rectFn;
        readonly DarkOverlay parent;
        readonly bool autoPause;
        IntPtr mag;
        string filter;
        public bool Paused;
        bool shown, firstSample = true;
        Rectangle last;
        IntPtr[] lastExclude = new IntPtr[0];

        public static void Init()
        {
            try { Available = N.MagInitialize(); } catch { Available = false; }
            if (!Available) Log.Info("MagInitialize a échoué : mode sombre indisponible");
        }

        public static void Shutdown()
        {
            foreach (var o in all.ToList()) o.Close();
            if (Available) try { N.MagUninitialize(); } catch { }
        }

        public static IEnumerable<DarkOverlay> Overlays { get { return all; } }

        /// <param name="rectFn">null = cadre de la fenêtre cible ; sinon zone fournie (miniatures).</param>
        /// <param name="parent">overlay de la fenêtre source (les miniatures suivent sa mise en veille).</param>
        public DarkOverlay(IntPtr target, string filter, Func<Rectangle> rectFn, bool autoPause, DarkOverlay parent = null)
        {
            Target = target;
            this.filter = filter;
            this.rectFn = rectFn;
            this.autoPause = autoPause && parent == null;
            this.parent = parent;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Text = "dwaTop dark";
            var h = Handle;
            all.Add(this);
            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Tick += delegate { try { TickAll(); } catch (Exception ex) { Log.Error(ex); } };
            }
            timer.Interval = Math.Max(10, 1000 / Math.Max(10, Config.S.DarkFps));
            timer.Start();
            if (this.autoPause) InitialSample();
            UpdateExclude(true);
            Sync();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            N.SetLayeredWindowAttributes(Handle, 0, 255, N.LWA_ALPHA);
            mag = N.CreateWindowEx(0, "Magnifier", "dwaTopMag", N.WS_CHILD | N.WS_VISIBLE, 0, 0, 16, 16, Handle, IntPtr.Zero, N.GetModuleHandle(null), IntPtr.Zero);
            if (mag == IntPtr.Zero) { Log.Info("Création de la loupe impossible"); return; }
            var t = new MAGTRANSFORM { v = new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } };
            N.MagSetWindowTransform(mag, ref t);
            SetFilter(filter);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)N.WM_MOUSEACTIVATE) { m.Result = new IntPtr(N.MA_NOACTIVATE); return; }
            if (m.Msg == (int)N.WM_NCHITTEST) { m.Result = new IntPtr(N.HTTRANSPARENT); return; }
            base.WndProc(ref m);
        }

        public void SetFilter(string f)
        {
            filter = f;
            if (mag == IntPtr.Zero) return;
            var e = new MAGCOLOREFFECT { t = Filters.Matrix(f) };
            N.MagSetColorEffect(mag, ref e);
        }

        public bool IsShown { get { return shown; } }

        bool ShouldShow()
        {
            if (mag == IntPtr.Zero) return false;
            if (Paused || (parent != null && parent.Paused)) return false;
            if (!N.IsWindow(Target) || !N.IsWindowVisible(Target) || N.IsIconic(Target) || Win.IsCloaked(Target)) return false;
            if (rectFn == null && Manager.I != null && Manager.I.IsHiddenSource(Target)) return false;
            return true;
        }

        public void Sync()
        {
            if (IsDisposed) return;
            if (!ShouldShow()) { HideOverlay(); return; }
            Rectangle r = rectFn != null ? rectFn() : Win.Frame(Target);
            if (r.Width < 4 || r.Height < 4) { HideOverlay(); return; }
            if (r != last)
            {
                N.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, N.SWP_NOZORDER | N.SWP_NOACTIVATE);
                N.MoveWindow(mag, 0, 0, r.Width, r.Height, false);
                last = r;
            }
            Follow.PlaceAbove(Handle, Target);
            N.MagSetWindowSource(mag, new RECT(r));
            if (!shown) { N.ShowWindow(Handle, N.SW_SHOWNA); shown = true; }
            N.InvalidateRect(mag, IntPtr.Zero, false);
        }

        void HideOverlay()
        {
            if (!shown) return;
            N.ShowWindow(Handle, N.SW_HIDE);
            shown = false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            all.Remove(this);
            if (all.Count == 0 && timer != null) timer.Stop();
            base.OnFormClosed(e);
        }

        static void TickAll()
        {
            foreach (var o in all.ToArray()) o.Sync();
            ticks++;
            if (ticks % 40 == 0) foreach (var o in all) o.UpdateExclude(false);
            if (!sampling && Environment.TickCount - lastSample > 2000) StartSampling();
        }

        public static void SyncAll() { foreach (var o in all.ToArray()) o.Sync(); }

        /// <summary>La loupe ignore toutes les fenêtres de dwaTop (sauf sa propre cible : les miniatures).</summary>
        void UpdateExclude(bool force)
        {
            if (mag == IntPtr.Zero) return;
            var ours = new List<IntPtr>();
            foreach (var h in Win.TopLevelWindows())
                if (h != Target && Win.Pid(h) == Win.OwnPid) ours.Add(h);
            var arr = ours.ToArray();
            if (!force && arr.SequenceEqual(lastExclude)) return;
            lastExclude = arr;
            N.MagSetWindowFilterList(mag, N.MW_FILTERMODE_EXCLUDE, arr.Length, arr);
        }

        static Rectangle ContentArea(Bitmap b)
        {
            return new Rectangle((int)(b.Width * 0.06), (int)(b.Height * 0.15), (int)(b.Width * 0.88), (int)(b.Height * 0.8));
        }

        void InitialSample()
        {
            try
            {
                if (N.IsHungAppWindow(Target)) return;
                using (var b = Win.Capture(Target, 240))
                {
                    if (b == null) return;
                    float l = Win.Luminance(b, ContentArea(b));
                    if (l >= 0 && l < 0.38f)
                    {
                        Paused = true;
                        Osd.Flash("dark", "Déjà sombre — mode sombre en veille (auto)");
                    }
                    firstSample = false;
                }
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        static void StartSampling()
        {
            lastSample = Environment.TickCount;
            var jobs = all.Where(o => o.autoPause && !o.IsDisposed && N.IsWindow(o.Target)).ToList();
            if (jobs.Count == 0) return;
            sampling = true;
            var targets = jobs.Select(o => o.Target).ToList();
            ThreadPool.QueueUserWorkItem(delegate
            {
                var lum = new float[targets.Count];
                for (int i = 0; i < targets.Count; i++)
                {
                    lum[i] = -1;
                    try
                    {
                        if (N.IsHungAppWindow(targets[i]) || N.IsIconic(targets[i])) continue;
                        using (var b = Win.Capture(targets[i], 240))
                            if (b != null) lum[i] = Win.Luminance(b, ContentArea(b));
                    }
                    catch { }
                }
                UI.Post(delegate
                {
                    sampling = false;
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        var o = jobs[i];
                        if (o.IsDisposed || lum[i] < 0) continue;
                        bool p = o.Paused;
                        if (!p && lum[i] < 0.38f) p = true;
                        else if (p && lum[i] > 0.5f) p = false;
                        if (p != o.Paused)
                        {
                            o.Paused = p;
                            if (p && o.firstSample) Osd.Flash("dark", "Déjà sombre — mode sombre en veille (auto)");
                            o.Sync();
                            if (Manager.I != null) Manager.I.Notify();
                        }
                        o.firstSample = false;
                    }
                });
            });
        }
    }

    /// <summary>Bordure colorée autour des fenêtres épinglées.</summary>
    public class BorderOverlay : LayeredForm
    {
        static readonly bool win11 = Environment.OSVersion.Version.Build >= 22000;
        readonly IntPtr target;
        Size lastSize;
        Point lastPos;
        bool shown, lastZoomed;

        public bool IsShown { get { return shown && !IsDisposed; } }

        public BorderOverlay(IntPtr target) : base(true, false)
        {
            this.target = target;
            Text = "dwaTop border";
            var h = Handle;
        }

        public void Sync(bool force)
        {
            if (IsDisposed) return;
            bool show = N.IsWindow(target) && N.IsWindowVisible(target) && !N.IsIconic(target) && !Win.IsCloaked(target)
                && !Manager.I.IsHiddenSource(target) && !Win.IsFullscreen(target);
            if (!show) { if (shown) { Hide(); shown = false; } return; }
            Rectangle f = Win.Frame(target);
            bool zoomed = N.IsZoomed(target);
            float s = Win.ScaleAt(f);
            int w = Math.Max(1, (int)Math.Round(Config.S.PinBorderWidth * s));
            Rectangle r = zoomed ? f : Rectangle.Inflate(f, w, w);
            if (force || r.Size != lastSize || zoomed != lastZoomed)
            {
                Render(r, w, zoomed, s);
                lastSize = r.Size; lastZoomed = zoomed; lastPos = r.Location;
            }
            else if (r.Location != lastPos)
            {
                N.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, 0, 0, N.SWP_NOSIZE | N.SWP_NOZORDER | N.SWP_NOACTIVATE);
                lastPos = r.Location;
            }
            var mw = Manager.I.Get(target, false);
            IntPtr below = mw != null && mw.DarkOv != null && mw.DarkOv.IsShown ? mw.DarkOv.Handle : target;
            Follow.PlaceAbove(Handle, below);
            if (!shown) { ShowNoActivate(); shown = true; }
        }

        void Render(Rectangle r, int w, bool zoomed, float s)
        {
            Color c = Theme.Parse(Config.S.PinBorderColor, Theme.Accent);
            int alpha = Math.Max(10, Math.Min(255, Config.S.PinBorderOpacity * 255 / 100));
            using (var bmp = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height), PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float rad = (!zoomed && win11) ? 8 * s + w : 0;
                    var rect = new RectangleF(w / 2f, w / 2f, r.Width - w, r.Height - w);
                    using (var pen = new Pen(Color.FromArgb(alpha, c), w))
                    using (var p = Theme.Round(rect, rad))
                        g.DrawPath(pen, p);
                }
                SetBitmap(bmp, r.Location, 255);
            }
        }
    }
}
