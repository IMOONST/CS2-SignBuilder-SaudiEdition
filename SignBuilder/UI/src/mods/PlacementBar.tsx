import { t } from "./l10n";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { Dialog } from "./Dialog";
import { useMemo } from "react";
import { adjust, placeInfo$ } from "./bindings";
import { Choice } from "./Controls";
import { ArrowIcon } from "./Icons";
import styles from "./SignEditor.module.scss";

type PlaceInfo = {
  surface?: boolean; free?: boolean; angle?: number; elevation?: number; width?: number; scale?: number; posts?: boolean; bottom?: number; lift?: number; gap?: number;
  lights?: boolean;
};
const signed = (v: number) => `${v >= 0 ? "+" : ""}${v.toFixed(2)} m`;

const Step = (p: { action: string; children: any; title?: string }) => (
  <div className={styles.step} title={p.title} onClick={() => adjust(p.action)}>{p.children}</div>
);

/** The small bar at the top of the screen while placing a roadside sign or choosing where a gantry goes. */
export const PlacementBar = (props: { status: string; onBack: () => void; onClose: () => void }) => {
  const infoJson = useValue(placeInfo$);
  const info: PlaceInfo = useMemo(() => { try { return JSON.parse(infoJson); } catch { return {}; } }, [infoJson]);
  return (
    <Dialog dock="top" title={info.surface ? t("Prop sign") : info.posts ? t("Roadside sign") : t("Place gantry")} className={styles.compact} onClose={props.onClose}>
      <div className={styles.compactBody}>
        <div className={styles.status}>{props.status}</div>
        <div className={styles.hint}>{info.free
          ? t("Right-drag to rotate; Page Up / Page Down or Ctrl + mouse wheel to raise and lower.")
          : t("Alt + mouse wheel: distance from the road. Ctrl + mouse wheel: height.")}</div>
        {!info.surface && (
          <div className={styles.barRow}>
            <Choice options={[{ value: "road", label: t("Snap to road") }, { value: "free", label: t("Free placement") }]}
              value={info.free ? "free" : "road"} onChange={(v) => adjust(v)} />
          </div>
        )}
        <div className={styles.barRow}>
          {info.free && (
            <>
              <div className={styles.barLabel}>{t("Rotate")}</div>
              <Step action="rotateLeft" title={t("Rotate left (or right-drag)")}>-15°</Step>
              <div className={styles.barValueSmall}>{`${Math.round(info.angle ?? 0)}°`}</div>
              <Step action="rotateRight" title={t("Rotate right (or right-drag)")}>+15°</Step>
            </>
          )}
          <div className={styles.barLabel} style={{ marginLeft: info.free ? "16rem" : "0rem" }}>{info.surface ? t("Height") : info.posts ? t("Sign bottom") : info.free ? t("Base") : t("Height")}</div>
          <Step action="down" title={t("Lower (Ctrl + mouse wheel)")}><ArrowIcon direction="down" size={12} /></Step>
          <div className={styles.barValueSmall}>{info.surface
            ? signed(info.elevation ?? 0)
            : info.posts
            ? `${(info.bottom ?? 0).toFixed(2)} ${t("m")}`
            : signed(info.free ? info.elevation ?? 0 : info.lift ?? 0)}</div>
          <Step action="up" title={t("Raise (Ctrl + mouse wheel)")}><ArrowIcon direction="up" size={12} /></Step>
          {!info.free && (
            <>
              <div className={styles.barLabel} style={{ marginLeft: "16rem" }}>{info.posts ? t("From road") : t("Posts from road")}</div>
              <Step action="closer" title={t("Closer to the road (Alt + mouse wheel)")}>-</Step>
              <div className={styles.barValueSmall}>{`${(info.gap ?? 0).toFixed(2)} ${t("m")}`}</div>
              <Step action="farther" title={t("Farther from the road (Alt + mouse wheel)")}>+</Step>
            </>
          )}
        </div>
        {info.posts && (
          <div className={styles.barRow}>
            <div className={styles.barLabel}>{t("Sign size")}</div>
            <Step action="smaller">-</Step>
            <div className={styles.barValueSmall}>{`${Math.round((info.scale ?? 1) * 100)}%`}</div>
            <Step action="larger">+</Step>
            <div className={styles.barLabel} style={{ marginLeft: "16rem" }}>{t("Lights")}</div>
            <Choice options={[{ value: "lightsOff", label: t("Off") }, { value: "lightsOn", label: t("On"), title: t("A light fixture below the sign, lit at night") }]}
              value={info.lights ? "lightsOn" : "lightsOff"} onChange={(v) => adjust(v)} />
          </div>
        )}
        <div className={styles.actions}>
          <Button variant="flat" onSelect={props.onBack}><div className={styles.flatLabel}>{t("Back to editor")}</div></Button>
        </div>
      </div>
    </Dialog>
  );
};
