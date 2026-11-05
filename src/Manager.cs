using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DwaTop
{
    /// <summary>État d'une fenêtre modifiée par dwaTop + valeurs d'origine pour tout restaurer.</summary>
    public class ManagedWindow
    {
        public IntPtr Hwnd;
        public uint Pid;
        public string Process = "";
        // valeurs d'origine
        public bool Captured;
        public int OrigEx;
        public bool OrigLayered, OrigLwa, UlwLayered, OrigTopmost;
        public byte OrigAlpha = 255;
        public uint OrigKey, OrigFlags;
        // effets
        public bool Pinned, PinSuspended, TopmostTouched;
        public int Opacity = 100, LastOpacity;
        public bool ClickThrough, HoverOpaque, HoverActive, HoverExplicit;
        public bool Blur, Glass;
        public uint GlassKey;
        public string Dark;
        public DarkOverlay DarkOv;
        public BorderOverlay Border;
        public PipWindow Pip;
        public bool HiddenByPip, TempReveal;
        public bool SourceTop;   // source cachée gardée « au premier plan » pour que l'appli continue de dessiner
        public bool Anchor;

        public bool HasEffects
        {
            get
            {
                return Pinned || Opacity < 100 || ClickThrough || Blur || Glass || !string.IsNullOrEmpty(Dark)
                    || Pip != null || HiddenByPip || Anchor;
            }
        }

        public int EffectCount
        {
            get
            {
                int n = 0;
                if (Pinned) n++; if (Opacity < 100) n++; if (ClickThrough) n++; if (Blur) n++; if (Glass) n++;
                if (!string.IsNullOrEmpty(Dark)) n++; if (Pip != null) n++; if (Anchor) n++;
                return n;
            }
        }
    }

    public class Manager
    {
        public static Manager I;
        public readonly Dictionary<IntPtr, ManagedWindow> Map = new Dictionary<IntPtr, ManagedWindow>();
        public readonly List<IntPtr> Mru = new List<IntPtr>();
        public event Action Changed;
        public bool PipsHidden;

        readonly Timer timer = new Timer();
        readonly Dictionary<IntPtr, int> pendingRules = new Dictionary<IntPtr, int>();
        readonly HashSet<IntPtr> rulesApplied = new HashSet<IntPtr>();
        static string SessionPath { get { return Path.Combine(Config.Dir, "session.txt"); } }

        public Manager()
        {
            I = this;
            timer.Interval = 100;
            timer.Tick += delegate { try { Tick(); } catch (Exception ex) { Log.Error(ex); } };
            timer.Start();
        }

        public void Notify() { if (Changed != null) Changed(); }

        public ManagedWindow Get(IntPtr h, bool create)
        {
            ManagedWindow mw;
            if (Map.TryGetValue(h, out mw)) return mw;
            if (!create || h == IntPtr.Zero || !N.IsWindow(h) || Win.IsOurs(h)) return null;
            mw = new ManagedWindow { Hwnd = h, Pid = Win.Pid(h), Process = Win.ProcessName(h) };
            Map[h] = mw;
            return mw;
        }

        void Capture(ManagedWindow mw)
        {
            if (mw.Captured) return;
            IntPtr h = mw.Hwnd;
            mw.OrigEx = N.GetExStyle(h);
            mw.OrigTopmost = (mw.OrigEx & N.WS_EX_TOPMOST) != 0;
            mw.OrigLayered = (mw.OrigEx & N.WS_EX_LAYERED) != 0;
            if (mw.OrigLayered)
            {
                uint key, fl; byte a;
                mw.OrigLwa = N.GetLayeredWindowAttributes(h, out key, out a, out fl);
                if (mw.OrigLwa) { mw.OrigKey = key; mw.OrigAlpha = a; mw.OrigFlags = fl; }
                else mw.UlwLayered = true;
            }
            mw.Captured = true;
            SaveSession();
        }

        /// <summary>Applique opacité / click-through / masquage / verre au style "layered" de la fenêtre.</summary>
        bool ApplyLayered(ManagedWindow mw)
        {
            Capture(mw);
            IntPtr h = mw.Hwnd;
            if (mw.UlwLayered) return false;
            bool hidden = mw.HiddenByPip && !mw.TempReveal;
            int op = mw.HoverActive ? 100 : mw.Opacity;
            bool needLayer = hidden || op < 100 || mw.ClickThrough || mw.Glass;
            bool needTransp = hidden || mw.ClickThrough;
            int cur = N.GetExStyle(h);
            int ex = cur;
            if (needLayer) ex |= N.WS_EX_LAYERED; else if (!mw.OrigLayered) ex &= ~N.WS_EX_LAYERED;
            if (needTransp) ex |= N.WS_EX_TRANSPARENT; else if ((mw.OrigEx & N.WS_EX_TRANSPARENT) == 0) ex &= ~N.WS_EX_TRANSPARENT;
            bool removedLayer = (cur & N.WS_EX_LAYERED) != 0 && (ex & N.WS_EX_LAYERED) == 0;
            if (ex != cur) N.SetExStyle(h, ex);
            if (needLayer)
            {
                byte a = hidden ? (byte)0 : (byte)Math.Round(op * 255 / 100.0);
                uint fl = N.LWA_ALPHA, key = 0;
                if (mw.Glass && !hidden) { fl |= N.LWA_COLORKEY; key = mw.GlassKey; }
                N.SetLayeredWindowAttributes(h, key, a, fl);
                if ((N.GetExStyle(h) & N.WS_EX_LAYERED) == 0) return false;
            }
            else if (mw.OrigLayered && mw.OrigLwa)
            {
                N.SetLayeredWindowAttributes(h, mw.OrigKey, mw.OrigAlpha, mw.OrigFlags);
            }
            if (removedLayer) Win.Redraw(h);
            KeepAwake(mw, hidden && mw.Pip != null);
            return true;
        }

        /// <summary>
        /// Source cachée (alpha 0, traversée par la souris) placée au-dessus de tout : invisible, mais jamais
        /// « recouverte », donc Chrome & co continuent de dessiner la vidéo pour la miniature.
        /// </summary>
        void KeepAwake(ManagedWindow mw, bool on)
        {
            IntPtr h = mw.Hwnd;
            if (on && !mw.SourceTop)
            {
                if (Win.IsTopmost(h)) return;
                N.SetWindowPos(h, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                mw.SourceTop = Win.IsTopmost(h);
                if (mw.SourceTop) SaveSession();
            }
            else if (!on && mw.SourceTop)
            {
                mw.SourceTop = false;
                if (!mw.Pinned && !mw.OrigTopmost && N.IsWindow(h))
                    N.SetWindowPos(h, N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            }
        }

        static void Denied()
        {
            Osd.Flash("close", "Fenêtre protégée — lance dwaTop en administrateur", true);
        }

        /// <summary>Fenêtre la plus haute de la "pile" cible + calques dwaTop (mode sombre, bordure).</summary>
        public IntPtr TopFollower(IntPtr h)
        {
            ManagedWindow mw;
            if (!Map.TryGetValue(h, out mw)) return h;
            if (mw.Border != null && mw.Border.IsShown) return mw.Border.Handle;
            if (mw.DarkOv != null && mw.DarkOv.IsShown) return mw.DarkOv.Handle;
            return h;
        }

        // ───────────── Épingler ─────────────

        public void SetPin(IntPtr h, bool on)
        {
            var mw = Get(h, true); if (mw == null) return;
            Capture(mw);
            mw.Pinned = on; mw.PinSuspended = false; mw.TopmostTouched = true;
            N.SetWindowPos(h, on ? N.HWND_TOPMOST : N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            if (Win.IsTopmost(h) != on) { mw.Pinned = Win.IsTopmost(h) && !mw.OrigTopmost; Denied(); }
            else Osd.Flash(on ? "pinOn" : "pin", on ? "Épinglée au premier plan" : "Désépinglée");
            UpdateBorder(mw);
            Release(mw);
            Notify();
        }

        public void TogglePin(IntPtr h)
        {
            var mw = Get(h, false);
            SetPin(h, mw == null || !mw.Pinned);
        }

        public void UpdateBorder(ManagedWindow mw)
        {
            bool want = mw.Pinned && Config.S.PinBorder;
            if (want && mw.Border == null) mw.Border = new BorderOverlay(mw.Hwnd);
            if (!want && mw.Border != null) { mw.Border.Close(); mw.Border = null; }
            if (mw.Border != null) mw.Border.Sync(true);
        }

        public void RefreshBorders()
        {
            foreach (var mw in Map.Values.ToList()) { if (mw.Border != null) { mw.Border.Close(); mw.Border = null; } UpdateBorder(mw); }
        }

        // ───────────── Opacité / click-through ─────────────

        public void SetOpacity(IntPtr h, int v, bool osd = true)
        {
            var mw = Get(h, true); if (mw == null) return;
            v = Math.Max(10, Math.Min(100, v));
            if (mw.Opacity == 100 && v < 100 && !mw.HoverExplicit) mw.HoverOpaque = Config.S.HoverOpaqueDefault;
            mw.Opacity = v;
            if (v < 100) mw.LastOpacity = v;
            if (!ApplyLayered(mw)) { mw.Opacity = 100; ApplyLayered(mw); Denied(); }
            else if (osd) Osd.Flash("opacity", "Opacité " + mw.Opacity + " %");
            Release(mw);
            Notify();
        }

        public void ToggleOpacity(IntPtr h)
        {
            var mw = Get(h, false);
            if (mw != null && mw.Opacity < 100) SetOpacity(h, 100);
            else SetOpacity(h, mw != null && mw.LastOpacity > 0 ? mw.LastOpacity : Config.S.DefaultOpacity);
        }

        public void StepOpacity(IntPtr h, int delta)
        {
            var mw = Get(h, false);
            int cur = mw != null ? mw.Opacity : 100;
            SetOpacity(h, cur + delta);
        }

        public void SetHoverOpaque(IntPtr h, bool on)
        {
            var mw = Get(h, true); if (mw == null) return;
            mw.HoverOpaque = on; mw.HoverExplicit = true;
            if (!on && mw.HoverActive) { mw.HoverActive = false; ApplyLayered(mw); }
            Notify();
        }

        public void SetClickThrough(IntPtr h, bool on)
        {
            var mw = Get(h, true); if (mw == null) return;
            mw.ClickThrough = on;
            if (on && mw.Opacity == 100) { mw.Opacity = Config.S.DefaultOpacity; mw.LastOpacity = mw.Opacity; }
            if (!ApplyLayered(mw)) { mw.ClickThrough = false; ApplyLayered(mw); Denied(); }
            else
            {
                string hk = Config.S.Hotkeys.ContainsKey("clickThroughOff") ? Config.S.Hotkeys["clickThroughOff"] : "";
                Osd.Flash("click", on ? "Click-through activé" + (hk.Length > 0 ? "  (" + hk + " pour annuler)" : "") : "Click-through désactivé");
            }
            Release(mw);
            Notify();
        }

        public void ToggleClickThrough(IntPtr h)
        {
            var mw = Get(h, false);
            SetClickThrough(h, mw == null || !mw.ClickThrough);
        }

        public void ClickThroughOffAll()
        {
            int n = 0;
            foreach (var mw in Map.Values.ToList())
                if (mw.ClickThrough) { mw.ClickThrough = false; ApplyLayered(mw); Release(mw); n++; }
            Osd.Flash("click", n > 0 ? "Click-through désactivé (" + n + ")" : "Aucun click-through actif");
            Notify();
        }

        // ───────────── Flou / verre ─────────────

        public static bool SetAccent(IntPtr h, int state)
        {
            var accent = new ACCENTPOLICY { AccentState = state, AccentFlags = state == 4 ? 2 : 0, GradientColor = state == 4 ? 0x30101010 : 0 };
            int size = Marshal.SizeOf(accent);
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, p, false);
                var data = new WINCOMPATTRDATA { Attribute = 19, Data = p, SizeOfData = size };
                return N.SetWindowCompositionAttribute(h, ref data) != 0;
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        public void SetBlur(IntPtr h, bool on)
        {
            var mw = Get(h, true); if (mw == null) return;
            Capture(mw);
            bool ok = SetAccent(h, on ? 3 : 0);
            mw.Blur = on && ok;
            if (on && ok && mw.Opacity == 100) SetOpacity(h, Math.Min(Config.S.DefaultOpacity, 85), false);
            Osd.Flash("blur", on ? (ok ? "Flou derrière activé (expérimental)" : "Flou refusé par cette fenêtre") : "Flou désactivé");
            Release(mw);
            Notify();
        }

        public void ToggleBlur(IntPtr h) { var mw = Get(h, false); SetBlur(h, mw == null || !mw.Blur); }

        public void SetGlass(IntPtr h, bool on)
        {
            var mw = Get(h, true); if (mw == null) return;
            if (on)
            {
                uint key;
                if (!DetectBackground(h, out key)) { Osd.Flash("glass", "Mode verre impossible ici"); Release(mw); return; }
                mw.GlassKey = key;
            }
            mw.Glass = on;
            if (on && mw.Opacity == 100) { mw.Opacity = Config.S.GlassOpacity; mw.LastOpacity = mw.Opacity; }
            if (!ApplyLayered(mw)) { mw.Glass = false; ApplyLayered(mw); Denied(); }
            else Osd.Flash("glass", on ? "Mode verre : fond transparent (expérimental)" : "Mode verre désactivé");
            Release(mw);
            Notify();
        }

        public void ToggleGlass(IntPtr h) { var mw = Get(h, false); SetGlass(h, mw == null || !mw.Glass); }

        /// <summary>Couleur de fond dominante de la fenêtre (pixels strictement identiques).</summary>
        static bool DetectBackground(IntPtr h, out uint colorref)
        {
            colorref = 0;
            using (var bmp = Win.Capture(h, 0))
            {
                if (bmp == null) return false;
                Rectangle wr = Win.WindowRect(h), fr = Win.Frame(h);
                var area = new Rectangle(fr.X - wr.X, fr.Y - wr.Y + (int)(40 * Win.ScaleAt(fr)), fr.Width, fr.Height - (int)(40 * Win.ScaleAt(fr)));
                area.Intersect(new Rectangle(0, 0, bmp.Width, bmp.Height));
                if (area.Width < 10 || area.Height < 10) return false;
                var counts = new Dictionary<int, int>();
                int step = Math.Max(2, Math.Min(area.Width, area.Height) / 80);
                var data = bmp.LockBits(area, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    byte[] px = new byte[data.Stride * area.Height];
                    Marshal.Copy(data.Scan0, px, 0, px.Length);
                    for (int y = 0; y < area.Height; y += step)
                        for (int x = 0; x < area.Width; x += step)
                        {
                            int i = y * data.Stride + x * 4;
                            int rgb = px[i + 2] | (px[i + 1] << 8) | (px[i] << 16);
                            int c; counts.TryGetValue(rgb, out c); counts[rgb] = c + 1;
                        }
                }
                finally { bmp.UnlockBits(data); }
                if (counts.Count == 0) return false;
                var best = counts.OrderByDescending(kv => kv.Value).First();
                int total = counts.Values.Sum();
                if (best.Value < total * 0.15) return false;
                if (best.Key == 0 && best.Value == total) return false;
                colorref = (uint)best.Key;
                return true;
            }
        }

        // ───────────── Mode sombre ─────────────

        public void SetDark(IntPtr h, string filter)
        {
            var mw = Get(h, true); if (mw == null) return;
            if (string.IsNullOrEmpty(filter))
            {
                mw.Dark = null;
                if (mw.DarkOv != null) { mw.DarkOv.Close(); mw.DarkOv = null; }
                if (mw.Pip != null) mw.Pip.SyncDark();
                Osd.Flash("dark", "Mode sombre désactivé");
            }
            else
            {
                if (!DarkOverlay.Available) { Osd.Flash("dark", "Mode sombre indisponible (Magnification)"); Release(mw); return; }
                mw.Dark = filter;
                if (mw.DarkOv == null) mw.DarkOv = new DarkOverlay(h, filter, null, Config.S.DarkAutoPause);
                else mw.DarkOv.SetFilter(filter);
                if (mw.Pip != null) mw.Pip.SyncDark();
                Osd.Flash("dark", "Mode sombre : " + Filters.Label(filter));
            }
            Release(mw);
            Notify();
        }

        public void ToggleDark(IntPtr h)
        {
            var mw = Get(h, false);
            SetDark(h, mw != null && !string.IsNullOrEmpty(mw.Dark) ? null : Config.S.DarkFilter);
        }

        public bool IsHiddenSource(IntPtr h)
        {
            ManagedWindow mw;
            return Map.TryGetValue(h, out mw) && mw.HiddenByPip && !mw.TempReveal;
        }

        // ───────────── PiP / recadrage ─────────────

        public void TogglePip(IntPtr h)
        {
            var mw = Get(h, false);
            if (mw != null && mw.Pip != null) { ClosePip(mw, true); return; }
            OpenPip(h, Rectangle.Empty, !Config.S.PipShowOriginal);
        }

        public PipWindow OpenPip(IntPtr h, Rectangle cropRel, bool hideOriginal)
        {
            var mw = Get(h, true); if (mw == null) return null;
            Capture(mw);
            if (mw.Pip != null) { if (!cropRel.IsEmpty) mw.Pip.SetCrop(cropRel); return mw.Pip; }
            if (N.IsIconic(h))
            {
                if (hideOriginal) { mw.HiddenByPip = true; ApplyLayered(mw); }
                N.ShowWindow(h, N.SW_SHOWNOACTIVATE);
            }
            var pip = new PipWindow(mw, cropRel);
            mw.Pip = pip;
            if (hideOriginal)
            {
                mw.HiddenByPip = true;
                if (!ApplyLayered(mw)) { mw.HiddenByPip = false; Osd.Flash("pip", "Fenêtre protégée : l'original reste visible"); }
            }
            if (PipsHidden) { PipsHidden = false; foreach (var o in Map.Values) if (o.Pip != null && o.Pip != pip) o.Pip.Show(); }
            pip.Show();
            if (mw.DarkOv != null) mw.DarkOv.Refresh();
            Notify();
            return pip;
        }

        public void ClosePip(ManagedWindow mw, bool activate)
        {
            if (mw.Pip != null)
            {
                var p = mw.Pip;
                mw.Pip = null;
                p.CloseQuiet();
            }
            if (mw.HiddenByPip)
            {
                mw.HiddenByPip = false;
                mw.TempReveal = false;
                ApplyLayered(mw);
                if (activate && N.IsWindow(mw.Hwnd)) Win.ForceForeground(mw.Hwnd);
            }
            Release(mw);
            Notify();
        }

        public void SetSourceHidden(ManagedWindow mw, bool hidden)
        {
            if (mw.HiddenByPip == hidden) return;
            mw.HiddenByPip = hidden;
            if (!ApplyLayered(mw)) { mw.HiddenByPip = false; ApplyLayered(mw); }
            Notify();
        }

        public void SetTempReveal(ManagedWindow mw, bool reveal)
        {
            if (mw.TempReveal == reveal) return;
            mw.TempReveal = reveal;
            ApplyLayered(mw);
        }

        public void StartCrop(IntPtr h)
        {
            var mw = Get(h, true); if (mw == null) return;
            CropSelector.Start(mw, delegate (Rectangle rel)
            {
                if (rel.IsEmpty) { Release(mw); return; }
                if (mw.Pip != null) mw.Pip.SetCrop(rel);
                else OpenPip(mw.Hwnd, rel, Config.S.CropHidesOriginal);
                Osd.Flash("crop", "Zone recadrée");
            });
        }

        public void ToggleAllPips()
        {
            var pips = Map.Values.Where(m => m.Pip != null).Select(m => m.Pip).ToList();
            if (pips.Count == 0) { PipsHidden = false; Osd.Flash("pip", "Aucune miniature"); return; }
            PipsHidden = !PipsHidden;
            foreach (var p in pips) { if (PipsHidden) p.HideAll(); else p.Show(); }
            Osd.Flash(PipsHidden ? "hide" : "eye", PipsHidden ? "Miniatures cachées (" + pips.Count + ")" : "Miniatures affichées (" + pips.Count + ")");
            Notify();
        }

        // ───────────── Ancres ─────────────

        public void ToggleAnchor(IntPtr h)
        {
            var mw = Get(h, true); if (mw == null) return;
            mw.Anchor = !mw.Anchor;
            Osd.Flash("anchor", mw.Anchor ? "Ancre activée pour cette fenêtre" : "Ancre désactivée");
            Release(mw);
            Notify();
        }

        // ───────────── Réinitialisation ─────────────

        public void Reset(IntPtr h, bool osd = true)
        {
            var mw = Get(h, false);
            if (mw == null) { if (osd) Osd.Flash("reset", "Rien à réinitialiser"); return; }
            ResetCore(mw);
            if (osd) Osd.Flash("reset", "Fenêtre réinitialisée");
            Notify();
        }

        void ResetCore(ManagedWindow mw)
        {
            if (mw.Pip != null || mw.HiddenByPip) ClosePip(mw, false);
            if (mw.DarkOv != null) { mw.DarkOv.Close(); mw.DarkOv = null; }
            mw.Dark = null;
            if (mw.Blur) { SetAccent(mw.Hwnd, 0); mw.Blur = false; }
            if (mw.Pinned)
            {
                mw.Pinned = false;
                N.SetWindowPos(mw.Hwnd, mw.OrigTopmost ? N.HWND_TOPMOST : N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
            }
            if (mw.Border != null) { mw.Border.Close(); mw.Border = null; }
            mw.Opacity = 100; mw.ClickThrough = false; mw.Glass = false; mw.HoverActive = false; mw.Anchor = false;
            if (N.IsWindow(mw.Hwnd)) ApplyLayered(mw);
            Release(mw);
        }

        public void ResetAll()
        {
            foreach (var mw in Map.Values.ToList()) ResetCore(mw);
            Map.Clear();
            SaveSession();
            Osd.Flash("reset", "Tout est réinitialisé");
            Notify();
        }

        /// <summary>Retire la fenêtre de la liste si plus aucun effet n'est actif.</summary>
        void Release(ManagedWindow mw)
        {
            if (mw.HasEffects) { SaveSession(); return; }
            if (mw.Captured && N.IsWindow(mw.Hwnd) && !mw.UlwLayered)
            {
                int cur = N.GetExStyle(mw.Hwnd);
                int ex = (cur & ~(N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT)) | (mw.OrigEx & (N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT));
                if (ex != cur) { N.SetExStyle(mw.Hwnd, ex); Win.Redraw(mw.Hwnd); }
                if (mw.OrigLayered && mw.OrigLwa) N.SetLayeredWindowAttributes(mw.Hwnd, mw.OrigKey, mw.OrigAlpha, mw.OrigFlags);
            }
            Map.Remove(mw.Hwnd);
            SaveSession();
        }

        /// <summary>À la fermeture de dwaTop : tout remettre comme avant.</summary>
        public void RestoreAll()
        {
            foreach (var mw in Map.Values.ToList())
            {
                try
                {
                    if (mw.Pip != null) { var p = mw.Pip; mw.Pip = null; p.CloseQuiet(); }
                    mw.HiddenByPip = false; mw.TempReveal = false;
                    if (mw.DarkOv != null) { mw.DarkOv.Close(); mw.DarkOv = null; }
                    if (mw.Border != null) { mw.Border.Close(); mw.Border = null; }
                    if (!N.IsWindow(mw.Hwnd)) continue;
                    if (mw.Blur) SetAccent(mw.Hwnd, 0);
                    if (mw.Pinned && !mw.OrigTopmost)
                        N.SetWindowPos(mw.Hwnd, N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                    mw.Opacity = 100; mw.ClickThrough = false; mw.Glass = false; mw.HoverActive = false;
                    ApplyLayered(mw);
                    int cur = N.GetExStyle(mw.Hwnd);
                    int ex = (cur & ~(N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT)) | (mw.OrigEx & (N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT));
                    if (ex != cur) { N.SetExStyle(mw.Hwnd, ex); Win.Redraw(mw.Hwnd); }
                }
                catch (Exception ex) { Log.Error(ex); }
            }
            Map.Clear();
            try { File.Delete(SessionPath); } catch { }
        }

        // ───────────── Session (récupération après plantage) ─────────────

        void SaveSession()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var mw in Map.Values)
                {
                    if (!mw.Captured) continue;
                    sb.AppendLine(string.Join("|", new[] {
                        mw.Hwnd.ToInt64().ToString(), mw.Pid.ToString(), mw.OrigEx.ToString(), mw.OrigLayered ? "1" : "0",
                        mw.OrigLwa ? "1" : "0", mw.OrigAlpha.ToString(), mw.OrigKey.ToString(), mw.OrigFlags.ToString(),
                        mw.OrigTopmost ? "1" : "0", mw.Pinned || mw.SourceTop ? "1" : "0", mw.Blur ? "1" : "0" }));
                }
                if (sb.Length == 0) { if (File.Exists(SessionPath)) File.Delete(SessionPath); return; }
                File.WriteAllText(SessionPath, sb.ToString());
            }
            catch { }
        }

        /// <summary>Si dwaTop a planté, remet en état les fenêtres qu'il avait modifiées.</summary>
        public static int RecoverSession()
        {
            int n = 0;
            try
            {
                if (!File.Exists(SessionPath)) return 0;
                foreach (string line in File.ReadAllLines(SessionPath))
                {
                    var p = line.Split('|');
                    if (p.Length < 11) continue;
                    IntPtr h = new IntPtr(long.Parse(p[0]));
                    if (!N.IsWindow(h) || Win.Pid(h).ToString() != p[1]) continue;
                    int origEx = int.Parse(p[2]);
                    int cur = N.GetExStyle(h);
                    int ex = (cur & ~(N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT)) | (origEx & (N.WS_EX_LAYERED | N.WS_EX_TRANSPARENT));
                    if (ex != cur) { N.SetExStyle(h, ex); Win.Redraw(h); }
                    if (p[3] == "1" && p[4] == "1") N.SetLayeredWindowAttributes(h, uint.Parse(p[6]), byte.Parse(p[5]), uint.Parse(p[7]));
                    if (p[9] == "1" && p[8] == "0") N.SetWindowPos(h, N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                    if (p[10] == "1") SetAccent(h, 0);
                    n++;
                }
                File.Delete(SessionPath);
            }
            catch (Exception ex) { Log.Error(ex); }
            return n;
        }

        // ───────────── Règles par application ─────────────

        public void OnWindowShown(IntPtr h)
        {
            if (Config.S.Rules.Count == 0 || rulesApplied.Contains(h)) return;
            if (N.GetAncestor(h, N.GA_ROOT) != h) return;
            pendingRules[h] = Environment.TickCount + 600;
        }

        public void ApplyRulesToExisting()
        {
            if (Config.S.Rules.Count == 0) return;
            foreach (var h in Win.TopLevelWindows()) if (Win.IsEligible(h)) ApplyRules(h);
        }

        void ApplyRules(IntPtr h)
        {
            if (rulesApplied.Contains(h) || !Win.IsEligible(h)) return;
            string proc = Win.ProcessName(h);
            if (proc.Length == 0) return;
            string title = Win.Title(h);
            foreach (var r in Config.S.Rules)
            {
                if (!r.Enabled || !string.Equals(r.Process, proc, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(r.TitleContains) && title.IndexOf(r.TitleContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                rulesApplied.Add(h);
                bool osd = Config.S.Osd; Config.S.Osd = false;
                try
                {
                    if (r.Pin) SetPin(h, true);
                    if (r.Opacity < 100) SetOpacity(h, r.Opacity, false);
                    if (r.HoverOpaque) SetHoverOpaque(h, true);
                    if (r.ClickThrough) SetClickThrough(h, true);
                    if (!string.IsNullOrEmpty(r.Dark)) SetDark(h, r.Dark);
                    if (r.Blur) SetBlur(h, true);
                    if (r.Pip) OpenPip(h, Rectangle.Empty, !Config.S.PipShowOriginal);
                }
                finally { Config.S.Osd = osd; }
                return;
            }
        }

        public void RememberRule(IntPtr h)
        {
            var mw = Get(h, false);
            string proc = Win.ProcessName(h);
            if (proc.Length == 0) return;
            var rule = Config.S.Rules.FirstOrDefault(r => string.Equals(r.Process, proc, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(r.TitleContains));
            if (rule == null) { rule = new Rule { Process = proc }; Config.S.Rules.Add(rule); }
            rule.Enabled = true;
            rule.Pin = mw != null && mw.Pinned;
            rule.Opacity = mw != null ? mw.Opacity : 100;
            rule.HoverOpaque = mw != null && mw.HoverOpaque && mw.Opacity < 100;
            rule.ClickThrough = mw != null && mw.ClickThrough;
            rule.Dark = mw != null ? (mw.Dark ?? "") : "";
            rule.Blur = mw != null && mw.Blur;
            rule.Pip = mw != null && mw.Pip != null;
            Config.Save();
            rulesApplied.Add(h);
            Osd.Flash("save", "Mémorisé pour " + proc + " : " + rule.Describe());
        }

        // ───────────── Événements ─────────────

        public void OnDestroyed(IntPtr h)
        {
            rulesApplied.Remove(h);
            pendingRules.Remove(h);
            Mru.Remove(h);
            ManagedWindow mw;
            if (!Map.TryGetValue(h, out mw)) return;
            UI.Delay(50, delegate { if (!N.IsWindow(h)) Cleanup(mw); });
        }

        void Cleanup(ManagedWindow mw)
        {
            if (mw.Pip != null) { var p = mw.Pip; mw.Pip = null; p.CloseQuiet(); }
            if (mw.DarkOv != null) { mw.DarkOv.Close(); mw.DarkOv = null; }
            if (mw.Border != null) { mw.Border.Close(); mw.Border = null; }
            Map.Remove(mw.Hwnd);
            SaveSession();
            Notify();
        }

        public void OnForeground(IntPtr h)
        {
            if (h == IntPtr.Zero || Win.IsOurs(h)) return;
            Mru.Remove(h);
            Mru.Insert(0, h);
            if (Mru.Count > 64) Mru.RemoveAt(Mru.Count - 1);
        }

        public void OnLocation(IntPtr h)
        {
            ManagedWindow mw;
            if (!Map.TryGetValue(h, out mw)) return;
            if (mw.Border != null) mw.Border.Sync(false);
            if (mw.DarkOv != null) mw.DarkOv.Refresh();
        }

        public void OnMinimizeStart(IntPtr h)
        {
            ManagedWindow mw;
            if (!Map.TryGetValue(h, out mw)) return;
            if (mw.HiddenByPip)
                UI.Delay(250, delegate { if (N.IsWindow(h) && N.IsIconic(h) && mw.HiddenByPip) N.ShowWindow(h, N.SW_SHOWNOACTIVATE); });
        }

        void Tick()
        {
            // règles en attente
            if (pendingRules.Count > 0)
            {
                int now = Environment.TickCount;
                foreach (var kv in pendingRules.ToList())
                    if (now - kv.Value >= 0) { pendingRules.Remove(kv.Key); if (N.IsWindow(kv.Key)) ApplyRules(kv.Key); }
            }
            if (Map.Count == 0) return;
            Point cur = Win.Cursor();
            foreach (var mw in Map.Values.ToList())
            {
                if (!N.IsWindow(mw.Hwnd)) { Cleanup(mw); continue; }
                // opaque au survol
                if (mw.HoverOpaque && mw.Opacity < 100 && !mw.HiddenByPip)
                {
                    bool over = Win.Frame(mw.Hwnd).Contains(cur) && !N.IsIconic(mw.Hwnd);
                    if (over != mw.HoverActive) { mw.HoverActive = over; ApplyLayered(mw); }
                }
                else if (mw.HoverActive) { mw.HoverActive = false; ApplyLayered(mw); }
                // épinglage suspendu quand la fenêtre est agrandie
                if (mw.Pinned && Config.S.PinDisableWhenMaximized)
                {
                    bool zoomed = N.IsZoomed(mw.Hwnd);
                    if (zoomed && !mw.PinSuspended)
                    {
                        mw.PinSuspended = true;
                        N.SetWindowPos(mw.Hwnd, N.HWND_NOTOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                    }
                    else if (!zoomed && mw.PinSuspended)
                    {
                        mw.PinSuspended = false;
                        N.SetWindowPos(mw.Hwnd, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                    }
                }
                else if (mw.Pinned && !mw.PinSuspended && !Win.IsTopmost(mw.Hwnd))
                {
                    // certaines applis retirent elles-mêmes le "toujours au-dessus"
                    N.SetWindowPos(mw.Hwnd, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);
                }
                if (mw.Border != null) mw.Border.Sync(false);
            }
        }
    }
}
