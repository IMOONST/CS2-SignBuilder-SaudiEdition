// Small inline icons for the editor's option buttons (Gameface renders inline SVG; no icon font needed).

// Straight arrows, as in the renderer: one shape per direction ("diagonal" points up-right), each as long as the
// arrow in the direction it points and centered on (50, 75); a missing one is the opposite one turned around.
const ARROW_FALLBACK: Record<string, [string, number][]> = {
  up: [["up", 0], ["down", 180]], down: [["down", 0], ["up", 180]], left: [["left", 0], ["right", 180]], right: [["right", 0], ["left", 180]],
  "up-right": [["diagonal", 0]], "down-right": [["diagonal", 0]], "down-left": [["diagonal", 0]], "up-left": [["diagonal", 0]],
};
// Diagonals are mirrored, not turned (as in the renderer).
const DIAGONAL_MIRROR: Record<string, string> = { "down-right": "1 -1", "down-left": "-1 -1", "up-left": "-1 1" };

// Arrow styles, each with its directions (laid out <perRow> to a row). "" is the theme's own arrows (shown as the U.S. ones); a style without diagonals uses the theme's.
// <box> is a square that fits every shape of the style turned any way. Lane arrows ("lane" style: turn-, slight-,
// split-, corner- and add- directions, and fork) point up and stand on the bottom of a box 150 tall; "-right" mirrors them.
export const ARROW_STYLES: Record<string, { label: string; title: string; box: string; directions: string[]; perRow: number; shapes: Record<string, string> }> = {
  "": {
    label: "Standard", title: "The theme's arrows", box: "-25 0 150 150",
    directions: ["up-left", "up", "up-right", "left", "right", "down-left", "down", "down-right"], perRow: 4,
    shapes: {
      up: "M96.2 65.1 L53.5 1.9 C52.7 0.7 51.5 0 50 0 C48.5 0 47.3 0.7 46.5 1.9 L3.8 65.1 " +
      "C3.4 65.7 3.2 66.5 3.2 67.4 C3.2 69.7 5 71.6 7.4 71.6 C7.7 71.6 8 71.5 8.4 71.5 L38.7 64.2 L35 150 " +
      "L65 150 L61.3 64.2 L91.6 71.5 C92 71.5 92.3 71.6 92.6 71.6 C95 71.6 96.8 69.7 96.8 67.4 " +
      "C96.8 66.5 96.6 65.7 96.2 65.1 Z",
      left: "M125 56.2 L48.4 56.2 L56.6 34.5 C56.9 33.7 57 32.9 57 32 C57 28.2 53.9 25 50 25 " +
      "C48.5 25 47.1 25.5 46 26.2 L-25 75 L46 123.8 C47.1 124.5 48.5 125 50 125 C53.9 125 57 121.8 57 118 " +
      "C57 117.1 56.9 116.2 56.6 115.5 L48.4 93.8 L125 93.8 L125 56.2 Z",
      diagonal: "M94.2 96 L108.8 21.2 C109 19.8 108.6 18.4 107.7 17.4 C106.6 16.4 105.2 16 103.9 16.2 L29 30.8 " +
      "C28.2 30.9 27.5 31.3 26.9 31.9 C25.2 33.5 25.2 36.2 26.9 37.8 C27.1 38.1 27.4 38.3 27.7 38.5 " +
      "L54.2 54.8 L-9 112.8 L12.2 134 L70.2 70.8 L86.5 97.4 C86.7 97.6 86.9 97.9 87.2 98.1 " +
      "C88.8 99.8 91.5 99.8 93.1 98.1 C93.7 97.5 94.1 96.8 94.2 96 Z",
    },
  },
  lane: {
    label: "Lane", title: "Lane-use arrows: straight, curving off, both, or turning", box: "-36.6 -11.6 173.2 173.2",
    directions: ["corner-left", "turn-left", "slight-left", "slight-right", "turn-right", "corner-right",
      "add-left", "split-left", "up", "fork", "split-right", "add-right"], perRow: 6,
    shapes: {
      up: "M26.6 34.1 L49.9 0 L73.5 34.2 C73.7 34.6 73.9 35.1 73.9 35.6 C74.1 36.9 73.5 38.3 72.4 39 " +
      "C71.8 39.4 71.1 39.6 70.4 39.6 C70.3 39.6 70.2 39.6 70 39.6 C69.8 39.6 69.6 39.6 69.4 39.5 " +
      "L58.8 36.1 L58.7 36.1 L58.7 150 L40.9 150 L40.9 36.2 L30.6 39.5 C30.1 39.7 29.4 39.8 28.9 39.7 " +
      "C27.2 39.3 26 37.9 25.9 36.1 C25.9 35.4 26.2 34.7 26.6 34.1 Z",
      turn: "M49.3 48.1 C49.8 48 50.4 48.1 50.8 48.2 C52.1 48.6 53.1 49.6 53.4 51 C53.5 51.7 53.4 52.3 53.1 53 " +
      "C53.1 53.1 53.1 53.2 53 53.4 C52.9 53.5 52.8 53.7 52.7 53.9 L45.7 62.1 C84.8 75.8 91 100.8 91 113.9 " +
      "L91 150 L73.3 150 L73.3 113.9 C73.3 104.8 68.3 88.4 38.8 78.4 L38.7 78.6 L38.7 78.7 L37.7 89.6 " +
      "C37.7 90.1 37.5 90.8 37.3 91.2 C36.3 92.6 34.5 93.2 32.8 92.6 C32.1 92.3 31.6 91.8 31.2 91.2 " +
      "L9 56.6 L49.3 48.1 Z",
      split: "M27 43.2 C27.5 43.1 28.1 43.1 28.5 43.3 C29.8 43.6 30.8 44.6 31.1 46 C31.3 46.7 31.2 47.3 30.9 48 " +
      "C30.9 48.2 30.8 48.3 30.8 48.4 C30.7 48.6 30.6 48.8 30.5 48.9 L23.5 57.3 " +
      "C56.1 68.2 66.6 75.5 80.2 91.3 L80.2 36.2 L69.9 39.5 C69.4 39.7 68.7 39.8 68.2 39.7 " +
      "C66.5 39.3 65.3 37.9 65.2 36.2 C65.2 35.4 65.5 34.7 65.9 34.1 L89.2 0 L112.7 34.2 " +
      "C113 34.6 113.1 35.1 113.2 35.6 C113.3 37 112.8 38.3 111.6 39.1 C111 39.5 110.4 39.6 109.7 39.6 " +
      "C109.5 39.7 109.4 39.6 109.3 39.6 C109.1 39.6 108.9 39.6 108.7 39.5 L98.1 36.2 L98 42.3 L98 150 " +
      "L80.2 149.9 L80.2 134.6 C80.2 121.2 67.9 85.8 16.8 74 L16.8 74 L16 85.1 " +
      "C16 85.6 15.9 86.2 15.6 86.6 C14.6 88.1 12.8 88.8 11.2 88.1 C10.5 87.9 9.9 87.4 9.5 86.7 " +
      "L-13.3 52.3 L27 43.2 Z",
      corner: "M96.1 60.1 L33.3 60.1 L40.2 79 C40.5 79.7 40.6 80.5 40.6 81.2 C40.6 84.7 37.8 87.5 34.4 87.5 " +
      "C33.1 87.5 31.9 87.1 30.9 86.4 L-31.3 43.8 L30.9 1.1 C31.9 0.4 33.1 0 34.4 0 " +
      "C37.8 0 40.6 2.8 40.6 6.3 C40.6 7 40.5 7.8 40.2 8.5 L33.4 27.3 L127.3 27.3 " +
      "C129.5 27.3 131.3 29 131.3 31.2 L131.3 150 L98.4 150 L98.4 62.4 C98.4 61.2 97.4 60.1 96.1 60.1 Z",
      slight: "M42.1 83.6 L42.1 83.6 L29.4 104.3 C29.2 104.5 29.1 104.7 28.9 104.9 " +
      "C27.6 106.2 25.5 106.2 24.2 104.9 C23.7 104.3 23.4 103.6 23.3 102.9 L17.9 51.4 " +
      "C17.8 50.5 18.2 49.5 18.9 48.8 C19.6 48 20.6 47.7 21.5 47.8 L73 53.2 C73.7 53.3 74.4 53.6 75 54.1 " +
      "C76.3 55.4 76.3 57.5 75 58.8 C74.8 59 74.6 59.1 74.4 59.3 L53.7 72 L79.3 95.4 " +
      "C79.8 95.9 80.2 96.8 80.2 97.7 L82.2 150 L60 150 L61.7 105.9 C61.7 105.3 61.4 104.7 60.9 104.2 " +
      "L42.1 83.6 Z",
      add: "M69.1 83.7 C68.2 82.6 67.3 81.5 66.3 80.5 L40.1 54.3 C34.2 48.4 30.9 40.4 30.9 32 L30.9 27 L46.6 27 " +
      "L26.4 0 L6.1 27 L21.9 27 L21.9 32 C21.9 42.8 26.1 53.1 33.7 60.7 L59.9 86.8 " +
      "C65.8 92.8 69.1 100.8 69.1 109.2 L69.1 150 L78.1 150 L78.1 27 L93.9 27 L73.6 0 L53.4 27 L69.1 27 " +
      "L69.1 83.7 Z",
      fork: "M59.8 127 C59.8 104.5 70.5 66.2 110.5 42.8 L113.5 53.5 C113.6 54 113.9 54.6 114.3 55 " +
      "C115.6 56.3 117.6 56.5 119.2 55.5 C119.9 55.1 120.3 54.5 120.6 53.7 L136.6 13.3 L92.8 13 " +
      "C92.3 13 91.7 13.2 91.3 13.5 C90 14.1 89.2 15.4 89.2 16.8 C89.2 17.6 89.5 18.2 89.9 18.9 " +
      "C89.9 19 90 19.1 90.1 19.2 C90.3 19.4 90.4 19.6 90.6 19.7 L100.4 27.4 C76.5 41 60.5 60.5 50 85.9 " +
      "C39.5 60.5 23.5 41 -0.4 27.4 L9.4 19.7 C9.6 19.6 9.8 19.4 9.9 19.2 C10 19.1 10.1 19 10.1 18.9 " +
      "C10.5 18.2 10.8 17.6 10.8 16.8 C10.8 15.4 10 14.1 8.7 13.5 C8.2 13.2 7.7 13 7.2 13 L-36.6 13.3 " +
      "L-20.6 53.7 C-20.3 54.5 -19.8 55.1 -19.2 55.5 C-17.6 56.5 -15.6 56.3 -14.3 55 " +
      "C-13.9 54.6 -13.6 54 -13.5 53.5 L-10.5 42.8 C29.5 66.2 40.2 104.5 40.2 127 L40.2 150 L59.8 150 " +
      "L59.8 127 Z",
    },
  },
  overhead: {
    label: "Overhead", title: "The U.S. arrows over the lanes on overhead signs", box: "-59.1 -34.1 218.2 218.2",
    directions: ["up", "left", "right", "down"], perRow: 4,
    shapes: {
      down: "M152.2 40.9 C151.5 40.9 150.9 40.9 150.3 41.1 L72.1 61.4 L72.1 0 L27.7 0 L27.7 61.4 L-50.6 41.1 " +
      "C-51.2 40.9 -51.8 40.9 -52.3 40.9 C-56.1 40.9 -59.1 43.9 -59.1 47.7 C-59.1 49.5 -58.4 51.3 -57 52.6 " +
      "L-56.9 52.8 L50 150 L156.7 52.8 C158.2 51.4 159.1 49.5 159.1 47.7 C159.1 43.9 156 40.9 152.2 40.9 Z",
      right: "M49.3 139.3 L122.8 79.7 C124.1 78.6 125 76.9 125 75 C125 73.1 124.1 71.5 122.8 70.3 L49.3 10.7 " +
      "C48.3 9.9 46.9 9.4 45.5 9.4 C42.2 9.4 39.5 12.1 39.5 15.4 C39.5 15.9 39.6 16.3 39.7 16.8 L50 60 " +
      "L-25 54.8 L-25 95.3 L50 90 L39.7 133.2 C39.6 133.7 39.5 134.1 39.5 134.6 " +
      "C39.5 137.9 42.2 140.6 45.5 140.6 C46.9 140.6 48.3 140.1 49.3 139.3 Z",
    },
  },
};

