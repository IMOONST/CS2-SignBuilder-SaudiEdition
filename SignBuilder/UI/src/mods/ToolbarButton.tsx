import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { t } from "./l10n";
import { useLanguage } from "./language";
import { editorOpen$, setEditorOpen } from "./bindings";

/** Top-left toolbar button that opens and closes the Sign Builder editor. */
export const ToolbarButton = () => {
  // Re-render the tooltip when the interface language changes.
  useLanguage();
  const open = useValue(editorOpen$);
  return (
    <Button variant="floating" selected={open} onSelect={() => setEditorOpen(!open)} tooltipLabel={t("Sign Builder: Saudi Edition")}>
      <img src="coui://signbuildersa/icon.svg" style={{ width: "30rem", height: "30rem" }} />
    </Button>
  );
};
