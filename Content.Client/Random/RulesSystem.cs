using System.Numerics;
using Content.Client.Instruments;
using Content.Shared._Finster.TestHaus;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Area;
using Content.Shared.Audio.Jukebox;
using Content.Shared.CombatMode;
using Content.Shared.Instruments;
using Content.Shared.Random;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Client.RandomRules;

public class RulesSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly AccessReaderSystem _reader = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly AreaSystem _area = default!;

    public bool IsTrue(EntityUid uid, RulesPrototype rules)
    {
        var inRange = new HashSet<Entity<IComponent>>();
        foreach (var rule in rules.Rules)
        {
            switch (rule)
            {
                case AlwaysTrueRule:
                    break;
                case CombatModeRule combatRule:
                    if (TryComp<CombatModeComponent>(uid, out var combatMode))
                    {
                        if (combatMode.IsInCombatMode == combatRule.State)
                            continue;
                        else
                            return false;
                    }
                    else
                    {
                        continue;
                    }
                    break;
                case GridInRangeRule griddy:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform))
                    {
                        return false;
                    }

                    if (xform.GridUid != null)
                    {
                        return !griddy.Inverted;
                    }

                    var worldPos = _transform.GetWorldPosition(xform);
                    var gridRange = new Vector2(griddy.Range, griddy.Range);

                    foreach (var _ in _mapManager.FindGridsIntersecting(
                                 xform.MapID,
                                 new Box2(worldPos - gridRange, worldPos + gridRange)))
                    {
                        return !griddy.Inverted;
                    }

                    break;
                }
                case InSpaceRule:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid != null)
                    {
                        return false;
                    }

                    break;
                }
                case OutOfArea outOfArea:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid == null)
                        return false;

                    var areaId = _area.GetAreaForEntity(uid);
                    if (areaId == null)
                        return true;
                    else
                        return false;
                }
                case InArea area:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid == null)
                        return false;

                    var areaId = _area.GetAreaForEntity(uid);
                    if (areaId == area.ID)
                        return true;
                    else
                        return false;
                }
                case NearbyAccessRule access:
                {
                    var xformQuery = GetEntityQuery<TransformComponent>();

                    if (!xformQuery.TryGetComponent(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform, xformQuery);
                    var count = 0;

                    // TODO: Update this when we get the callback version
                    var entities = new HashSet<Entity<AccessReaderComponent>>();
                    _lookup.GetEntitiesInRange(xform.MapID, worldPos, access.Range, entities);
                    foreach (var comp in entities)
                    {
                        if (!_reader.AreAccessTagsAllowed(access.Access, comp) ||
                            access.Anchored &&
                            (!xformQuery.TryGetComponent(comp, out var compXform) ||
                             !compXform.Anchored))
                        {
                            continue;
                        }

                        count++;

                        if (count < access.Count)
                            continue;

                        found = true;
                        break;
                    }

                    if (!found)
                        return false;

                    break;
                }
                case NearbyComponentsRule nearbyComps:
                {
                    var xformQuery = GetEntityQuery<TransformComponent>();

                    if (!xformQuery.TryGetComponent(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform);
                    var count = 0;

                    foreach (var compType in nearbyComps.Components.Values)
                    {
                        inRange.Clear();
                        _lookup.GetEntitiesInRange(compType.Component.GetType(), xform.MapID, worldPos, nearbyComps.Range, inRange);
                        foreach (var comp in inRange)
                        {
                            if (nearbyComps.Anchored &&
                                (!xformQuery.TryGetComponent(comp, out var compXform) ||
                                 !compXform.Anchored))
                            {
                                continue;
                            }

                            count++;

                            if (count < nearbyComps.Count)
                                continue;

                            found = true;
                            break;
                        }

                        if (found)
                            break;
                    }

                    if (!found)
                        return !nearbyComps.State;
                    else
                        return nearbyComps.State;

                    break;
                }
                case NearbyMusicSourceRule nMSRule:
                {
                    var xformQuery = GetEntityQuery<TransformComponent>();

                    if (!xformQuery.TryGetComponent(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform);

                    // Check for music sources
                    {
                        foreach (var ent in _lookup.GetEntitiesInRange(xform.MapID, worldPos, nMSRule.Range))
                        {
                            if (TryComp<InstrumentComponent>(ent, out var iComp) && iComp.Playing ||
                                TryComp<JukeboxComponent>(ent, out var jukeboxComp) && jukeboxComp.AudioStream != null)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found == nMSRule.State)
                        break;
                    else
                        return false;

                    break;
                }
                case NearbyEntitiesRule entity:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    var found = false;
                    var worldPos = _transform.GetWorldPosition(xform);
                    var count = 0;

                    foreach (var ent in _lookup.GetEntitiesInRange(xform.MapID, worldPos, entity.Range))
                    {
                        if (_whitelistSystem.IsWhitelistFail(entity.Whitelist, ent))
                            continue;

                        count++;

                        if (count < entity.Count)
                            continue;

                        found = true;
                        break;
                    }

                    if (!found)
                        return false;

                    break;
                }
                case NearbyTilesPercentRule tiles:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        !TryComp<MapGridComponent>(xform.GridUid, out var grid))
                    {
                        return false;
                    }

                    var physicsQuery = GetEntityQuery<PhysicsComponent>();
                    var tileCount = 0;
                    var matchingTileCount = 0;

                    foreach (var tile in grid.GetTilesIntersecting(new Circle(_transform.GetWorldPosition(xform),
                                 tiles.Range)))
                    {
                        // Only consider collidable anchored (for reasons some subfloor stuff has physics but non-collidable)
                        if (tiles.IgnoreAnchored)
                        {
                            var gridEnum = grid.GetAnchoredEntitiesEnumerator(tile.GridIndices);
                            var found = false;

                            while (gridEnum.MoveNext(out var ancUid))
                            {
                                if (!physicsQuery.TryGetComponent(ancUid, out var physics) ||
                                    !physics.CanCollide)
                                {
                                    continue;
                                }

                                found = true;
                                break;
                            }

                            if (found)
                                continue;
                        }

                        tileCount++;

                        if (!tiles.Tiles.Contains(_tileDef[tile.Tile.TypeId].ID))
                            continue;

                        matchingTileCount++;
                    }

                    if (tileCount == 0 || matchingTileCount / (float) tileCount < tiles.Percent)
                        return false;

                    break;
                }
                case OnMapGridRule:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid != xform.MapUid ||
                        xform.MapUid == null)
                    {
                        return false;
                    }

                    break;
                }
                case OnDevMapRule:
                {
                    if (!TryComp<TransformComponent>(uid, out var xform) ||
                        xform.GridUid != xform.MapUid ||
                        xform.MapUid == null ||
                        !TryComp<TestHausComponent>(xform.MapUid, out var _))
                    {
                        return false;
                    }

                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }

        return true;
    }
}