/** The same arrow shapes the sign renderer draws, in a square box. */
export const ArrowIcon = (props: { direction: string; style?: string; size?: number; color?: string }) => {
  const size = props.size ?? 18;
  const lane = props.direction === "fork" ? [props.direction, "fork", ""] : /^(turn|slight|split|corner|add)-(left|right)$/.exec(props.direction);
  const style = ARROW_STYLES[props.style ?? ""] ?? ARROW_STYLES[""];
  const ways = ARROW_FALLBACK[props.direction] ?? ARROW_FALLBACK.down;
  const [key, rotation] = ways.find(([k]) => style.shapes[k]) ?? ways.find(([k]) => ARROW_STYLES[""].shapes[k]) ?? ways[0];
  return (
    <svg width={size + "rem"} height={size + "rem"} viewBox={lane ? ARROW_STYLES.lane.box : style.box}>
      {lane ? (
        <path d={ARROW_STYLES.lane.shapes[lane[1]]} fill={props.color ?? "#ffffff"} transform={lane[2] === "right" ? "translate(100 0) scale(-1 1)" : undefined} />
      ) : (
        <path d={style.shapes[key] ?? ARROW_STYLES[""].shapes[key]} fill={props.color ?? "#ffffff"}
          transform={DIAGONAL_MIRROR[props.direction] ? `translate(50 75) scale(${DIAGONAL_MIRROR[props.direction]}) translate(-50 -75)` : `rotate(${rotation} 50 75)`} />
      )}
    </svg>
  );
};

