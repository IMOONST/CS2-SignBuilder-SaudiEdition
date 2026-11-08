import { t, tf, localized } from "./l10n";
import { useValue } from "cs2/api";
import { Scrollable } from "cs2/ui";
import { Dialog } from "./Dialog";
import { openThemesFolder, toolMode$ } from "./bindings";
import { ROAD_TOKENS, RoadClass, RoadInfo, cancelPickRoad, parseRoadInfo, pickRoad, roadInfo$, signSpeed } from "./roadPick";
import { ReactNode, useEffect, useMemo, useState } from "react";
import { ArrowIcon, AlignIcon, SymbolIcon, ThemeArrowIcon, ThemeShieldIcon } from "./Icons";
import { Choice, ColorBox, ColorRows, Field, NumberBox, Option, Select, Swatches, TextBox } from "./Controls";
import { Design, Item, clone, duplicateItem, getItem, insertItem, kindOf, removeItem, updateItem } from "./designModel";
import { PartsList } from "./PartsList";
import { SignPreview, useRenderer } from "./SignPreview";
import { Template, TemplateLibrary } from "./TemplateLibrary";
import { DESIGN_FORMAT, LibrarySymbol, SymbolLibrary, Theme, baseParts, blankFor, convertDesign, themeOf } from "./themeModel";
import styles from "./SignEditor.module.scss";

/** Whether a design has parts whose look depends on its format (arrows and road diagrams). */
const usesArrows = (design: { items?: Item[]; [key: string]: any }): boolean =>
  (design.items ?? []).some((i) => i.type === "arrow" || i.type === "diagram" || usesArrows(i));

/** An arrow's style ("" for straight arrows): lane arrows saved before there were styles have none. */
const arrowStyleOf = (item: Item): string =>
  item.style ?? (/^((turn|slight|split|corner|add)-(left|right)|fork)$/.test(item.direction ?? "") ? "lane" : "");
const ALIGN: Option[] = ["start", "center", "end"].map((a) => ({ value: a, icon: <AlignIcon align={a} />, title: a }));
// Parts side by side in a row line up by their tops, middles or bottoms.
const ROW_ALIGN = localized((): Option[] => [{ value: "start", label: t("Top") }, { value: "center", label: t("Middle") }, { value: "end", label: t("Bottom") }]);
const ROW_SPACING = localized((): Option[] => [
  { value: "", label: t("Packed") },
  { value: "between", label: t("Spread out"), title: t("Fill the width, with the space between the parts") },
  { value: "even", label: t("Even"), title: t("Fill the width, each part centered in an equal share of it (dividers keep their width)") },
]);
const LANE_KIND = localized((): Option[] => [
  { value: "", label: t("Highway"), title: t("Highway lanes (4 m)") },
  { value: "local", label: t("Local"), title: t("Local road lanes (3 m)") },
]);
const PLACE = localized((): Option[] => [{ value: "", label: t("Sign default") }, ...ALIGN]);
const DIAGRAM_SHAPES = localized((): Option[] => [
  { value: "exit-left", label: t("Exit left") }, { value: "exit-right", label: t("Exit right") },
  { value: "fork", label: t("Fork") }, { value: "roundabout", label: t("Roundabout") },
]);
// How many of an exit's lanes are through lanes too. Diagrams made before this setting draw every exit lane from
// the through lanes (no value: neither button is on unless that is one lane).
const SHARED_LANES = localized((): Option[] => [
  { value: "0", label: t("Exit only"), title: t("The exit lanes are extra lanes beside the through lanes, ending at the exit") },
  { value: "1", label: t("One shared"), title: t("The nearest exit lane is also a through lane (an option lane); any others are exit only") },
]);
const ROUNDABOUT_EXITS: Option[] = ["left", "up-left", "up", "up-right", "right"].map((d) => ({ value: d, icon: <ArrowIcon direction={d} />, title: d }));
// LED colors: amber (the default, no value), white, green, red.
/**
 * A panel's or chip's colors: one of the theme's sign colors, or Custom (any background and legend color, stored as
 * "background" / "foreground" and taking precedence over the scheme).
 */
const BoxColorField = (props: { label: string; item: Item; theme?: Theme; onChange: (patch: Partial<Item>) => void }) => {
  const { item } = props;
  const custom = !!item.background;
  return (
    <>
      <Field label={props.label}>
        <Swatches schemes={props.theme?.def?.schemes ?? {}} scheme={item.scheme ?? ""} custom={custom}
          onScheme={(scheme) => props.onChange({ scheme, background: undefined, foreground: undefined })}
          onCustom={() => props.onChange({ background: item.background ?? "#000000", foreground: item.foreground ?? "#ffffff" })} />
      </Field>
      {custom && (
        <div className={styles.customColors}>
          <Field label={t("Background")}><ColorBox value={item.background} onChange={(background) => props.onChange({ background })} /></Field>
          <Field label={t("Legend")}><ColorBox value={item.foreground ?? "#ffffff"} onChange={(foreground) => props.onChange({ foreground })} /></Field>
        </div>
      )}
    </>
  );
};

const LED_COLORS = localized(() => [{ value: "", color: "#ffb000", title: t("Amber") }, { value: "#f4f4f4", title: t("White") }, { value: "#46ff74", title: t("Green") }, { value: "#ff4031", title: t("Red") }]);

/** An LED color: one of the usual ones or any color. <value> undefined is the default amber. */
const LedColorField = (props: { label: string; value?: string; onChange: (color: string | undefined) => void }) => {
  const known = LED_COLORS().some((c) => c.value && c.value === props.value?.toLowerCase());
  const custom = !!props.value && !known;
  return (
    <Field label={props.label}>
      <ColorRows colors={LED_COLORS().map((c) => ({
        color: c.color ?? c.value, title: c.title, on: c.value ? c.value === props.value?.toLowerCase() : !props.value, onClick: () => props.onChange(c.value || undefined),
      }))} custom={{ on: custom, onClick: () => props.onChange(props.value ?? "#ff8800") }} />
      {custom && <ColorBox value={props.value!} onChange={(c) => props.onChange(c)} />}
    </Field>
  );
};
const CHIP_SPAN = localized((): Option[] => [{ value: "", label: t("Fit") }, { value: "full", label: t("Full width"), title: t("As wide as the column it is in") }]);
const TAB_SIDE = localized((): Option[] => [{ value: "left", label: t("Left") }, { value: "center", label: t("Center") }, { value: "right", label: t("Right") }]);
// A panel across the sign (a band) or down one of its sides, its full height (e.g. the black HOV diamond panel).
const PANEL_SIDE = localized((): Option[] => [
  { value: "", label: t("Across"), title: t("A band across the sign's full width") },
  { value: "left", label: t("Left side"), title: t("Down the sign's left edge, its full height") },
  { value: "right", label: t("Right side"), title: t("Down the sign's right edge, its full height") },
]);

