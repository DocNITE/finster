using Content.KayMisaZlevels.Shared.Miscellaneous;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Climbing.Components
{
    /// <summary>
    /// Indicates this entity can be vaulted on top of.
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed partial class ClimbableComponent : Component
    {
        /// <summary>
        ///     The range from which this entity can be climbed.
        /// </summary>
        [DataField("range")] public float Range = SharedInteractionSystem.InteractionRange / 1.4f;

        /// <summary>
        ///     The time it takes to climb onto the entity.
        /// </summary>
        [DataField("delay")]
        public float ClimbDelay = 1.5f;

        /// <summary>
        ///     Sound to be played when a climb is started.
        /// </summary>
        [DataField("startClimbSound")]
        public SoundSpecifier? StartClimbSound = null;

        /// <summary>
        ///     Sound to be played when a climb finishes.
        /// </summary>
        [DataField("finishClimbSound")]
        public SoundSpecifier? FinishClimbSound = null;

        /// <summary>
        /// If not null - move player on Z level.
        /// </summary>
        [DataField]
        public ClimbDirection? DescendDirection;

        [DataField]
        public bool IgnoreSkillCheck;

        /// <summary>
        /// Should be used for ladders on descend.
        /// </summary>
        [DataField]
        public bool IgnoreTiles;
    }

    public enum ClimbDirection : byte
    {
        None,
        Up,
        Down,
    }
}
