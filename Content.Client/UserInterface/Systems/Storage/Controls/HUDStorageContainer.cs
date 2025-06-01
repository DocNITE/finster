using System.Linq;
using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Client.Hands.Systems;
using Content.Client.Storage.Systems;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public class HUDStorageContainer : HUDBoxContainer
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public EntityUid? StorageEntity;

    public bool IsOpen => Visible;

    public HUDGameplayType StorageStyle = HUDGameplayType.Lifeweb;

    private StorageUIController _controller;

    public HUDStorageContainer(StorageUIController uiController)
    {
        IoCManager.InjectDependencies(this);

        _controller = uiController;
    }

    public void UpdateContainer(Entity<StorageComponent>? entity)
    {
        Visible = entity != null;
        StorageEntity = entity;
        if (entity == null)
            return;

        BuildGridRepresentation();
    }

    private void BuildGridRepresentation()
    {
        BuildItems();
    }

    public void BuildItems()
    {
        if (!_entity.EntityExists(StorageEntity))
            return;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComp))
            return;

        if (!storageComp.Grid.Any())
            return;

        var boundingGrid = storageComp.Grid.GetBoundingBox();
        var size = HUDItemGridControl.DefaultButtonSize;

        DisposeAllChildren();

        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                var control = new HUDItemGridControl();
                var currentPosition = new Vector2i(x, y);
                var uiPosition = new Vector2i(x, y);
                if (StorageStyle == HUDGameplayType.Interbay)
                    uiPosition = new Vector2i(y, x); // Inverted :p

                foreach (var (itemEnt, itemPos) in storageComp.StoredItems)
                {
                    if (itemPos.Position != currentPosition)
                        continue;

                    control.Entity = itemEnt;
                    var metadata = IoCManager.Resolve<IEntityManager>().GetComponent<MetaDataComponent>(itemEnt);
                    control.Name = metadata.EntityName;
                }

                control.Position = uiPosition * size;
                control.GridPosition = currentPosition;
                control.OnKeyBindDown += (args) =>
                {
                    _controller.ItemInteraction(args, control);
                };
                AddChild(control);
            }
        }

        // Update container size for next calculation
        UpdateSize();

        // There need add close button
        var closeButton = new HUDStorageCloseControl();
        if (StorageStyle == HUDGameplayType.Interbay)
            closeButton.Position = (0, Size.Y);
        else
            closeButton.Position = (Size.X, 0);
        closeButton.OnPressed += (args) =>
        {
            Close();
        };
        AddChild(closeButton);
    }

    public void Close()
    {
        Visible = false;

        if (StorageEntity == null)
            return;

        _entity.System<StorageSystem>().CloseStorageWindow(StorageEntity.Value);
        StorageEntity = null;
    }
}
