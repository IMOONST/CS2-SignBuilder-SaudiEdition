import { useValue } from "cs2/api";
import { Language, getLanguage, languageBinding, setLanguage } from "./l10n";

/**
 * Sign Builder's interface language from C# (SignBuilderSA.Systems.LanguageUISystem): "ar" or "en", the player's choice
 * in the options (Auto / Arabic / English). Arabic until the binding says otherwise.
 */
export const language$ = languageBinding;

/**
 * Subscribes the calling component to the interface language and returns it. A component that calls this re-renders
 * (with its whole subtree) when the language changes, so every t() under it shows the new language. Call it at the
 * top of each UI root (toolbar button, editor, panels).
 */
export const useLanguage = (): Language => {
  const value = useValue(language$);
  // The module-level subscription in l10n.ts normally got there first; this makes sure t() is current in this render.
  setLanguage(value);
  return getLanguage();
};

/** Renders nothing; keeps the language in step for code outside the mounted roots. Append to "Game" in index.tsx. */
export const LanguageSync = () => {
  useLanguage();
  return null;
};
