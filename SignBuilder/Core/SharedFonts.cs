using Newtonsoft.Json.Linq;

namespace SignBuilderSA.Core
{
    /// <summary>
    /// Arabic fonts shipped once in Resources/Fonts and offered by every theme. They are also the fallback for
    /// Arabic letters in any other font: the renderer writes "font-family: &lt;part font&gt;, &lt;sign's Arabic
    /// font&gt;, Noto Sans Arabic", and the game's UI engine picks a font per character, so Latin letters keep the
    /// theme's road font while Arabic letters come from the Arabic one (no empty boxes).
    /// "scale" and "offset" come from each font's own metrics (cap height, hhea ascender/descender), with the same
    /// formula that gives Roadgeek 2014 its 1.75 / 0.125.
    /// </summary>
    public static class SharedFonts
    {
        public const string BaseUrl = "coui://signbuildersa/Fonts/";
        public const string DefaultArabic = "Cairo";

        private static readonly (string Family, string File, double Scale, double Offset, string Label)[] s_Arabic =
        {
            ("Cairo", "Cairo-Bold.ttf", 1.429, -0.023, "Cairo"),
            ("Cairo Black", "Cairo-Black.ttf", 1.429, -0.023, "Cairo Black"),
            ("Tajawal", "Tajawal-Bold.ttf", 1.58, 0.274, "Tajawal"),
            ("Tajawal ExtraBold", "Tajawal-ExtraBold.ttf", 1.58, 0.274, "Tajawal ExtraBold"),
            ("Amiri", "Amiri-Bold.ttf", 1.548, 0.121, "Amiri (Naskh)"),
        };

        /// <summary>Adds the shared Arabic fonts to a theme's font list (if the theme does not define them itself).</summary>
        public static void AddTo(JObject themeDef)
        {
            if (themeDef == null) return;
            if (themeDef["fonts"] is not JArray fonts)
            {
                fonts = new JArray();
                themeDef["fonts"] = fonts;
            }

            foreach (var f in s_Arabic)
            {
                var exists = false;
                foreach (var t in fonts)
                    if (t is JObject o && (string)o["family"] == f.Family) { exists = true; break; }
                if (exists) continue;
                fonts.Add(new JObject
                {
                    ["family"] = f.Family,
                    ["file"] = BaseUrl + f.File,
                    ["scale"] = f.Scale,
                    ["offset"] = f.Offset,
                    ["label"] = f.Label,
                    ["arabic"] = true,
                    // Loaded only when a sign actually uses it (a theme's style sheet otherwise fetches every font).
                    ["lazy"] = true,
                });
            }

            if (themeDef["defaults"] is not JObject defaults)
            {
                defaults = new JObject();
                themeDef["defaults"] = defaults;
            }
            if (defaults["arabicFont"] == null) defaults["arabicFont"] = DefaultArabic;
        }

        /// <summary>Families of the shared Arabic fonts, for the editor's Arabic-font chooser.</summary>
        public static JArray ArabicFamilies()
        {
            var list = new JArray();
            foreach (var f in s_Arabic) list.Add(new JObject { ["family"] = f.Family, ["label"] = f.Label });
            list.Add(new JObject { ["family"] = "Noto Sans Arabic", ["label"] = "Noto Sans Arabic (game)" });
            return list;
        }
    }
}
