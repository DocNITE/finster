using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Area;

public sealed class AreaSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SetTileAreaComponent, ComponentStartup>(OnTileAreaStartup);
        SubscribeLocalEvent<RemoveTileAreaComponent, ComponentStartup>(OnRemoveTileAreaStartup);
    }

    private void OnTileAreaStartup(EntityUid uid, SetTileAreaComponent component, ComponentStartup args)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null ||
            !TryComp<AreaComponent>(xform.GridUid.Value, out var areaZones))
            return;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tilePos = grid.TileIndicesFor(xform.Coordinates);
        var markerProto = MetaData(uid).EntityPrototype;
        if (markerProto is null)
            return;
        var areaId = markerProto.ID;

        // Add zone
        if (!areaZones.Data.TryGetValue(areaId, out var areaData))
        {
            areaData = new AreaData { Color = component.Color };
            areaZones.Data[areaId] = areaData;
        }

        areaData.Tiles.Add(tilePos);
        Dirty(gridUid, areaZones);

        // Remove marker because it is necessary lol
        Del(uid);
    }

    private void OnRemoveTileAreaStartup(EntityUid uid, RemoveTileAreaComponent marker, ComponentStartup args)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null ||
            !TryComp<AreaComponent>(xform.GridUid.Value, out var areaZones))
            return;

        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tilePos = grid.TileIndicesFor(xform.Coordinates);

        // Remove this tile from ALL zones
        bool anyZonesModified = false;
        foreach (var (zoneId, zoneData) in areaZones.Data)
        {
            if (zoneData.Tiles.Remove(tilePos))
                anyZonesModified = true;
        }

        // Clean up empty zones
        if (anyZonesModified)
        {
            var zonesToDelete = areaZones.Data
                .Where(kv => kv.Value.Tiles.Count == 0)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var zoneId in zonesToDelete)
            {
                areaZones.Data.Remove(zoneId);
            }
        }

        Dirty(gridUid, areaZones);

        // Delete the marker
        Del(uid);
    }

    public string? GetAreaForEntity(EntityUid uid, AreaComponent? area = null)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null ||
            !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
            return null;

        if (!Resolve(xform.GridUid.Value, ref area, false))
            return null;

        var tilePos = grid.TileIndicesFor(xform.Coordinates);

        foreach (var (zoneId, areaData) in area.Data)
        {
            if (areaData.Tiles.Contains(tilePos))
                return zoneId;
        }

        return null;
    }
}
