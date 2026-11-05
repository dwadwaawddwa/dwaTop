using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DwaTop
{
    /// <summary>Fenêtre de paramètres (thème sombre, sections à gauche).</summary>
    public class SettingsForm : Form
    {
        static SettingsForm inst;
        readonly float s;
        readonly Panel host = new Panel();
        readonly FlowLayoutPanel nav = new FlowLayoutPanel();
        readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        readonly List<Button> navButtons = new List<Button>();
        readonly Dictionary<string, Label> hkStatus = new Dictionary<string, Label>();
        ListView rulesList;

        public static void Open(string page = null)
        {
            if (inst == null || inst.IsDisposed) inst = new SettingsForm();
            inst.Show();
            if (inst.WindowState == FormWindowState.Minimized) inst.WindowState = FormWindowState.Normal;
            inst.Activate();
            if (page != null) inst.ShowPage(page);
        }

        SettingsForm()
        {
            using (var g = CreateGraphics()) s = g.DpiX / 96f;
            AutoScaleMode = AutoScaleMode.None;
            Text = "dwaTop — paramètres";
            Icon = Icons.AppIcon(32);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(S(820), S(620));
            MinimumSize = new Size(S(640), S(420));
            var h = Handle;
            Win.SetDarkFrame(h);

            nav.Dock = DockStyle.Left;
            nav.Width = S(190);
            nav.FlowDirection = FlowDirection.TopDown;
            nav.WrapContents = false;
            nav.BackColor = Theme.Panel;
            nav.Padding = new Padding(S(8), S(12), S(8), S(8));
            host.Dock = DockStyle.Fill;
            host.BackColor = Theme.Bg;
            Controls.Add(host);
            Controls.Add(nav);

            var logo = new Label { Text = "dwaTop", Font = new Font("Segoe UI Semibold", 14f), ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(S(6), 0, 0, S(14)) };
            nav.Controls.Add(logo);

            AddPage("general", "Général", BuildGeneral());
            AddPage("pin", "Épingler", BuildPin());
            AddPage("opacity", "Opacité", BuildOpacity());
            AddPage("pip", "Miniature et recadrage", BuildPip());
            AddPage("dark", "Mode sombre", BuildDark());
            AddPage("anchors", "Ancres", BuildAnchors());
            AddPage("hotkeys", "Raccourcis", BuildHotkeys());
            AddPage("rules", "Règles par appli", BuildRules());
            AddPage("about", "À propos", BuildAbout());
            ShowPage("general");
        }

        int S(float v) { return (int)Math.Round(v * s); }

        void AddPage(string id, string label, Control page)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            host.Controls.Add(page);
            pages[id] = page;
            var b = new Button
            {
                Text = label, Tag = id, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                Width = S(172), Height = S(34), ForeColor = Theme.Text, BackColor = Theme.Panel, Margin = new Padding(0, 0, 0, S(2)),
                Padding = new Padding(S(8), 0, 0, 0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Theme.TileHover;
            b.Click += delegate { ShowPage(id); };
            navButtons.Add(b);
            nav.Controls.Add(b);
        }

        void ShowPage(string id)
        {
            foreach (var kv in pages) kv.Value.Visible = kv.Key == id;
            foreach (var b in navButtons) b.BackColor = (string)b.Tag == id ? Theme.TileOn : Theme.Panel;
            if (id == "rules") RefreshRules();
            if (id == "hotkeys") RefreshHotkeyStatus();
        }

        // ───────── helpers de construction ─────────

        FlowLayoutPanel NewPage(string title, string subtitle)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Bg, Padding = new Padding(S(22), S(16), S(16), S(16)) };
            p.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(0, 0, 0, S(2)) });
            if (!string.IsNullOrEmpty(subtitle))
                p.Controls.Add(new Label { Text = subtitle, ForeColor = Theme.Sub, AutoSize = true, MaximumSize = new Size(S(560), 0), Margin = new Padding(0, 0, 0, S(14)) });
            return p;
        }

        void Section(FlowLayoutPanel p, string text)
        {
            p.Controls.Add(new Label { Text = text.ToUpperInvariant(), ForeColor = Theme.Accent, Font = new Font("Segoe UI Semibold", 8.5f), AutoSize = true, Margin = new Padding(0, S(12), 0, S(6)) });
        }

        CheckBox Check(FlowLayoutPanel p, string text, bool value, Action<bool> set, string hint = null)
        {
            var c = new CheckBox { Text = text, Checked = value, AutoSize = true, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, S(3), 0, hint == null ? S(3) : 0), Cursor = Cursors.Hand };
            c.FlatAppearance.BorderColor = Theme.Sub;
            c.FlatAppearance.CheckedBackColor = Theme.Accent;
            c.CheckedChanged += delegate { set(c.Checked); Config.Save(); };
            p.Controls.Add(c);
            if (hint != null) Hint(p, hint);
            return c;
        }

        void Hint(FlowLayoutPanel p, string text)
        {
            p.Controls.Add(new Label { Text = text, ForeColor = Theme.Sub, AutoSize = true, MaximumSize = new Size(S(560), 0), Margin = new Padding(S(20), 0, 0, S(6)) });
        }

        FlowLayoutPanel Row(FlowLayoutPanel p, string label)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0, S(3), 0, S(3)) };
            row.Controls.Add(new Label { Text = label, ForeColor = Theme.Text, AutoSize = false, Width = S(250), Height = S(26), TextAlign = ContentAlignment.MiddleLeft });
            p.Controls.Add(row);
            return row;
        }

        void Number(FlowLayoutPanel p, string label, int min, int max, int value, Action<int> set, string unit = "")
        {
            var row = Row(p, label);
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = S(80), BackColor = Theme.Tile, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            n.ValueChanged += delegate { set((int)n.Value); Config.Save(); };
            row.Controls.Add(n);
            if (unit.Length > 0) row.Controls.Add(new Label { Text = unit, ForeColor = Theme.Sub, AutoSize = true, Margin = new Padding(S(4), S(6), 0, 0) });
        }

        void Combo(FlowLayoutPanel p, string label, string[][] options, string value, Action<string> set)
        {
            var row = Row(p, label);
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(200), BackColor = Theme.Tile, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            foreach (var o in options) c.Items.Add(o[1]);
            int idx = Array.FindIndex(options, o => o[0] == value);
            c.SelectedIndex = idx >= 0 ? idx : 0;
            c.SelectedIndexChanged += delegate { set(options[c.SelectedIndex][0]); Config.Save(); };
            row.Controls.Add(c);
        }

        Button Btn(Control parent, string text, Action click)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Theme.Tile, ForeColor = Theme.Text, AutoSize = true, Padding = new Padding(S(8), S(2), S(8), S(2)), Cursor = Cursors.Hand, Margin = new Padding(0, S(4), S(8), S(4)) };
            b.FlatAppearance.BorderColor = Theme.Line;
            b.FlatAppearance.MouseOverBackColor = Theme.TileHover;
            b.Click += delegate { try { click(); } catch (Exception ex) { Log.Error(ex); MessageBox.Show(ex.Message, "dwaTop"); } };
            parent.Controls.Add(b);
            return b;
        }

        // ───────── pages ─────────

        Control BuildGeneral()
        {
            var p = NewPage("Général", "dwaTop ajoute un petit bouton ⌄ dans la barre de titre de la fenêtre active : clique dessus pour l'épingler, la rendre transparente, la mettre en miniature, la passer en mode sombre…");
            Section(p, "Démarrage");
            Check(p, "Lancer dwaTop au démarrage de Windows", StartupEnabled(), SetStartup);
            Section(p, "Bouton dans la barre de titre");
            Check(p, "Afficher le bouton sur la fenêtre active", Config.S.TitleButton, v => { Config.S.TitleButton = v; App.RefreshButton(); });
            Check(p, "Seulement quand la souris passe sur la barre de titre", Config.S.TitleButtonHoverOnly, v => { Config.S.TitleButtonHoverOnly = v; App.RefreshButton(); });
            Hint(p, "Clic = panneau d'actions · clic molette = épingler directement. Sans bouton, utilise le raccourci « Ouvrir le panneau » (Menu+Espace par défaut).");
            Section(p, "Retour visuel");
            Check(p, "Afficher une bulle d'info en bas de l'écran après chaque action", Config.S.Osd, v => Config.S.Osd = v);
            Section(p, "Remise à zéro");
            var row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg };
            Btn(row, "Tout réinitialiser maintenant", delegate { Manager.I.ResetAll(); });
            Btn(row, "Désactiver le click-through partout", delegate { Manager.I.ClickThroughOffAll(); });
            p.Controls.Add(row);
            Hint(p, "En quittant dwaTop, toutes les fenêtres retrouvent leur état d'origine (même après un plantage, au prochain lancement).");
            return p;
        }

        Control BuildPin()
        {
            var p = NewPage("Épingler au premier plan", "La fenêtre reste au-dessus des autres, même quand tu cliques ailleurs. Raccourci par défaut : Alt+Z.");
            Section(p, "Bordure");
            Check(p, "Bordure colorée autour des fenêtres épinglées", Config.S.PinBorder, v => { Config.S.PinBorder = v; Manager.I.RefreshBorders(); });
            var row = Row(p, "Couleur");
            var sw = new Panel { Width = S(40), Height = S(24), BackColor = Theme.Parse(Config.S.PinBorderColor, Theme.Accent), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            sw.Click += delegate
            {
                using (var cd = new ColorDialog { Color = sw.BackColor, FullOpen = true })
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        sw.BackColor = cd.Color;
                        Config.S.PinBorderColor = ColorTranslator.ToHtml(cd.Color);
                        Config.Save();
                        Manager.I.RefreshBorders();
                    }
            };
            row.Controls.Add(sw);
            Number(p, "Épaisseur", 1, 10, Config.S.PinBorderWidth, v => { Config.S.PinBorderWidth = v; Manager.I.RefreshBorders(); }, "px");
            Number(p, "Opacité de la bordure", 10, 100, Config.S.PinBorderOpacity, v => { Config.S.PinBorderOpacity = v; Manager.I.RefreshBorders(); }, "%");
            Section(p, "Comportement");
            Check(p, "Suspendre l'épinglage quand la fenêtre est agrandie (plein écran)", Config.S.PinDisableWhenMaximized, v => Config.S.PinDisableWhenMaximized = v);
            return p;
        }

        Control BuildOpacity()
        {
            var p = NewPage("Opacité et click-through", "Rends une fenêtre transparente (Alt+A), puis éventuellement « click-through » : la souris passe à travers, vers la fenêtre du dessous.");
            Section(p, "Opacité");
            Number(p, "Niveau par défaut (Alt+A)", 10, 95, Config.S.DefaultOpacity, v => Config.S.DefaultOpacity = v, "%");
            Check(p, "Redevenir opaque quand la souris est au-dessus (par défaut)", Config.S.HoverOpaqueDefault, v => Config.S.HoverOpaqueDefault = v,
                "Se règle aussi fenêtre par fenêtre dans le panneau.");
            Section(p, "Click-through");
            Hint(p, "Une fenêtre click-through ne reçoit plus les clics. Pour l'annuler : son raccourci (Menu+X quand elle est active), « Désactiver le click-through partout » (Menu+Shift+X), ou le menu de l'icône dwaTop.");
            Section(p, "Verre et flou (expérimental)");
            Number(p, "Opacité du texte en mode verre", 30, 100, Config.S.GlassOpacity, v => Config.S.GlassOpacity = v, "%");
            Hint(p, "Mode verre : la couleur de fond dominante devient totalement transparente, le texte reste visible. Marche surtout avec les applis classiques (Bloc-notes, Explorateur…). Flou : demande à Windows un flou derrière la fenêtre (selon les applis).");
            return p;
        }

        Control BuildPip()
        {
            var p = NewPage("Miniature (PiP) et recadrage", "Transforme n'importe quelle fenêtre en petite vignette vivante, toujours au premier plan (Alt+Q). Menu+Z cache / réaffiche toutes les miniatures.");
            Section(p, "Miniature");
            Check(p, "Laisser la fenêtre originale visible", Config.S.PipShowOriginal, v => Config.S.PipShowOriginal = v,
                "Par défaut l'original devient invisible (il continue de tourner) et réapparaît quand tu fermes la miniature.");
            Check(p, "Barre de titre sur la miniature", Config.S.PipTitleBar, v => { Config.S.PipTitleBar = v; foreach (var m in Manager.I.Map.Values) if (m.Pip != null) m.Pip.Relayout(); });
            Check(p, "Grand aperçu au survol", Config.S.PipHoverPreview, v => Config.S.PipHoverPreview = v);
            Number(p, "Taille de l'aperçu", 25, 90, Config.S.PipHoverPreviewPercent, v => Config.S.PipHoverPreviewPercent = v, "% de l'écran");
            Number(p, "Largeur par défaut", 160, 1600, Config.S.PipDefaultWidth, v => Config.S.PipDefaultWidth = v, "px");
            Check(p, "Retenir la position / taille par application", Config.S.PipRemember, v => Config.S.PipRemember = v);
            Hint(p, "Sur la miniature : glisser = déplacer · molette = taille · Ctrl+molette = opacité · double-clic = restaurer · clic molette = fermer · clic droit = menu.");
            Section(p, "Interaction");
            var row = Row(p, "Applis en mode « interagir » d'office");
            var tb = new TextBox { Text = Config.S.PipInteractApps, Width = S(280), BackColor = Theme.Tile, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            tb.Leave += delegate { Config.S.PipInteractApps = tb.Text; Config.Save(); };
            row.Controls.Add(tb);
            Hint(p, "En mode interagir, les clics, la molette et le clavier sont transmis à la vraie fenêtre (selon les applis). Shift+Échap pour en sortir.");
            Section(p, "Recadrage");
            Check(p, "Le recadrage cache la fenêtre originale", Config.S.CropHidesOriginal, v => Config.S.CropHidesOriginal = v);
            Check(p, "Détecter automatiquement les éléments (survol + clic)", Config.S.CropAutoDetect, v => Config.S.CropAutoDetect = v);
            return p;
        }

        Control BuildDark()
        {
            var p = NewPage("Mode sombre", "Force n'importe quelle fenêtre en sombre en inversant ses couleurs à la volée (loupe Windows).");
            if (!DarkOverlay.Available) Hint(p, "⚠ L'API Magnification n'a pas pu démarrer : mode sombre indisponible sur ce PC.");
            Section(p, "Filtre");
            Combo(p, "Filtre par défaut", Filters.All, Config.S.DarkFilter, v => Config.S.DarkFilter = v);
            Hint(p, "Intelligent = inverse la luminosité en gardant les teintes (les photos restent à peu près lisibles).");
            Check(p, "Mise en veille auto si la fenêtre est déjà sombre", Config.S.DarkAutoPause, v => Config.S.DarkAutoPause = v,
                "Vérifie toutes les 2 s la luminosité de la fenêtre : un site déjà sombre n'est pas inversé.");
            Combo(p, "Fluidité", new[] { new[] { "30", "30 images/s (économe)" }, new[] { "60", "60 images/s" } }, Config.S.DarkFps.ToString(), v => Config.S.DarkFps = int.Parse(v));
            Hint(p, "Les images ne sont pas protégées de l'inversion (contrairement à WindowTop Pro).");
            return p;
        }

        Control BuildAnchors()
        {
            var p = NewPage("Ancres", "Une icône flottante apparaît pour chaque fenêtre totalement recouverte par d'autres. Clic = la ramener devant.");
            Section(p, "Activation");
            Check(p, "Ancres pour toutes les fenêtres", Config.S.AnchorsAll, v => { Config.S.AnchorsAll = v; App.Anchors.Update(); });
            Hint(p, "Sinon, active-les fenêtre par fenêtre depuis le panneau (tuile « Ancre »).");
            Section(p, "Apparence");
            Combo(p, "Position", new[] { new[] { "right", "Côté droit de la fenêtre" }, new[] { "left", "Côté gauche" }, new[] { "center", "Centre" } }, Config.S.AnchorSide, v => Config.S.AnchorSide = v);
            Number(p, "Taille des icônes", 24, 96, Config.S.AnchorSize, v => Config.S.AnchorSize = v, "px");
            Check(p, "Les plus récentes en plus gros", Config.S.AnchorEmphasisSize, v => Config.S.AnchorEmphasisSize = v);
            Number(p, "Nombre d'icônes agrandies", 1, 6, Config.S.AnchorEmphasisCount, v => Config.S.AnchorEmphasisCount = v);
            Number(p, "Agrandissement max", 100, 250, (int)(Config.S.AnchorMaxScale * 100), v => Config.S.AnchorMaxScale = v / 100.0, "%");
            Check(p, "Les plus anciennes plus transparentes", Config.S.AnchorEmphasisOpacity, v => Config.S.AnchorEmphasisOpacity = v);
            Number(p, "Opacité minimale", 10, 100, Config.S.AnchorMinOpacity, v => Config.S.AnchorMinOpacity = v, "%");
            Number(p, "Nombre max d'ancres (0 = illimité)", 0, 30, Config.S.AnchorMax, v => Config.S.AnchorMax = v);
            Check(p, "Titre seulement quand une appli a plusieurs fenêtres", Config.S.AnchorTitleDuplicatesOnly, v => Config.S.AnchorTitleDuplicatesOnly = v);
            Section(p, "Souris");
            var acts = new[] { new[] { "minimize", "Réduire la fenêtre" }, new[] { "close", "Fermer la fenêtre" }, new[] { "menu", "Ouvrir un menu" } };
            Combo(p, "Clic droit", acts, Config.S.AnchorRightClick, v => Config.S.AnchorRightClick = v);
            Combo(p, "Clic molette", acts, Config.S.AnchorMiddleClick, v => Config.S.AnchorMiddleClick = v);
            Hint(p, "Glisse un fichier sur une ancre pour amener sa fenêtre devant et l'y déposer.");
            return p;
        }

        Control BuildHotkeys()
        {
            var p = NewPage("Raccourcis clavier", "Clique dans une case puis tape la combinaison. « Menu » = la touche Menu à droite du clavier (à côté de Windows / Ctrl droit). Retour arrière = effacer.");
            foreach (var a in Actions.All)
            {
                string id = a[0];
                var row = Row(p, a[1]);
                var box = new HotkeyBox { Width = S(170), BackColor = Theme.Tile, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
                string cur;
                Config.S.Hotkeys.TryGetValue(id, out cur);
                box.Text = string.IsNullOrEmpty(cur) ? "—" : cur;
                box.Changed += delegate (string spec)
                {
                    Config.S.Hotkeys[id] = spec;
                    Config.Save();
                    App.Hotkeys.Apply(Config.S.Hotkeys);
                    RefreshHotkeyStatus();
                };
                row.Controls.Add(box);
                var st = new Label { ForeColor = Theme.Danger, AutoSize = true, Margin = new Padding(S(8), S(6), 0, 0) };
                hkStatus[id] = st;
                row.Controls.Add(st);
            }
            var r2 = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0, S(12), 0, 0) };
            Btn(r2, "Remettre les raccourcis par défaut", delegate
            {
                foreach (var a in Actions.All) Config.S.Hotkeys[a[0]] = a[2];
                Config.Save();
                App.Hotkeys.Apply(Config.S.Hotkeys);
                Close();
                Open("hotkeys");
            });
            p.Controls.Add(r2);
            Hint(p, "Depuis un autre programme (ex. LayerKeyApp) : dwaTop.exe hidePips  —  toute action de cette liste marche en argument (pin, pip, dark, crop…).");
            return p;
        }

        void RefreshHotkeyStatus()
        {
            foreach (var kv in hkStatus)
            {
                string why;
                kv.Value.Text = App.Hotkeys.Failed.TryGetValue(kv.Key, out why) ? "⚠ " + why : "";
            }
        }

        Control BuildRules()
        {
            var p = NewPage("Règles par application", "Appliquées automatiquement à chaque nouvelle fenêtre de l'appli. Pour en créer une : règle la fenêtre comme tu veux puis « Mémoriser » dans le panneau.");
            rulesList = new ListView { View = View.Details, FullRowSelect = true, Width = S(560), Height = S(300), BackColor = Theme.Tile, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, OwnerDraw = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            rulesList.Columns.Add("Appli", S(140));
            rulesList.Columns.Add("Titre contient", S(110));
            rulesList.Columns.Add("Effets", S(230));
            rulesList.Columns.Add("Active", S(60));
            rulesList.DrawColumnHeader += (o, e) =>
            {
                using (var b = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, rulesList.Font, Rectangle.Inflate(e.Bounds, -4, 0), Theme.Sub, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            rulesList.DrawItem += (o, e) => { e.DrawDefault = true; };
            rulesList.DrawSubItem += (o, e) => { e.DrawDefault = true; };
            p.Controls.Add(rulesList);
            var row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0, S(8), 0, 0) };
            Btn(row, "Activer / désactiver", delegate { foreach (var r in SelectedRules()) r.Enabled = !r.Enabled; Config.Save(); RefreshRules(); });
            Btn(row, "Modifier le filtre de titre…", delegate
            {
                var sel = SelectedRules().FirstOrDefault();
                if (sel == null) return;
                string v = Prompt("Ne s'appliquer qu'aux fenêtres dont le titre contient (vide = toutes) :", sel.TitleContains);
                if (v != null) { sel.TitleContains = v.Trim(); Config.Save(); RefreshRules(); }
            });
            Btn(row, "Supprimer", delegate { foreach (var r in SelectedRules()) Config.S.Rules.Remove(r); Config.Save(); RefreshRules(); });
            p.Controls.Add(row);
            return p;
        }

        List<Rule> SelectedRules()
        {
            var list = new List<Rule>();
            if (rulesList == null) return list;
            foreach (ListViewItem it in rulesList.SelectedItems) list.Add((Rule)it.Tag);
            return list;
        }

        void RefreshRules()
        {
            if (rulesList == null) return;
            rulesList.Items.Clear();
            foreach (var r in Config.S.Rules)
            {
                var it = new ListViewItem(new[] { r.Process, string.IsNullOrEmpty(r.TitleContains) ? "(toutes)" : r.TitleContains, r.Describe(), r.Enabled ? "oui" : "non" });
                it.Tag = r;
                it.ForeColor = r.Enabled ? Theme.Text : Theme.Sub;
                rulesList.Items.Add(it);
            }
        }

        string Prompt(string text, string value)
        {
            using (var f = new Form { Text = "dwaTop", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, BackColor = Theme.Bg, ForeColor = Theme.Text, AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(S(420), S(130)) })
            {
                var l = new Label { Text = text, Left = S(12), Top = S(12), Width = S(396), Height = S(36), ForeColor = Theme.Text };
                var t = new TextBox { Text = value, Left = S(12), Top = S(52), Width = S(396), BackColor = Theme.Tile, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = S(250), Top = S(90), Width = S(75), FlatStyle = FlatStyle.Flat, BackColor = Theme.Tile };
                var ca = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, Left = S(333), Top = S(90), Width = S(75), FlatStyle = FlatStyle.Flat, BackColor = Theme.Tile };
                f.Controls.AddRange(new Control[] { l, t, ok, ca });
                f.AcceptButton = ok; f.CancelButton = ca;
                Win.SetDarkFrame(f.Handle);
                return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
            }
        }

        Control BuildAbout()
        {
            var p = NewPage("À propos", "dwaTop " + Program.Version + " — fenêtres au premier plan, transparentes, en miniature ou en mode sombre. Inspiré de WindowTop, écrit de zéro.");
            Section(p, "Fonctions");
            Hint(p, "• Épingler (toujours au-dessus) + bordure colorée\n• Opacité, opaque au survol, click-through\n• Miniature PiP vivante : déplacer, zoomer, aperçu au survol, interagir, cacher toutes (Menu+Z)\n• Recadrer une zone (libre ou élément détecté)\n• Mode sombre avec 7 filtres + veille auto\n• Ancres pour les fenêtres recouvertes\n• Flou et mode verre (expérimental)\n• Règles automatiques par application\n• Raccourcis globaux, y compris avec la touche Menu");
            Section(p, "Fichiers");
            var row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg };
            Btn(row, "Ouvrir le dossier de configuration", delegate { Process.Start("explorer.exe", "\"" + Config.Dir + "\""); });
            Btn(row, "Ouvrir le journal", delegate
            {
                string log = System.IO.Path.Combine(Config.Dir, "dwaTop.log");
                if (System.IO.File.Exists(log)) Process.Start("notepad.exe", "\"" + log + "\"");
            });
            p.Controls.Add(row);
            return p;
        }

        // ───────── démarrage avec Windows ─────────

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static bool StartupEnabled()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("dwaTop") != null; }
            catch { return false; }
        }

        static void SetStartup(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue("dwaTop", "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue("dwaTop", false);
                }
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            App.Hotkeys.Suspended = false;
            Config.Save();
            if (inst == this) inst = null;
            base.OnFormClosed(e);
        }
    }

    /// <summary>Case de saisie d'un raccourci (gère la touche Menu comme modificateur).</summary>
    public class HotkeyBox : TextBox
    {
        public event Action<string> Changed;
        bool menuHeld;

        public HotkeyBox()
        {
            ReadOnly = true;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
        }

        protected override void OnEnter(EventArgs e) { base.OnEnter(e); App.Hotkeys.Suspended = true; BackColor = Theme.TileOn; }
        protected override void OnLeave(EventArgs e) { base.OnLeave(e); App.Hotkeys.Suspended = false; BackColor = Theme.Tile; menuHeld = false; }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x7B) return; // WM_CONTEXTMENU (la touche Menu ne doit pas ouvrir de menu ici)
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
            Keys k = e.KeyCode;
            if (k == Keys.Apps) { menuHeld = true; Text = "Menu+…"; return; }
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin) return;
            if ((k == Keys.Back || k == Keys.Delete) && !e.Control && !e.Alt && !e.Shift && !menuHeld)
            {
                Text = "—";
                if (Changed != null) Changed("");
                return;
            }
            var spec = new HotkeySpec { Ctrl = e.Control, Alt = e.Alt, Shift = e.Shift, Win = (Control.ModifierKeys & Keys.LWin) != 0, Menu = menuHeld, Key = k };
            spec.Win = (N.GetAsyncKeyState(N.VK_LWIN) & 0x8000) != 0 || (N.GetAsyncKeyState(N.VK_RWIN) & 0x8000) != 0;
            string str = spec.ToString();
            if (HotkeySpec.Parse(str) == null) { Text = "Ajoute Ctrl, Alt, Win ou Menu"; return; }
            Text = str;
            if (Changed != null) Changed(str);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Apps) { menuHeld = false; if (Text == "Menu+…") Text = "—"; }
        }
    }
}
