using Robust.Shared.Serialization;

namespace Content.Shared.Inventory;

/// <summary>
///     Defines what slot types an item can fit into.
/// </summary>
[Serializable, NetSerializable]
[Flags]
public enum SlotFlags
{
    NONE = 0,
    PREVENTEQUIP = 1 << 0,
    HEAD = 1 << 1,
    EYES = 1 << 2,
    EARS = 1 << 3,
    MASK = 1 << 4,
    OUTERCLOTHING = 1 << 5,
    UNDERCLOTHING = 1 << 6, // Shirts (Uniform2)
    INNERCLOTHING = 1 << 7, // Pants and legacy jumpsuits (Uniform)
    NECK = 1 << 8,
    BACK = 1 << 9,
    BACK2 = 1 << 10,
    BELT = 1 << 11,
    GLOVES = 1 << 12,
    WRISTRIGHT = 1 << 13,
    WRISTLEFT = 1 << 14,
    IDCARD = 1 << 15,
    POCKET = 1 << 16,
    LEGS = 1 << 17,
    FEET = 1 << 18,
    SUITSTORAGE = 1 << 19, // LEGACY
    All = ~NONE,

    WITHOUT_POCKET = All & ~POCKET
}
