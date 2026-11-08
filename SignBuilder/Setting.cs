using System.Collections.Generic;
using System.IO;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;

namespace SignBuilderSA
{
    /// <summary>Sign Builder's page in the game's Options.</summary>
    [FileLocation(nameof(SignBuilderSA))]
    [SettingsUIGroupOrder(kLanguageGroup, kKeysGroup, kMaintenanceGroup, kFoldersGroup, kAboutGroup)]
    [SettingsUIShowGroupName(kLanguageGroup, kKeysGroup, kMaintenanceGroup, kFoldersGroup, kAboutGroup)]
    [SettingsUIKeyboardAction(kToggleAction)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kLanguageGroup = "Language";
        public const string kKeysGroup = "Keys";
        public const string kMaintenanceGroup = "Maintenance";
        public const string kFoldersGroup = "Folders";
        public const string kAboutGroup = "About";
        public const string kToggleAction = "ToggleSignBuilderSA";

        /// <summary>The interface language of Sign Builder (its editor and this options page).</summary>
        public enum InterfaceLanguage
        {
            /// <summary>Arabic when the game is in Arabic, otherwise English.</summary>
            Auto = 0,
            Arabic = 1,
            English = 2,
        }

        public Setting(IMod mod) : base(mod)
        {
        }

        /// <summary>Sign Builder's interface language; applied at once, without restarting the game.</summary>
        [SettingsUISection(kSection, kLanguageGroup)]
        public InterfaceLanguage Language { get; set; } = InterfaceLanguage.Auto;

        /// <summary>Opens or closes Sign Builder, like its toolbar button. No key by default.</summary>
        [SettingsUIKeyboardBinding(kToggleAction)]
        [SettingsUISection(kSection, kKeysGroup)]
        public ProxyBinding ToggleBinding { get; set; }

        /// <summary>Deletes sign structures not used in the open city (their library entries; meshes and materials at the next start).</summary>
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(kSection, kMaintenanceGroup)]
        public bool CleanUpUnused
        {
            set => Assemblies.AssemblyCleanup.RemoveUnusedInCity();
        }

        [SettingsUIButton]
        [SettingsUISection(kSection, kFoldersGroup)]
        public bool OpenTemplatesFolder
        {
            set => OpenFolder(Systems.SignBuilderUISystem.UserTemplatesDir);
        }

        [SettingsUIButton]
        [SettingsUISection(kSection, kFoldersGroup)]
        public bool OpenThemesFolder
        {
            set => OpenFolder(Core.Themes.UserThemesDir);
        }

        /// <summary>The installed Sign Builder version (the ModVersion in PublishConfiguration.xml).</summary>
        [SettingsUISection(kSection, kAboutGroup)]
        public string Version => typeof(Setting).Assembly.GetName().Version.ToString(3);

        public static void OpenFolder(string dir)
        {
            Directory.CreateDirectory(dir);
            UnityEngine.Application.OpenURL("file:///" + dir.Replace('\\', '/'));
        }

