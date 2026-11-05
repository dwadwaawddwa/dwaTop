using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace DwaTop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public RECT(Rectangle r) { Left = r.Left; Top = r.Top; Right = r.Right; Bottom = r.Bottom; }
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
        public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE
    {
        public int cx, cy;
        public SIZE(int x, int y) { cx = x; cy = y; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TITLEBARINFOEX
    {
        public int cbSize;
        public RECT rcTitleBar;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] rgstate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public RECT[] rgrect;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public int fVisible;
        public int fSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ACCENTPOLICY
    {
        public int AccentState, AccentFlags, GradientColor, AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINCOMPATTRDATA
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MAGTRANSFORM
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] public float[] v;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MAGCOLOREFFECT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)] public float[] t;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    public static class N
    {
        public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_CHILD = 0x40000000,
            WS_VISIBLE = 0x10000000, WS_MINIMIZEBOX = 0x20000, WS_MAXIMIZEBOX = 0x10000, WS_SYSMENU = 0x80000,
            WS_CLIPCHILDREN = 0x02000000, WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
            WS_EX_APPWINDOW = 0x40000, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
        public const uint LWA_COLORKEY = 1, LWA_ALPHA = 2;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero, HWND_BOTTOM = new IntPtr(1),
            HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2), HWND_MESSAGE = new IntPtr(-3);
        public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOREDRAW = 8, SWP_NOACTIVATE = 0x10,
            SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40, SWP_HIDEWINDOW = 0x80, SWP_NOOWNERZORDER = 0x200,
            SWP_ASYNCWINDOWPOS = 0x4000;
        public const uint GA_PARENT = 1, GA_ROOT = 2, GA_ROOTOWNER = 3;
        public const uint GW_HWNDNEXT = 2, GW_HWNDPREV = 3, GW_OWNER = 4;
        public const int SW_HIDE = 0, SW_SHOWNORMAL = 1, SW_SHOWMINIMIZED = 2, SW_SHOWNOACTIVATE = 4, SW_SHOW = 5,
            SW_MINIMIZE = 6, SW_SHOWNA = 8, SW_RESTORE = 9;
        public const uint WM_CLOSE = 0x10, WM_GETICON = 0x7F, WM_SETCURSOR = 0x20, WM_HOTKEY = 0x312, WM_COPYDATA = 0x4A,
            WM_NCHITTEST = 0x84, WM_SIZING = 0x214, WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202,
            WM_LBUTTONDBLCLK = 0x203, WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207,
            WM_MBUTTONUP = 0x208, WM_MOUSEWHEEL = 0x20A, WM_MOUSEHWHEEL = 0x20E, WM_KEYDOWN = 0x100, WM_KEYUP = 0x101,
            WM_CHAR = 0x102, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105, WM_GETTITLEBARINFOEX = 0x33F,
            WM_NCLBUTTONDOWN = 0xA1, WM_MOUSEACTIVATE = 0x21, WM_DISPLAYCHANGE = 0x7E, WM_DPICHANGED = 0x2E0;
        public const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
            HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17, HTTRANSPARENT = -1;
        public const int MA_NOACTIVATE = 3;
        public const uint SMTO_ABORTIFHUNG = 2, SMTO_BLOCK = 1;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14, DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34;
        public const int DWM_TNP_RECTDESTINATION = 1, DWM_TNP_RECTSOURCE = 2, DWM_TNP_OPACITY = 4,
            DWM_TNP_VISIBLE = 8, DWM_TNP_SOURCECLIENTAREAONLY = 0x10;
        public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_APPS = 0x5D;
        public const uint KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2;
        public const uint INPUT_KEYBOARD = 1;
        public const uint EVENT_SYSTEM_FOREGROUND = 3, EVENT_SYSTEM_MOVESIZESTART = 0xA, EVENT_SYSTEM_MOVESIZEEND = 0xB,
            EVENT_SYSTEM_MINIMIZESTART = 0x16, EVENT_SYSTEM_MINIMIZEEND = 0x17, EVENT_OBJECT_DESTROY = 0x8001,
            EVENT_OBJECT_SHOW = 0x8002, EVENT_OBJECT_HIDE = 0x8003, EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
            EVENT_OBJECT_NAMECHANGE = 0x800C, EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
        public const uint WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 2;
        public const int OBJID_WINDOW = 0;
        public const uint RDW_INVALIDATE = 1, RDW_ERASE = 4, RDW_ALLCHILDREN = 0x80, RDW_FRAME = 0x400, RDW_UPDATENOW = 0x100;
        public const uint ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;
        public const int MW_FILTERMODE_EXCLUDE = 0;
        public const uint PW_RENDERFULLCONTENT = 2;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int GCLP_HICON = -14, GCLP_HICONSM = -34;
        public const int CWP_SKIPINVISIBLE = 1, CWP_SKIPDISABLED = 2, CWP_SKIPTRANSPARENT = 4;
        public const int MK_LBUTTON = 1, MK_RBUTTON = 2, MK_SHIFT = 4, MK_CONTROL = 8, MK_MBUTTON = 0x10;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        public static int GetExStyle(IntPtr h) { return (int)GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64(); }
        public static void SetExStyle(IntPtr h, int v) { SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(v)); }
        public static int GetStyle(IntPtr h) { return (int)GetWindowLongPtr(h, GWL_STYLE).ToInt64(); }

        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte a, uint f);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte a, out uint f);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, ref COPYDATASTRUCT l);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, ref TITLEBARINFOEX l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT p, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromRect(ref RECT r, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr hmod, WinEventProc cb, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SetWindowsHookEx(int id, HookProc cb, IntPtr hmod, uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool RedrawWindow(IntPtr h, IntPtr rc, IntPtr rgn, uint flags);
        [DllImport("user32.dll")] public static extern bool InvalidateRect(IntPtr h, IntPtr rc, bool erase);
        [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO gi);
        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] public static extern IntPtr GetClassLongPtr(IntPtr h, int idx);
        [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateWindowEx(int ex, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
        [DllImport("user32.dll")] public static extern int SetWindowCompositionAttribute(IntPtr h, ref WINCOMPATTRDATA data);
        [DllImport("user32.dll")] public static extern bool ChangeWindowMessageFilterEx(IntPtr h, uint msg, uint action, IntPtr p);
        [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
        [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES p);
        [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE size);

        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

        [DllImport("Magnification.dll")] public static extern bool MagInitialize();
        [DllImport("Magnification.dll")] public static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowSource(IntPtr h, RECT r);
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowTransform(IntPtr h, ref MAGTRANSFORM t);
        [DllImport("Magnification.dll")] public static extern bool MagSetColorEffect(IntPtr h, ref MAGCOLOREFFECT e);
        [DllImport("Magnification.dll")] public static extern bool MagSetWindowFilterList(IntPtr h, int mode, int count, IntPtr[] list);

        public static IntPtr MakeLParam(int lo, int hi) { return new IntPtr((int)(((ushort)(short)hi << 16) | (ushort)(short)lo)); }
    }
}
