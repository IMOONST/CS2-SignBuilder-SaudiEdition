using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SignBuilder.Core
{
    /// <summary>
    /// Symbol libraries: arrows (by category, each shape with the turns and mirrors it allows), road diagrams'
    /// arrowhead and symbols, shared by the themes that list them ("libraries": ["mutcd"], or "mutcd/overhead" for one
    /// arrow category). Built-in ones ship in Resources/Symbols; mods carry theirs in a SignBuilderSymbols folder and
    /// players add their own in ModsData/SignBuilder/Symbols (one folder per library: library.json and its SVG files).
    /// A library with the id of one loaded before replaces it (logged). "basic" holds the arrows of Sign Builder 1.0
    /// and is every theme's last resort.
    /// </summary>
    public static class SymbolLibraries
    {
        public const string Basic = "basic";
        public const string ModFolder = "SignBuilderSymbols";

        public static string BuiltInDir => Path.Combine(SignBuilderRuntime.ResourcesDir, "Symbols");
        public static string UserDir => Path.Combine(SignBuilderRuntime.DataDir, "Symbols");

        private static Dictionary<string, JObject> s_Libraries;

        public static IReadOnlyDictionary<string, JObject> All
        {
            get
            {
                if (s_Libraries == null) Reload();
                return s_Libraries;
            }
        }

        public static void Reload()
        {
            s_Libraries = new Dictionary<string, JObject>();
            Load(BuiltInDir);
            foreach (var dir in Themes.ModFolders(ModFolder)) Load(dir);
            Load(UserDir);
        }

        /// <summary>Every library for the editor: { id: library }.</summary>
        public static string Json() => new JObject(All.Select(l => new JProperty(l.Key, l.Value))).ToString(Formatting.None);

        /// <summary>
        /// The libraries a design needs to be drawn: its theme's, the basic one, and those its symbols and borrowed
        /// arrows ("library") come from ({ id: library }).
        /// </summary>
        public static JObject ForDesign(JObject design, JObject theme)
        {
            var ids = new List<string>();
            foreach (var entry in (theme?["libraries"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>()) ids.Add(entry.Split('/')[0]);
            ids.Add(Basic);
            foreach (var token in design.SelectTokens("$..symbol").Concat(design.SelectTokens("$..library")))
            {
                var reference = token.Type == JTokenType.String ? (string)token : null;
                if (!string.IsNullOrEmpty(reference)) ids.Add(reference.Split('/')[0]);
            }
            var result = new JObject();
            foreach (var id in ids.Distinct())
            {
                if (All.TryGetValue(id, out var library)) result[id] = library;
            }
            return result;
        }

        private static void Load(string root)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d))
            {
                var file = Path.Combine(dir, "library.json");
                if (!File.Exists(file)) continue;
                try
                {
                    var library = JObject.Parse(File.ReadAllText(file));
                    if (!Formats.ReadLibrary(library, dir)) continue;
                    var id = (string)library["id"] ?? Path.GetFileName(dir);
                    library["id"] = id;
                    var legend = Colors(library["legend"]);
                    if (library["symbols"] is JObject symbols)
                    {
                        foreach (var symbol in symbols.Properties().ToList())
                        {
                            if (!(symbol.Value is JObject entry) || !ResolveFile(entry, dir, id + "/" + symbol.Name, legend)) symbol.Remove();
                        }
                    }
                    if (s_Libraries.ContainsKey(id)) Mod.log.Warn($"Symbol library {dir} replaces the library \"{id}\" loaded before it");
                    s_Libraries[id] = library;
                }
                catch (Exception e)
                {
                    Mod.log.Warn($"Skipping symbol library {dir}: {e.Message}");
                }
            }
        }

        /// <summary>Colors listed in a "legend" entry, normalized (see <see cref="SvgShapes.Normalize"/>).</summary>
        private static HashSet<string> Colors(JToken token) =>
            new HashSet<string>((token as JArray)?.Values<string>().Select(SvgShapes.Normalize) ?? Enumerable.Empty<string>());

        /// <summary>
        /// A symbol's SVG file read into what the renderer draws: its view box and filled parts. Fills in the legend
        /// colors (the symbol's "legend", else its library's) take the sign's legend color.
        /// </summary>
        private static bool ResolveFile(JObject entry, string dir, string name, HashSet<string> legend)
        {
            var file = (string)entry["file"];
            if (string.IsNullOrEmpty(file)) return entry["parts"] is JArray;
            try
            {
                var svg = SvgShapes.Read(Path.Combine(dir, file), entry["legend"] != null ? Colors(entry["legend"]) : legend);
                entry.Remove("file");
                entry["viewBox"] = svg.viewBox;
                entry["parts"] = svg.parts;
                return true;
            }
            catch (Exception e)
            {
                Mod.log.Warn($"Symbol {name}: {file} could not be read: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// The filled shapes of a simple SVG file (as exported from a sign manual): paths, rectangles, circles, ellipses and
    /// polygons, with their groups' transforms and fills. Fills in <c>legend</c> become the legend color
    /// ("currentColor"); other colors are kept.
    /// </summary>
    public static class SvgShapes
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static (JArray viewBox, JArray parts) Read(string path, HashSet<string> legend)
        {
            var root = XDocument.Load(path).Root ?? throw new InvalidDataException("empty file");
            var box = Numbers((string)root.Attribute("viewBox"));
            if (box.Length != 4) box = new[] { 0, 0, Number((string)root.Attribute("width")), Number((string)root.Attribute("height")) };
            var parts = new JArray();
            Walk(root, Identity, Style(root, "fill") ?? "#000000", Style(root, "fill-rule") ?? "nonzero", legend, parts);
            return (new JArray(box.Select(v => (object)Round(v)).ToArray()), parts);
        }

        private static readonly double[] Identity = { 1, 0, 0, 1, 0, 0 };

        private static void Walk(XElement element, double[] matrix, string fill, string rule, HashSet<string> legend, JArray parts)
        {
            foreach (var child in element.Elements())
            {
                var m = Multiply(matrix, Transform((string)child.Attribute("transform")));
                var f = Style(child, "fill") ?? fill;
                var r = Style(child, "fill-rule") ?? rule;
                var d = PathOf(child);
                if (child.Name.LocalName == "g" || child.Name.LocalName == "svg") Walk(child, m, f, r, legend, parts);
                else if (d != null && f != "none")
                {
                    var part = new JObject { ["d"] = d, ["fill"] = legend.Contains(Normalize(f)) ? "currentColor" : f };
                    if (!m.SequenceEqual(Identity)) part["transform"] = "matrix(" + string.Join(" ", m.Select(v => Round(v).ToString(Inv))) + ")";
                    if (r == "evenodd") part["rule"] = "evenodd";
                    parts.Add(part);
                }
            }
        }

        private static string PathOf(XElement e)
        {
            double A(string n) => Number((string)e.Attribute(n));
            switch (e.Name.LocalName)
            {
                case "path": return (string)e.Attribute("d");
                case "rect":
                    return $"M{F(A("x"))} {F(A("y"))}H{F(A("x") + A("width"))}V{F(A("y") + A("height"))}H{F(A("x"))}Z";
                case "circle":
                case "ellipse":
                    var rx = e.Name.LocalName == "circle" ? A("r") : A("rx");
                    var ry = e.Name.LocalName == "circle" ? A("r") : A("ry");
                    return $"M{F(A("cx") - rx)} {F(A("cy"))}A{F(rx)} {F(ry)} 0 1 0 {F(A("cx") + rx)} {F(A("cy"))}A{F(rx)} {F(ry)} 0 1 0 {F(A("cx") - rx)} {F(A("cy"))}Z";
                case "polygon":
                    var p = Numbers((string)e.Attribute("points"));
                    return p.Length < 4 ? null : "M" + string.Join("L", Enumerable.Range(0, p.Length / 2).Select(i => F(p[2 * i]) + " " + F(p[2 * i + 1]))) + "Z";
                default: return null;
            }
        }

        /// <summary>A presentation property from the element's style attribute, or the attribute itself.</summary>
        private static string Style(XElement e, string name)
        {
            var style = (string)e.Attribute("style");
            if (style != null)
            {
                foreach (var decl in style.Split(';'))
                {
                    var i = decl.IndexOf(':');
                    if (i > 0 && decl.Substring(0, i).Trim() == name) return decl.Substring(i + 1).Trim();
                }
            }
            return (string)e.Attribute(name);
        }

        /// <summary>A color as "#rrggbb" (from #rgb, rgb(r, g, b), white or black), so fills and lists compare.</summary>
        public static string Normalize(string color)
        {
            var c = (color ?? "").Replace(" ", "").ToLowerInvariant();
            if (c == "white") return "#ffffff";
            if (c == "black") return "#000000";
            if (Regex.IsMatch(c, "^#[0-9a-f]{3}$")) return "#" + string.Concat(c.Substring(1).Select(ch => new string(ch, 2)));
            var rgb = Regex.Match(c, @"^rgb\((\d+),(\d+),(\d+)\)$");
            if (rgb.Success) return "#" + string.Concat(Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value).ToString("x2")));
            return c;
        }

        private static double[] Transform(string t)
        {
            var m = Identity;
            if (string.IsNullOrEmpty(t)) return m;
            foreach (Match f in Regex.Matches(t, @"(matrix|translate|scale|rotate)\s*\(([^)]*)\)"))
            {
                var v = Numbers(f.Groups[2].Value);
                double[] next;
                switch (f.Groups[1].Value)
                {
                    case "matrix": next = v.Length == 6 ? v : Identity; break;
                    case "translate": next = new[] { 1, 0, 0, 1, v.ElementAtOrDefault(0), v.ElementAtOrDefault(1) }; break;
                    case "scale": next = new[] { v.ElementAtOrDefault(0), 0, 0, v.Length > 1 ? v[1] : v.ElementAtOrDefault(0), 0, 0 }; break;
                    default:
                        var a = v.ElementAtOrDefault(0) * Math.PI / 180;
                        double cx = v.ElementAtOrDefault(1), cy = v.ElementAtOrDefault(2), cos = Math.Cos(a), sin = Math.Sin(a);
                        next = new[] { cos, sin, -sin, cos, cx - cos * cx + sin * cy, cy - sin * cx - cos * cy };
                        break;
                }
                m = Multiply(m, next);
            }
            return m;
        }

        /// <summary>Applies <paramref name="b"/> first, then <paramref name="a"/> (SVG matrices a b c d e f).</summary>
        private static double[] Multiply(double[] a, double[] b) => new[]
        {
            a[0] * b[0] + a[2] * b[1], a[1] * b[0] + a[3] * b[1],
            a[0] * b[2] + a[2] * b[3], a[1] * b[2] + a[3] * b[3],
            a[0] * b[4] + a[2] * b[5] + a[4], a[1] * b[4] + a[3] * b[5] + a[5],
        };

        private static double[] Numbers(string s) => s == null ? new double[0]
            : Regex.Matches(s, @"-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?").Cast<Match>().Select(m => double.Parse(m.Value, Inv)).ToArray();

        private static double Number(string s) => Numbers(s?.Replace("px", "")).FirstOrDefault();

        private static double Round(double v) => Math.Round(v, 4);

        private static string F(double v) => Round(v).ToString(Inv);
    }
}