/**
 * Buttons laid out in rows by their label lengths (Gameface does not grow a wrapped flex row, so a second line would
 * overlap whatever follows).
 */
const Palette = (props: { entries: { label: string; onClick: () => void }[] }) => {
  const rows: { label: string; onClick: () => void }[][] = [[]];
  let used = 0;
  for (const e of props.entries) {
    const width = e.label.length * 8 + 24;
    if (used + width > 560 && rows[rows.length - 1].length > 0) { rows.push([]); used = 0; }
    rows[rows.length - 1].push(e);
    used += width;
  }
  return (
    <div className={styles.choiceRows}>
      {rows.map((row, i) => (
        <div key={i} className={styles.palette}>{row.map((e) => <div key={e.label} className={styles.chip} onClick={e.onClick}>{e.label}</div>)}</div>
      ))}
    </div>
  );
};

/** Alignment of one part within its column, overriding the sign's "Align lines". */
const PlaceField = (props: { item: Item; onChange: (patch: Partial<Item>) => void }) => (
  <Field label={t("Align this part")}><Choice options={PLACE()} value={props.item.place ?? ""} onChange={(place) => props.onChange({ place: place || undefined })} /></Field>
);

/**
 * A color: its default (no value: the sign's or chip's legend color, or none), one of the theme's colors, or any color.
 */
const ColorField = (props: {
  label: string; value?: string; theme?: Theme; defaultLabel: string; defaultTitle: string; onChange: (color: string | undefined) => void;
}) => {
  const palette = Array.from(new Set(Object.values(props.theme?.def.schemes ?? {}).flatMap((s) => [s.fg, s.bg].map((c) => c.toLowerCase()))));
  const color = props.value;
  const custom = !!color && !palette.includes(color.toLowerCase());
  return (
    <Field label={props.label}>
      <ColorRows perRow={7}
        lead={[<div key="default" className={!color ? styles.choiceOn : styles.choice} title={props.defaultTitle} onClick={() => props.onChange(undefined)}>{props.defaultLabel}</div>]}
        colors={palette.map((c) => ({ color: c, on: color?.toLowerCase() === c, onClick: () => props.onChange(c) }))}
        custom={{ on: custom, onClick: () => props.onChange(color ?? "#ff8800") }} />
      {custom && <ColorBox value={color!} onChange={(c) => props.onChange(c)} />}
    </Field>
  );
};

/** Color of a text, arrow, symbol or divider: the default (the sign's or chip's legend color), a theme color, or any. */
const PartColorField = (props: { item: Item; theme?: Theme; onChange: (patch: Partial<Item>) => void }) => (
  <ColorField label={t("Color")} value={props.item.color} theme={props.theme} defaultLabel={t("Default")} defaultTitle={t("The sign's legend color")}
    onChange={(color) => props.onChange({ color })} />
);

const SYMBOL_DISPLAY = localized((): Option[] => [
  { value: "", label: t("Symbol only") },
  { value: "box", label: t("In a box"), title: t("Centered in a square box with rounded corners") },
]);
const BOX_BORDER = localized((): Option[] => [{ value: "", label: t("Line") }, { value: "none", label: t("None") }]);

/** A symbol from its reference ("<library>/<id>"). */
const symbolOf = (libraries: Record<string, SymbolLibrary>, ref?: string): LibrarySymbol | undefined => {
  if (!ref) return undefined;
  const [lib, ...id] = ref.split("/");
  return libraries[lib]?.symbols?.[id.join("/")];
};

// Symbols picked lately, most recent first (kept between sessions where the page can store them).
const RECENT_KEY = "signBuilderSA.recentSymbols";
const recentSymbols = (): string[] => {
  try { return JSON.parse(localStorage.getItem(RECENT_KEY) ?? "[]"); } catch { return []; }
};
const rememberSymbol = (ref: string) => {
  try { localStorage.setItem(RECENT_KEY, JSON.stringify([ref, ...recentSymbols().filter((r) => r !== ref)].slice(0, 12))); } catch { /* not stored */ }
};

/** An arrow picked in the symbol picker: the part becomes an Arrow part ("library": another library's arrows). */
export type PickedArrow = { library?: string; style: string; direction: string };

/**
 * Picks a symbol, or an arrow: the theme's own libraries by default, any other library from the list. Symbols are
 * grouped by category, arrows by style; only the category shown is drawn. Search covers the symbols of the library
 * shown, or of every library on request. Drawn as a shade over the whole designer (as its discard question is):
 * inside the parts panel it would be laid out and clipped there.
 */
