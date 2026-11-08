using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SignBuilderSA.Core
{
    /// <summary>A sign theme: one country's (or style's) sign system, from a folder with theme.json.</summary>
    public sealed class ThemeInfo
    {
        public string Id;
        public string Name;
        public bool BuiltIn;
        /// <summary>Name of the installed mod (Paradox Mods or local) the theme came from, if any.</summary>
        public string FromMod;
        /// <summary>Folder on disk, and the same folder as a URL the renderer can load fonts and images from.</summary>
        public string Dir;
        public string BaseUrl;
        /// <summary>theme.json with file references resolved (optional fonts the player has not supplied removed).</summary>
        public JObject Def;
    }

    /// <summary>
    /// Sign themes. Built-in ones ship in Resources/Themes; players add their own in ModsData/SignBuilderSA/Themes
    /// (one folder per theme: theme.json, fonts/, shields/, templates/). A player folder with the id of a built-in
    /// theme and no theme.json only supplies files for it, such as fonts that cannot ship with the mod.
    /// Any installed mod (from Paradox Mods or the local Mods folder) can carry themes too, in a SignBuilderSAThemes
    /// folder (or the original Sign Builder's SignBuilderThemes folder) laid out the same way: subscribing to a theme pack is all a player needs to do.
    /// </summary>
    public static class Themes
    {
        public const string DataHost = "signbuildersadata";
        public const string DefaultId = "us";

        public static string BuiltInDir => Path.Combine(SignBuilderRuntime.ResourcesDir, "Themes");
        public static string UserThemesDir => Path.Combine(SignBuilderRuntime.DataDir, "Themes");

        private static Dictionary<string, ThemeInfo> s_Themes;

        public static IReadOnlyCollection<ThemeInfo> All
        {
            get
            {
                if (s_Themes == null) Reload();
                return s_Themes.Values;
            }
        }

        public static ThemeInfo Get(string id)
        {
            if (s_Themes == null) Reload();
            return s_Themes.TryGetValue(id ?? DefaultId, out var t) ? t : s_Themes.TryGetValue(DefaultId, out var d) ? d : s_Themes.Values.FirstOrDefault();
        }

        public static void Reload()
        {
            s_Themes = new Dictionary<string, ThemeInfo>();
            Load(BuiltInDir, true, $"coui://signbuildersa/Themes/");
            // Mod themes before the player's own, so a local folder can override a published theme while editing it.
            s_ModHosts = ModFolders(ModFolderNames).Select((dir, i) => (host: $"signbuildersamod{i}", dir)).ToList();
            foreach (var (host, dir) in s_ModHosts) Load(dir, false, $"coui://{host}/", ModName(dir));
            Load(UserThemesDir, false, $"coui://{DataHost}/Themes/");
            foreach (var theme in s_Themes.Values) BorrowFonts(theme);
            // After file resolution: shared fonts are coui URLs, not files in the theme's folder.
            foreach (var theme in s_Themes.Values) SharedFonts.AddTo(theme.Def);
        }

        /// <summary>
        /// Font files written "@&lt;theme&gt;/path" come from another theme's folder (Canada uses the US fonts), so they
        /// ship once.
        /// </summary>
        private static void BorrowFonts(ThemeInfo theme)
        {
            if (theme.Def["fonts"] is not JArray fonts) return;
            foreach (var font in fonts.OfType<JObject>())
            {
                var file = (string)font["file"];
                if (string.IsNullOrEmpty(file) || !file.StartsWith("@")) continue;
                var slash = file.IndexOf('/');
                var owner = slash > 1 ? file.Substring(1, slash - 1) : null;
                if (owner != null && s_Themes.TryGetValue(owner, out var from) && File.Exists(Path.Combine(from.Dir, file.Substring(slash + 1))))
                {
                    font["file"] = from.BaseUrl + file.Substring(slash + 1);
                }
                else
                {
                    Mod.log.Warn($"Theme {theme.Id}: font {file} not found");
                    font.Remove();
                }
            }
        }

        /// <summary>
        /// Folder names in a mod that hold themes: this edition's, then the original Sign Builder's, so theme packs
        /// made for Sign Builder keep working.
        /// </summary>
        public static readonly string[] ModFolderNames = { "SignBuilderSAThemes", "SignBuilderThemes" };

        private static List<(string host, string dir)> s_ModHosts = new();

        /// <summary>
        /// coui hosts the renderer loads mod themes' fonts and artwork from (host name, folder). Registered with the
        /// game's UI system wherever signs are drawn (editor preview and the off-screen baker).
        /// </summary>
        public static IReadOnlyList<(string host, string dir)> ModHosts
        {
            get
            {
                if (s_Themes == null) Reload();
                return s_ModHosts;
            }
        }

        /// <summary>Folders named any of <paramref name="names"/> in installed mods: subscribed Paradox Mods and local mods.</summary>
        public static IEnumerable<string> ModFolders(params string[] names)
        {
            var userRoot = UnityEngine.Application.persistentDataPath;
            foreach (var mods in new[] { Path.Combine(userRoot, ".cache", "Mods", "pdx_mods"), Path.Combine(userRoot, "Mods") })
            {
                if (!Directory.Exists(mods)) continue;
                string[] dirs;
                try { dirs = Directory.GetDirectories(mods); } catch { continue; }
                foreach (var mod in dirs.OrderBy(d => d))
                {
                    foreach (var name in names)
                    {
                        var folder = Path.Combine(mod, name);
                        if (Directory.Exists(folder)) yield return folder;
                    }
                }
            }
        }

        private static string ModName(string themesDir) => Path.GetFileName(Path.GetDirectoryName(themesDir));

        private static void Load(string root, bool builtIn, string urlRoot, string fromMod = null)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d))
            {
                var file = Path.Combine(dir, "theme.json");
                if (!File.Exists(file)) continue;
                try
                {
                    var def = JObject.Parse(File.ReadAllText(file));
                    if (!Formats.ReadTheme(def, dir)) continue;
                    var id = (string)def["id"] ?? Path.GetFileName(dir);
                    def["id"] = id;
                    var theme = new ThemeInfo
                    {
                        Id = id, Name = (string)def["name"] ?? id, BuiltIn = builtIn, Dir = dir, FromMod = fromMod,
                        BaseUrl = urlRoot + Uri.EscapeDataString(Path.GetFileName(dir)) + "/", Def = def,
                    };
                    ResolveFonts(theme);
                    s_Themes[id] = theme;
                }
                catch (Exception e)
                {
                    Mod.log.Warn($"Skipping theme {dir}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Font files are looked up in the theme's folder, then in the player's folder for that theme id. Optional
        /// fonts (licensed faces players add themselves) are dropped when neither has the file.
        /// </summary>
        private static void ResolveFonts(ThemeInfo theme)
        {
            if (theme.Def["fonts"] is not JArray fonts) return;
            var extras = Path.Combine(UserThemesDir, theme.Id);
            foreach (var font in fonts.OfType<JObject>().ToList())
            {
                var file = (string)font["file"];
                // No file: a family the game provides. "@theme/...": borrowed from another theme (BorrowFonts).
                if (string.IsNullOrEmpty(file) || file.StartsWith("@")) continue;
                if (File.Exists(Path.Combine(theme.Dir, file))) continue;
                if (File.Exists(Path.Combine(extras, file)))
                {
                    font["file"] = $"coui://{DataHost}/Themes/{Uri.EscapeDataString(theme.Id)}/{file.Replace('\\', '/')}";
                    continue;
                }
                if ((bool?)font["optional"] == true) font.Remove();
                else Mod.log.Warn($"Theme {theme.Id}: font file {file} not found");
            }
        }

        /// <summary>
        /// The design with its theme and the symbol libraries it draws from attached ("_theme", "_themeBase",
        /// "_libraries"), as the renderer needs it to bake.
        /// </summary>
        public static JObject ForRender(JObject design)
        {
            // The format version picks the arrows: a design made with 1.0 keeps 1.0's.
            var copy = Formats.Versioned((JObject)design.DeepClone());
            var theme = Get((string)design["theme"]);
            if (theme == null) return copy;
            copy["theme"] = theme.Id;
            copy["_theme"] = theme.Def;
            copy["_themeBase"] = theme.BaseUrl;
            copy["_libraries"] = SymbolLibraries.ForDesign(design, theme.Def);
            return copy;
        }

        /// <summary>All themes for the editor: [{ id, name, builtIn, base, def }].</summary>
        public static string Json() => new JArray(All.Select(t => new JObject
        {
            ["id"] = t.Id, ["name"] = t.Name, ["builtIn"] = t.BuiltIn, ["fromMod"] = t.FromMod, ["base"] = t.BaseUrl, ["def"] = t.Def,
        })).ToString(Formatting.None);
    }
}
