using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Climbing.Components;

/// <summary>
/// Indicates that this entity is able to be placed on top of surfaces like tables.
/// Does not by itself allow the entity to carry out the action of climbing, unless
/// <see cref="CanClimb"/> is true. Use <see cref="CanForceClimb"/> to control whether
/// the entity can force other entities onto surfaces.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ClimbingComponent : Component
{
    /// <summary>
    /// Whether the owner is able to climb onto things by their own action.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanClimb = true;

    /// <summary>
    /// Whether the owner is climbing on a climbable entity.
    /// </summary>
    [AutoNetworkedField, DataField]
    public bool IsClimbing;

    /// <summary>
    /// Whether the owner is being moved onto the climbed entity.
    /// </summary>
    [AutoNetworkedField, DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan? NextTransition;

    /// <summary>
    /// Direction to move when transition.
    /// </summary>
    [AutoNetworkedField, DataField]
    public Vector2 Direction;

    /// <summary>
    /// Base range for descdending.
    /// It should use for descend on bottom maps from top.
    /// </summary>
    [DataField]
    public float DescendRange = 2f;

    /// <summary>
    /// If is not null - instead of climb on object, we should move
    /// character on target map (on top or bottom)
    /// </summary>
    public EntityCoordinates? DescendCoords;

    public bool IgnoreSkillCheck;

    /// <summary>
    /// How fast the entity is moved when climbing.
    /// </summary>
    [DataField]
    public float TransitionRate = 5f;

    [AutoNetworkedField, DataField]
    public Dictionary<string, int> DisabledFixtureMasks = new();
}
