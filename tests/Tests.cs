using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>Petite fenêtre de test (sans focus, hors barre des tâches).</summary>
    class Tiny : Form
    {
        public Tiny(Color c, Rectangle r, bool topmost)
        {
            FormBorderStyle = FormBorderStyle.None; BackColor = c; StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false; TopMost = topmost; Bounds = r;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE; return cp; }
        }
    }

    static class Tests
    {
        static int pass, fail;

        static void Ok(bool c, string name)
        {
            if (c) pass++; else fail++;
            Console.WriteLine((c ? "  ok    " : "  ÉCHEC ") + name);
        }

        static void Pump(int ms)
        {
            int end = Environment.TickCount + ms;
            while (Environment.TickCount - end < 0) { Application.DoEvents(); Thread.Sleep(8); }
        }

        static Color Sample(Point p)
        {
            using (var b = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(b))
            {
                g.CopyFromScreen(p.X, p.Y, 0, 0, new Size(1, 1));
                return b.GetPixel(0, 0);
            }
        }

        static float[] Apply(float[] m, float r, float g, float b)
        {
            var i = new[] { r, g, b, 1f, 1f };
            var o = new float[3];
            for (int j = 0; j < 3; j++) { float s = 0; for (int k = 0; k < 5; k++) s += i[k] * m[k * 5 + j]; o[j] = s; }
            return o;
        }

        static bool Near(float a, float b, float tol) { return Math.Abs(a - b) <= tol; }

        [STAThread]
        static int Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            UI.Init();
            Config.S = new Settings();
            foreach (var a in Actions.All) Config.S.Hotkeys[a[0]] = a[2];
            Config.S.Osd = false;
            PipWindow.TestOffscreen = true;
            Directory.CreateDirectory(Config.Dir);

            Console.WriteLine("Unitaires");
            Unit();
            Console.WriteLine("Rendu (fenêtres de 24 px dans le coin, < 2 s)");
            Visual();
            if (args.Length > 0)
            {
                Console.WriteLine("Intégration (fenêtre cible hors écran)");
                Integration(args[0]);
            }
            Console.WriteLine();
            Console.WriteLine(pass + " ok, " + fail + " échec(s)");
            return fail == 0 ? 0 : 1;
        }

        static void Unit()
        {
            var h = HotkeySpec.Parse("Menu+Z");
            Ok(h != null && h.Menu && h.Key == Keys.Z && !h.Alt, "Menu+Z se lit comme touche Menu + Z");
            Ok(h != null && h.ToString() == "Menu+Z", "Menu+Z se réécrit pareil");
            var a = HotkeySpec.Parse("Alt+Z");
            Ok(a != null && a.Mods == N.MOD_ALT && !a.Menu, "Alt+Z = modificateur Alt");
            Ok(HotkeySpec.Parse("Z") == null, "une lettre seule est refusée");
            var x = HotkeySpec.Parse("Menu+Shift+X");
            Ok(x != null && x.Menu && x.Shift && x.Key == Keys.X, "Menu+Shift+X");
            var d = HotkeySpec.Parse("Ctrl+Alt+1");
            Ok(d != null && d.Key == Keys.D1 && d.ToString() == "Ctrl+Alt+1", "chiffres (Ctrl+Alt+1)");
            Ok(HotkeySpec.Parse("Menu+Up").Key == Keys.Up && HotkeySpec.Parse("Menu+Space").Key == Keys.Space && HotkeySpec.Parse("Menu+Back").Key == Keys.Back, "flèches / espace / retour arrière");
            bool allDefaults = true;
            foreach (var act in Actions.All) if (act[2].Length > 0 && HotkeySpec.Parse(act[2]) == null) allDefaults = false;
            Ok(allDefaults, "tous les raccourcis par défaut sont valides");
            Ok(Config.S.Hotkeys["hidePips"] == "Menu+Z", "Menu+Z = cacher/afficher les miniatures");

            var inv = Filters.Matrix("invert");
            var o = Apply(inv, 1, 1, 1);
            Ok(Near(o[0], 0, 0.01f) && Near(o[1], 0, 0.01f) && Near(o[2], 0, 0.01f), "inversion : blanc → noir");
            o = Apply(inv, 0, 0, 0);
            Ok(Near(o[0], 1, 0.01f) && Near(o[2], 1, 0.01f), "inversion : noir → blanc");
            var smart = Filters.Matrix("smart");
            o = Apply(smart, 1, 1, 1);
            Ok(o[0] < 0.05f && o[1] < 0.05f && o[2] < 0.05f, "intelligent : blanc → noir");
            o = Apply(smart, 0, 0, 0);
            Ok(o[0] > 0.85f && o[1] > 0.85f && o[2] > 0.85f, "intelligent : noir → clair");
            o = Apply(smart, 0.9f, 0.1f, 0.1f);
            Ok(o[0] > o[1] && o[0] > o[2], "intelligent : le rouge reste rouge");
            o = Apply(Filters.Matrix("gray"), 1, 0, 0);
            Ok(Near(o[0], o[1], 0.001f) && Near(o[1], o[2], 0.001f) && Near(o[0], 0.2126f, 0.01f), "gris : luminance");

            var st = new Settings();
            st.Rules.Add(new Rule { Process = "spotify.exe", Pin = true, Opacity = 70, Dark = "smart" });
            st.Hotkeys["hidePips"] = "Menu+Z";
            st.PipPlacements["vlc.exe"] = new[] { 10, -20, 300 };
            var js = new JavaScriptSerializer();
            string json = Config.Pretty(js.Serialize(st));
            var back = js.Deserialize<Settings>(json);
            Ok(back.Rules.Count == 1 && back.Rules[0].Process == "spotify.exe" && back.Rules[0].Opacity == 70 && back.Rules[0].Pin, "config : règles conservées (JSON indenté)");
            Ok(back.Hotkeys["hidePips"] == "Menu+Z" && back.PipPlacements["vlc.exe"][1] == -20, "config : raccourcis et positions conservés");
            Ok(json.Contains("\n  \""), "config : JSON lisible (indenté)");

            long lp = N.MakeLParam(-5, 300).ToInt64();
            Ok((short)(lp & 0xFFFF) == -5 && ((lp >> 16) & 0xFFFF) == 300, "coordonnées négatives (multi-écran) dans lParam");
        }

        static void Visual()
        {
            var scr = Screen.PrimaryScreen.Bounds;
            DarkOverlay.Init();
            Ok(DarkOverlay.Available, "API Magnification disponible");

            // 1) Mode sombre : un carré rouge doit apparaître cyan à travers la loupe inversée
            var red = new Tiny(Color.Red, new Rectangle(scr.Right - 24, scr.Top, 24, 24), true);
            red.Show(); Pump(150);
            var center = new Point(scr.Right - 12, scr.Top + 12);
            Color before = Sample(center);
            Ok(before.R > 200 && before.G < 60, "témoin rouge visible (" + before + ")");
            if (DarkOverlay.Available)
            {
                var ov = new DarkOverlay(red.Handle, "invert", null, false);
                Pump(350);
                Color c = Sample(center);
                Ok(c.R < 70 && c.G > 190 && c.B > 190, "mode sombre (inversion) : rouge → cyan (" + c + ")");
                ov.SetFilter("gray");
                Pump(250);
                c = Sample(center);
                Ok(Math.Abs(c.R - c.G) < 12 && Math.Abs(c.G - c.B) < 12 && c.R > 30 && c.R < 90, "mode sombre (gris) : rouge → gris foncé (" + c + ")");
                ov.Close(); Pump(200);
                c = Sample(center);
                Ok(c.R > 200 && c.G < 60, "calque retiré : rouge à nouveau (" + c + ")");
            }

            // 2) Une miniature DWM dans une fenêtre semi-transparente suit-elle l'opacité de la fenêtre ?
            var blue = new Tiny(Color.Blue, new Rectangle(scr.Right - 48, scr.Top, 24, 24), true);
            blue.Show(); Pump(100);
            var dst = new Tiny(Color.Black, new Rectangle(scr.Right - 48, scr.Top, 24, 24), true);
            dst.Opacity = 0.5;
            dst.Show(); Pump(100);
            IntPtr thumb;
            N.DwmRegisterThumbnail(dst.Handle, red.Handle, out thumb);
            var p = new DWM_THUMBNAIL_PROPERTIES { dwFlags = N.DWM_TNP_RECTDESTINATION | N.DWM_TNP_VISIBLE | N.DWM_TNP_OPACITY, rcDestination = new RECT(0, 0, 24, 24), opacity = 255, fVisible = 1 };
            N.DwmUpdateThumbnailProperties(thumb, ref p);
            Pump(300);
            Color m = Sample(new Point(scr.Right - 36, scr.Top + 12));
            Console.WriteLine("        info : miniature dans fenêtre à 50 % = " + m + (m.B > 60 ? "  → l'opacité de la fenêtre s'applique" : "  → opacité ignorée"));
            N.DwmUnregisterThumbnail(thumb);
            dst.Close(); blue.Close(); red.Close();
            Pump(100);
        }

        static IntPtr FindTarget()
        {
            return N.FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, "dwaTopTestTarget");
        }

        static void Integration(string targetExe)
        {
            var proc = Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = false });
            IntPtr h = IntPtr.Zero;
            for (int i = 0; i < 100 && h == IntPtr.Zero; i++) { Pump(50); h = FindTarget(); }
            Ok(h != IntPtr.Zero, "fenêtre cible trouvée");
            if (h == IntPtr.Zero) { try { proc.Kill(); } catch { } return; }
            try
            {
                var vs = SystemInformation.VirtualScreen;
                Ok(!vs.IntersectsWith(Win.Frame(h)), "la cible est bien hors écran");
                Ok(Win.IsEligible(h), "la cible est une fenêtre gérable");
                int origEx = N.GetExStyle(h);
                var m = new Manager();

                m.SetPin(h, true);
                var mw = m.Get(h, false);
                Ok(Win.IsTopmost(h) && mw != null && mw.Pinned, "épingler → toujours au-dessus");
                Ok(mw.Border != null, "bordure créée pour la fenêtre épinglée");

                m.SetOpacity(h, 50, false);
                uint key, fl; byte alpha;
                N.GetLayeredWindowAttributes(h, out key, out alpha, out fl);
                Ok((N.GetExStyle(h) & N.WS_EX_LAYERED) != 0 && Math.Abs(alpha - 128) <= 1, "opacité 50 % (alpha=" + alpha + ")");

                m.SetClickThrough(h, true);
                Ok((N.GetExStyle(h) & N.WS_EX_TRANSPARENT) != 0, "click-through activé");
                m.SetClickThrough(h, false);
                Ok((N.GetExStyle(h) & N.WS_EX_TRANSPARENT) == 0 && (N.GetExStyle(h) & N.WS_EX_LAYERED) != 0, "click-through désactivé, opacité gardée");

                m.SetHoverOpaque(h, true);
                Ok(mw.HoverOpaque, "opaque au survol");
                m.SetHoverOpaque(h, false);

                var pip = m.OpenPip(h, Rectangle.Empty, true);
                Pump(300);
                N.GetLayeredWindowAttributes(h, out key, out alpha, out fl);
                Ok(pip != null && mw.Pip == pip && pip.Visible, "miniature ouverte");
                Ok(alpha == 0 && (N.GetExStyle(h) & N.WS_EX_TRANSPARENT) != 0, "original caché (invisible + click-through)");
                Ok(N.IsWindowVisible(h) && !N.IsIconic(h), "l'original reste « vivant » (pas réduit)");
                Ok(pip.Left > vs.Right, "miniature de test placée hors écran");

                m.ToggleAllPips();
                Ok(!pip.Visible && m.PipsHidden, "Menu+Z : miniatures cachées");
                m.ToggleAllPips();
                Ok(pip.Visible && !m.PipsHidden, "Menu+Z : miniatures réaffichées");

                pip.SetCrop(new Rectangle(10, 10, 200, 100));
                Ok(pip.IsCropped && Math.Abs(pip.ThumbScreenRect().Width / (double)pip.ThumbScreenRect().Height - 2.0) < 0.08, "recadrage 200×100 → ratio 2:1");
                pip.ClearCrop();

                m.ClosePip(mw, false);
                Pump(100);
                N.GetLayeredWindowAttributes(h, out key, out alpha, out fl);
                Ok(mw.Pip == null && Math.Abs(alpha - 128) <= 1 && (N.GetExStyle(h) & N.WS_EX_TRANSPARENT) == 0, "miniature fermée → original revenu à 50 %");

                if (DarkOverlay.Available)
                {
                    m.SetDark(h, "smart");
                    Ok(mw.DarkOv != null && mw.Dark == "smart", "mode sombre activé");
                    m.SetDark(h, null);
                    Ok(mw.DarkOv == null, "mode sombre désactivé");
                }

                m.SetGlass(h, true);
                N.GetLayeredWindowAttributes(h, out key, out alpha, out fl);
                Ok(mw.Glass && (fl & N.LWA_COLORKEY) != 0 && key == 0xFFFFFF, "mode verre : fond blanc détecté (clé=" + key.ToString("X6") + ")");
                m.SetGlass(h, false);

                m.SetBlur(h, true);
                Console.WriteLine("        info : flou accepté par la fenêtre = " + mw.Blur);
                m.SetBlur(h, false);

                // interface : construite et dessinée hors écran (aucune fenêtre affichée)
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
                Rectangle fr = Win.Frame(h);
                var compute = typeof(TitleButton).GetMethod("Compute", flags);
                var br = (Rectangle)compute.Invoke(null, new object[] { h });
                Ok(!br.IsEmpty && fr.Contains(br) && br.Top < fr.Top + 40 * Win.ScaleAt(fr), "bouton de barre de titre placé dans la barre (" + br + ")");
                m.SetPin(h, true);
                m.SetDark(h, "smart");
                var qp = (Form)Activator.CreateInstance(typeof(QuickPanel), flags, null, new object[] { h, new Rectangle(fr.Right - 40, fr.Top, 30, 20) }, null);
                using (var bmp = new Bitmap(qp.Width, qp.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    typeof(Control).GetMethod("OnPaint", flags).Invoke(qp, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, qp.Size)) });
                    Ok(qp.Width > 200 && qp.Height > 250, "panneau rapide : mise en page (" + qp.Size + ")");
                    bmp.Save(Path.Combine(Config.Dir, "preview-panel.png"));
                }
                qp.Dispose();
                m.Reset(h, false);
                App.Hotkeys = new HotkeyManager();
                App.Hotkeys.Apply(Config.S.Hotkeys);
                Ok(App.Hotkeys.Failed.Count == 0 || !App.Hotkeys.Failed.ContainsKey("hidePips"), "Menu+Z enregistré (hook clavier)");
                foreach (var kv in App.Hotkeys.Failed) Console.WriteLine("        info : raccourci " + kv.Key + " → " + kv.Value);
                var sf = (Form)Activator.CreateInstance(typeof(SettingsForm), true);
                Ok(sf.Controls.Count >= 2, "fenêtre de paramètres : toutes les pages se construisent");
                sf.ShowInTaskbar = false;
                sf.StartPosition = FormStartPosition.Manual;
                sf.Location = new Point(vs.Right + 300, vs.Top + 40);
                N.ShowWindow(sf.Handle, N.SW_SHOWNOACTIVATE);
                Pump(250);
                foreach (var page in new[] { "general", "pip", "hotkeys" })
                {
                    typeof(SettingsForm).GetMethod("ShowPage", flags).Invoke(sf, new object[] { page });
                    Pump(120);
                    using (var sb = new Bitmap(sf.Width, sf.Height)) { sf.DrawToBitmap(sb, new Rectangle(Point.Empty, sf.Size)); sb.Save(Path.Combine(Config.Dir, "preview-settings-" + page + ".png")); }
                }
                sf.Dispose();
                App.Hotkeys.Dispose();
                App.Hotkeys = null;

                m.Reset(h, false);
                int ex = N.GetExStyle(h);
                Ok((ex & (N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT)) == (origEx & (N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT)) && !Win.IsTopmost(h), "réinitialiser → état d'origine");
                Ok(m.Get(h, false) == null, "fenêtre retirée de la liste");

                // règle automatique
                Config.S.Rules.Add(new Rule { Process = Win.ProcessName(h), Pin = true, Opacity = 80 });
                m.OnWindowShown(h);
                Pump(900);
                mw = m.Get(h, false);
                Ok(mw != null && mw.Pinned && mw.Opacity == 80, "règle par appli appliquée automatiquement");
                m.Reset(h, false);
                Config.S.Rules.Clear();

                // récupération après plantage
                m.SetPin(h, true);
                m.SetOpacity(h, 40, false);
                Ok(File.Exists(Path.Combine(Config.Dir, "session.txt")), "session enregistrée sur disque");
                int n = Manager.RecoverSession();
                ex = N.GetExStyle(h);
                Ok(n == 1 && (ex & N.WS_EX_LAYERED) == (origEx & N.WS_EX_LAYERED) && !Win.IsTopmost(h), "après plantage : fenêtre remise en état");
                m.Map.Clear();

                m.SetPin(h, true);
                m.SetOpacity(h, 60, false);
                m.RestoreAll();
                ex = N.GetExStyle(h);
                Ok((ex & N.WS_EX_LAYERED) == (origEx & N.WS_EX_LAYERED) && !Win.IsTopmost(h), "fermeture de dwaTop → tout restauré");
            }
            catch (Exception ex) { Ok(false, "exception : " + ex); }
            finally { try { proc.Kill(); } catch { } }
        }
    }
}
