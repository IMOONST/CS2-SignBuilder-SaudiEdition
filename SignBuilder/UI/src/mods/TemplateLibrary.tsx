import { t as tr, tf, isArabic } from "./l10n";
import { bindValue, trigger, useValue } from "cs2/api";
import { Scrollable } from "cs2/ui";
import { useMemo, useState } from "react";
import mod from "mod.json";
import { deleteTemplate, openTemplatesFolder, renameTemplate, saveTemplate } from "./bindings";
import { Design } from "./designModel";
import { SignPreview } from "./SignPreview";
import { TextBox } from "./Controls";
import { LockIcon, PencilIcon } from "./Icons";
import styles from "./SignEditor.module.scss";

export type Template = { id: string; name: string; builtIn?: boolean; design: Design };

// C# SignBuilderSA.Systems.LanguageUISystem (template packs, see TemplateExchange.cs).
/** JSON result of the last export / import: { op, count, file?, skipped?, failed?, files?, error?, nonce }. */
const templateExchange$ = bindValue<string>(mod.id, "templateExchange", "");
/** Writes all the player's own templates into one pack in Templates/Export and opens that folder. */
const exportTemplates = () => trigger(mod.id, "exportTemplates");
/** Imports every pack file in Templates/Import (moved to Import/Done afterwards); opens that folder when it is empty. */
const importTemplates = () => trigger(mod.id, "importTemplates");

type ExchangeResult = { op?: "export" | "import"; count?: number; file?: string; skipped?: number; failed?: number; files?: number; error?: string; nonce?: string };

/** The status line for an export / import result. */
function exchangeMessage(r: ExchangeResult): string {
  if (r.op === "export") {
    if (r.error) return tf("Export failed: {error}", { error: r.error });
    if (!r.count) return tr("You have no templates of your own to export.");
    return tf("Exported {n} templates to {file} (Export folder).", { n: r.count, file: r.file ?? "" });
  }
  if (r.error) return tf("Import failed: {error}", { error: r.error });
  if (!r.files) return tr("No template packs found. Put .json pack files in the Import folder (now open) and press Import again.");
  const parts = [tf("Imported {n} templates", { n: r.count ?? 0 })];
  if (r.skipped) parts.push(tf("{n} already in your library", { n: r.skipped }));
  if (r.failed) parts.push(tf("{n} could not be read", { n: r.failed }));
  return parts.join(isArabic() ? "، " : ", ") + ".";
}

/**
 * Template cards with a live thumbnail of each sign. Built-in templates ship with the mod (deleting one hides it);
 * the player's own are JSON files in ModsData/SignBuilderSA/Templates, easy to share.
 */
export const TemplateLibrary = (props: { templates: Template[]; current: Design; onPick: (t: Template) => void; onSaved?: () => void }) => {
  const [naming, setNaming] = useState<{ id?: string; name: string } | null>(null);
  const [menu, setMenu] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const exchangeJson = useValue(templateExchange$);
  // Only results of exports / imports made while this library is open (not one left from an earlier visit).
  const [seenExchange] = useState(exchangeJson);
  const exchange: ExchangeResult | null = useMemo(() => {
    if (!exchangeJson || exchangeJson === seenExchange) return null;
    try { return JSON.parse(exchangeJson); } catch { return null; }
  }, [exchangeJson, seenExchange]);
  const words = search.toLowerCase().split(/\s+/).filter((w) => w);
  const shown = props.templates.filter((t) => words.every((w) => t.name.toLowerCase().includes(w)));
  // Two cards per row, as separate rows: Gameface does not grow a wrapped flex row's height.
  const rows: Template[][] = [];
  for (let i = 0; i < shown.length; i += 2) rows.push(shown.slice(i, i + 2));

  const commit = () => {
    if (!naming || !naming.name.trim()) { setNaming(null); return; }
    if (naming.id) renameTemplate(naming.id, naming.name.trim());
    else saveTemplate(naming.name.trim(), props.current);
    if (!naming.id) props.onSaved?.();
    setNaming(null);
  };

  return (
    <div className={styles.library}>
      <div className={styles.libraryActions}>
        <div className={styles.chip} onClick={() => setNaming({ name: props.current.name && !props.current.id ? props.current.name : tr("My sign") })}>{tr("Save current as template")}</div>
        <div className={styles.chip} onClick={() => openTemplatesFolder()}>{tr("Open folder")}</div>
        <div className={styles.chip} title={tr("Save all your own templates into one pack file (in the Export folder) to share or back up")} onClick={() => exportTemplates()}>{tr("Export")}</div>
        <div className={styles.chip} title={tr("Add the templates from every pack file in the Import folder (files are moved to Import/Done afterwards)")} onClick={() => importTemplates()}>{tr("Import")}</div>
      </div>
      {exchange && <div className={exchange.error ? styles.status : styles.hint}>{exchangeMessage(exchange)}</div>}
      {naming && (
        <div className={styles.namingRow}>
          <TextBox value={naming.name} autoFocus onChange={(name) => setNaming({ ...naming, name })} />
          <div className={styles.chip} onClick={commit}>{naming.id ? tr("Rename") : tr("Save")}</div>
          <div className={styles.chip} onClick={() => setNaming(null)}>{tr("Cancel")}</div>
        </div>
      )}
      <div className={styles.searchRow}>
        <TextBox value={search} placeholder={tr("Search templates")} onChange={setSearch} />
      </div>
      <Scrollable vertical trackVisibility="scrollable" className={styles.cards}>
        {rows.map((row, r) => (
          <div key={r} className={styles.cardRow}>
            {row.map((t) => (
              <div key={t.id} className={styles.card} onClick={() => props.onPick(t)}>
                <div className={styles.thumb}>
                  <SignPreview design={t.design} interactive={false} maxWidth={124} maxHeight={64} padding={6} />
                </div>
                <div className={styles.cardName}>{t.name}</div>
                <div className={styles.cardBadge} title={t.builtIn ? tr("Built-in template (comes with its theme)") : tr("Your template")}>
                  {t.builtIn ? <LockIcon /> : <PencilIcon />}
                </div>
                <div className={styles.cardMenu} onClick={(e) => { e.stopPropagation(); setMenu(menu === t.id ? null : t.id); }}>···</div>
                {menu === t.id && (
                  <div className={styles.cardActions} onClick={(e) => e.stopPropagation()}>
                    {!t.builtIn && <div className={styles.chip} onClick={() => { setNaming({ id: t.id, name: t.name }); setMenu(null); }}>{tr("Rename")}</div>}
                    {!t.builtIn && <div className={styles.chip} onClick={() => { saveTemplate(t.name, props.current, t.id); setMenu(null); }}>{tr("Overwrite with current")}</div>}
                    <div className={styles.chip} onClick={() => { deleteTemplate(t.id); setMenu(null); }}>{t.builtIn ? tr("Hide") : tr("Delete")}</div>
                  </div>
                )}
              </div>
            ))}
          </div>
        ))}
        {shown.length === 0 && <div className={styles.hint}>{search ? tr("No templates match.") : tr("No templates yet.")}</div>}
      </Scrollable>
    </div>
  );
};
