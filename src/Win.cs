using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Petites fonctions utilitaires autour des fenêtres Win32.</summary>
    public static class Win
    {
        public static readonly uint OwnPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        static readonly Dictionary<uint, string> procCache = new Dictionary<uint, string>();
        static readonly HashSet<string> blockedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
            "ForegroundStaging", "MultitaskingViewFrame", "TaskListThumbnailWnd", "#32768", "SysShadow",
            "tooltips_class32", "Xaml_WindowedPopupClass", "Shell_InputSwitchTopLevelWindow", "LockScreenBackstopFrame",
            "ApplicationManager_DesktopShellWindow", "WindowsDashboard", "Shell_LightDismissOverlay", "Ghost",
            "IME", "MSCTFIME UI", "CicMarshalWndClass", "EdgeUiInputTopWndClass", "NativeHWNDHost"
        };

        public static string Title(IntPtr h)
        {
            int n = N.GetWindowTextLength(h);
            if (n <= 0) return "";
            var sb = new StringBuilder(n + 2);
            N.GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string Class(IntPtr h)
        {
            var sb = new StringBuilder(256);
            N.GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }

        public static uint Pid(IntPtr h)
        {
            uint pid;
            N.GetWindowThreadProcessId(h, out pid);
            return pid;
        }

        public static uint Thread(IntPtr h)
        {
            uint pid;
            return N.GetWindowThreadProcessId(h, out pid);
        }

        public static bool IsOurs(IntPtr h) { return h != IntPtr.Zero && Pid(h) == OwnPid; }

        public static string ProcessPath(uint pid)
        {
            string p;
            lock (procCache) { if (procCache.TryGetValue(pid, out p)) return p; }
            p = "";
            IntPtr hp = N.OpenProcess(N.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp != IntPtr.Zero)
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (N.QueryFullProcessImageName(hp, 0, sb, ref size)) p = sb.ToString();
                N.CloseHandle(hp);
            }
            lock (procCache) { procCache[pid] = p; }
            return p;
        }

        /// <summary>Pid "réel" (gère les applis UWP hébergées dans ApplicationFrameHost).</summary>
        public static uint AppPid(IntPtr h)
        {
            if (Class(h) == "ApplicationFrameWindow")
            {
                IntPtr core = N.FindWindowEx(h, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
                if (core != IntPtr.Zero) return Pid(core);
            }
            return Pid(h);
        }

        public static string ProcessName(IntPtr h)
        {
            string path = ProcessPath(AppPid(h));
            if (string.IsNullOrEmpty(path)) return "";
            return Path.GetFileName(path).ToLowerInvariant();
        }

        public static Rectangle WindowRect(IntPtr h)
        {
            RECT r;
            N.GetWindowRect(h, out r);
            return r.ToRectangle();
        }

        /// <summary>Cadre visible (sans les bordures invisibles de redimensionnement de Windows 10/11).</summary>
        public static Rectangle Frame(IntPtr h)
        {
            RECT r;
            if (N.DwmGetWindowAttribute(h, N.DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) == 0 && r.Width > 0)
                return r.ToRectangle();
            N.GetWindowRect(h, out r);
            return r.ToRectangle();
        }

        public static bool IsCloaked(IntPtr h)
        {
            int v;
            return N.DwmGetWindowAttribute(h, N.DWMWA_CLOAKED, out v, 4) == 0 && v != 0;
        }

        public static bool IsTopmost(IntPtr h) { return (N.GetExStyle(h) & N.WS_EX_TOPMOST) != 0; }

        /// <summary>Fenêtre principale "normale" sur laquelle dwaTop peut agir.</summary>
        public static bool IsEligible(IntPtr h)
        {
            if (h == IntPtr.Zero || !N.IsWindow(h) || !N.IsWindowVisible(h)) return false;
            if (IsOurs(h)) return false;
            if (N.GetAncestor(h, N.GA_ROOT) != h) return false;
            if (IsCloaked(h)) return false;
            string cls = Class(h);
            if (blockedClasses.Contains(cls)) return false;
            int st = N.GetStyle(h);
            int ex = N.GetExStyle(h);
            if ((st & N.WS_CHILD) != 0) return false;
            bool caption = (st & N.WS_CAPTION) == N.WS_CAPTION;
            bool frame = caption || (st & N.WS_THICKFRAME) != 0;
            if (!frame && cls != "ApplicationFrameWindow") return false;
            if ((ex & N.WS_EX_TOOLWINDOW) != 0 && !caption) return false;
            if (h == N.GetShellWindow()) return false;
            Rectangle r = Frame(h);
            if (r.Width < 100 || r.Height < 50) return false;
            return true;
        }

        /// <summary>Fenêtre qui apparaît dans Alt+Tab (pour les ancres).</summary>
        public static bool IsAltTab(IntPtr h)
        {
            if (!IsEligible(h)) return false;
            int ex = N.GetExStyle(h);
            if ((ex & N.WS_EX_TOOLWINDOW) != 0 && (ex & N.WS_EX_APPWINDOW) == 0) return false;
            IntPtr owner = N.GetWindow(h, N.GW_OWNER);
            if (owner != IntPtr.Zero && (ex & N.WS_EX_APPWINDOW) == 0 && N.IsWindowVisible(owner)) return false;
            return Title(h).Length > 0;
        }

        public static Rectangle MonitorBounds(Rectangle r, bool work)
        {
            RECT rr = new RECT(r);
            IntPtr mon = N.MonitorFromRect(ref rr, N.MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!N.GetMonitorInfo(mon, ref mi)) return Screen.PrimaryScreen.Bounds;
            return work ? mi.rcWork.ToRectangle() : mi.rcMonitor.ToRectangle();
        }

        public static Rectangle MonitorAt(Point p, bool work) { return MonitorBounds(new Rectangle(p.X, p.Y, 1, 1), work); }

        public static float ScaleAt(Rectangle r)
        {
            try
            {
                RECT rr = new RECT(r);
                IntPtr mon = N.MonitorFromRect(ref rr, N.MONITOR_DEFAULTTONEAREST);
                uint dx, dy;
                if (N.GetDpiForMonitor(mon, 0, out dx, out dy) == 0 && dx > 0) return dx / 96f;
            }
            catch { }
            return 1f;
        }

        public static float ScaleAt(Point p) { return ScaleAt(new Rectangle(p.X, p.Y, 1, 1)); }

        public static Point Cursor()
        {
            POINT p;
            N.GetCursorPos(out p);
            return new Point(p.X, p.Y);
        }

        public static bool IsFullscreen(IntPtr h)
        {
            Rectangle f = Frame(h);
            Rectangle m = MonitorBounds(f, false);
            int st = N.GetStyle(h);
            bool covers = f.Left <= m.Left && f.Top <= m.Top && f.Right >= m.Right && f.Bottom >= m.Bottom;
            if (!covers) return false;
            return (st & N.WS_CAPTION) != N.WS_CAPTION || !N.IsZoomed(h);
        }

        public static void ForceForeground(IntPtr h)
        {
            if (h == IntPtr.Zero || !N.IsWindow(h)) return;
            if (N.IsIconic(h)) N.ShowWindow(h, N.SW_RESTORE);
            IntPtr fg = N.GetForegroundWindow();
            if (fg == h) return;
            uint cur = N.GetCurrentThreadId();
            uint fgThread = fg != IntPtr.Zero ? Thread(fg) : 0;
            bool attached = fgThread != 0 && fgThread != cur && N.AttachThreadInput(cur, fgThread, true);
            N.BringWindowToTop(h);
            N.SetForegroundWindow(h);
            if (attached) N.AttachThreadInput(cur, fgThread, false);
        }

        public static void Redraw(IntPtr h)
        {
            N.RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, N.RDW_ERASE | N.RDW_INVALIDATE | N.RDW_FRAME | N.RDW_ALLCHILDREN);
        }

        public static Bitmap WindowIcon(IntPtr h, int size)
        {
            IntPtr hi = IntPtr.Zero, res;
            try
            {
                if (N.SendMessageTimeout(h, N.WM_GETICON, new IntPtr(1), IntPtr.Zero, N.SMTO_ABORTIFHUNG, 80, out res) != IntPtr.Zero) hi = res;
                if (hi == IntPtr.Zero && N.SendMessageTimeout(h, N.WM_GETICON, new IntPtr(2), IntPtr.Zero, N.SMTO_ABORTIFHUNG, 80, out res) != IntPtr.Zero) hi = res;
                if (hi == IntPtr.Zero) hi = N.GetClassLongPtr(h, N.GCLP_HICON);
                if (hi == IntPtr.Zero) hi = N.GetClassLongPtr(h, N.GCLP_HICONSM);
                Bitmap src = null;
                if (hi != IntPtr.Zero)
                {
                    using (var ic = Icon.FromHandle(hi)) src = ic.ToBitmap();
                }
                else
                {
                    string path = ProcessPath(AppPid(h));
                    if (File.Exists(path))
                        using (var ic = Icon.ExtractAssociatedIcon(path)) src = ic.ToBitmap();
                }
                if (src == null) return null;
                var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, size, size);
                }
                src.Dispose();
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>Capture du contenu d'une fenêtre (même cachée/derrière) via PrintWindow.</summary>
        public static Bitmap Capture(IntPtr h, int maxSide)
        {
            Rectangle wr = WindowRect(h);
            if (wr.Width <= 0 || wr.Height <= 0) return null;
            var full = new Bitmap(wr.Width, wr.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(full))
            {
                IntPtr hdc = g.GetHdc();
                bool ok = N.PrintWindow(h, hdc, N.PW_RENDERFULLCONTENT);
                g.ReleaseHdc(hdc);
                if (!ok) { full.Dispose(); return null; }
            }
            if (maxSide <= 0 || Math.Max(wr.Width, wr.Height) <= maxSide) return full;
            float k = maxSide / (float)Math.Max(wr.Width, wr.Height);
            var small = new Bitmap(Math.Max(1, (int)(wr.Width * k)), Math.Max(1, (int)(wr.Height * k)), PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(small))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(full, 0, 0, small.Width, small.Height);
            }
            full.Dispose();
            return small;
        }

        /// <summary>Luminosité moyenne (0..1) d'une image, -1 si l'image est vide/noire (capture impossible).</summary>
        public static float Luminance(Bitmap b, Rectangle area)
        {
            area.Intersect(new Rectangle(0, 0, b.Width, b.Height));
            if (area.Width <= 0 || area.Height <= 0) return -1;
            var data = b.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] px = new byte[stride * area.Height];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                double sum = 0; int n = 0; int maxv = 0;
                int step = Math.Max(1, Math.Min(area.Width, area.Height) / 60);
                for (int y = 0; y < area.Height; y += step)
                    for (int x = 0; x < area.Width; x += step)
                    {
                        int i = y * stride + x * 4;
                        int bl = px[i], gr = px[i + 1], rd = px[i + 2];
                        sum += (0.2126 * rd + 0.7152 * gr + 0.0722 * bl) / 255.0;
                        int m = Math.Max(rd, Math.Max(gr, bl));
                        if (m > maxv) maxv = m;
                        n++;
                    }
                if (n == 0 || maxv == 0) return -1;
                return (float)(sum / n);
            }
            finally { b.UnlockBits(data); }
        }

        public static void SetDarkFrame(IntPtr h)
        {
            try
            {
                int one = 1;
                N.DwmSetWindowAttribute(h, N.DWMWA_USE_IMMERSIVE_DARK_MODE, ref one, 4);
                int round = 2;
                N.DwmSetWindowAttribute(h, N.DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
            }
            catch { }
        }

        public static void SetCorner(IntPtr h, int pref)
        {
            try { N.DwmSetWindowAttribute(h, N.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4); } catch { }
        }

        public static List<IntPtr> TopLevelWindows()
        {
            var list = new List<IntPtr>();
            N.EnumWindows(delegate (IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }
    }

    /// <summary>Accès au thread UI depuis n'importe où.</summary>
    public static class UI
    {
        static Control ctl;
        public static void Init()
        {
            ctl = new Control();
            ctl.CreateControl();
            IntPtr h = ctl.Handle;
        }
        public static void Post(Action a)
        {
            if (ctl == null || ctl.IsDisposed) return;
            try { ctl.BeginInvoke(new MethodInvoker(delegate { try { a(); } catch (Exception ex) { Log.Error(ex); } })); } catch { }
        }
        public static void Delay(int ms, Action a)
        {
            var t = new System.Windows.Forms.Timer();
            t.Interval = Math.Max(1, ms);
            t.Tick += delegate { t.Stop(); t.Dispose(); try { a(); } catch (Exception ex) { Log.Error(ex); } };
            t.Start();
        }
    }

    public static class Log
    {
        static readonly object locker = new object();
        public static void Info(string s)
        {
            try
            {
                lock (locker)
                {
                    string p = Path.Combine(Config.Dir, "dwaTop.log");
                    if (File.Exists(p) && new FileInfo(p).Length > 512 * 1024) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + s + Environment.NewLine);
                }
            }
            catch { }
        }
        public static void Error(Exception ex) { Info("ERREUR " + ex); }
    }
}