const SymbolPicker = (props: {
  libraries: Record<string, SymbolLibrary>; own: string[]; value?: string; themeId: string; version: number;
  onPick: (ref: string) => void; onPickArrow: (arrow: PickedArrow) => void; onClose: () => void;
}) => {
  const renderer = useRenderer();
  const others = Object.values(props.libraries)
    .filter((l) => !props.own.includes(l.id) && (Object.keys(l.symbols ?? {}).length > 0 || Object.keys(l.arrows ?? {}).length > 0))
    .sort((a, b) => a.name.localeCompare(b.name));
  const [library, setLibrary] = useState(props.value && !props.own.includes(props.value.split("/")[0]) ? props.value.split("/")[0] : "");
  const [search, setSearch] = useState("");
  const [everywhere, setEverywhere] = useState(false);
  const shown = library ? [library] : props.own;
  const all = (ids: string[]) => ids.flatMap((id) => Object.entries(props.libraries[id]?.symbols ?? {}).map(([key, sym]) => ({ ref: `${id}/${key}`, lib: id, sym })));
  const entries = all(shown);
  // The arrows of the theme (as the sign draws them), or of the library shown.
  const arrowStyles = renderer ? renderer.arrowStyles(props.themeId, library ? 2 : props.version, library || undefined) : [];
  const arrowCategory = (label: string) => tf("Arrows: {name}", { name: label });
  const categories = [...Array.from(new Set(entries.map((e) => e.sym.category ?? t("Other")))).sort(), ...arrowStyles.map((s) => arrowCategory(s.label))];
  const [category, setCategory] = useState<string | null>(null);
  const current = category && categories.includes(category) ? category : categories[0];
  const term = search.trim().toLowerCase();
  const matches = (e: { ref: string; sym: LibrarySymbol }) => !term || e.sym.name.toLowerCase().includes(term) || (e.sym.number ?? "").toLowerCase().includes(term) || e.ref.toLowerCase().includes(term);
  const list = term ? all(everywhere ? Object.keys(props.libraries) : shown).filter(matches) : entries.filter((e) => (e.sym.category ?? t("Other")) === current);
  const recent = recentSymbols().filter((r) => symbolOf(props.libraries, r));
  const pick = (ref: string) => { rememberSymbol(ref); props.onPick(ref); props.onClose(); };
  const arrowStyle = !term ? arrowStyles.find((s) => arrowCategory(s.label) === current) : undefined;
  const arrowCells = arrowStyle ? arrowStyle.directions.map((d) => (
    <div key={d} className={styles.symbolCell} title={`${arrowStyle.label}: ${d}`}
      onClick={() => { props.onPickArrow({ library: library || undefined, style: arrowStyle.id, direction: d }); props.onClose(); }}>
      <ThemeArrowIcon theme={props.themeId} style={arrowStyle.id} direction={d} version={library ? 2 : props.version} library={library || undefined} box={arrowStyle.box} size={36} />
    </div>
  )) : null;
  const cell = (e: { ref: string; lib: string; sym: LibrarySymbol }) => (
    <div key={e.ref} className={e.ref === props.value ? styles.symbolCellOn : styles.symbolCell} onClick={() => pick(e.ref)}
      title={e.sym.name + (e.sym.number ? ` (${e.sym.number})` : "") + (props.own.includes(e.lib) ? "" : ` · ${props.libraries[e.lib]?.name ?? e.lib}`)}>
      <SymbolIcon symbol={e.sym} size={40} />
    </div>
  );
  const cells = arrowCells ?? list.map(cell);
  return (
    <div className={styles.confirmShade} onClick={props.onClose}>
    <div className={`${styles.dialog} ${styles.symbolDialog}`} onClick={(e) => e.stopPropagation()}>
      <div className={styles.dialogHeader}>
        <div className={styles.dialogTitle}>{t("Choose a symbol")}</div>
        <div className={styles.dialogHeaderSpace} />
        <div className={styles.dialogClose} title={t("Close")} onClick={props.onClose} />
      </div>
      <div className={styles.symbolTop}>
        <div className={styles.symbolSearch}><TextBox value={search} onChange={(v) => { setSearch(v); setEverywhere(false); }} /></div>
        <div className={styles.symbolLibrary}>
          <Select value={library} onChange={(v) => { setLibrary(v); setCategory(null); }}
            options={[{ value: "", label: t("This theme's") }, ...others.map((l) => ({ value: l.id, label: l.name, title: l.description }))]} />
        </div>
      </div>
      {recent.length > 0 && !term && (
        <div className={styles.symbolRecent}>
          {recent.map((ref) => cell({ ref, lib: ref.split("/")[0], sym: symbolOf(props.libraries, ref)! }))}
        </div>
      )}
      <div className={styles.symbolBody}>
        {!term && (
          <div className={styles.symbolCategories}>
            {categories.map((c) => <div key={c} className={c === current ? styles.choiceOn : styles.choice} onClick={() => setCategory(c)}>{c}</div>)}
          </div>
        )}
        <Scrollable vertical trackVisibility="scrollable" className={styles.symbolGrid}>
          {/* Rows of eight: a wrapped flex row does not grow in Gameface. */}
          {Array.from({ length: Math.ceil(cells.length / 8) }, (_, r) => <div key={r} className={styles.symbolCells}>{cells.slice(r * 8, r * 8 + 8)}</div>)}
          {cells.length === 0 && (
            <div className={styles.symbolEmpty}>
              {term && !everywhere ? <div className={styles.chip} onClick={() => setEverywhere(true)}>{t("Search all libraries")}</div>
                : term ? t("No symbol matches.") : t("This library has no symbols.")}
            </div>
          )}
        </Scrollable>
      </div>
    </div>
    </div>
  );
};

/** The symbol part's button: the chosen symbol and its name; asks the designer to open the picker. */
const SymbolField = (props: { item: Item; theme?: Theme; libraries: Record<string, SymbolLibrary>; onOpen: () => void }) => {
  const renderer = useRenderer();
  const sym = symbolOf(props.libraries, props.item.symbol);
  const lib = props.item.symbol ? String(props.item.symbol).split("/")[0] : "";
  const own = renderer && props.theme ? renderer.symbolLibraries(props.theme.id) : [];
  const missing = !!props.item.symbol && !sym;
  return (
    <Field label={t("Symbol")}>
      <div className={styles.symbolButton} onClick={props.onOpen}>
        <SymbolIcon symbol={sym} size={28} />
        <div className={styles.symbolButtonText}>
          {sym ? sym.name : missing ? tf("Missing: needs the \"{lib}\" library", { lib }) : t("Choose symbol…")}
          {sym && !own.includes(lib) && <span className={styles.symbolButtonLib}> · {props.libraries[lib]?.name ?? lib}</span>}
        </div>
      </div>
    </Field>
  );
};

/** Extra space around a part (sign units), added to the usual spacing: top, bottom, left, right. */
const MarginField = (props: { item: Item; onChange: (patch: Partial<Item>) => void }) => {
  const m = props.item.margin ?? {};
  const side = (key: string, label: string) => (
    <div className={styles.marginSide}>
      <div className={styles.marginLabel}>{label}</div>
      <NumberBox compact value={m[key] ?? 0} min={-40} onChange={(v) => {
        const next = { ...m, [key]: v || undefined };
        props.onChange({ margin: Object.values(next).some((x) => x) ? next : undefined });
      }} />
    </div>
  );
  return (
    <Field label={t("Margin")}>
      <div className={styles.marginGrid}>
        <div className={styles.inlineRow}>{side("top", t("Top"))}{side("bottom", t("Bottom"))}</div>
        <div className={styles.inlineRow}>{side("left", t("Left"))}{side("right", t("Right"))}</div>
      </div>
    </Field>
  );
};

