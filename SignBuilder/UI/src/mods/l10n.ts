import { bindValue } from "cs2/api";
import mod from "mod.json";
import { AR } from "./ar";
import de from "../../../i18n/de.json";
import es from "../../../i18n/es.json";
import fr from "../../../i18n/fr.json";
import it from "../../../i18n/it.json";
import ja from "../../../i18n/ja.json";
import ko from "../../../i18n/ko.json";
import pl from "../../../i18n/pl.json";
import ptBR from "../../../i18n/pt-BR.json";
import ru from "../../../i18n/ru.json";
import zhHans from "../../../i18n/zh-HANS.json";
import zhHant from "../../../i18n/zh-HANT.json";

/**
 * Interface language. Every visible English string in the UI goes through t(); each dictionary is keyed by the English
 * text, so an untranslated string simply shows in English instead of breaking. Arabic is ar.ts; the game's other
 * languages are SignBuilder/i18n/<code>.json (shared with the options page in C#).
 *
 * The language comes from C# (SignBuilderSA.Systems.LanguageUISystem, binding "language": a code such as "ar", "en",
 * "de", "zh-HANS"), the player's choice in the options (Auto = the game's language, Windows, or a fixed language).
 * t() always reads the current one; components re-render on a change through useLanguage() (language.ts).
 */
export type Language = string;

const TABLES: Record<string, Record<string, string>> = {
  ar: AR,
  de, es, fr, it, ja, ko, pl,
  "pt-BR": ptBR,
  ru,
  "zh-HANS": zhHans,
  "zh-HANT": zhHant,
};

/** C# binding "language" (group = mod id). language.ts re-exports it as language$. */
export const languageBinding = bindValue<string>(mod.id, "language", "en");

const normalize = (value: unknown): Language => (typeof value === "string" && (value === "en" || value in TABLES) ? value : "en");

let language: Language = "en";
let table: Record<string, string> | undefined;
/** Bumped on every change, so cached localized values (see localized()) know to rebuild. */
let revision = 0;

export const setLanguage = (lang: Language | string) => {
  const next = normalize(lang);
  if (next === language) return;
  language = next;
  table = TABLES[next];
  revision++;
};
export const getLanguage = (): Language => language;
export const isArabic = () => language === "ar";

// Follow the binding from the moment this module loads, independent of any component being mounted, so t() is right
// even in code that runs outside React renders (event handlers, effects, module-level helpers).
try {
  const subscription = languageBinding.subscribe((value) => setLanguage(value));
  setLanguage(subscription.value);
} catch {
  // Binding not available (e.g. outside the game): stay English.
}

export const t = (en: string): string => (table && table[en]) || en;

/** t() with {placeholders}: tf("{n} signs", { n: 3 }). */
export const tf = (en: string, values: Record<string, string | number>): string =>
  t(en).replace(/\{(\w+)\}/g, (m, k) => (k in values ? String(values[k]) : m));

/**
 * A module-level value built with t() that follows the language: `const OPTIONS = localized(() => [{ label: t("Top") }])`,
 * then `OPTIONS()` where it is used. Built on first use and again after each language change (same object otherwise).
 */
export function localized<T>(build: () => T): () => T {
  let cached: T;
  let builtAt = -1;
  return () => {
    if (builtAt !== revision) {
      cached = build();
      builtAt = revision;
    }
    return cached;
  };
}
