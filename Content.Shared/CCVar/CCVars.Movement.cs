using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Is mob pushing enabled.
    /// </summary>
    public static readonly CVarDef<bool> MovementMobPushing =
        CVarDef.Create("finster_movement.mob_pushing", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Can we push mobs not moving.
    /// </summary>
    public static readonly CVarDef<bool> MovementPushingStatic =
        CVarDef.Create("finster_movement.pushing_static", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Dot product for the pushed entity's velocity to a target entity's velocity before it gets moved.
    /// </summary>
    public static readonly CVarDef<float> MovementPushingVelocityProduct =
        CVarDef.Create("finster_movement.pushing_velocity_product", 0.0f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Cap for how much an entity can be pushed per second.
    /// </summary>
    public static readonly CVarDef<float> MovementPushingCap =
        CVarDef.Create("finster_movement.pushing_cap", 100f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Minimum pushing impulse per tick. If the value is below this it rounds to 0.
    /// This is an optimisation to avoid pushing small values that won't actually move the mobs.
    /// </summary>
    public static readonly CVarDef<float> MovementMinimumPush =
        CVarDef.Create("finster_movement.minimum_push", 0.1f, CVar.SERVER | CVar.REPLICATED);

    // Really this just exists because hot reloading is cooked on rider.
    /// <summary>
    /// Penetration depth cap for considering mob collisions.
    /// </summary>
    public static readonly CVarDef<float> MovementPenetrationCap =
        CVarDef.Create("finster_movement.penetration_cap", 0.3f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Based on the mass difference multiplies the push amount by this proportionally.
    /// </summary>
    public static readonly CVarDef<float> MovementPushMassCap =
        CVarDef.Create("finster_movement.push_mass_cap", 1.75f, CVar.SERVER | CVar.REPLICATED);
}