function ItemProperties(props: {
  item: Item; onChange: (patch: Partial<Item>) => void; focusToken: number; inRow: boolean; theme?: Theme; libraries: Record<string, SymbolLibrary>;
  /** The sign's design format (its arrows: 1.0's, or the theme's libraries'). */
  version?: number;
  /** Opens the symbol picker for this part. */
  onPickSymbol: () => void;
}) {
  const { item, onChange, theme } = props;
  const renderer = useRenderer();
  const def = theme?.def;
  const place = (
    <>
      {!props.inRow && <PlaceField item={item} onChange={onChange} />}
      <MarginField item={item} onChange={onChange} />
    </>
  );
  const fonts: Option[] = (def?.fonts ?? []).filter((f) => !f.hidden).map((f) => ({ value: f.family, label: f.label ?? f.family, style: { fontFamily: f.family } }));
  const networks: Option[] = Object.entries(def?.shields ?? {}).map(([id, s]) => ({ value: id, icon: <ThemeShieldIcon theme={theme} network={id} />, title: s.label ?? id }));
  const schemes: Option[] = Object.entries(def?.schemes ?? {}).map(([id, s]) => ({
    value: id, title: s.label ?? id, label: " ", style: { backgroundColor: s.bg, minWidth: "28rem", border: `2rem solid ${s.fg}` },
  }));
  switch (item.type) {
    case "text":
      return (
        <>
          <Field label={t("Text")}><TextBox value={item.text ?? ""} focusToken={props.focusToken} onChange={(text) => onChange({ text })} /></Field>
          <Field label={t("Letter height")}><NumberBox value={item.height ?? def?.defaults?.textHeight ?? 16} onChange={(height) => onChange({ height })} /></Field>
          <Field label={t("Letter spacing")}><NumberBox value={item.tracking ?? 0} step={2} min={-20} onChange={(tracking) => onChange({ tracking: tracking || undefined })} /></Field>
          {fonts.length > 1 && <Field label={t("Font")}><Select options={fonts} value={item.font ?? def?.defaults?.font ?? ""} onChange={(font) => onChange({ font })} /></Field>}
          <PartColorField item={item} theme={theme} onChange={onChange} />
          {place}
        </>
      );
    case "shield":
      return (
        <>
          <Field label={t("Route")}><Choice options={networks} perRow={5} value={item.network} onChange={(network) => onChange({ network })} /></Field>
          <Field label={t("Number")}><TextBox value={String(item.number ?? "")} focusToken={props.focusToken} onChange={(number) => onChange({ number })} /></Field>
          <Field label={t("Size")}><NumberBox value={item.height ?? def?.defaults?.shieldHeight ?? 36} step={2} onChange={(height) => onChange({ height })} /></Field>
          {place}
        </>
      );
    case "arrow": {
      // The theme's arrow styles and their directions come from its symbol libraries (through the renderer).
      const library = item.library as string | undefined;
      const styles_ = renderer && theme ? renderer.arrowStyles(theme.id, library ? 2 : props.version, library) : [];
      const sources: Option[] = [{ value: "", label: t("This theme's") }, ...Object.values(props.libraries)
        .filter((l) => Object.keys(l.arrows ?? {}).length > 0).sort((a, b) => a.name.localeCompare(b.name))
        .map((l) => ({ value: l.id, label: l.name, title: l.description }))];
      const version = library ? 2 : props.version;
      const styleId = arrowStyleOf(item);
      const arrowStyle = styles_.find((s) => s.id === styleId) ?? styles_[0];
      const directionsOf = (id: string) => styles_.find((s) => s.id === id)?.directions ?? [];
      // An arrow already on the sign stays selectable even where its style does not offer it.
      const directions = [...(arrowStyle?.directions ?? [])];
      if (item.direction && !directions.includes(item.direction)) directions.push(item.direction);
      return (
        <>
          <Field label={t("Arrows from")}>
            <Select options={sources} value={library ?? ""} onChange={(lib) => {
              // The new set's styles and directions: keep the arrow's where they exist, otherwise its first.
              const next = renderer && theme ? renderer.arrowStyles(theme.id, lib ? 2 : props.version, lib || undefined) : [];
              const st = next.find((s) => s.id === arrowStyleOf(item)) ?? next[0];
              onChange({ library: lib || undefined, style: st?.id || undefined, direction: st?.directions.includes(item.direction ?? "") ? item.direction : st?.directions[0] });
            }} />
          </Field>
          <Field label={t("Style")}>
            <Choice options={styles_.map((s) => ({
              value: s.id, label: s.label, title: s.title,
              icon: <ThemeArrowIcon theme={theme!.id} style={s.id} direction={s.directions.includes("up") ? "up" : s.directions[0]} version={version} library={library} size={14} />,
            }))}
              value={styleId} onChange={(style) => onChange({
                style: style || undefined,
                // Styles share few directions: keep the arrow's if the new style has it, otherwise its first.
                direction: directionsOf(style).includes(item.direction ?? "") ? item.direction : directionsOf(style)[0],
              })} />
          </Field>
          <Field label={t("Direction")}>
            <Choice options={directions.map((a) => ({ value: a, icon: <ThemeArrowIcon theme={theme!.id} style={styleId} direction={a} version={version} library={library} box={arrowStyle?.box} />, title: a }))}
              perRow={arrowStyle?.perRow ?? 4} value={item.direction ?? "up"} onChange={(direction) => onChange({ direction })} />
          </Field>
          <Field label={t("Size")}><NumberBox value={item.height ?? def?.defaults?.arrowHeight ?? 24} step={2} onChange={(height) => onChange({ height })} /></Field>
          <PartColorField item={item} theme={theme} onChange={onChange} />
          {place}
        </>
      );
    }
    case "row":
      return (
        <>
          <Field label={t("Line up parts by their")}><Choice options={ROW_ALIGN()} value={item.align ?? "center"} onChange={(align) => onChange({ align })} /></Field>
          <Field label={t("Spacing")}><Choice options={ROW_SPACING()} value={item.justify ?? ""} onChange={(justify) => onChange({ justify: justify || undefined })} /></Field>
          {place}
        </>
      );
    case "column":
      return (
        <>
          <Field label={t("Align within the column")}><Choice options={ALIGN} value={item.align ?? "start"} onChange={(align) => onChange({ align })} /></Field>
          {place}
        </>
      );
    case "chip":
      return (
        <>
          <BoxColorField label={t("Chip color")} item={item} theme={theme} onChange={onChange} />
          <Field label={t("Border")}><Choice options={[{ value: "no", label: t("None") }, { value: "yes", label: t("Legend color") }]} value={item.border ? "yes" : "no"}
            onChange={(v) => onChange({ border: v === "yes" ? true : undefined })} /></Field>
          <Field label={t("Width")}><Choice options={CHIP_SPAN()} value={item.span ?? ""} onChange={(span) => onChange({ span: span || undefined })} /></Field>
          <Field label={t("Align within the chip")}><Choice options={ALIGN} value={item.align ?? "center"} onChange={(align) => onChange({ align })} /></Field>
          {place}
        </>
      );
    case "panel":
      return (
        <>
          <BoxColorField label={t("Panel color")} item={item} theme={theme} onChange={onChange} />
          <Field label={t("Position")}><Choice options={PANEL_SIDE()} value={item.side ?? ""} onChange={(side) => onChange({ side: side || undefined })} /></Field>
          <Field label={t("Align within the panel")}><Choice options={ALIGN} value={item.align ?? "center"} onChange={(align) => onChange({ align })} /></Field>
        </>
      );
    case "symbol":
      return (
        <>
          <SymbolField item={item} theme={theme} libraries={props.libraries} onOpen={props.onPickSymbol} />
          <Field label={t("Size")}><NumberBox value={item.height ?? def?.defaults?.shieldHeight ?? 36} step={2} onChange={(height) => onChange({ height })} /></Field>
          <PartColorField item={item} theme={theme} onChange={onChange} />
          <Field label={t("Display")}><Choice options={SYMBOL_DISPLAY()} value={item.box ? "box" : ""} onChange={(v) => onChange({ box: v === "box" ? item.box ?? {} : undefined })} /></Field>
          {item.box && (
            <>
              <ColorField label={t("Box background")} value={item.box.background} theme={theme} defaultLabel={t("None")} defaultTitle={t("No background: the sign shows through")}
                onChange={(background) => onChange({ box: { ...item.box, background } })} />
              <Field label={t("Box border")}><Choice options={BOX_BORDER()} value={item.box.border === false ? "none" : ""}
                onChange={(v) => onChange({ box: { ...item.box, border: v === "none" ? false : undefined } })} /></Field>
              {item.box.border !== false && (
                <ColorField label={t("Border color")} value={item.box.borderColor} theme={theme} defaultLabel={t("Legend")} defaultTitle={t("The sign's legend color")}
                  onChange={(borderColor) => onChange({ box: { ...item.box, borderColor } })} />
              )}
            </>
          )}
          {place}
        </>
      );
    case "diagram":
      return (
        <>
          <Field label={t("Shape")}><Choice options={DIAGRAM_SHAPES()} perRow={3} value={item.shape ?? "exit-left"} onChange={(shape) => onChange({ shape })} /></Field>
          {item.shape === "roundabout" ? (
            <Field label={t("Exits")}>
              <div className={styles.choices}>
                {ROUNDABOUT_EXITS.map((o) => {
                  const exits: string[] = item.exits ?? ["left", "up", "right"];
                  const on = exits.includes(o.value);
                  return (
                    <div key={o.value} title={o.title} className={on ? styles.choiceOn : styles.choice}
                      onClick={() => onChange({ exits: on ? exits.filter((x) => x !== o.value) : [...exits, o.value] })}>{o.icon}</div>
                  );
                })}
              </div>
            </Field>
          ) : (
            <div className={styles.inlineRow}>
              <Field label={t("Through lanes")}><NumberBox compact value={item.lanes ?? 2} min={1} max={6} onChange={(lanes) => onChange({ lanes })} /></Field>
              <div style={{ width: "12rem" }} />
              <Field label={t("Exit lanes")}><NumberBox compact value={item.exitLanes ?? 1} min={1} max={4} onChange={(exitLanes) => onChange({ exitLanes })} /></Field>
            </div>
          )}
          {item.shape !== "roundabout" && item.shape !== "fork" && (
            <Field label={t("Exit lanes from through lanes")}>
              <Choice options={SHARED_LANES()} value={item.sharedLanes != null ? String(item.sharedLanes) : (item.exitLanes ?? 1) === 1 ? "1" : ""}
                onChange={(v) => onChange({ sharedLanes: Number(v) })} />
            </Field>
          )}
          <Field label={t("Size")}><NumberBox value={item.height ?? 50} step={2} onChange={(height) => onChange({ height })} /></Field>
          <PartColorField item={item} theme={theme} onChange={onChange} />
          {place}
        </>
      );
    case "led": {
      const lines: string[] = item.lines ?? [""];
      const setLine = (k: number, text: string) => onChange({ lines: lines.map((l, i) => (i === k ? text : l)) });
      return (
        <>
          <Field label={t("Lines")}>
            {lines.map((line, k) => (
              <div key={k} className={styles.inlineRow} style={{ marginBottom: "3rem" }}>
                <div style={{ width: "200rem" }}><TextBox value={line} focusToken={k === 0 ? props.focusToken : undefined} onChange={(t) => setLine(k, t)} /></div>
                {lines.length > 1 && <div className={styles.rowDelete} title={t("Remove this line")} onClick={() => onChange({ lines: lines.filter((_, i) => i !== k) })}>✕</div>}
              </div>
            ))}
            <div className={styles.chip} style={{ alignSelf: "flex-start" }} onClick={() => onChange({ lines: [...lines, ""] })}>{t("Add line")}</div>
          </Field>
          <Field label={t("Letter height")}><NumberBox value={item.height ?? 12} onChange={(height) => onChange({ height })} /></Field>
          <LedColorField label={t("LED color")} value={item.color} onChange={(color) => onChange({ color })} />
          {place}
        </>
      );
    }
    case "divider":
      return (
        <>
          <PartColorField item={item} theme={theme} onChange={onChange} />
          {place}
        </>
      );
    case "tab":
      return (
        <>
          <Field label={t("Position")}><Choice options={TAB_SIDE()} value={item.align ?? "right"} onChange={(align) => onChange({ align })} /></Field>
          <Field label={t("Color")}><Choice options={[{ value: "", label: t("Sign") }, ...schemes]} perRow={7} value={item.scheme ?? ""} onChange={(scheme) => onChange({ scheme: scheme || undefined })} /></Field>
        </>
      );
    default:
      return place;
  }
}

