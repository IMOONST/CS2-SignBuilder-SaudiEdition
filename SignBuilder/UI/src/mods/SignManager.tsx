import { useValue } from "cs2/api";
import { BackConsumer } from "cs2/input";
import { Button, Scrollable } from "cs2/ui";
import { useEffect, useMemo, useState } from "react";
import { Dialog } from "./Dialog";
import { TextBox } from "./Controls";
import { t, tf } from "./l10n";
import { useLanguage } from "./language";
import {
  ManagedSign, SignKind, managerDelete, managerFocus, managerOpen$, managerSigns$, setManagerOpen,
} from "./signManagerBindings";
import styles from "./SignManager.module.scss";

/** Rows drawn at once: the list stays fast in cities with thousands of signs (search narrows it down). */
const MAX_ROWS = 200;
const KINDS: SignKind[] = ["gantry", "roadside", "prop"];

const kindLabel = (kind: SignKind) =>
  kind === "gantry" ? t("Gantry") : kind === "roadside" ? t("Roadside") : t("Surface sign");

const KindIcon = (p: { kind: SignKind; className?: string }) => {
  const stroke = "rgba(255,255,255,0.85)";
  if (p.kind === "gantry") {
    return (
      <svg className={p.className} viewBox="0 0 26 26">
        <rect x="3" y="5" width="2.4" height="19" fill={stroke} />
        <rect x="20.6" y="5" width="2.4" height="19" fill={stroke} />
        <rect x="3" y="5" width="20" height="2.4" fill={stroke} />
        <rect x="6.5" y="8.5" width="6" height="5" rx="0.8" fill="#2e8b57" />
        <rect x="13.5" y="8.5" width="6" height="5" rx="0.8" fill="#2e8b57" />
      </svg>
    );
  }
  if (p.kind === "roadside") {
    return (
      <svg className={p.className} viewBox="0 0 26 26">
        <rect x="8" y="12" width="2" height="12" fill={stroke} />
        <rect x="16" y="12" width="2" height="12" fill={stroke} />
        <rect x="4" y="3" width="18" height="10" rx="1.2" fill="#2e8b57" stroke={stroke} strokeWidth="1" />
      </svg>
    );
  }
  return (
    <svg className={p.className} viewBox="0 0 26 26">
      <rect x="5" y="6" width="16" height="12" rx="1.2" fill="#2f6fb5" stroke={stroke} strokeWidth="1" />
      <rect x="3" y="20" width="20" height="2" fill={stroke} />
    </svg>
  );
};

/** List icon for the toolbar button. */
const ListIcon = (p: { className?: string }) => (
  <svg className={p.className} viewBox="0 0 28 28">
    {[6, 13, 20].map((y) => (
      <g key={y}>
        <rect x="4" y={y} width="4" height="3" rx="0.8" fill="#ffd400" />
        <rect x="10" y={y} width="14" height="3" rx="0.8" fill="#ffffff" />
      </g>
    ))}
  </svg>
);

/**
 * Search key: lower case, Arabic diacritics and tatweel removed, alef / yaa / taa marbuta forms unified, so "الرياض"
 * finds "الرّياض" and "riyadh" finds "Riyadh".
 */
const normalize = (s: string) =>
  s
    .toLowerCase()
    .replace(/[ً-ْٰـ]/g, "")
    .replace(/[آأإٱ]/g, "ا")
    .replace(/ى/g, "ي")
    .replace(/ة/g, "ه")
    .replace(/\s+/g, " ")
    .trim();

const parse = (json: string): ManagedSign[] => {
  try {
    const list = JSON.parse(json);
    return Array.isArray(list) ? list : [];
  } catch {
    return [];
  }
};

/** One placed structure: what it is, what it says, where; Go to and Delete (two presses). */
const Row = (p: { sign: ManagedSign; armed: boolean; onArm: () => void; onDelete: () => void }) => {
  const s = p.sign;
  return (
    <div className={styles.row}>
      <div className={styles.kind}>
        <KindIcon kind={s.kind} className={styles.kindIcon} />
        <div className={styles.kindLabel}>{kindLabel(s.kind)}</div>
      </div>
      <div className={styles.info}>
        {s.legend ? <div className={styles.legend} title={s.legend}>{s.legend}</div> : <div className={styles.legendNone}>{t("(no text)")}</div>}
        <div className={styles.meta}>
          {s.district && <div className={styles.metaItem}>{s.district}</div>}
          <div className={styles.metaItem}>{s.signs === 1 ? t("1 sign") : tf("{n} signs", { n: s.signs })}</div>
        </div>
      </div>
      <div className={styles.actions}>
        <div className={styles.go} title={t("Select it and move the camera to it")} onClick={() => managerFocus(s.entity)}>{t("Go to")}</div>
        <div className={p.armed ? styles.delArmed : styles.del} onClick={() => (p.armed ? p.onDelete() : p.onArm())}>
          {p.armed ? t("Confirm delete") : t("Delete")}
        </div>
      </div>
    </div>
  );
};

