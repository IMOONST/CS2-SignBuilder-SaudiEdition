// Lists every string passed to t()/tf() in the UI and reports which have no Arabic translation in ar.ts.
const fs = require("fs"), path = require("path");
const dir = path.join(__dirname, "src/mods");
const keys = new Set();
for (const f of fs.readdirSync(dir)) {
  if (/^ar(-.*)?.ts$/.test(f)) continue;
  const s = fs.readFileSync(path.join(dir, f), "utf8");
  const re = /\b(?:t|tr|tf)\(("(?:[^"\\]|\\.)*")/g;
  let m;
  while ((m = re.exec(s))) keys.add(JSON.parse(m[1]));
}
const all = [...keys].sort();
fs.writeFileSync(path.join(__dirname, "keys.json"), JSON.stringify(all, null, 1));
const ar = fs.readdirSync(dir).filter((f) => /^ar(-.*)?.ts$/.test(f)).map((f) => fs.readFileSync(path.join(dir, f), "utf8")).join("\n");
const have = new Set();
const re = /^\s*("(?:[^"\\]|\\.)*")\s*:/gm;
let m;
while ((m = re.exec(ar))) have.add(JSON.parse(m[1]));
const missing = all.filter((k) => !have.has(k));
console.log(`${all.length} keys, ${missing.length} untranslated`);
if (process.argv.includes("--list")) console.log(missing.join("\n"));
