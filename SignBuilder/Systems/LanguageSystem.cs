using System;
using System.Globalization;
using Colossal.Localization;
using Colossal.UI.Binding;
using Game.SceneFlow;
using Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SignBuilderSA.Systems
{
    /// <summary>
    /// Sign Builder's interface language and template packs.
    /// Values (group "SignBuilderSA"): language ("ar" or "en"), templateExchange (JSON result of the last export / import).
    /// Triggers: exportTemplates, importTemplates.
    /// The language is the player's choice in the options (Auto / Arabic / English); Auto is Arabic when the game's
    /// active locale is Arabic, otherwise English. It follows setting and game locale
    /// changes at once, and keeps the options page text (<see cref="OptionsLocalization"/>) in the same language.
    /// </summary>
    public partial class LanguageUISystem : UISystemBase
    {
        public const string Arabic = "ar";
        public const string English = "en";


        private ValueBinding<string> m_Language;
        private ValueBinding<string> m_TemplateExchange;
        private LocalizationManager m_Locales;
        private Setting m_WatchedSetting;
        private bool m_Refreshing;

        /// <summary>
        /// The interface language now: "ar" or "en". Safe to call at any time (before the settings load it is Auto).
        /// </summary>
        public static string Resolve()
        {
            var choice = Mod.Settings != null ? Mod.Settings.Language : Setting.InterfaceLanguage.Auto;
            if (choice == Setting.InterfaceLanguage.Arabic) return Arabic;
            if (choice == Setting.InterfaceLanguage.English) return English;
            string locale = null;
            try { locale = GameManager.instance?.localizationManager?.activeLocaleId; }
            catch (Exception) { /* no active dictionary yet */ }
            // Auto follows the game's language only: Windows' language does not count (pick Arabic to force it).
            return locale != null && locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? Arabic : English;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(m_Language = new ValueBinding<string>(SignBuilderUISystem.Group, "language", Resolve()));
            AddBinding(m_TemplateExchange = new ValueBinding<string>(SignBuilderUISystem.Group, "templateExchange", ""));
            AddBinding(new TriggerBinding(SignBuilderUISystem.Group, "exportTemplates", ExportTemplates));
            AddBinding(new TriggerBinding(SignBuilderUISystem.Group, "importTemplates", ImportTemplates));

            m_Locales = GameManager.instance?.localizationManager;
            if (m_Locales != null)
            {
                m_Locales.onActiveDictionaryChanged += OnLocaleChanged;
                m_Locales.onSupportedLocalesChanged += OnSupportedLocalesChanged;
            }
            WatchSetting();
        }

        protected override void OnDestroy()
        {
            if (m_Locales != null)
            {
                m_Locales.onActiveDictionaryChanged -= OnLocaleChanged;
                m_Locales.onSupportedLocalesChanged -= OnSupportedLocalesChanged;
            }
            if (m_WatchedSetting != null) m_WatchedSetting.onSettingsApplied -= OnSettingsApplied;
            m_WatchedSetting = null;
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            // The settings object appears after OnLoad, and its saved values load without an "applied" event: checking
            // here (a few comparisons) catches both.
            WatchSetting();
            Refresh();
        }

        /// <summary>Follows the "applied" event of the current settings object (it is created in Mod.OnLoad).</summary>
        private void WatchSetting()
        {
            var setting = Mod.Settings;
            if (setting == m_WatchedSetting) return;
            if (m_WatchedSetting != null) m_WatchedSetting.onSettingsApplied -= OnSettingsApplied;
            m_WatchedSetting = setting;
            if (m_WatchedSetting != null) m_WatchedSetting.onSettingsApplied += OnSettingsApplied;
        }

        private void OnSettingsApplied(Game.Settings.Setting setting) => Refresh();

        private void OnLocaleChanged() => Refresh();

        private void OnSupportedLocalesChanged()
        {
            OptionsLocalization.EnsureAllLocales();
            Refresh();
        }

        /// <summary>Pushes the current language to the UI and brings the options page text in line with it.</summary>
        private void Refresh()
        {
            if (m_Refreshing || m_Language == null) return;
            m_Refreshing = true;
            try
            {
                var language = Resolve();
                if (language != m_Language.value) m_Language.Update(language);
                OptionsLocalization.ReloadIfStale(language);
            }
            catch (Exception e)
            {
                Mod.log.Warn(e, "Updating Sign Builder's language failed");
            }
            finally
            {
                m_Refreshing = false;
            }
        }

        // ------------------------------------------------------------------ template packs

        private void ExportTemplates()
        {
            JObject result;
            try
            {
                var export = TemplateExchange.ExportAll();
                result = new JObject { ["op"] = "export", ["count"] = export.Count, ["file"] = export.FileName ?? "" };
                if (export.Count > 0) Setting.OpenFolder(TemplateExchange.ExportDir);
            }
            catch (Exception e)
            {
                Mod.log.Warn(e, "Exporting templates failed");
                result = new JObject { ["op"] = "export", ["error"] = e.Message };
            }
            Report(result);
        }

        private void ImportTemplates()
        {
            JObject result;
            try
            {
                var import = TemplateExchange.ImportAll();
                result = new JObject
                {
                    ["op"] = "import", ["count"] = import.Imported, ["skipped"] = import.Skipped,
                    ["failed"] = import.Failed, ["files"] = import.Files,
                };
                // The new template files show up in the library at once.
                if (import.Imported > 0) World.GetExistingSystemManaged<SignBuilderUISystem>()?.RefreshLibrary();
                // Nothing to import: open the Import folder so the player sees where pack files go.
                if (import.Files == 0) Setting.OpenFolder(TemplateExchange.ImportDir);
            }
            catch (Exception e)
            {
                Mod.log.Warn(e, "Importing templates failed");
                result = new JObject { ["op"] = "import", ["error"] = e.Message };
            }
            Report(result);
        }

        /// <summary>A fresh value every time (nonce), so the UI shows the result even when it repeats.</summary>
        private void Report(JObject result)
        {
            result["nonce"] = Guid.NewGuid().ToString("N");
            m_TemplateExchange.Update(result.ToString(Formatting.None));
        }
    }
}