/** The panel's content; mounted only while open, so its search and filter start fresh each time. */
const ManagerPanel = () => {
  const json = useValue(managerSigns$);
  const signs = useMemo(() => parse(json), [json]);
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState<"all" | SignKind>("all");
  const [armed, setArmed] = useState<string | null>(null);

  // Delete asks once: the first press arms it, the second deletes. It disarms after a few seconds.
  useEffect(() => {
    if (!armed) return;
    const timer = setTimeout(() => setArmed(null), 4000);
    return () => clearTimeout(timer);
  }, [armed]);

  const counts = useMemo(() => {
    const c: Record<SignKind, number> = { gantry: 0, roadside: 0, prop: 0 };
    for (const s of signs) if (s.kind in c) c[s.kind]++;
    return c;
  }, [signs]);

  // Search keys are computed once per list, not per keystroke.
  const indexed = useMemo(
    () =>
      signs
        .map((s) => ({ sign: s, key: normalize(`${s.legend} ${s.district} ${s.theme}`) }))
        .sort((a, b) => KINDS.indexOf(a.sign.kind) - KINDS.indexOf(b.sign.kind) || a.sign.legend.localeCompare(b.sign.legend)),
    [signs],
  );

  const shown = useMemo(() => {
    const words = normalize(query).split(" ").filter((w) => w.length > 0);
    return indexed.filter((x) => (kind === "all" || x.sign.kind === kind) && words.every((w) => x.key.indexOf(w) >= 0));
  }, [indexed, query, kind]);

  const filters: { value: "all" | SignKind; label: string }[] = [
    { value: "all", label: t("All") },
    ...KINDS.map((k) => ({ value: k, label: kindLabel(k) })),
  ];

  return (
    <div className={styles.body}>
      <div className={styles.summary}>
        <div className={styles.total}>{tf("{n} structures in the city", { n: signs.length })}</div>
        {KINDS.map((k) => (
          <div key={k} className={styles.stat}>
            {kindLabel(k)}
            <span className={styles.statCount}>{counts[k]}</span>
          </div>
        ))}
      </div>
      <div className={styles.toolbar}>
        <div className={styles.search}>
          <TextBox value={query} onChange={setQuery} placeholder={t("Search sign text...")} autoFocus />
        </div>
        <div className={styles.filters}>
          {filters.map((f) => (
            <div key={f.value} className={kind === f.value ? styles.filterOn : styles.filter} onClick={() => setKind(f.value)}>{f.label}</div>
          ))}
        </div>
      </div>
      <Scrollable vertical trackVisibility="scrollable" className={styles.list}>
        {shown.length === 0 ? (
          <div className={styles.empty}>
            {signs.length === 0 ? t("No Sign Builder signs are placed in this city yet.") : t("No signs match the search.")}
          </div>
        ) : (
          shown.slice(0, MAX_ROWS).map((x) => (
            <Row key={x.sign.entity} sign={x.sign} armed={armed === x.sign.entity}
              onArm={() => setArmed(x.sign.entity)}
              onDelete={() => { setArmed(null); managerDelete(x.sign.entity); }} />
          ))
        )}
        {shown.length > MAX_ROWS && (
          <div className={styles.more}>{tf("Showing {n} of {total}. Search to narrow the list.", { n: MAX_ROWS, total: shown.length })}</div>
        )}
      </Scrollable>
    </div>
  );
};

/** Sign manager: every Sign Builder structure placed in the city (C#: Systems/SignManagerUISystem.cs). */
export const SignManager = () => {
  useLanguage();
  const open = useValue(managerOpen$);
  if (!open) return null;
  const close = () => setManagerOpen(false);
  // Escape closes it, as the game's own panels.
  return (
    <BackConsumer onAction={close}>
      <Dialog title={t("Sign manager")} dock="top" className={styles.panel} onClose={close}>
        <ManagerPanel />
      </Dialog>
    </BackConsumer>
  );
};

/** Top-left toolbar button that opens and closes the sign manager. */
export const SignManagerButton = () => {
  useLanguage();
  const open = useValue(managerOpen$);
  return (
    <Button variant="floating" selected={open} onSelect={() => setManagerOpen(!open)} tooltipLabel={t("Sign manager")}>
      <div className={styles.button}>
        <ListIcon className={styles.buttonIcon} />
      </div>
    </Button>
  );
};
