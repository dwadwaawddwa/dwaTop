using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>
    /// Sélection d'une zone d'une fenêtre : glisser = rectangle libre, clic = élément détecté (UI Automation).
    /// Le calque est click-through ; la souris est lue par un hook bas niveau qui avale les clics.
    /// </summary>
    public class CropSelector : LayeredForm
    {
        static CropSelector active;
        const int HK_ESC = 9001, HK_ENTER = 9002;

        readonly ManagedWindow mw;
        readonly Action<Rectangle> done;
        readonly bool pipWasVisible;
        Rectangle frame, sel, detected;
        Point dragStart;
        bool dragging, finished, begun, movePending;
        Point lastMove;
        float s = 1;
        IntPtr hook;
        N.HookProc hookProc;
        Bitmap canvas;
        Thread uia;
        volatile bool uiaRun;
        readonly AutoResetEvent uiaSignal = new AutoResetEvent(false);
        Point uiaPoint;
        uint pidA, pidB;

        public static void Start(ManagedWindow mw, Action<Rectangle> done)
        {
            if (active != null) active.Finish(Rectangle.Empty);
            active = new CropSelector(mw, done);
        }

        CropSelector(ManagedWindow mw, Action<Rectangle> done) : base(true, true)
        {
            this.mw = mw;
            this.done = done;
            Text = "dwaTop crop";
            var h = Handle;
            pidA = Win.Pid(mw.Hwnd);
            pidB = Win.AppPid(mw.Hwnd);
            if (mw.Pip != null && mw.Pip.Visible) { pipWasVisible = true; mw.Pip.HideAll(); }
            if (mw.HiddenByPip) Manager.I.SetTempReveal(mw, true);
            Win.ForceForeground(mw.Hwnd);
            UI.Delay(160, Begin);
        }

        void Begin()
        {
            if (finished) return;
            if (!N.IsWindow(mw.Hwnd)) { Finish(Rectangle.Empty); return; }
            frame = Win.Frame(mw.Hwnd);
            s = Win.ScaleAt(frame);
            canvas = new Bitmap(Math.Max(1, frame.Width), Math.Max(1, frame.Height), PixelFormat.Format32bppArgb);
            Render();
            ShowNoActivate();
            N.SetWindowPos(Handle, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            hookProc = MouseProc;
            hook = N.SetWindowsHookEx(N.WH_MOUSE_LL, hookProc, N.GetModuleHandle(null), 0);
            App.Hotkeys.TempHotkey += OnTempKey;
            App.Hotkeys.RegisterTemp(HK_ESC, Keys.Escape);
            App.Hotkeys.RegisterTemp(HK_ENTER, Keys.Enter);
            if (Config.S.CropAutoDetect)
            {
                uiaRun = true;
                uia = new Thread(UiaLoop) { IsBackground = true, Name = "dwaTop UIA" };
                uia.SetApartmentState(ApartmentState.MTA);
                uia.Start();
            }
            begun = true;
            Osd.Flash("crop", "Glisse pour choisir la zone · clic = élément détecté · Échap = annuler");
        }

        void OnTempKey(int id)
        {
            if (id == HK_ESC) Finish(Rectangle.Empty);
            else if (id == HK_ENTER) Finish(!sel.IsEmpty ? sel : detected);
        }

        IntPtr MouseProc(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0 && !finished)
            {
                var ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
                var p = new Point(ms.pt.X, ms.pt.Y);
                switch ((uint)w.ToInt32())
                {
                    case N.WM_MOUSEMOVE:
                        lastMove = p;
                        if (!movePending) { movePending = true; UI.Post(delegate { movePending = false; OnMove(lastMove); }); }
                        break;
                    case N.WM_LBUTTONDOWN: UI.Post(delegate { OnDown(p); }); return new IntPtr(1);
                    case N.WM_LBUTTONUP: UI.Post(delegate { OnUp(p); }); return new IntPtr(1);
                    case N.WM_RBUTTONDOWN: return new IntPtr(1);
                    case N.WM_RBUTTONUP: UI.Post(delegate { Finish(Rectangle.Empty); }); return new IntPtr(1);
                    case N.WM_MBUTTONDOWN:
                    case N.WM_MBUTTONUP:
                    case N.WM_MOUSEWHEEL:
                    case N.WM_LBUTTONDBLCLK:
                        return new IntPtr(1);
                }
            }
            return N.CallNextHookEx(hook, code, w, l);
        }

        Point Clamp(Point p)
        {
            return new Point(Math.Max(frame.Left, Math.Min(frame.Right, p.X)), Math.Max(frame.Top, Math.Min(frame.Bottom, p.Y)));
        }

        void OnMove(Point p)
        {
            if (finished || !begun) return;
            if (dragging)
            {
                Point c = Clamp(p);
                sel = Rectangle.FromLTRB(Math.Min(dragStart.X, c.X), Math.Min(dragStart.Y, c.Y), Math.Max(dragStart.X, c.X), Math.Max(dragStart.Y, c.Y));
                Render();
            }
            else if (uiaRun && frame.Contains(p))
            {
                uiaPoint = p;
                uiaSignal.Set();
            }
        }

        void OnDown(Point p)
        {
            if (finished || !begun) return;
            if (!frame.Contains(p)) { Finish(Rectangle.Empty); return; }
            dragging = true;
            dragStart = Clamp(p);
            sel = Rectangle.Empty;
        }

        void OnUp(Point p)
        {
            if (finished || !dragging) return;
            dragging = false;
            if (sel.Width >= 8 && sel.Height >= 8) { Finish(sel); return; }
            sel = Rectangle.Empty;
            if (!detected.IsEmpty) Finish(detected);
            else Render();
        }

        void SetDetected(Rectangle r)
        {
            if (finished || dragging) return;
            r.Intersect(frame);
            long area = (long)r.Width * r.Height, fa = (long)frame.Width * frame.Height;
            if (r.Width < 16 || r.Height < 16 || area > fa * 0.92) r = Rectangle.Empty;
            if (r == detected) return;
            detected = r;
            Render();
        }

        void UiaLoop()
        {
            Point lastAsked = new Point(int.MinValue, 0);
            while (uiaRun)
            {
                uiaSignal.WaitOne(400);
                if (!uiaRun) break;
                Point p = uiaPoint;
                if (p == lastAsked) continue;
                lastAsked = p;
                try
                {
                    var el = AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y));
                    if (el == null) continue;
                    var cur = el.Current;
                    uint pid = (uint)cur.ProcessId;
                    var b = cur.BoundingRectangle;
                    if (b.IsEmpty || (pid != pidA && pid != pidB)) { UI.Post(delegate { SetDetected(Rectangle.Empty); }); continue; }
                    var rr = new Rectangle((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height);
                    UI.Post(delegate { SetDetected(rr); });
                }
                catch { }
            }
        }

        void Render()
        {
            if (canvas == null) return;
            Rectangle hl = !sel.IsEmpty ? sel : (dragging ? Rectangle.Empty : detected);
            using (var g = Graphics.FromImage(canvas))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.FromArgb(120, 0, 0, 0));
                if (!hl.IsEmpty)
                {
                    var local = new Rectangle(hl.X - frame.X, hl.Y - frame.Y, hl.Width, hl.Height);
                    using (var clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0))) g.FillRectangle(clear, local);
                    g.CompositingMode = CompositingMode.SourceOver;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(sel.IsEmpty ? Color.FromArgb(220, 255, 200, 60) : Theme.Accent, Math.Max(2, 2 * s)))
                    {
                        if (sel.IsEmpty) pen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(pen, local);
                    }
                    string label = hl.Width + " × " + hl.Height + (sel.IsEmpty ? "  (clic pour choisir)" : "");
                    using (var f = new Font("Segoe UI Semibold", 11f * s, GraphicsUnit.Pixel))
                    {
                        var ts = g.MeasureString(label, f);
                        float lx = local.X, ly = local.Y - ts.Height - 6 * s;
                        if (ly < 0) ly = local.Y + 6 * s;
                        using (var b = new SolidBrush(Color.FromArgb(220, 15, 15, 17))) g.FillRectangle(b, lx, ly, ts.Width + 10 * s, ts.Height + 2);
                        using (var b = new SolidBrush(Color.White)) g.DrawString(label, f, b, lx + 5 * s, ly + 1);
                    }
                }
                else g.CompositingMode = CompositingMode.SourceOver;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                string hint = "Glisser = zone libre   ·   clic = élément surligné   ·   Échap / clic droit = annuler";
                using (var f = new Font("Segoe UI", 12f * s, GraphicsUnit.Pixel))
                {
                    var ts = g.MeasureString(hint, f);
                    float x = (canvas.Width - ts.Width) / 2, y = 14 * s;
                    using (var p = Theme.Round(new RectangleF(x - 12 * s, y - 6 * s, ts.Width + 24 * s, ts.Height + 12 * s), 10 * s))
                    using (var b = new SolidBrush(Color.FromArgb(225, 18, 18, 20))) g.FillPath(b, p);
                    using (var b = new SolidBrush(Theme.Text)) g.DrawString(hint, f, b, x, y);
                }
            }
            SetBitmap(canvas, frame.Location, 255);
        }

        void Finish(Rectangle rect)
        {
            if (finished) return;
            finished = true;
            uiaRun = false;
            uiaSignal.Set();
            if (hook != IntPtr.Zero) { N.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            if (begun)
            {
                App.Hotkeys.TempHotkey -= OnTempKey;
                App.Hotkeys.UnregisterTemp(HK_ESC);
                App.Hotkeys.UnregisterTemp(HK_ENTER);
            }
            if (active == this) active = null;
            Manager.I.SetTempReveal(mw, false);
            if (pipWasVisible && mw.Pip != null && !Manager.I.PipsHidden) mw.Pip.Show();
            Rectangle rel = Rectangle.Empty;
            if (!rect.IsEmpty && rect.Width >= 8 && rect.Height >= 8)
                rel = new Rectangle(rect.X - frame.X, rect.Y - frame.Y, rect.Width, rect.Height);
            Close();
            if (canvas != null) { canvas.Dispose(); canvas = null; }
            try { done(rel); } catch (Exception ex) { Log.Error(ex); }
        }
    }
}
