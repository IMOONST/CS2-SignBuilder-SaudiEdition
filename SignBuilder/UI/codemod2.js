// Second pass: strings inside conditionals, template literals and constants that codemod.js could not reach.
const fs = require("fs"), path = require("path");
const dir = path.join(__dirname, "src/mods");
const R = {
  "Arrange.tsx": [
    [`title={info.replacing ? "Edit gantry" : "Gantry"}`, `title={info.replacing ? t("Edit gantry") : t("Gantry")}`],
    [`{info.replacing ? "Save" : "Build"}`, `{info.replacing ? t("Save") : t("Build")}`],
    [`title={l[1] ? "A lane this side's signs face" : "Traffic the other way"}`, `title={l[1] ? t("A lane this side's signs face") : t("Traffic the other way")}`],
  ],
  "Controls.tsx": [[`props.custom.title ?? "Any color"`, `props.custom.title ?? t("Any color")`]],
  "Designer.tsx": [
    [`const arrowCategory = (label: string) => "Arrows: " + label;`, `const arrowCategory = (label: string) => tf("Arrows: {name}", { name: label });`],
    [`entries.map((e) => e.sym.category ?? "Other")`, `entries.map((e) => e.sym.category ?? t("Other"))`],
    [`(e.sym.category ?? "Other") === current`, `(e.sym.category ?? t("Other")) === current`],
    [`: term ? "No symbol matches." : "This library has no symbols."}`, `: term ? t("No symbol matches.") : t("This library has no symbols.")}`],
    ['missing ? `Missing: needs the "${lib}" library` : "Choose symbol…"', 'missing ? tf("Missing: needs the \\"{lib}\\" library", { lib }) : t("Choose symbol…")'],
    [`{side("top", "Top")}{side("bottom", "Bottom")}`, `{side("top", t("Top"))}{side("bottom", t("Bottom"))}`],
    [`{side("left", "Left")}{side("right", "Right")}`, `{side("left", t("Left"))}{side("right", t("Right"))}`],
  ],
  "designModel.ts": [
    [`if (id === "ground") return "Roadside posts";`, `if (id === "ground") return t("Roadside posts");`],
    [`const t = STRUCTURES.find((x) => x.id === type)?.label ?? type;`, `const name = STRUCTURES.find((x) => x.id === type)?.label ?? type;`],
    ['const s = SPANS.find((x) => x.id === span)?.label.toLowerCase() ?? span ?? "";', 'const label = SPANS.find((x) => x.id === span)?.label;\n  const s = (label && (isArabic() ? label : label.toLowerCase())) ?? span ?? "";'],
    ['return `${t}, ${s}${side === "left" ? " (left side)" : ""}`;', 'return `${name}${isArabic() ? "، " : ", "}${s}${side === "left" ? " " + t("(left side)") : ""}`;'],
    ['case "shield": return `Shield "${item.number ?? ""}"`;', 'case "shield": return `${t("Shield")} "${item.number ?? ""}"`;'],
    ['case "symbol": return item.symbol ? `Symbol ${String(item.symbol).split("/").pop()!.replace(/-/g, " ")}` : "Symbol";', 'case "symbol": return item.symbol ? `${t("Symbol")} ${String(item.symbol).split("/").pop()!.replace(/-/g, " ")}` : t("Symbol");'],
    ['case "chip": return "Chip";', 'case "chip": return t("Chip");'],
    ['case "diagram": return `Diagram ${String(item.shape ?? "exit-left").replace("-", " ")}`;', 'case "diagram": return `${t("Diagram")} ${t(String(item.shape ?? "exit-left"))}`;'],
    ['case "led": return `LED "${(item.lines ?? [])[0] ?? ""}"`;', 'case "led": return `${t("LED")} "${(item.lines ?? [])[0] ?? ""}"`;'],
    ['case "panel": return item.side ? "Side panel" : "Panel";', 'case "panel": return item.side ? t("Side panel") : t("Panel");'],
    ['case "tab": return "Tab";', 'case "tab": return t("Tab");'],
    ['case "arrow": return `Arrow ${String(item.direction ?? "up").replace("-", " ")}`;', 'case "arrow": return `${t("Arrow")} ${t(String(item.direction ?? "up"))}`;'],
    ['case "row": return "Row";', 'case "row": return t("Row");'],
    ['case "column": return "Column";', 'case "column": return t("Column");'],
    ['case "divider": return "Divider";', 'case "divider": return t("Divider");'],
    ['case "text": return "Text";', 'case "text": return t("Text");'],
    ['case "shield": return "Shield";', 'case "shield": return t("Shield");'],
    ['case "symbol": return "Symbol";', 'case "symbol": return t("Symbol");'],
    ['case "diagram": return "Diagram";', 'case "diagram": return t("Diagram");'],
    ['case "led": return "LED text";', 'case "led": return t("LED text");'],
    ['case "arrow": return "Arrow";', 'case "arrow": return t("Arrow");'],
  ],
  "PlacementBar.tsx": [
    [`title={info.surface ? "Prop sign" : info.posts ? "Roadside sign" : "Place gantry"}`, `title={info.surface ? t("Prop sign") : info.posts ? t("Roadside sign") : t("Place gantry")}`],
    [`? "Right-drag to rotate; Page Up / Page Down or Ctrl + mouse wheel to raise and lower."`, `? t("Right-drag to rotate; Page Up / Page Down or Ctrl + mouse wheel to raise and lower.")`],
    [`: "Alt + mouse wheel: distance from the road. Ctrl + mouse wheel: height."}`, `: t("Alt + mouse wheel: distance from the road. Ctrl + mouse wheel: height.")}`],
    [`{info.surface ? "Height" : info.posts ? "Sign bottom" : info.free ? "Base" : "Height"}`, `{info.surface ? t("Height") : info.posts ? t("Sign bottom") : info.free ? t("Base") : t("Height")}`],
    [`{info.posts ? "From road" : "Posts from road"}`, `{info.posts ? t("From road") : t("Posts from road")}`],
    ['`${(info.bottom ?? 0).toFixed(2)} m`', '`${(info.bottom ?? 0).toFixed(2)} ${t("m")}`'],
    ['`${(info.gap ?? 0).toFixed(2)} m`', '`${(info.gap ?? 0).toFixed(2)} ${t("m")}`'],
  ],
  "SignEditor.tsx": [
    [`const LOSE_GANTRY = "Close without building? The gantry and its changes will be lost.";`, `const loseGantry = () => t("Close without building? The gantry and its changes will be lost.");`],
    [`confirm = "Discard") => {`, `confirm = t("Discard")) => {`],
    [`cancel="Keep editing"`, `cancel={t("Keep editing")}`],
    [`guard(unsaved, LOSE_GANTRY, close)`, `guard(unsaved, loseGantry(), close)`],
    [`guard(unsaved, "Cancel this gantry? Its changes will be lost.",`, `guard(unsaved, t("Cancel this gantry? Its changes will be lost."),`],
    [`guard(true, gantryUnsaved ? LOSE_GANTRY : "Close the gantry editor?", close, gantryUnsaved ? "Discard" : "Close")`, `guard(true, gantryUnsaved ? loseGantry() : t("Close the gantry editor?"), close, gantryUnsaved ? t("Discard") : t("Close"))`],
    [`let title = "Sign Builder";`, `let title = t("Sign Builder");`],
    [`title = "Edit roadside sign";`, `title = t("Edit roadside sign");`],
    ['title = `Edit gantry sign ${shown.index + 1}`;', 'title = tf("Edit gantry sign {n}", { n: shown.index + 1 });'],
    [`title = "Add a sign to the gantry";`, `title = t("Add a sign to the gantry");`],
    [`guard(dirty, "Discard your changes to this sign?", close)`, `guard(dirty, t("Discard your changes to this sign?"), close)`],
    [`guard(dirty, "Discard your changes to this sign?", () =>`, `guard(dirty, t("Discard your changes to this sign?"), () =>`],
    [`guard(dirty, "Discard this sign?", () =>`, `guard(dirty, t("Discard this sign?"), () =>`],
    [`inGantry ? LOSE_GANTRY : "Close the Sign Builder? Your changes to this sign will be lost."`, `inGantry ? loseGantry() : t("Close the Sign Builder? Your changes to this sign will be lost.")`],
    [`guard(true, "Close the Sign Builder?", close, "Close")`, `guard(true, t("Close the Sign Builder?"), close, t("Close"))`],
    [`guard(dirty, "Load this? Your changes to the current sign will be lost.",`, `guard(dirty, t("Load this? Your changes to the current sign will be lost."),`],
  ],
  "SignInfoSection.tsx": [
    [`const t = setTimeout(() => setArmed(false), 4000);\n    return () => clearTimeout(t);`, `const timer = setTimeout(() => setArmed(false), 4000);\n    return () => clearTimeout(timer);`],
    [`label={armed ? "Confirm delete" : "Delete"}`, `label={armed ? t("Confirm delete") : t("Delete")}`],
    ['`, ${props.signs} signs`', '`، ${tf("{n} signs", { n: props.signs })}`'],
    ['{`Sign Builder: ${summary}`}', '{`${t("Sign Builder")}: ${summary}`}'],
  ],
  "TemplateLibrary.tsx": [
    [`: "My sign" })`, `: tr("My sign") })`],
    [`{naming.id ? "Rename" : "Save"}`, `{naming.id ? tr("Rename") : tr("Save")}`],
    [`{search ? "No templates match." : "No templates yet."}`, `{search ? tr("No templates match.") : tr("No templates yet.")}`],
  ],
  "themeModel.ts": [
    [`item: { type: "text", text: "Destination", height: th }`, `item: { type: "text", text: t("Destination"), height: th }`],
    [`lines: ["ACCIDENT AHEAD"]`, `lines: [t("ACCIDENT AHEAD")]`],
    [`items: [{ type: "text", text: "Text", height: small }]`, `items: [{ type: "text", text: t("Text"), height: small }]`],
    [`items: [{ type: "text", text: "Destination", height: d?.defaults?.textHeight ?? 16 }]`, `items: [{ type: "text", text: t("Destination"), height: d?.defaults?.textHeight ?? 16 }]`],
  ],
  "ToolbarButton.tsx": [
    [`import { Button } from "cs2/ui";`, `import { Button } from "cs2/ui";\nimport { t } from "./l10n";`],
    [`tooltipLabel="Sign Builder"`, `tooltipLabel={t("Sign Builder")}`],
  ],
};
let bad = 0;
for (const [f, reps] of Object.entries(R)) {
  const file = path.join(dir, f);
  let s = fs.readFileSync(file, "utf8");
  for (const [a, b] of reps) {
    if (!s.includes(a)) { console.log("MISSING", f, a.slice(0, 70)); bad++; continue; }
    s = s.split(a).join(b);
  }
  // tf / isArabic imports where used.
  const need = ["t", ...(/\btf\(/.test(s) ? ["tf"] : []), ...(/\bisArabic\(/.test(s) ? ["isArabic"] : [])];
  if (f === "TemplateLibrary.tsx") need[0] = "t as tr";
  const imp = `import { ${need.join(", ")} } from "./l10n";`;
  if (/import \{[^}]*\} from "\.\/l10n";/.test(s)) s = s.replace(/import \{[^}]*\} from "\.\/l10n";/, imp);
  fs.writeFileSync(file, s);
}
console.log(bad ? `${bad} missing` : "all applied");