/** Built-in (read-only) template badge. */
export const LockIcon = () => (
  <svg width="10rem" height="12rem" viewBox="0 0 10 12">
    <path d="M2.5 5 V3.5 a2.5 2.5 0 0 1 5 0 V5" stroke="#ffffff" strokeWidth="1.4" fill="none" />
    <rect x="1" y="5" width="8" height="6.5" rx="1" fill="#ffffff" />
  </svg>
);

/** The player's own (editable) template badge. */
export const PencilIcon = () => (
  <svg width="12rem" height="12rem" viewBox="0 0 12 12">
    <path d="M1 11 L1.6 8.2 L8.4 1.4 L10.6 3.6 L3.8 10.4 Z" fill="#ffd400" />
  </svg>
);

/** Two overlapping sheets, for duplicate buttons. */
export const DuplicateIcon = (props: { size?: number }) => (
  <svg width={(props.size ?? 12) + "rem"} height={(props.size ?? 12) + "rem"} viewBox="0 0 12 12">
    <rect x="0.75" y="0.75" width="7" height="7" rx="1" fill="none" stroke="#ffffff" strokeWidth="1.5" />
    <rect x="4" y="4" width="7.25" height="7.25" rx="1" fill="#ffffff" />
  </svg>
);

/** A small trash can, for delete buttons. */
export const TrashIcon = (props: { size?: number }) => (
  <svg width={(props.size ?? 12) + "rem"} height={(props.size ?? 12) + "rem"} viewBox="0 0 12 12">
    <rect x="1" y="2" width="10" height="1.5" fill="#ffffff" />
    <rect x="4.5" y="0.5" width="3" height="1.5" fill="#ffffff" />
    <path d="M2.5 4 H9.5 L9 11.5 H3 Z" fill="#ffffff" />
  </svg>
);