        public override void SetDefaults()
        {
            Language = InterfaceLanguage.Auto;
        }
    }

    /// <summary>The options page in Arabic.</summary>
    public class LocaleAR : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleAR(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "صانع اللوحات: النسخة السعودية" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "الرئيسية" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLanguageGroup), "اللغة" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kKeysGroup), "اختصارات لوحة المفاتيح" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMaintenanceGroup), "الصيانة" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kFoldersGroup), "المجلدات" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutGroup), "حول" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Language)), "لغة الواجهة" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Language)),
                    "لغة صانع اللوحات (المحرر وهذه الصفحة). «تلقائي» يتبع لغة اللعبة: العربية إذا كانت اللعبة بالعربية، وإلا الإنجليزية. يُطبَّق التغيير فوراً." },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.Auto), "تلقائي" },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.Arabic), "العربية" },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.English), "English" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleBinding)), "فتح أو إغلاق صانع اللوحات" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleBinding)), "يفتح صانع اللوحات أو يغلقه، مثل زره في شريط الأدوات." },
                { m_Setting.GetBindingKeyLocaleID(Setting.kToggleAction), "فتح أو إغلاق صانع اللوحات" },
                { m_Setting.GetBindingMapLocaleID(), "صانع اللوحات: النسخة السعودية" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanUpUnused)), "حذف هياكل اللوحات غير المستخدمة" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CleanUpUnused)),
                    "يحذف المجسمات والخامات المولدة لهياكل اللوحات غير الموضوعة في المدينة المفتوحة (تُزال ملفاتها عند تشغيل اللعبة القادم). " +
                    "المدن الأخرى تحتفظ بلوحاتها: يُعاد بناؤها من الحفظ عند تحميل تلك المدينة." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.CleanUpUnused)),
                    "حذف كل هيكل لوحة غير مستخدم في هذه المدينة؟ المدن المحفوظة الأخرى تعيد بناء لوحاتها عند تحميلها." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenTemplatesFolder)), "فتح مجلد القوالب" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenTemplatesFolder)), "قوالب لوحاتك المحفوظة، ملف JSON لكل قالب. شاركها بنسخ الملفات." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenThemesFolder)), "فتح مجلد الأنماط" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenThemesFolder)), "أنماط اللوحات (دول أو أساليب)، مجلد لكل نمط فيه theme.json والخطوط والشعارات والقوالب." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Version)), "الإصدار" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Version)), "الإصدار المثبت من صانع اللوحات." },
            };
        }

        public void Unload()
        {
        }
    }

    /// <summary>The options page in English.</summary>
    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Sign Builder: Saudi Edition" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kLanguageGroup), "Language" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kKeysGroup), "Key bindings" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kMaintenanceGroup), "Maintenance" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kFoldersGroup), "Folders" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutGroup), "About" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Language)), "Interface language" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Language)),
                    "The language of Sign Builder (its editor and this page). Auto follows the game: Arabic when the game is in Arabic, otherwise English. Takes effect at once." },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.Auto), "Auto" },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.Arabic), "العربية" },
                { m_Setting.GetEnumValueLocaleID(Setting.InterfaceLanguage.English), "English" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ToggleBinding)), "Open or close Sign Builder" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ToggleBinding)), "Opens or closes Sign Builder, like its toolbar button." },
                { m_Setting.GetBindingKeyLocaleID(Setting.kToggleAction), "Open or close Sign Builder" },
                { m_Setting.GetBindingMapLocaleID(), "Sign Builder: Saudi Edition" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanUpUnused)), "Delete unused sign structures" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CleanUpUnused)),
                    "Deletes the generated meshes and materials of sign structures not placed in the open city (their files are removed at the next game start). " +
                    "Other cities keep their signs: they are rebuilt from the save when that city is loaded." },
                { m_Setting.GetOptionWarningLocaleID(nameof(Setting.CleanUpUnused)),
                    "Delete every sign structure not used in this city? Other saved cities rebuild their signs when they are loaded." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenTemplatesFolder)), "Open templates folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenTemplatesFolder)), "Your saved sign templates, one JSON file per template. Share them by copying the files." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenThemesFolder)), "Open themes folder" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenThemesFolder)), "Sign themes (countries or styles), one folder per theme with its theme.json, fonts, shields and templates." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Version)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Version)), "The installed version of Sign Builder." },
            };
        }

        public void Unload()
        {
        }
    }

    /// <summary>
    /// One source per game locale that serves the options page in Sign Builder's interface language (Arabic or English,
    /// see <see cref="Systems.LanguageUISystem.Resolve"/>), whatever the game's own language is.
    /// </summary>
    public class OptionsLocaleSource : IDictionarySource
    {
        private readonly LocaleAR m_Arabic;
        private readonly LocaleEN m_English;

        public OptionsLocaleSource(string localeId, Setting setting)
        {
            LocaleId = localeId;
            m_Arabic = new LocaleAR(setting);
            m_English = new LocaleEN(setting);
        }

        /// <summary>The game locale this source is registered for.</summary>
        public string LocaleId { get; }

        /// <summary>The language ("ar" or "en") last read into the game's dictionary; null before the first read.</summary>
        public string Served { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var language = Systems.LanguageUISystem.Resolve();
            Served = language;
            return language == Systems.LanguageUISystem.Arabic
                ? m_Arabic.ReadEntries(errors, indexCounts)
                : m_English.ReadEntries(errors, indexCounts);
        }

        public void Unload()
        {
        }
    }

    /// <summary>Registers the options page text with the game and reloads it when Sign Builder's language changes.</summary>
    public static class OptionsLocalization
    {
        private static readonly Dictionary<string, OptionsLocaleSource> s_Sources = new Dictionary<string, OptionsLocaleSource>();
        private static Setting s_Setting;
        private static string s_LastReloadAttempt;
        private static bool s_Reloading;

        /// <summary>Adds the options page text to every game locale.</summary>
        public static void Register(Setting setting)
        {
            s_Setting = setting;
            EnsureAllLocales();
        }

        /// <summary>Adds a source to locales that have none yet (e.g. locales added by other mods after startup).</summary>
        public static void EnsureAllLocales()
        {
            if (s_Setting == null) return;
            var locales = GameManager.instance?.localizationManager;
            if (locales == null) return;
            foreach (var locale in locales.GetSupportedLocales())
            {
                if (s_Sources.ContainsKey(locale)) continue;
                var source = new OptionsLocaleSource(locale, s_Setting);
                // Recorded before AddSource: adding to the active locale reads it at once and notifies listeners.
                s_Sources[locale] = source;
                locales.AddSource(locale, source);
            }
        }

        /// <summary>
        /// Reloads the active game locale when its options text is in another language than <paramref name="language"/>
        /// (the player changed Sign Builder's language, or Auto now resolves differently).
        /// </summary>
        public static void ReloadIfStale(string language)
        {
            if (s_Setting == null || s_Reloading) return;
            var locales = GameManager.instance?.localizationManager;
            if (locales == null) return;
            string active;
            try { active = locales.activeLocaleId; }
            catch { return; }
            if (string.IsNullOrEmpty(active)) return;
            if (!s_Sources.TryGetValue(active, out var source))
            {
                EnsureAllLocales();
                return;
            }
            if (source.Served == language) { s_LastReloadAttempt = null; return; }
            // One attempt per locale and language: a locale the game cannot reload is not retried every frame.
            var attempt = active + "|" + language;
            if (attempt == s_LastReloadAttempt) return;
            s_LastReloadAttempt = attempt;
            s_Reloading = true;
            try { locales.ReloadActiveLocale(); }
            catch (System.Exception e) { Mod.log.Warn(e, "Reloading the options text failed"); }
            finally { s_Reloading = false; }
        }

        /// <summary>Removes the options text from every game locale (mod unloaded).</summary>
        public static void Unregister()
        {
            var locales = GameManager.instance?.localizationManager;
            if (locales != null)
                foreach (var pair in s_Sources) locales.RemoveSource(pair.Key, pair.Value);
            s_Sources.Clear();
            s_Setting = null;
            s_LastReloadAttempt = null;
        }
    }
}
