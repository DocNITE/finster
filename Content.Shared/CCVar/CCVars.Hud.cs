using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> HudTheme =
        CVarDef.Create("finster_hud.theme", 0, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> HudType =
        CVarDef.Create("finster_hud.type", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> HudCustomCursor =
        CVarDef.Create("finster_hud.custom_cursor", false, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> HudHeldItemShow =
        CVarDef.Create("finster_hud.held_item_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> OfferModeIndicatorsPointShow =
        CVarDef.Create("finster_hud.offer_mode_indicators_point_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> CombatModeIndicatorsPointShow =
        CVarDef.Create("finster_hud.combat_mode_indicators_point_show", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> LoocAboveHeadShow =
        CVarDef.Create("finster_hud.show_looc_above_head", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> HudHeldItemOffset =
        CVarDef.Create("finster_hud.held_item_offset", 28f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> HudFpsCounterVisible =
        CVarDef.Create("finster_hud.fps_counter_visible", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ModernProgressBar =
        CVarDef.Create("finster_hud.modern_progress_bar", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
