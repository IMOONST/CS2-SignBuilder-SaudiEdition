// Wraps visible English strings with t(): JSX attributes, object label/title/hint props, JSX text nodes.
const fs = require("fs"), path = require("path");
const dir = path.join(__dirname, "src/mods");
const skip = new Set(["l10n.ts", "ar.ts", "bindings.ts", "Icons.tsx", "SignPreview.tsx"]);
const keys = new Set();
const ATTR = /\b(label|title|placeholder|hint|defaultLabel|defaultTitle|left|right)="([^"{}]*[A-Za-z][^"{}]*)"/g;
const PROP = /\b(label|title|hint)\s*:\s*"([^"\\]*[A-Za-z][^"\\]*)"/g;
const TEXT = /(?<![=\-])>(\s*)([A-Z0-9][^<>{}\n]*?[A-Za-z.!?)%][^<>{}\n]*?)(\s*)<\//g;
for (const f of fs.readdirSync(dir).filter(f => /\.(tsx?|ts)$/.test(f) && !skip.has(f))) {
  const file = path.join(dir, f);
  let s = fs.readFileSync(file, "utf8"), before = s, n = 0;
  s = s.replace(ATTR, (m, a, v) => { keys.add(v); n++; return `${a}={t(${JSON.stringify(v)})}`; });
  s = s.replace(PROP, (m, a, v) => { keys.add(v); n++; return `${a}: t(${JSON.stringify(v)})`; });
  if (f.endsWith(".tsx")) s = s.replace(TEXT, (m, a, v, b) => { keys.add(v.trim()); n++; return `>${a}{t(${JSON.stringify(v.trim())})}${b}</`; });
  if (s !== before) {
    if (!/from "\.\/l10n"/.test(s)) s = `import { t } from "./l10n";\n` + s;
    fs.writeFileSync(file, s);
  }
  console.log(f, n);
}
fs.writeFileSync(path.join(__dirname, "keys.json"), JSON.stringify([...keys].sort(), null, 1));
console.log("unique keys", keys.size);
