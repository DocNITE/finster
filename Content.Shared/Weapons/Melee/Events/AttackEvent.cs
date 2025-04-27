using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Melee.Events
{
    [Serializable, NetSerializable]
    public abstract class AttackEvent : EntityEventArgs
    {
        /// <summary>
        /// Coordinates being attacked.
        /// </summary>
        public readonly NetCoordinates Coordinates;

        /// <summary>
        /// Ignore weapon cooldown and attack immediately.
        /// </summary>
        public readonly bool IgnoreCooldown = false;

        protected AttackEvent(NetCoordinates coordinates, bool ignoreCooldown = false)
        {
            Coordinates = coordinates;
            IgnoreCooldown = ignoreCooldown;
        }
    }

    /// <summary>
    ///     Event raised on entities that have been attacked.
    /// </summary>
    public sealed class AttackedEvent : EntityEventArgs
    {
        /// <summary>
        ///     Entity used to attack, for broadcast purposes.
        /// </summary>
        public EntityUid Used { get; }

        /// <summary>
        ///     Entity that triggered the attack.
        /// </summary>
        public EntityUid User { get; }

        /// <summary>
        ///     The original location that was clicked by the user.
        /// </summary>
        public EntityCoordinates ClickLocation { get; }

        public DamageSpecifier BonusDamage = new();

        public TargetBodyPart? TargetPart;

        public AttackedEvent(EntityUid used, EntityUid user, EntityCoordinates clickLocation, TargetBodyPart? targetPart)
        {
            Used = used;
            User = user;
            ClickLocation = clickLocation;
            TargetPart = targetPart;
        }
    }
}
