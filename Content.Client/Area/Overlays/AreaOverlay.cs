using System.Numerics;
using Content.Shared.Area;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Area.Overlays;

public sealed class AreaOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<AreaComponent> _areaQuery;

    public AreaOverlay()
    {
        _gridQuery = _entityManager.GetEntityQuery<MapGridComponent>();
        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
        _areaQuery = _entityManager.GetEntityQuery<AreaComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var xformSystem = EntitySystem.Get<TransformSystem>();

        var playerUid = _player.LocalEntity;

        if (playerUid is null)
            return;

        if (!_xformQuery.TryComp(playerUid.Value, out var xform) || xform is null)
            return;

        var grid = xformSystem.GetGrid(playerUid.Value);
        if (grid is null ||
            !_gridQuery.TryComp(grid, out var gridComp))
            return;

        if (!_areaQuery.TryComp(grid.Value, out var areaZones))
            return;

        foreach (var (zoneId, zoneData) in areaZones.Data)
        {
            foreach (var tile in zoneData.Tiles)
            {
                var worldPos = gridComp.GridTileToWorldPos(tile);
                var aabb = new Box2(worldPos, worldPos + gridComp.TileSizeVector);

                handle.DrawRect(aabb, zoneData.Color);
            }
        }
    }
}
