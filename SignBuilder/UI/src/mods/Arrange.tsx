import { t } from "./l10n";
import { useValue } from "cs2/api";
import { Dialog } from "./Dialog";
import { ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { adjust, arrange, arrangeInfo$, toolStatus$ } from "./bindings";
import { Choice, NumberBox } from "./Controls";
import { Design, SPANS, STRUCTURES } from "./designModel";
import { ArrowIcon } from "./Icons";
import { SignPreview } from "./SignPreview";
import styles from "./SignEditor.module.scss";

type Sign = { index: number; x: number; w: number; h: number; ready: boolean; mode: string; lane: number; fromRight: boolean; offset: number; raise?: number; design: Design };
type Info = {
  lanes: number[]; allLanes: [number, boolean][]; deck: number[]; roads: [number, number, boolean][]; structure: number[]; posts: number[]; signs: Sign[];
  type: string; span: string; side: string; reverse: boolean; free: boolean; scale: number; lift: number; lights: boolean; gap: number;
  view: string; otherSide: number; total: number;
  angle: number; elevation: number; width: number; replacing: boolean; busy: boolean; dirty: boolean;
};


/**
 * The gantry arrangement panel, docked at the bottom of the screen: a to-scale view of the structure over the
 * lanes it crosses, as drivers see it. Drag signs sideways (they snap to lane centers); pick the structure; Build
 * makes it at the pinned road spot.
 */
const Step = (p: { onClick: (e: { shiftKey?: boolean; ctrlKey?: boolean }) => void; children: any; title?: string }) => (
  <div className={styles.step} title={p.title} onClick={p.onClick}>{p.children}</div>
);
/** Nudge step in meters: Shift for larger steps, Ctrl for finer ones (as the number boxes do). */
const NUDGE = 0.25;
const stepOf = (e: { shiftKey?: boolean; ctrlKey?: boolean }) => NUDGE * (e.shiftKey ? 10 : e.ctrlKey ? 0.1 : 1);
const Label = (p: { children: any }) => <div className={styles.barLabel} style={{ marginLeft: "16rem" }}>{p.children}</div>;

export const Arrange = (props: {
  onEditSign: (index: number, design: Design) => void;
  onAddSign: () => void;
  /** Close (X) or Cancel; the argument says whether anything would be lost. */
  onClose: (unsaved: boolean) => void;
  onCancel: (unsaved: boolean) => void;
  overlay?: ReactNode;
}) => {
  const json = useValue(arrangeInfo$);
  const status = useValue(toolStatus$);
  const info: Info | null = useMemo(() => { try { return json ? JSON.parse(json) : null; } catch { return null; } }, [json]);
  const [selected, setSelected] = useState(0);
  const [drag, setDrag] = useState<{ index: number; x: number } | null>(null);
  const area = useRef<HTMLDivElement>(null);
  const [areaWidth, setAreaWidth] = useState(1100);
  useEffect(() => {
    const w = area.current?.clientWidth;
    if (w && Math.abs(w - areaWidth) > 2) setAreaWidth(w);
  });

  if (!info) {
    return (
      <Dialog dock="bottom" title={t("Gantry")} className={styles.arrange} onClose={() => props.onClose(true)}>
        <div className={styles.compactBody}><div className={styles.status}>{status || "Preparing..."}</div></div>
      </Dialog>
    );
  }

  const signs = info.signs;
  // The selected sign, by its index among all signs (this side shows only some of them).
  const selPos = Math.max(0, signs.findIndex((s) => s.index === selected));
  const sel = Math.min(selPos, Math.max(0, signs.length - 1));
  const xOf = (i: number) => (drag && drag.index === i ? drag.x : signs[i].x);
  const allLanes = info.allLanes ?? info.lanes.map((x) => [x, true] as [number, boolean]);
  // Visible range: deck, structure and signs, with a margin.
  const lefts = [info.deck[0], info.structure[0], ...info.roads.map((r) => r[0]), ...signs.map((s, i) => xOf(i) - s.w / 2)];
  const rights = [info.deck[1], info.structure[1], ...info.roads.map((r) => r[1]), ...signs.map((s, i) => xOf(i) + s.w / 2)];
  const min = Math.min(...lefts) - 1.5;
  const max = Math.max(...rights) + 1.5;
  const maxH = Math.max(2.5, ...signs.map((s) => s.h));
  const scale = Math.min(areaWidth / (max - min), 100 / maxH);
  // Centered when the height limits the scale (the structure is then narrower than the view).
  const left = Math.max(0, (areaWidth - (max - min) * scale) / 2);
  const px = (x: number) => left + (x - min) * scale;
  const beamY = 12 + (maxH * scale) / 2;
  const roadY = beamY + (maxH * scale) / 2 + 26;
  const height = roadY + 30;
  const nearest = (xs: number[], x: number) => (xs.length ? xs.reduce((b, l, k) => (Math.abs(l - x) < Math.abs(xs[b] - x) ? k : b), 0) : -1);

  const startDrag = (e: any, i: number) => {
    setSelected(signs[i].index);
    const startX = e.clientX;
    const origin = signs[i].x;
    let last = 0;
    let x = origin;
    const onMove = (ev: MouseEvent) => {
      x = origin + (ev.clientX - startX) / scale;
      setDrag({ index: i, x });
      const now = Date.now();
      if (now - last > 120) {
        last = now;
        arrange({ op: "moveSign", index: signs[i].index, x, snap: false });
      }
    };
    const onUp = (ev: MouseEvent) => {
      document.removeEventListener("mousemove", onMove);
      document.removeEventListener("mouseup", onUp);
      if (Math.abs(ev.clientX - startX) > 3) arrange({ op: "moveSign", index: signs[i].index, x, snap: true });
      setTimeout(() => setDrag(null), 150);
    };
    document.addEventListener("mousemove", onMove);
    document.addEventListener("mouseup", onUp);
  };

  const set = (field: string, value: number) => arrange({ op: "set", field, value });
  const laneXs = allLanes.map((l) => l[0]);
  const dragLane = drag ? nearest(laneXs, drag.x) : -1;
  const snapping = drag && dragLane >= 0 && Math.abs(laneXs[dragLane] - drag.x) < 0.75;
  // Lanes in order across the view; markings between neighbors on the same road.
  const ordered = allLanes.map((l, k) => ({ x: l[0], faces: l[1], k })).sort((a, b) => a.x - b.x);
  const unsaved = info.dirty !== false;
  const ready = info.total > 0 && !info.busy && !signs.some((s) => !s.ready);

  return (
    <Dialog dock="bottom" title={info.replacing ? t("Edit gantry") : t("Gantry")} className={styles.arrange} onClose={() => props.onClose(unsaved)}
      header={
        <>
          <div className={ready ? styles.primaryButton : styles.primaryButtonOff} onClick={() => ready && arrange({ op: "build" })}>{info.replacing ? t("Save") : t("Build")}</div>
          <div className={styles.chip} style={{ alignSelf: "center" }} onClick={() => arrange({ op: "auto" })}>{t("Auto arrange")}</div>
          <div className={styles.chip} style={{ alignSelf: "center" }} title={t("Click another spot on a road to move the gantry there")} onClick={() => arrange({ op: "repick" })}>{t("Move gantry")}</div>
          <div className={styles.chip} style={{ alignSelf: "center" }} onClick={() => props.onCancel(unsaved)}>{t("Cancel")}</div>
          <Label>{t("Showing")}</Label>
          <Choice options={[
            { value: "front", label: t("Front") },
            { value: "back", label: t("Back"), title: t("Signs on the other face of the structure, for traffic the other way") },
          ]} value={info.view} onChange={(value) => { arrange({ op: "view", value }); setSelected(-1); }} />
          <Label>{t("Lights")}</Label>
          <Choice options={[{ value: "off", label: t("Off") }, { value: "on", label: t("On"), title: t("Light fixtures below each sign, lit at night") }]}
            value={info.lights ? "on" : "off"} onChange={(v) => arrange({ op: "lights", value: v === "on" })} />
          <Label>{t("Sign size")}</Label>
          <NumberBox compact value={Math.round(info.scale * 100)} step={5} min={50} max={170} unit="%" onChange={(v) => set("scale", v)} />
          {status && <div className={styles.status} style={{ marginLeft: "12rem" }}>{status}</div>}
        </>
      }>
      <div className={styles.arrangeBody}>
        <div className={styles.barRow}>
          <div className={styles.barLabel}>{t("Structure")}</div>
          <Choice options={STRUCTURES().map((s) => ({ value: s.id, label: s.label }))} value={info.type} onChange={(value) => arrange({ op: "type", value })} />
          <Label>{t("Type")}</Label>
          <Choice options={SPANS().map((s) => ({ value: s.id, label: s.label, title: s.hint }))} value={info.span} onChange={(value) => arrange({ op: "span", value })} />
          {info.span === "cantilever" && (
            <Choice options={[{ value: "left", label: t("Post left") }, { value: "right", label: t("Post right") }]} value={info.side}
              onChange={(value) => arrange({ op: "side", value })} />
          )}
          {!info.free && (
            <div className={styles.chip} style={{ alignSelf: "center", marginLeft: "12rem" }} title={t("Face the signs the other way")}
              onClick={() => arrange({ op: "swap" })}>{t("Swap directions")}</div>
          )}
          <Label>{t("Height")}</Label>
          <NumberBox compact value={info.lift} step={0.25} min={-5.3} max={12} unit=" m" onChange={(v) => set("lift", v)} />
          {!info.free && (
            <>
              <Label>{t("From road")}</Label>
              <NumberBox compact value={info.gap ?? 2} step={0.25} min={-2} max={30} unit=" m" onChange={(v) => set("gap", v)} />
            </>
          )}
        </div>
        {info.free && (
        <div className={styles.barRow}>
            <>
              <Label>{t("Rotate")}</Label>
              <NumberBox compact value={Math.round(info.angle)} step={15} min={-360} max={720} unit="°" onChange={(v) => set("angle", v)} />
              <Label>{t("Base")}</Label>
              <NumberBox compact value={info.elevation} step={0.25} min={-5} max={40} unit=" m" onChange={(v) => set("elevation", v)} />
              <Label>{t("Road width")}</Label>
              <NumberBox compact value={info.width} step={1} min={4} max={60} unit=" m" onChange={(v) => set("width", v)} />
            </>
        </div>
        )}

        <div ref={area} className={styles.diagram} style={{ height: height + "px" }}>
          {(info.roads.length ? info.roads : [[info.deck[0], info.deck[1], true] as [number, number, boolean]]).map((r, k) => (
            <div key={`r${k}`} className={r[2] ? styles.deck : styles.deckOther}
              style={{ left: px(r[0]) + "px", width: (r[1] - r[0]) * scale + "px", top: roadY + "px" }} />
          ))}
          {ordered.slice(1).map((l, k) => Math.abs(l.x - ordered[k].x) < 5.5 && (
            <div key={`d${k}`} className={ordered[k].faces === l.faces ? styles.laneLine : styles.laneLineCenter}
              style={{ left: px((l.x + ordered[k].x) / 2) - 1 + "px", top: roadY + "px" }} />
          ))}
          {allLanes.map((l, k) => {
            const faced = info.lanes.findIndex((x) => Math.abs(x - l[0]) < 0.01);
            return (
              <div key={k} className={k === dragLane && snapping ? styles.laneOn : l[1] ? styles.lane : styles.laneOther}
                style={{ left: px(l[0]) - 18 + "px", top: roadY + 3 + "px" }}
                title={l[1] ? t("A lane this side's signs face") : t("Traffic the other way")}>
                <ArrowIcon direction={l[1] ? "up" : "down"} size={10} color={k === dragLane && snapping ? "#ffd400" : "rgba(255,255,255,0.8)"} />
                {faced >= 0 && <span style={{ marginLeft: "2px" }}>{`${faced + 1}`}</span>}
              </div>
            );
          })}
          <div className={styles.beam} style={{ left: px(info.structure[0]) + "px", width: (info.structure[1] - info.structure[0]) * scale + "px", top: beamY - 3 + "px" }} />
          {info.posts.map((p, k) => (
            <div key={`p${k}`} className={styles.post} style={{ left: px(p) - 3 + "px", top: beamY + "px", height: roadY - beamY + "px" }} />
          ))}
          {signs.map((s, i) => (
            <div key={s.index} className={i === sel ? styles.signBoxOn : styles.signBox}
              style={{ left: px(xOf(i) - s.w / 2) + "px", top: beamY - (s.h * scale) / 2 - (s.raise ?? 0) * scale + "px", width: s.w * scale + "px", height: s.h * scale + "px" }}
              onMouseDown={(e) => startDrag(e, i)}>
              <SignPreview design={s.design} interactive={false} maxWidth={s.w * scale} maxHeight={s.h * scale} padding={0} />
            </div>
          ))}
        </div>
        <div className={styles.barRow}>
          <div className={styles.chip} style={{ alignSelf: "center", marginRight: "14rem" }} onClick={props.onAddSign}>{t("Add sign")}</div>
          {signs.length > 0 && (
          <>
            <div className={styles.barLabel}>{t("Nudge")}</div>
            <Step title={t("Left (Shift: more, Ctrl: less)")} onClick={(e) => arrange({ op: "nudge", index: signs[sel].index, value: -stepOf(e) })}><ArrowIcon direction="left" size={12} /></Step>
            <Step title={t("Right (Shift: more, Ctrl: less)")} onClick={(e) => arrange({ op: "nudge", index: signs[sel].index, value: stepOf(e) })}><ArrowIcon direction="right" size={12} /></Step>
            <Step title={t("Up (Shift: more, Ctrl: less)")} onClick={(e) => arrange({ op: "raiseSign", index: signs[sel].index, value: stepOf(e) })}><ArrowIcon direction="up" size={12} /></Step>
            <Step title={t("Down (Shift: more, Ctrl: less)")} onClick={(e) => arrange({ op: "raiseSign", index: signs[sel].index, value: -stepOf(e) })}><ArrowIcon direction="down" size={12} /></Step>
            <div className={styles.chip} style={{ marginLeft: "12rem" }} onClick={() => props.onEditSign(signs[sel].index, signs[sel].design)}>{t("Edit sign")}</div>
            <div className={styles.chip} onClick={() => arrange({ op: "duplicateSign", index: signs[sel].index })}>{t("Duplicate")}</div>
            <div className={styles.chip} onClick={() => { arrange({ op: "removeSign", index: signs[sel].index }); setSelected(0); }}>{t("Remove")}</div>
          </>
          )}
          {info.span !== "cantilever" && (
            <>
              {/* Pushes the hint to the right end (Gameface has no auto margins). */}
              <div style={{ flexGrow: 1 }} />
              <div className={styles.hint}>{t("Click a road to add it to the gantry's span, or click it again to remove it.")}</div>
            </>
          )}
        </div>
        {props.overlay}
      </div>
    </Dialog>
  );
};
