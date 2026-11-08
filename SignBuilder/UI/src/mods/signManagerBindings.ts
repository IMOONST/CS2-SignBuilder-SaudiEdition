import { bindValue, trigger } from "cs2/api";
import mod from "mod.json";

// C# side: SignBuilderSA.Systems.SignManagerUISystem (binding group = mod id).
const group = mod.id;

export type SignKind = "gantry" | "roadside" | "prop";

/** One placed Sign Builder structure, as listed by the sign manager. */
export interface ManagedSign {
  /** "index:version" of the placed object entity. */
  entity: string;
  kind: SignKind;
  /** The first texts of its signs, e.g. "Riyadh, Dammam / Exit 12". */
  legend: string;
  theme: string;
  /** Number of signs (designs) on the structure. */
  signs: number;
  /** District it stands in, "" if none. */
  district: string;
}

export const managerOpen$ = bindValue<boolean>(group, "managerOpen", false);
/** JSON array of ManagedSign; only kept up to date while the panel is open. */
export const managerSigns$ = bindValue<string>(group, "managerSigns", "[]");

export const setManagerOpen = (open: boolean) => trigger(group, "setManagerOpen", open);
/** Selects the structure (its info panel opens) and moves the camera to it. */
export const managerFocus = (entity: string) => trigger(group, "managerFocus", entity);
/** Deletes the structure, as the selected-object panel's Delete does. */
export const managerDelete = (entity: string) => trigger(group, "managerDelete", entity);
