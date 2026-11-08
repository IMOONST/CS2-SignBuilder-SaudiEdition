import { bindValue, trigger } from "cs2/api";
import mod from "mod.json";

// C# side: SignBuilderSA.Systems.RoadPickUISystem (binding group = mod id).
const group = mod.id;

export type RoadClass = "highway" | "major" | "medium" | "local" | "other";

/** A road read from the map: picked from the designer, or the one the placement tool last snapped to. */
export type RoadInfo = {
  street: string;
  /** The street name when it is written in Arabic. */
  streetAr?: string;
  district: string;
  roadClass: RoadClass;
  /** Speed limit in km/h (0 when unknown). */
  speedLimit: number;
  /** Driving lanes in the road's main direction. */
  lanes: number;
  source?: "pick" | "placement";
  nonce: string;
};

/** JSON RoadInfo, "" until a road is known. */
export const roadInfo$ = bindValue<string>(group, "roadInfo", "");

/** Starts picking a road in the world (the designer stays open). */
export const pickRoad = () => trigger(group, "pickRoad");
/** Stops picking without reading a road. */
export const cancelPickRoad = () => trigger(group, "cancelPickRoad");

export const parseRoadInfo = (json: string): RoadInfo | null => {
  if (!json) return null;
  try {
    const o = JSON.parse(json);
    return o && typeof o === "object" ? (o as RoadInfo) : null;
  } catch {
    return null;
  }
};

/** The speed a sign shows: the nearest multiple of 10 km/h (0 when unknown); C# RoadInfo.SignSpeed. */
export const signSpeed = (kmh: number): number => (kmh > 0 ? Math.max(10, Math.round(kmh / 10) * 10) : 0);

/** The road placeholders a sign's text and shield numbers may hold; the placement tool fills them in. */
export const ROAD_TOKENS = { street: "{street}", district: "{district}", speed: "{speed}" } as const;
