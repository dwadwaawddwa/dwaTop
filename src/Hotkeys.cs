using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Raccourci du type "Ctrl+Alt+Shift+Win+Menu+Touche" ("Menu" = touche Menu/AppsKey).</summary>
    public class HotkeySpec
    {
        public bool Ctrl, Alt, Shift, Win, Menu;
        public Keys Key = Keys.None;

        public static HotkeySpec Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var h = new HotkeySpec();
            foreach (string raw in s.Split('+'))
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                string l = p.ToLowerInvariant();
                if (l == "ctrl" || l == "control") h.Ctrl = true;
                else if (l == "alt") h.Alt = true;
                else if (l == "shift" || l == "maj") h.Shift = true;
                else if (l == "win") h.Win = true;
                else if (l == "menu" || l == "apps") h.Menu = true;
                else
                {
                    Keys k;
                    if (p.Length == 1 && char.IsDigit(p[0])) p = "D" + p;
                    if (Enum.TryParse<Keys>(p, true, out k)) h.Key = k;
                    else return null;
                }
            }
            if (h.Key == Keys.None) return null;
            if (!h.Menu && !h.Ctrl && !h.Alt && !h.Win && !h.Shift && !(h.Key >= Keys.F1 && h.Key <= Keys.F24)) return null;
            return h;
        }

        public override string ToString()
        {
            var parts = new List<string>();
            if (Menu) parts.Add("Menu");
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            string k = Key.ToString();
            if (Key >= Keys.D0 && Key <= Keys.D9) k = k.Substring(1);
            parts.Add(k);
            return string.Join("+", parts.ToArray());
        }

        public uint Mods
        {
            get
            {
                uint m = 0;
                if (Ctrl) m |= N.MOD_CONTROL;
                if (Alt) m |= N.MOD_ALT;
                if (Shift) m |= N.MOD_SHIFT;
                if (Win) m |= N.MOD_WIN;
                return m;
            }
        }
    }

    /// <summary>
    /// Fenêtre de messages : raccourcis globaux (RegisterHotKey), combinaisons avec la touche Menu
    /// (hook clavier bas niveau) et commandes reçues d'une autre instance (WM_COPYDATA).
    /// </summary>
    public class HotkeyManager : NativeWindow, IDisposable
    {
        public const string WindowTitle = "dwaTop.msg";
        static readonly IntPtr Magic = new IntPtr(0x0D3A7039);
        static readonly HashSet<string> repeatable = new HashSet<string> { "opacityUp", "opacityDown" };

        public event Action<string> Triggered;
        public event Action<string> Command;
        public event Action<int> TempHotkey;
        public readonly Dictionary<string, string> Failed = new Dictionary<string, string>();
        public bool Suspended;

        readonly Dictionary<int, string> ids = new Dictionary<int, string>();
        readonly List<KeyValuePair<HotkeySpec, string>> menuCombos = new List<KeyValuePair<HotkeySpec, string>>();
        int nextId = 1;
        IntPtr kbHook = IntPtr.Zero;
        N.HookProc kbProc;
        bool menuDown, menuUsed;
        readonly HashSet<uint> swallowUp = new HashSet<uint>();
        readonly HashSet<uint> held = new HashSet<uint>();

        public HotkeyManager()
        {
            var cp = new CreateParams();
            cp.Caption = WindowTitle;
            cp.Parent = N.HWND_MESSAGE;
            CreateHandle(cp);
            try { N.ChangeWindowMessageFilterEx(Handle, N.WM_COPYDATA, 1, IntPtr.Zero); } catch { }
        }

        public void Apply(Dictionary<string, string> map)
        {
            foreach (int id in ids.Keys) if (id < 9000) N.UnregisterHotKey(Handle, id);
            var keep = new Dictionary<int, string>();
            foreach (var kv in ids) if (kv.Key >= 9000) keep[kv.Key] = kv.Value;
            ids.Clear();
            foreach (var kv in keep) ids[kv.Key] = kv.Value;
            menuCombos.Clear();
            Failed.Clear();
            nextId = 1;
            foreach (var kv in map)
            {
                var spec = HotkeySpec.Parse(kv.Value);
                if (spec == null) { if (!string.IsNullOrEmpty(kv.Value)) Failed[kv.Key] = "invalide"; continue; }
                if (spec.Menu) { menuCombos.Add(new KeyValuePair<HotkeySpec, string>(spec, kv.Key)); continue; }
                int id = nextId++;
                uint mods = spec.Mods | (repeatable.Contains(kv.Key) ? 0u : N.MOD_NOREPEAT);
                if (N.RegisterHotKey(Handle, id, mods, (uint)spec.Key)) ids[id] = kv.Key;
                else Failed[kv.Key] = "déjà utilisé par une autre appli";
            }
            if (menuCombos.Count > 0) InstallHook(); else RemoveHook();
        }

        /// <summary>Raccourci temporaire sans modificateur (Échap/Entrée pendant une sélection).</summary>
        public bool RegisterTemp(int id, Keys key)
        {
            if (N.RegisterHotKey(Handle, id, 0, (uint)key)) { ids[id] = "#temp"; return true; }
            return false;
        }

        public void UnregisterTemp(int id)
        {
            N.UnregisterHotKey(Handle, id);
            ids.Remove(id);
        }

        void InstallHook()
        {
            if (kbHook != IntPtr.Zero) return;
            kbProc = KbProc;
            kbHook = N.SetWindowsHookEx(N.WH_KEYBOARD_LL, kbProc, N.GetModuleHandle(null), 0);
            if (kbHook == IntPtr.Zero) Log.Info("Hook clavier impossible: " + Marshal.GetLastWin32Error());
        }

        void RemoveHook()
        {
            if (kbHook == IntPtr.Zero) return;
            N.UnhookWindowsHookEx(kbHook);
            kbHook = IntPtr.Zero;
        }

        static bool Down(int vk) { return (N.GetAsyncKeyState(vk) & 0x8000) != 0; }

        IntPtr KbProc(int code, IntPtr w, IntPtr l)
        {
            if (code < 0) return N.CallNextHookEx(kbHook, code, w, l);
            var k = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(KBDLLHOOKSTRUCT));
            int msg = w.ToInt32();
            bool down = msg == N.WM_KEYDOWN || msg == N.WM_SYSKEYDOWN;
            bool up = msg == N.WM_KEYUP || msg == N.WM_SYSKEYUP;
            if (k.dwExtraInfo == Magic || Suspended) return N.CallNextHookEx(kbHook, code, w, l);

            if (k.vkCode == N.VK_APPS)
            {
                if (down)
                {
                    if (!menuDown) { menuDown = true; menuUsed = false; }
                    return new IntPtr(1);
                }
                if (up)
                {
                    bool used = menuUsed;
                    menuDown = false;
                    held.Clear();
                    if (!used) UI.Post(ReplayMenu);
                    return new IntPtr(1);
                }
            }

            if (menuDown && down)
            {
                bool repeat = held.Contains(k.vkCode);
                held.Add(k.vkCode);
                bool ctrl = Down(N.VK_CONTROL), alt = Down(N.VK_MENU), shift = Down(N.VK_SHIFT), win = Down(N.VK_LWIN) || Down(N.VK_RWIN);
                foreach (var c in menuCombos)
                {
                    var s = c.Key;
                    if ((uint)s.Key != k.vkCode || s.Ctrl != ctrl || s.Alt != alt || s.Shift != shift || s.Win != win) continue;
                    menuUsed = true;
                    swallowUp.Add(k.vkCode);
                    string action = c.Value;
                    if (!repeat || repeatable.Contains(action)) UI.Post(delegate { Fire(action); });
                    return new IntPtr(1);
                }
                if (!IsModifier(k.vkCode)) menuUsed = true;
            }
            if (up)
            {
                held.Remove(k.vkCode);
                if (swallowUp.Remove(k.vkCode)) return new IntPtr(1);
            }
            return N.CallNextHookEx(kbHook, code, w, l);
        }

        static bool IsModifier(uint vk)
        {
            return vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C;
        }

        /// <summary>La touche Menu seule a été tapée : on la renvoie pour garder le menu contextuel.</summary>
        void ReplayMenu()
        {
            var inputs = new INPUT[2];
            for (int i = 0; i < 2; i++)
            {
                inputs[i].type = N.INPUT_KEYBOARD;
                inputs[i].U.ki.wVk = (ushort)N.VK_APPS;
                inputs[i].U.ki.wScan = 0x5D;
                inputs[i].U.ki.dwFlags = N.KEYEVENTF_EXTENDEDKEY | (i == 1 ? N.KEYEVENTF_KEYUP : 0);
                inputs[i].U.ki.dwExtraInfo = Magic;
            }
            N.SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        void Fire(string action)
        {
            if (Triggered != null) Triggered(action);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)N.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                string action;
                if (ids.TryGetValue(id, out action))
                {
                    if (action == "#temp") { if (TempHotkey != null) TempHotkey(id); }
                    else Fire(action);
                }
                return;
            }
            if (m.Msg == (int)N.WM_COPYDATA)
            {
                try
                {
                    var cds = (COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(COPYDATASTRUCT));
                    string s = Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2).TrimEnd('\0');
                    UI.Post(delegate { if (Command != null) Command(s); });
                }
                catch (Exception ex) { Log.Error(ex); }
                m.Result = new IntPtr(1);
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>Envoie une commande à l'instance déjà lancée. Renvoie false si aucune instance.</summary>
        public static bool SendToRunning(string cmd)
        {
            IntPtr h = N.FindWindowEx(N.HWND_MESSAGE, IntPtr.Zero, null, WindowTitle);
            if (h == IntPtr.Zero) return false;
            IntPtr mem = Marshal.StringToHGlobalUni(cmd);
            try
            {
                var cds = new COPYDATASTRUCT { dwData = IntPtr.Zero, cbData = (cmd.Length + 1) * 2, lpData = mem };
                N.SendMessage(h, N.WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            finally { Marshal.FreeHGlobal(mem); }
            return true;
        }

        public void Dispose()
        {
            RemoveHook();
            foreach (int id in ids.Keys) N.UnregisterHotKey(Handle, id);
            ids.Clear();
            DestroyHandle();
        }
    }

    /// <summary>Événements système sur les fenêtres (premier plan, déplacement, fermeture…).</summary>
    public class WinEvents : IDisposable
    {
        public event Action<IntPtr> Foreground, Location, Destroyed, Shown, Hidden, MinimizeStart, MinimizeEnd, MoveSizeStart, MoveSizeEnd, NameChanged;
        readonly N.WinEventProc proc;
        readonly List<IntPtr> hooks = new List<IntPtr>();

        public WinEvents()
        {
            proc = Callback;
            Hook(N.EVENT_SYSTEM_FOREGROUND, N.EVENT_SYSTEM_FOREGROUND);
            Hook(N.EVENT_SYSTEM_MOVESIZESTART, N.EVENT_SYSTEM_MOVESIZEEND);
            Hook(N.EVENT_SYSTEM_MINIMIZESTART, N.EVENT_SYSTEM_MINIMIZEEND);
            Hook(N.EVENT_OBJECT_DESTROY, N.EVENT_OBJECT_HIDE);
            Hook(N.EVENT_OBJECT_LOCATIONCHANGE, N.EVENT_OBJECT_NAMECHANGE);
        }

        void Hook(uint a, uint b)
        {
            IntPtr h = N.SetWinEventHook(a, b, IntPtr.Zero, proc, 0, 0, N.WINEVENT_OUTOFCONTEXT | N.WINEVENT_SKIPOWNPROCESS);
            if (h != IntPtr.Zero) hooks.Add(h);
        }

        void Callback(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero || idObject != N.OBJID_WINDOW || idChild != 0) return;
            try
            {
                switch (ev)
                {
                    case N.EVENT_SYSTEM_FOREGROUND: Raise(Foreground, hwnd); break;
                    case N.EVENT_SYSTEM_MOVESIZESTART: Raise(MoveSizeStart, hwnd); break;
                    case N.EVENT_SYSTEM_MOVESIZEEND: Raise(MoveSizeEnd, hwnd); break;
                    case N.EVENT_SYSTEM_MINIMIZESTART: Raise(MinimizeStart, hwnd); break;
                    case N.EVENT_SYSTEM_MINIMIZEEND: Raise(MinimizeEnd, hwnd); break;
                    case N.EVENT_OBJECT_DESTROY: Raise(Destroyed, hwnd); break;
                    case N.EVENT_OBJECT_SHOW: Raise(Shown, hwnd); break;
                    case N.EVENT_OBJECT_HIDE: Raise(Hidden, hwnd); break;
                    case N.EVENT_OBJECT_LOCATIONCHANGE: Raise(Location, hwnd); break;
                    case N.EVENT_OBJECT_NAMECHANGE: Raise(NameChanged, hwnd); break;
                }
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        static void Raise(Action<IntPtr> a, IntPtr h) { if (a != null) a(h); }

        public void Dispose()
        {
            foreach (var h in hooks) N.UnhookWinEvent(h);
            hooks.Clear();
        }
    }
}
