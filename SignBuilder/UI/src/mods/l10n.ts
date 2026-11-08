import { bindValue } from "cs2/api";
import mod from "mod.json";
import { AR } from "./ar";

/**
 * Interface language. Every visible English string in the UI goes through t(); the Arabic dictionary (ar.ts) is
 * keyed by the English text, so an untranslated string simply shows in English instead of breaking.
 *
 * The language comes from C# (SignBuilderSA.Systems.LanguageUISystem, binding "language": "ar" or "en"), the player's
 * choice in the options (Auto / Arabic / English). t() always reads the current one; components re-render on a change
 * through useLanguage() (language.ts). Arabic until C# says otherwise: this is the Arabic edition.
 */
export type Language = "ar" | "en";

/** C# binding "language" (group = mod id). language.ts re-exports it as language$. */
export const languageBinding = bindValue<string>(mod.id, "language", "ar");

const normalize = (value: unknown): Language => (value === "en" ? "en" : "ar");

let language: Language = "ar";
/** Bumped on every change, so cached localized values (see localized()) know to rebuild. */
let revision = 0;

export const setLanguage = (lang: Language | string) => {
  const next = normalize(lang);
  if (next === language) return;
  language = next;
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
  // Binding not available (e.g. outside the game): stay Arabic.
}

export const t = (en: string): string => (language === "ar" && AR[en]) || en;

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