/** Text-alignment icon: four lines aligned left, center or right. */
export const AlignIcon = (props: { align: string }) => {
  const widths = [16, 10, 14, 8];
  const x = (w: number) => (props.align === "end" ? 18 - w : props.align === "center" ? (18 - w) / 2 : 0) + 1;
  return (
    <svg width="20rem" height="16rem" viewBox="0 0 20 16">
      {widths.map((w, i) => <rect key={i} x={x(w)} y={1 + i * 4} width={w} height="2" fill="#ffffff" />)}
    </svg>
  );
};

/** A sign outline with an exit tab on the given side (or none). */
export const ExitTabIcon = (props: { side: string }) => (
  <svg width="28rem" height="18rem" viewBox="0 0 28 18">
    {props.side !== "none" && <rect x={props.side === "left" ? 1 : 15} y="1" width="12" height="6" fill="#2e9d6a" stroke="#ffffff" strokeWidth="1" />}
    <rect x="1" y="6" width="26" height="11" fill="#2e9d6a" stroke="#ffffff" strokeWidth="1" />
  </svg>
);

/** EXIT ONLY panel icon: a sign with a yellow bar and its arrows. */
export const ExitOnlyIcon = (props: { arrows: number }) => (
  <svg width="28rem" height="18rem" viewBox="0 0 28 18">
    <rect x="1" y="1" width="26" height="16" fill="#2e9d6a" stroke="#ffffff" strokeWidth="1" />
    {props.arrows > 0 && <rect x="1.5" y="11" width="25" height="5.5" fill="#ffcc00" />}
    {props.arrows > 0 && <path d="M12 12 h2 v2 h1.5 l-2.5 2.2 l-2.5 -2.2 h1.5 z" fill="#000000" />}
    {props.arrows > 1 && <path d="M4 12 h2 v2 h1.5 l-2.5 2.2 l-2.5 -2.2 h1.5 z M21 12 h2 v2 h1.5 l-2.5 2.2 l-2.5 -2.2 h1.5 z" fill="#000000" />}
  </svg>
);

/** A theme's route plate as a small icon: its artwork, or a colored plate with its prefix. */
export const ThemeShieldIcon = (props: { theme?: { base: string; def: any }; network: string }) => {
  const s = props.theme?.def?.shields?.[props.network];
  if (!s) return <span>{props.network}</span>;
  if (s.img) return <img src={props.theme!.base + s.img} style={{ width: "22rem", height: "22rem" }} />;
  if (s.shape === "ellipse") return <div style={{ width: "24rem", height: "18rem", borderRadius: "50%", backgroundColor: s.bg ?? "#fff", border: `2rem solid ${s.border ?? "#000"}` }} />;
  const label = ((s.prefix ?? "") + (s.default ?? "")).trim() || props.network;
  return (
    <div style={{ padding: "1rem 5rem", fontSize: "12rem", fontWeight: "bold", color: s.fg ?? "#000", backgroundColor: s.shape === "plain" ? "rgba(0,0,0,0.5)" : s.bg ?? "#fff",
      border: s.border ? `2rem solid ${s.border}` : undefined, borderRadius: "3rem", whiteSpace: "nowrap" }}>{label}</div>
  );
};
