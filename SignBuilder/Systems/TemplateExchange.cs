using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignBuilderSA.Core;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// Template packs: all the player's own templates exported to one JSON file, and pack files imported from the
    /// templates folder's Import subfolder. A pack is a JSON array of templates or a single template; a template is a
    /// sign design (as stored in the templates folder) or { name, design } (as in the editor's template list).
    /// </summary>
    public static class TemplateExchange
    {
        public struct ExportResult
        {
            /// <summary>Templates written to the pack (0: there was nothing to export, no file written).</summary>
            public int Count;
            /// <summary>The pack's file name in <see cref="ExportDir"/>, or null.</summary>
            public string FileName;
        }

        public struct ImportResult
        {
            /// <summary>Templates added to the templates folder.</summary>
            public int Imported;
            /// <summary>Templates identical to one the player already has (not added again).</summary>
            public int Skipped;
            /// <summary>Templates (or whole files) that could not be read.</summary>
            public int Failed;
            /// <summary>Pack files processed.</summary>
            public int Files;
        }

        /// <summary>
        /// Exported packs. A subfolder, because every *.json directly in the templates folder is read as one template
        /// (a pack there would be skipped with a warning on every library refresh).
        /// </summary>
        public static string ExportDir => Path.Combine(SignBuilderUISystem.UserTemplatesDir, "Export");

        /// <summary>Where the player puts pack files to import.</summary>
        public static string ImportDir => Path.Combine(SignBuilderUISystem.UserTemplatesDir, "Import");

        /// <summary>Imported pack files are moved here.</summary>
        public static string DoneDir => Path.Combine(ImportDir, "Done");

        /// <summary>Pack files that could not be read at all are moved here.</summary>
        public static string FailedDir => Path.Combine(ImportDir, "Failed");

        /// <summary>Writes every player template into SignBuilderSA-templates-YYYYMMDD-HHMM.json in <see cref="ExportDir"/>.</summary>
        public static ExportResult ExportAll()
        {
            var pack = new JArray();
            foreach (var design in UserTemplates()) pack.Add(design.Value);
            if (pack.Count == 0) return new ExportResult { Count = 0, FileName = null };

            Directory.CreateDirectory(ExportDir);
            var stem = "SignBuilderSA-templates-" + DateTime.Now.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
            var path = Path.Combine(ExportDir, stem + ".json");
            for (var n = 2; File.Exists(path); n++) path = Path.Combine(ExportDir, $"{stem}-{n}.json");
            File.WriteAllText(path, pack.ToString(Formatting.Indented));
            Mod.log.Info($"Exported {pack.Count} templates to {path}");
            return new ExportResult { Count = pack.Count, FileName = Path.GetFileName(path) };
        }

        /// <summary>
        /// Imports every *.json pack in <see cref="ImportDir"/> as new player templates (never overwriting a file), then
        /// moves each pack to Import/Done (or Import/Failed when it is not readable JSON).
        /// </summary>
        public static ImportResult ImportAll()
        {
            Directory.CreateDirectory(ImportDir);
            var result = new ImportResult();
            var files = Directory.GetFiles(ImportDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) return result;

            Directory.CreateDirectory(SignBuilderUISystem.UserTemplatesDir);
            // Existing templates, to skip exact copies (re-importing one's own export, or the same pack twice).
            var existing = UserTemplates().Select(t => Normalized(t.Value)).ToList();

            foreach (var file in files)
            {
                result.Files++;
                JToken root;
                try
                {
                    root = JToken.Parse(File.ReadAllText(file));
                }
                catch (Exception e)
                {
                    Mod.log.Warn($"Template pack {file} is not readable JSON: {e.Message}");
                    result.Failed++;
                    MoveTo(file, FailedDir);
                    continue;
                }

                foreach (var entry in Entries(root))
                {
                    var design = DesignOf(entry, out var name);
                    if (design == null || !Formats.ReadDesign(design, file))
                    {
                        result.Failed++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(file);
                    design["name"] = name;
                    var normalized = Normalized(design);
                    if (existing.Any(e => JToken.DeepEquals(e, normalized)))
                    {
                        result.Skipped++;
                        continue;
                    }
                    var path = NewTemplatePath(name);
                    File.WriteAllText(path, design.ToString(Formatting.Indented));
                    existing.Add(normalized);
                    result.Imported++;
                }
                MoveTo(file, DoneDir);
            }
            Mod.log.Info($"Imported {result.Imported} templates from {result.Files} pack files ({result.Skipped} already present, {result.Failed} unreadable)");
            return result;
        }

        /// <summary>The player's own template designs (file path, design), as stored in the templates folder.</summary>
        private static IEnumerable<KeyValuePair<string, JObject>> UserTemplates()
        {
            var dir = SignBuilderUISystem.UserTemplatesDir;
            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                JObject design = null;
                try
                {
                    design = JToken.Parse(File.ReadAllText(file)) as JObject;
                }
                catch (Exception e)
                {
                    Mod.log.Warn($"Skipping template {file}: {e.Message}");
                }
                if (design == null || !(design["items"] is JArray)) continue;
                if (design["name"] == null) design["name"] = Path.GetFileNameWithoutExtension(file);
                yield return new KeyValuePair<string, JObject>(file, design);
            }
        }

        /// <summary>The templates in a pack: an array's elements, { templates: [...] }, or the one template it is.</summary>
        private static IEnumerable<JToken> Entries(JToken root)
        {
            if (root is JArray array) return array;
            if (root is JObject obj && obj["templates"] is JArray list && obj["items"] == null) return list;
            return new[] { root };
        }

        /// <summary>A copy of the design in a pack entry (a design, or { name, design }), and its name; null if neither.</summary>
        private static JObject DesignOf(JToken entry, out string name)
        {
            name = null;
            if (!(entry is JObject obj)) return null;
            JObject design;
            if (obj["design"] is JObject wrapped)
            {
                design = (JObject)wrapped.DeepClone();
                name = (string)obj["name"];
            }
            else design = (JObject)obj.DeepClone();
            if (!(design["items"] is JArray)) return null;
            // A template's id belongs to the PC it was saved on; the imported copy gets its own (its new file name).
            design.Remove("id");
            if (string.IsNullOrWhiteSpace(name)) name = design["name"]?.Type == JTokenType.String ? (string)design["name"] : null;
            return design;
        }

        /// <summary>The design as compared for duplicates: format version written out (a missing one is format 1).</summary>
        private static JObject Normalized(JObject design)
        {
            var copy = (JObject)design.DeepClone();
            copy.Remove("id");
            return Formats.Versioned(copy);
        }

        /// <summary>A new file in the templates folder named after the template (as the editor names saved templates).</summary>
        private static string NewTemplatePath(string name)
        {
            var dir = SignBuilderUISystem.UserTemplatesDir;
            var slug = Slug(name);
            var path = Path.Combine(dir, slug + ".json");
            for (var n = 2; File.Exists(path); n++) path = Path.Combine(dir, $"{slug}-{n}.json");
            return path;
        }

        private static string Slug(string name)
        {
            var chars = (name ?? "sign").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            var slug = new string(chars).Trim('-');
            while (slug.Contains("--")) slug = slug.Replace("--", "-");
            return slug.Length == 0 ? "sign" : slug.Length > 60 ? slug.Substring(0, 60) : slug;
        }

        /// <summary>Moves a processed pack file into <paramref name="dir"/>, renaming it rather than replacing another.</summary>
        private static void MoveTo(string file, string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var stem = Path.GetFileNameWithoutExtension(file);
                var ext = Path.GetExtension(file);
                var target = Path.Combine(dir, stem + ext);
                for (var n = 2; File.Exists(target); n++) target = Path.Combine(dir, $"{stem}-{n}{ext}");
                File.Move(file, target);
            }
            catch (Exception e)
            {
                Mod.log.Warn($"Could not move {file} to {dir}: {e.Message}");
            }
        }
    }
}