// ------------------------------------------------------------------ road picked from the map

/** The road last acted on (auto colors): each road read applies once, even when the designer opens again later. */
let handledRoadNonce = "";

const roadClassLabel = (c: RoadClass): string =>
  ({ highway: t("Highway"), major: t("Major road"), medium: t("Medium road"), local: t("Local street"), other: t("Other road") })[c] ?? t("Other road");

/** Saudi template categories (file name "NN-MM-id") that suit a road class, best first. */
const SUGGESTED_CATEGORIES: Record<RoadClass, string[]> = { highway: ["02", "01"], major: ["01", "03"], medium: ["01", "03"], local: ["03"], other: [] };
const templateCategory = (id: string): string | null => /(?:^|[/:])(\d{2})-\d{2}-/.exec(id)?.[1] ?? null;
const suggestedTemplates = (templates: Template[], roadClass: RoadClass, max = 6): Template[] => {
  const order = SUGGESTED_CATEGORIES[roadClass] ?? [];
  return templates
    .map((tpl) => ({ tpl, rank: order.indexOf(templateCategory(tpl.id) ?? "") }))
    .filter((x) => x.rank >= 0)
    .sort((a, b) => a.rank - b.rank || a.tpl.id.localeCompare(b.tpl.id))
    .slice(0, max)
    .map((x) => x.tpl);
};

