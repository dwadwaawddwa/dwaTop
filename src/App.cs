using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DwaTop
{
    /// <summary>Point central : raccourcis, événements système, icône de notification.</summary>
    public static class App
    {
        public static HotkeyManager Hotkeys;
        public static WinEvents Events;
        public static TitleButton Button;
        public static AnchorManager Anchors;

        public static void ShowSettings(string page = null) { SettingsForm.Open(page); }
        public static void RefreshButton() { if (Button != null) Button.Update(); }

        /// <summary>Fenêtre visée par un raccourci : la fenêtre active (ou la source si c'est une miniature).</summary>
        public static IntPtr ActionTarget()
        {
            IntPtr fg = N.GetForegroundWindow();
            if (fg != IntPtr.Zero && Win.IsOurs(fg))
            {
                foreach (var mw in Manager.I.Map.Values)
                    if (mw.Pip != null && !mw.Pip.IsDisposed && mw.Pip.Handle == fg) return mw.Hwnd;
                if (Button != null && N.IsWindow(Button.Target)) return Button.Target;
                return IntPtr.Zero;
            }
            return fg;
        }

        public static void Run(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            action = action.Trim();
            // actions globales
            switch (action)
            {
                case "hidePips": Manager.I.ToggleAllPips(); return;
                case "clickThroughOff": Manager.I.ClickThroughOffAll(); return;
                case "anchorsAll":
                    Config.S.AnchorsAll = !Config.S.AnchorsAll; Config.Save(); Anchors.Update();
                    Osd.Flash("anchor", Config.S.AnchorsAll ? "Ancres pour toutes les fenêtres" : "Ancres globales désactivées");
                    return;
                case "resetAll": Manager.I.ResetAll(); return;
                case "settings": ShowSettings(); return;
                case "quit": Application.Exit(); return;
            }
            IntPtr h = ActionTarget();
            bool hiddenSrc = Manager.I.IsHiddenSource(h);
            if (h == IntPtr.Zero || (!Win.IsEligible(h) && !hiddenSrc))
            {
                Osd.Flash("logo", "Aucune fenêtre active à modifier");
                return;
            }
            switch (action)
            {
                case "pin": Manager.I.TogglePin(h); break;
                case "opacity": Manager.I.ToggleOpacity(h); break;
                case "opacityUp": Manager.I.StepOpacity(h, 10); break;
                case "opacityDown": Manager.I.StepOpacity(h, -10); break;
                case "clickThrough": Manager.I.ToggleClickThrough(h); break;
                case "pip": Manager.I.TogglePip(h); break;
                case "crop": Manager.I.StartCrop(h); break;
                case "dark": Manager.I.ToggleDark(h); break;
                case "blur": Manager.I.ToggleBlur(h); break;
                case "glass": Manager.I.ToggleGlass(h); break;
                case "anchor": Manager.I.ToggleAnchor(h); break;
                case "reset": Manager.I.Reset(h); break;
                case "panel":
                    {
                        Rectangle anchor;
                        if (Button != null && Button.Visible && Button.Target == h) anchor = Button.Bounds;
                        else { Point c = Win.Cursor(); anchor = new Rectangle(c.X, c.Y, 1, 1); }
                        QuickPanel.ShowFor(h, anchor);
                        break;
                    }
                default: Log.Info("Action inconnue : " + action); break;
            }
        }
    }

    public class AppContext : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        bool cleaned;

        public AppContext(string startAction, bool firstRun, int recovered)
        {
            new Manager();
            App.Hotkeys = new HotkeyManager();
            App.Hotkeys.Triggered += App.Run;
            App.Hotkeys.Command += OnCommand;
            App.Hotkeys.Apply(Config.S.Hotkeys);

            App.Button = new TitleButton();
            App.Anchors = new AnchorManager();

            App.Events = new WinEvents();
            App.Events.Foreground += delegate (IntPtr h)
            {
                Manager.I.OnForeground(h);
                App.Button.Track(h);
                DarkOverlay.SyncAll();
            };
            App.Events.Location += delegate (IntPtr h)
            {
                Manager.I.OnLocation(h);
                if (h == App.Button.Target) App.Button.Update();
            };
            App.Events.Destroyed += delegate (IntPtr h) { Manager.I.OnDestroyed(h); };
            App.Events.Shown += delegate (IntPtr h) { Manager.I.OnWindowShown(h); };
            App.Events.MinimizeStart += delegate (IntPtr h) { Manager.I.OnMinimizeStart(h); App.Button.Update(); };
            App.Events.MinimizeEnd += delegate (IntPtr h) { App.Button.Update(); };
            App.Events.MoveSizeEnd += delegate (IntPtr h) { App.Button.Update(); };

            tray.Icon = Icons.AppIcon(32);
            tray.Text = "dwaTop";
            tray.Visible = true;
            tray.ContextMenuStrip = Menus.Create();
            tray.ContextMenuStrip.Opening += delegate { BuildTrayMenu(); };
            tray.MouseUp += delegate (object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) App.ShowSettings(); };
            BuildTrayMenu();

            App.Button.Track(N.GetForegroundWindow());
            Manager.I.ApplyRulesToExisting();

            SystemEvents.SessionEnding += delegate { Cleanup(); };
            Application.ApplicationExit += delegate { Cleanup(); };

            if (firstRun)
                UI.Delay(600, delegate
                {
                    tray.ShowBalloonTip(6000, "dwaTop est lancé",
                        "Un bouton ⌄ apparaît dans la barre de titre de la fenêtre active. Alt+Z épingler · Alt+A opacité · Alt+Q miniature · Menu+Z cacher les miniatures.", ToolTipIcon.Info);
                });
            if (recovered > 0) Log.Info(recovered + " fenêtre(s) remise(s) en état après un arrêt brutal");
            if (!string.IsNullOrEmpty(startAction)) UI.Delay(400, delegate { OnCommand(startAction); });
        }

        void OnCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return;
            cmd = cmd.Trim().TrimStart('-', '/');
            if (cmd == "show" || cmd.Length == 0) { App.ShowSettings(); return; }
            App.Run(cmd);
        }

        void BuildTrayMenu()
        {
            var m = tray.ContextMenuStrip;
            m.Items.Clear();
            Menus.Add(m.Items, "dwaTop " + Program.Version, null, null, false);
            Menus.Sep(m.Items);
            var managed = Manager.I.Map.Values.Where(w => N.IsWindow(w.Hwnd) && w.HasEffects).ToList();
            var sub = Menus.Sub(m.Items, "Fenêtres modifiées (" + managed.Count + ")");
            if (managed.Count == 0) Menus.Add(sub.DropDownItems, "(aucune)", null, null, false);
            foreach (var w in managed)
            {
                var mw = w;
                string t = Win.Title(mw.Hwnd);
                if (t.Length > 48) t = t.Substring(0, 47) + "…";
                var it = Menus.Sub(sub.DropDownItems, (t.Length > 0 ? t : mw.Process) + "  —  " + mw.Process);
                var items = it.DropDownItems;
                Menus.Add(items, "Épinglée", delegate { Manager.I.TogglePin(mw.Hwnd); }, mw.Pinned);
                var op = Menus.Sub(items, "Opacité (" + mw.Opacity + " %)");
                foreach (int v in new[] { 100, 90, 75, 60, 45, 30 })
                {
                    int vv = v;
                    Menus.Add(op.DropDownItems, v + " %", delegate { Manager.I.SetOpacity(mw.Hwnd, vv); }, mw.Opacity == v);
                }
                Menus.Add(items, "Click-through", delegate { Manager.I.ToggleClickThrough(mw.Hwnd); }, mw.ClickThrough);
                Menus.Add(items, "Mode sombre", delegate { Manager.I.ToggleDark(mw.Hwnd); }, !string.IsNullOrEmpty(mw.Dark));
                Menus.Add(items, "Miniature", delegate { Manager.I.TogglePip(mw.Hwnd); }, mw.Pip != null);
                Menus.Add(items, "Ancre", delegate { Manager.I.ToggleAnchor(mw.Hwnd); }, mw.Anchor);
                Menus.Sep(items);
                Menus.Add(items, "Afficher la fenêtre", delegate
                {
                    if (mw.Pip != null) Manager.I.ClosePip(mw, true); else Win.ForceForeground(mw.Hwnd);
                });
                Menus.Add(items, "Réinitialiser", delegate { Manager.I.Reset(mw.Hwnd); });
            }
            Menus.Add(m.Items, Manager.I.PipsHidden ? "Afficher les miniatures" : "Cacher les miniatures", delegate { Manager.I.ToggleAllPips(); });
            Menus.Add(m.Items, "Désactiver le click-through partout", delegate { Manager.I.ClickThroughOffAll(); });
            Menus.Add(m.Items, "Ancres pour toutes les fenêtres", delegate { App.Run("anchorsAll"); }, Config.S.AnchorsAll);
            Menus.Add(m.Items, "Bouton dans la barre de titre", delegate
            {
                Config.S.TitleButton = !Config.S.TitleButton; Config.Save(); App.RefreshButton();
            }, Config.S.TitleButton);
            Menus.Sep(m.Items);
            Menus.Add(m.Items, "Paramètres…", delegate { App.ShowSettings(); });
            Menus.Add(m.Items, "Tout réinitialiser", delegate { Manager.I.ResetAll(); });
            Menus.Add(m.Items, "Quitter (tout restaurer)", delegate { ExitThread(); });
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            try { Manager.I.RestoreAll(); } catch (Exception ex) { Log.Error(ex); }
            try { DarkOverlay.Shutdown(); } catch { }
            try { App.Anchors.Clear(); } catch { }
            try { App.Events.Dispose(); } catch { }
            try { App.Hotkeys.Dispose(); } catch { }
            try { Config.Save(); } catch { }
            tray.Visible = false;
            tray.Dispose();
            Log.Info("dwaTop arrêté, fenêtres restaurées");
        }

        protected override void ExitThreadCore()
        {
            Cleanup();
            base.ExitThreadCore();
        }
    }
}
