using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DwaTop
{
    public class Rule
    {
        public bool Enabled = true;
        public string Process = "";
        public string TitleContains = "";
        public bool Pin;
        public int Opacity = 100;
        public bool ClickThrough;
        public bool HoverOpaque;
        public string Dark = "";
        public bool Blur;
        public bool Pip;

        public string Describe()
        {
            var parts = new List<string>();
            if (Pin) parts.Add("épinglée");
            if (Opacity < 100) parts.Add("opacité " + Opacity + " %");
            if (HoverOpaque) parts.Add("opaque au survol");
            if (ClickThrough) parts.Add("click-through");
            if (!string.IsNullOrEmpty(Dark)) parts.Add("sombre (" + Filters.Label(Dark) + ")");
            if (Blur) parts.Add("flou");
            if (Pip) parts.Add("miniature");
            return parts.Count == 0 ? "(rien)" : string.Join(", ", parts.ToArray());
        }
    }

    public class Settings
    {
        // Général
        public bool TitleButton = true;
        public bool TitleButtonHoverOnly = false;
        public bool Osd = true;
        // Épingler
        public bool PinBorder = true;
        public string PinBorderColor = "#4CC2FF";
        public int PinBorderWidth = 2;
        public int PinBorderOpacity = 90;
        public bool PinDisableWhenMaximized = false;
        // Opacité
        public int DefaultOpacity = 75;
        public bool HoverOpaqueDefault = false;
        // PiP / recadrage
        public bool PipShowOriginal = false;
        public bool PipTitleBar = true;
        public bool PipHoverPreview = true;
        public int PipHoverPreviewPercent = 55;
        public int PipDefaultWidth = 420;
        public bool PipRemember = true;
        public string PipInteractApps = "chrome.exe, msedge.exe, brave.exe, vivaldi.exe, opera.exe, firefox.exe, winword.exe, excel.exe, onenote.exe, powerpnt.exe, outlook.exe, slack.exe, spotify.exe";
        public bool CropHidesOriginal = true;
        public bool CropAutoDetect = true;
        // Mode sombre
        public string DarkFilter = "smart";
        public bool DarkAutoPause = true;
        public int DarkFps = 60;
        // Ancres
        public bool AnchorsAll = false;
        public string AnchorSide = "right";
        public int AnchorSize = 40;
        public bool AnchorEmphasisSize = true;
        public int AnchorEmphasisCount = 3;
        public double AnchorMaxScale = 1.6;
        public bool AnchorEmphasisOpacity = true;
        public int AnchorMinOpacity = 45;
        public int AnchorMax = 0;
        public bool AnchorTitleDuplicatesOnly = true;
        public string AnchorRightClick = "minimize";
        public string AnchorMiddleClick = "close";
        // Verre / flou
        public int GlassOpacity = 92;

        public Dictionary<string, string> Hotkeys = new Dictionary<string, string>();
        public List<Rule> Rules = new List<Rule>();
        public Dictionary<string, int[]> PipPlacements = new Dictionary<string, int[]>();
    }

    public static class Actions
    {
        public static readonly string[][] All = {
            new[] { "pin", "Épingler au premier plan", "Alt+Z" },
            new[] { "opacity", "Opacité (activer / désactiver)", "Alt+A" },
            new[] { "opacityUp", "Opacité +10 %", "Menu+Up" },
            new[] { "opacityDown", "Opacité -10 %", "Menu+Down" },
            new[] { "clickThrough", "Click-through (fenêtre active)", "Menu+X" },
            new[] { "clickThroughOff", "Désactiver le click-through partout", "Menu+Shift+X" },
            new[] { "pip", "Miniature PiP (fenêtre active)", "Alt+Q" },
            new[] { "hidePips", "Cacher / afficher toutes les miniatures", "Menu+Z" },
            new[] { "crop", "Recadrer une zone de la fenêtre", "Menu+C" },
            new[] { "dark", "Mode sombre", "Menu+D" },
            new[] { "blur", "Flou derrière (expérimental)", "" },
            new[] { "glass", "Mode verre (expérimental)", "" },
            new[] { "anchor", "Ancre pour la fenêtre active", "" },
            new[] { "anchorsAll", "Ancres pour toutes les fenêtres", "" },
            new[] { "panel", "Ouvrir le panneau dwaTop", "Menu+Space" },
            new[] { "reset", "Réinitialiser la fenêtre active", "Menu+Back" },
            new[] { "resetAll", "Tout réinitialiser", "" },
        };

        public static string Label(string id)
        {
            foreach (var a in All) if (a[0] == id) return a[1];
            return id;
        }
    }

    public static class Config
    {
        public static readonly string Dir = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DWATOP_DATA"))
            ? Environment.GetEnvironmentVariable("DWATOP_DATA")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dwaTop");
        public static string FilePath { get { return Path.Combine(Dir, "config.json"); } }
        public static Settings S = new Settings();

        public static void Load()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            try
            {
                if (File.Exists(FilePath))
                {
                    var js = new JavaScriptSerializer();
                    S = js.Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new Settings();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                try { File.Copy(FilePath, FilePath + ".corrompu", true); } catch { }
                S = new Settings();
            }
            if (S.Hotkeys == null) S.Hotkeys = new Dictionary<string, string>();
            if (S.Rules == null) S.Rules = new List<Rule>();
            if (S.PipPlacements == null) S.PipPlacements = new Dictionary<string, int[]>();
            foreach (var a in Actions.All)
                if (!S.Hotkeys.ContainsKey(a[0])) S.Hotkeys[a[0]] = a[2];
            S.DefaultOpacity = Math.Max(10, Math.Min(100, S.DefaultOpacity));
            if (S.DarkFps < 10) S.DarkFps = 30;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var js = new JavaScriptSerializer();
                string json = Pretty(js.Serialize(S));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        /// <summary>Indentation simple d'un JSON compact.</summary>
        public static string Pretty(string json)
        {
            var sb = new StringBuilder();
            int indent = 0; bool inStr = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; sb.Append(c); break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) { sb.Append(json[++i]); break; }
                        indent++; sb.Append('\n').Append(' ', indent * 2); break;
                    case '}':
                    case ']':
                        indent--; sb.Append('\n').Append(' ', indent * 2).Append(c); break;
                    case ',': sb.Append(c).Append('\n').Append(' ', indent * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