/** Saudi sign color for a road class: green on highways and major roads, blue in town; null leaves the color. */
const schemeForRoad = (roadClass: RoadClass): string | null =>
  roadClass === "highway" || roadClass === "major" ? "green" : roadClass === "medium" || roadClass === "local" ? "blue" : null;

/** Whether the sign is still a new blank one (one "Destination" text in the theme), whatever its color. */
const isBlankDesign = (design: Design, theme?: Theme): boolean => {
  const extra = Object.keys(design).filter((k) => design[k] !== undefined && !["formatVersion", "theme", "scheme", "background", "foreground", "items"].includes(k));
  if (extra.length > 0 || (design.theme ?? "us") !== (theme?.id ?? "us")) return false;
  const items = design.items ?? [];
  return items.length === 1 && items[0].type === "text" && (items[0].text === t("Destination") || items[0].text === "Destination")
    && Object.keys(items[0]).every((k) => k === "type" || k === "text" || k === "height");
};

/** Path of the first part (depth first) that matches, or null. */
const findPath = (items: Item[], match: (i: Item) => boolean, prefix = ""): string | null => {
  for (let k = 0; k < items.length; k++) {
    const path = prefix ? `${prefix}.${k}` : String(k);
    if (match(items[k])) return path;
    if (items[k].items) {
      const inner = findPath(items[k].items, match, path);
      if (inner) return inner;
    }
  }
  return null;
};

const isSpeedShield = (i: Item | null | undefined) => !!i && i.type === "shield" && i.network === "SPD";

export type DesignerAction = { label: string; primary?: boolean; onSelect: () => void; title?: string };

/**
 * The sign designer: one sign at a time. Templates on the left, live preview and sign options in the middle,
 * parts on the right. Everything offered (fonts, colors, route plates, parts) comes from the sign's theme; the
 * actions row depends on what the sign is for (new sign, editing a placed one, a gantry sign).
 */
