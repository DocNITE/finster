using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     When true, Jetpacks can be enabled anywhere, even in gravity.
    /// </summary>
    public static readonly CVarDef<bool> JetpackEnableAnywhere =
        CVarDef.Create("finster_jetpack.enable_anywhere", true, CVar.REPLICATED);

    /// <summary>
    ///     When true, jetpacks can be enabled on grids that have zero gravity.
    /// </summary>
    public static readonly CVarDef<bool> JetpackEnableInNoGravity =
        CVarDef.Create("finster_jetpack.enable_in_no_gravity", true, CVar.REPLICATED);
}
