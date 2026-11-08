import { ModRegistrar } from "cs2/modding";
import { SignEditor } from "mods/SignEditor";
import { ToolbarButton } from "mods/ToolbarButton";
import { SignInfoSection } from "mods/SignInfoSection";
import { SignManager, SignManagerButton } from "mods/SignManager";
import { LanguageSync } from "mods/language";

const register: ModRegistrar = (moduleRegistry) => {
  moduleRegistry.append("GameTopLeft", ToolbarButton);
  moduleRegistry.append("Game", LanguageSync);
  moduleRegistry.append("Game", SignEditor);
  moduleRegistry.append("GameTopLeft", SignManagerButton);
  moduleRegistry.append("Game", SignManager);
  // C# SignBuilderSA.Systems.SignInfoSection writes sections under its full type name.
  moduleRegistry.extend(
    "game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx",
    "selectedInfoSectionComponents",
    (components: any) => {
      components["SignBuilderSA.Systems.SignInfoSection"] = SignInfoSection;
      return components;
    },
  );
};

export default register;