export const Designer = (props: {
  design: Design;
  setDesign: (d: Design) => void;
  /** Loads a fresh design (template or blank sign), asking first if the current one has unsaved changes; <then> runs once loaded. */
  onLoad: (d: Design, then: () => void) => void;
  onTemplateSaved: () => void;
  templates: Template[];
  themes: Theme[];
  /** The symbol libraries (arrows, symbols), by id. */
  libraries: Record<string, SymbolLibrary>;
  title: string;
  actions: DesignerAction[];
  onClose: () => void;
  /** Drawn over the dialog's content (the discard-changes question). */
  overlay?: ReactNode;
  /** More options under the sign's own (a placed roadside sign's lights). */
  extra?: ReactNode;
}) => {
  const { design, setDesign } = props;
  const [selected, setSelected] = useState<string | null>(null);
  // The symbol picker, open for the selected symbol part.
  const [picking, setPicking] = useState(false);
  const renderer = useRenderer();
  useEffect(() => setPicking(false), [selected]);
  // For the in-game tests: select a part the way a click does, open the symbol picker, read the design (test builds only).
  useEffect(() => {
    if (!__SIGNBUILDER_TESTS__) return;
    (window as any).__signBuilderSASelect = setSelected;
    (window as any).__signBuilderSAPickSymbol = () => setPicking(true);
    return () => { delete (window as any).__signBuilderSASelect; delete (window as any).__signBuilderSAPickSymbol; };
  }, []);
  useEffect(() => { if (__SIGNBUILDER_TESTS__) (window as any).__signBuilderSADesign = design; }, [design]);
  // Bumped when a part is picked on the preview, so its text box takes focus with the caret at the end.
  const [focusToken, setFocusToken] = useState(0);
  const theme = themeOf(props.themes, design);
  const def = theme?.def;
  const custom = !!design.background;
  // Font for Arabic letters on this sign (C#: Core/SharedFonts.cs adds the choices to every theme). Latin letters
  // keep each part's font; Arabic ones fall back to this font, so mixed text never shows empty boxes.
  const arabicFonts: Option[] = [
    ...(def?.fonts ?? []).filter((f) => f.arabic).map((f) => ({ value: f.family, label: f.label ?? f.family, style: { fontFamily: f.family } })),
    { value: "Noto Sans Arabic", label: t("Noto (game)"), style: { fontFamily: "Noto Sans Arabic" } },
  ];
  const widthMode = design.minLanes ? "lanes" : design.minWidth ? "custom" : "auto";
  const labels = Object.fromEntries(Object.entries(def?.shields ?? {}).map(([id, s]) => [id, s.label ?? id]));

  const item = selected != null ? getItem(design, selected) : null;
  const update = (patch: Partial<Item>) => { if (selected != null) setDesign(updateItem(design, selected, patch)); };
  // <insert> "first": the preset goes at the top of the sign (a panel across its top, like an E-ZPass panel).
  const add = (made: Item, insert?: "first") => {
    if (insert === "first") {
      const d = clone(design);
      d.items.unshift(clone(made));
      setDesign(d);
      setSelected("0");
      return;
    }
    // Tabs and panels belong to the sign itself, so they go at its top level (a panel after the selected part's top-level part).
    const anchor = made.type === "tab" ? null : made.type === "panel" ? selected?.split(".")[0] ?? null : selected;
    const r = insertItem(design, anchor, clone(made));
    setDesign(r.design);
    setSelected(r.path);
  };
  const templates = props.templates.filter((t) => (t.design.theme ?? "us") === (theme?.id ?? "us"));

  // ---- the road picked from the map (or the one last placed on): names, class and speed for the sign
  const toolMode = useValue(toolMode$);
  const roadJson = useValue(roadInfo$);
  const road: RoadInfo | null = useMemo(() => parseRoadInfo(roadJson), [roadJson]);
  const pickingRoad = toolMode === "pickInfo";
  // Picking needs the tool: not while a gantry is being arranged (one of its signs is open here).
  const canPick = toolMode === "off" || toolMode === "pickInfo";
  const blank = isBlankDesign(design, theme);
  const saudi = theme?.id === "sa";
  const suggestions = road && saudi && blank ? suggestedTemplates(templates, road.roadClass) : [];
  const streetName = road ? road.streetAr || road.street : "";
  const speed = road ? signSpeed(road.speedLimit) : 0;
  const hasSpeedShield = !!def?.shields?.SPD;

  // A new road read: a blank Saudi sign takes the color of its class (green highways and major roads, blue in town).
  useEffect(() => {
    if (!road || road.nonce === handledRoadNonce) return;
    handledRoadNonce = road.nonce;
    if (!saudi || !blank || design.board) return;
    const scheme = schemeForRoad(road.roadClass);
    if (!scheme || !def?.schemes?.[scheme] || (design.scheme === scheme && !design.background)) return;
    const d: Design = { ...design, scheme };
    delete d.background;
    delete d.foreground;
    setDesign(d);
  }, [road?.nonce]);

  /** Puts <text> in the selected text part (in place of its text, or after it when <append>), or adds a new text part. */
  const putText = (text: string, append: boolean) => {
    if (!text) return;
    if (item?.type === "text" && selected != null) {
      const current = String(item.text ?? "");
      update({ text: append && current.trim() ? `${current.replace(/\s+$/, "")} ${text}` : text });
      return;
    }
    add({ type: "text", text, height: def?.defaults?.textHeight ?? 16 });
  };
  /** The selected speed limit plate, else the sign's first one, says the road's limit; a sign without one gets one. */
  const applySpeed = () => {
    if (!speed) return;
    const path = isSpeedShield(item) ? selected : findPath(design.items, isSpeedShield);
    if (path != null) {
      setDesign(updateItem(design, path, { number: String(speed) }));
      setSelected(path);
    } else if (hasSpeedShield) {
      add({ type: "shield", network: "SPD", number: String(speed), height: def?.defaults?.shieldHeight });
    }
  };

  const fromTheMap = (
    <>
      <div className={styles.heading}>{t("From the map")}</div>
      <div className={styles.inlineRow}>
        <div className={styles.chip} style={canPick ? undefined : { opacity: 0.45 }}
          title={canPick ? t("Click a road in the city to read its street and district names, class and speed limit") : t("Finish placing the gantry first")}
          onClick={() => { if (canPick) pickRoad(); }}>{t("Pick a road")}</div>
        {!road && <div className={styles.hintInline}>{t("Signs may also say {street}, {district} and {speed}: they show the road they are placed on.")}</div>}
      </div>
      {road && (
        <>
          <div className={styles.hint}>
            {[
              tf("Street: {name}", { name: streetName || t("(unnamed)") }),
              tf("District: {name}", { name: road.district || t("(none)") }),
              roadClassLabel(road.roadClass),
              road.speedLimit > 0 ? tf("{n} km/h", { n: road.speedLimit }) : t("Speed unknown"),
              road.lanes > 0 ? tf("{n} lanes each way", { n: road.lanes }) : "",
            ].filter((s) => s).join(" · ")}
          </div>
          <Palette entries={[
            ...(streetName ? [{ label: t("Use street name"), onClick: () => putText(streetName, false) }] : []),
            ...(road.district ? [{ label: t("Use district name"), onClick: () => putText(road.district, false) }] : []),
            { label: t("Insert {street}"), onClick: () => putText(ROAD_TOKENS.street, true) },
            { label: t("Insert {district}"), onClick: () => putText(ROAD_TOKENS.district, true) },
            ...(speed && (hasSpeedShield || findPath(design.items, isSpeedShield)) ? [{ label: tf("Apply speed limit ({n})", { n: speed }), onClick: applySpeed }] : []),
          ]} />
          {suggestions.length > 0 && (
            <>
              <div className={styles.label}>{t("Suggested for this road")}</div>
              {Array.from({ length: Math.ceil(suggestions.length / 3) }, (_, r) => (
                <div key={r} className={styles.cardRow}>
                  {suggestions.slice(r * 3, r * 3 + 3).map((tpl) => (
                    <div key={tpl.id} className={styles.card} onClick={() => props.onLoad(clone(tpl.design), () => setSelected(null))}>
                      <div className={styles.thumb}>
                        <SignPreview design={tpl.design} interactive={false} maxWidth={124} maxHeight={64} padding={6} />
                      </div>
                      <div className={styles.cardName}>{tpl.name}</div>
                    </div>
                  ))}
                </div>
              ))}
            </>
          )}
        </>
      )}
    </>
  );

  // While a road is being picked the world must be clickable: only a small bar stays on screen (the sign and its
  // selection are kept for when the pick ends).
  if (pickingRoad) {
    return (
      <Dialog dock="top" title={t("Pick a road")} className={styles.compact} onClose={() => cancelPickRoad()}>
        <div className={styles.compactBody}>
          <div className={styles.status}>{t("Click a road to read its street, district and speed limit.")}</div>
          <div className={styles.hint}>{t("Right-click or Escape cancels.")}</div>
          <div className={styles.chip} onClick={() => cancelPickRoad()}>{t("Cancel")}</div>
        </div>
        {props.overlay}
      </Dialog>
    );
  }

  return (
    <Dialog dock="center" title={props.title} className={styles.panel} onClose={props.onClose}>
      <div className={styles.body}>
        <div className={styles.column}>
          <div className={styles.heading}>{t("Theme")}</div>
          <div className={styles.inlineRow}>
            <Select options={[...props.themes].sort((a, b) => a.name.localeCompare(b.name)).map((t) => ({ value: t.id, label: t.name, title: t.def.description }))} value={theme?.id ?? "us"}
              onChange={(id) => { const to = props.themes.find((t) => t.id === id); if (to) { setDesign(convertDesign(design, to)); setSelected(null); } }} />
            <div className={styles.chip} style={{ margin: "0 0 0 6rem" }} title={t("Add your own themes here (one folder each); they appear when the Sign Builder is opened again")}
              onClick={() => openThemesFolder()}>{t("Folder")}</div>
          </div>
          <div className={styles.heading}>{t("Templates")}</div>
          <TemplateLibrary templates={templates} current={design}
            onPick={(t) => props.onLoad(clone(t.design), () => setSelected(null))} onSaved={props.onTemplateSaved} />
          <div className={styles.chip} style={{ alignSelf: "flex-start", marginTop: "6rem" }} onClick={() => props.onLoad(blankFor(theme), () => setSelected(null))}>{t("New blank sign")}</div>
        </div>

        <div className={styles.center}>
          {(design.formatVersion ?? 1) < DESIGN_FORMAT && usesArrows(design) && (
            <div className={styles.legacyNote}>
              <div className={styles.legacyNoteText}>{t("This sign keeps the arrows it was made with (Sign Builder 1.0).")}</div>
              <div className={styles.chip} title={t("Draw its arrows and road diagrams with the theme's current arrows")}
                onClick={() => setDesign({ ...design, formatVersion: DESIGN_FORMAT })}>{t("Use the new arrows")}</div>
            </div>
          )}
          <div className={styles.preview}>
            <SignPreview design={design} selected={selected}
              onSelect={(p) => { setSelected(p); if (p) setFocusToken(focusToken + 1); }} maxWidth={560} maxHeight={270} />
          </div>
          <Scrollable vertical trackVisibility="scrollable" className={styles.optionsScroll}>
          <div className={styles.signOptions}>
            {fromTheMap}
            <Field label={t("Sign type")}>
              <Choice options={[{ value: "", label: t("Standard") }, { value: "led", label: t("Message board"), title: t("A black LED panel: lit dot-matrix lettering") }]}
                value={design.board ?? ""} onChange={(board) => {
                  const d: Design = { ...design, board: board || undefined };
                  if (!board) delete d.board;
                  delete d.foreground;
                  delete d.background;
                  setDesign(d);
                }} />
            </Field>
            {design.board === "led" && (
              <LedColorField label={t("LED color")} value={design.foreground}
                onChange={(c) => { const d: Design = { ...design, foreground: c }; if (!c) delete d.foreground; setDesign(d); }} />
            )}
            {design.board !== "led" && (
            <Field label={t("Color")}>
              <Swatches schemes={def?.schemes ?? {}} scheme={design.scheme ?? def?.defaults?.scheme ?? ""} custom={custom}
                onScheme={(scheme) => { const d: Design = { ...design, scheme }; delete d.background; delete d.foreground; setDesign(d); }}
                onCustom={() => setDesign({ ...design, background: design.background ?? "#2d6a4f", foreground: design.foreground ?? "#ffffff" })} />
            </Field>
            )}
            {arabicFonts.length > 0 && (
              <Field label={t("Arabic font")}>
                <Choice options={arabicFonts} perRow={3}
                  value={design.arabicFont ?? def?.defaults?.arabicFont ?? arabicFonts[0].value}
                  onChange={(arabicFont) => setDesign({ ...design, arabicFont })} />
              </Field>
            )}
            {/* The sign's spacing in percent of the theme's (100 = as the theme draws it). */}
            <Field label={t("Line spacing")}>
              <NumberBox value={design.lineSpacing ?? 100} step={10} min={0} max={400} unit="%"
                onChange={(v) => setDesign({ ...design, lineSpacing: v === 100 ? undefined : v })} />
            </Field>
            <Field label={t("Spacing between parts")}>
              <NumberBox value={design.partSpacing ?? 100} step={10} min={0} max={400} unit="%"
                onChange={(v) => setDesign({ ...design, partSpacing: v === 100 ? undefined : v })} />
            </Field>
            {custom && design.board !== "led" && (
              <div className={styles.customColors}>
                <Field label={t("Background")}><ColorBox value={design.background} onChange={(background) => setDesign({ ...design, background })} /></Field>
                <Field label={t("Legend")}><ColorBox value={design.foreground ?? "#ffffff"} onChange={(foreground) => setDesign({ ...design, foreground })} /></Field>
              </div>
            )}
            <Field label={t("Add part")}>
              <Palette entries={baseParts(theme).map((p) => ({ label: p.label, onClick: () => add(p.item) }))} />
            </Field>
            {(def?.presets ?? []).length > 0 && (
              <Field label={t("Add preset")}>
                <Palette entries={def!.presets!.map((p) => ({ label: p.label, onClick: () => add(p.item, p.insert) }))} />
              </Field>
            )}
            <Field label={t("Width")}>
              <div className={styles.inlineRow}>
                <Choice options={[{ value: "auto", label: t("Fit text") }, { value: "lanes", label: t("Lanes"), title: t("At least this many lanes wide (4 m lanes at 100% size)") },
                  { value: "custom", label: t("At least") }]} value={widthMode}
                  onChange={(v) => {
                    const d: Design = { ...design };
                    delete d.minLanes;
                    delete d.minWidth;
                    delete d.laneKind;
                    if (v === "lanes") d.minLanes = 1;
                    if (v === "custom") d.minWidth = 96;
                    setDesign(d);
                  }} />
                {widthMode === "lanes" && <NumberBox compact value={design.minLanes} min={1} onChange={(minLanes) => setDesign({ ...design, minLanes })} />}
                {widthMode === "lanes" && <Choice options={LANE_KIND()} value={design.laneKind ?? ""} onChange={(laneKind) => setDesign({ ...design, laneKind: laneKind || undefined })} />}
                {widthMode === "custom" && <NumberBox compact value={design.minWidth} step={12} onChange={(minWidth) => setDesign({ ...design, minWidth })} />}
              </div>
            </Field>
            <Field label={t("Align lines")}><Choice options={ALIGN} value={design.align ?? "start"} onChange={(align) => setDesign({ ...design, align })} /></Field>
            {props.extra}
          </div>
          </Scrollable>
          <div className={styles.actions}>
            {props.actions.some((a) => a.primary) && props.actions.length > 2 && <div className={styles.placeAs}>{t("Place as:")}</div>}
            {props.actions.map((a, i) => a.primary ? (
              <div key={a.label} className={styles.primaryButton} title={a.title} onClick={a.onSelect}>{a.label}</div>
            ) : (
              <div key={a.label} className={styles.chip} title={a.title} style={{ alignSelf: "center", marginLeft: i === 0 ? "0rem" : "6rem" }} onClick={a.onSelect}>{a.label}</div>
            ))}
          </div>
        </div>

        <div className={styles.column}>
          <Scrollable vertical trackVisibility="scrollable" className={styles.partsScroll}>
          <div className={styles.heading}>{t("Parts")}</div>
          <PartsList design={design} selected={selected} onSelect={setSelected} labels={labels}
            onChange={(d, path) => { setDesign(d); setSelected(path); }}
            onDelete={(path) => { setDesign(removeItem(design, path)); setSelected(null); }}
            onDuplicate={(path) => { const r = duplicateItem(design, path); setDesign(r.design); setSelected(r.path); }} />
          {item && (
            <>
              <div className={styles.heading}>{kindOf(item, labels)}</div>
              <ItemProperties item={item} onChange={update} focusToken={focusToken} theme={theme} libraries={props.libraries} version={design.formatVersion ?? 1}
                onPickSymbol={() => setPicking(true)}
                inRow={selected!.includes(".") && getItem(design, selected!.split(".").slice(0, -1).join("."))?.type === "row"} />
            </>
          )}
          </Scrollable>
        </div>
        {picking && item?.type === "symbol" && (
          <SymbolPicker libraries={props.libraries} own={renderer && theme ? renderer.symbolLibraries(theme.id) : []} value={item.symbol}
            themeId={theme?.id ?? "us"} version={design.formatVersion ?? 1}
            onPick={(symbol) => update({ symbol })} onClose={() => setPicking(false)}
            onPickArrow={(a) => update({
              // The part becomes an arrow, at the symbol's size.
              type: "arrow", library: a.library, style: a.style || undefined, direction: a.direction, symbol: undefined, box: undefined, color: item.color,
            })} />
        )}
        {props.overlay}
      </div>
    </Dialog>
  );
};
