using System.Numerics;
using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;
using Content.Client.Examine;
using Content.Client.Hands.Systems;
using Content.Client.Interaction;
using Content.Client.Storage.Systems;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Storage.Controls;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Client.Verbs.UI;
using Content.Shared.CCVar;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Storage;

public sealed class StorageUIController : UIController, IOnSystemChanged<StorageSystem>
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IViewportUserInterfaceManager _vpUIManager = default!;

    private HUDStorageContainer? _container;

    public override void Initialize()
    {
        base.Initialize();

        _vpUIManager.OnScreenLoad += OnHudScreenLoad;
        _vpUIManager.OnScreenUnload += OnHudScreenUnload;
    }

    private void OnHudScreenLoad(HUDRoot hud)
    {
        var hudGameplay = hud as HUDGameplayState;
        if (hudGameplay is null)
            return;

        _container = new HUDStorageContainer(this);
        if (hudGameplay.Type == HUDGameplayType.Lifeweb)
            _container.Position = (EyeManager.PixelsPerMeter * 3, EyeManager.PixelsPerMeter * 4);
        else
            _container.Position = (0,
            (EyeManager.PixelsPerMeter * ViewportUIController.ViewportHeight) - (EyeManager.PixelsPerMeter * 3)
            );
        hud.AddChild(_container);
    }

    private void OnHudScreenUnload(HUDRoot hud)
    {
        _container?.Close();
        _container = null;
    }

    public void OnSystemLoaded(StorageSystem system)
    {
        system.StorageUpdated += OnStorageUpdated;
        system.StorageOrderChanged += OnStorageOrderChanged;
    }

    public void OnSystemUnloaded(StorageSystem system)
    {
        system.StorageUpdated -= OnStorageUpdated;
        system.StorageOrderChanged -= OnStorageOrderChanged;
    }

    /// <summary>
    /// Update container, set the storageEntity for container.
    /// If null - container is empty, and should be unvisible.
    /// </summary>
    private void OnStorageOrderChanged(Entity<StorageComponent>? nullEnt)
    {
        if (_container is null)
            return;

        _container.UpdateContainer(nullEnt);

        if (nullEnt is null)
            _container.Close();
    }

    /// <summary>
    /// When something changed in storage - update UI.
    /// Raised on inserting, removing, deleting and etc.
    /// </summary>
    private void OnStorageUpdated(Entity<StorageComponent> uid)
    {
        if (_container?.StorageEntity != uid)
            return;

        _container.BuildItems();
    }

    public void ItemInteraction(GUIBoundKeyEventArgs args, HUDItemGridControl control)
    {
        if (_container is null ||
            !_container.IsOpen == true ||
            _container.StorageEntity is null)
            return;

        var storageSystem = _entity.System<StorageSystem>();
        var handsSystem = _entity.System<HandsSystem>();

        if (args.Function == ContentKeyFunctions.MoveStoredItem)
        {
            if (handsSystem.GetActiveHandEntity() is { } handEntity &&
                storageSystem.CanInsert(_container.StorageEntity.Value, handEntity, out _))
            {
                var pos = control.GridPosition;
                var insertLocation = new ItemStorageLocation(Angle.Zero, pos);

                if (storageSystem.ItemFitsInGridLocation(
                        (handEntity, null),
                        (_container.StorageEntity.Value, null),
                        insertLocation))
                {
                    _entity.RaisePredictiveEvent(new StorageInsertItemIntoLocationEvent(
                        _entity.GetNetEntity(handEntity),
                        _entity.GetNetEntity(_container.StorageEntity.Value),
                        insertLocation));
                    args.Handle();
                }
            }
            else
            {
                if (control.Entity is null)
                    return;

                _entity.RaisePredictiveEvent(new StorageInteractWithItemEvent(
                    _entity.GetNetEntity(control.Entity.Value),
                    _entity.GetNetEntity(_container.StorageEntity.Value)));
            }

            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.SaveItemLocation)
        {
            if (_container?.StorageEntity is not { } storage ||
                control.Entity is null)
                return;

            _entity.RaisePredictiveEvent(new StorageSaveItemLocationEvent(
                _entity.GetNetEntity(control.Entity.Value),
                _entity.GetNetEntity(storage)));
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.TextCursorSelect ||
            args.Function == ContentKeyFunctions.ExamineEntity)
        {
            if (control.Entity is null)
                return;

            _entity.System<ExamineSystem>().DoExamine(control.Entity.Value);
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick ||
            args.Function == EngineKeyFunctions.UseSecondary)
        {
            if (control.Entity is null)
                return;

            UIManager.GetUIController<VerbMenuUIController>().OpenVerbMenu(control.Entity.Value);
            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.ActivateItemInWorld)
        {
            if (control.Entity is null)
                return;

            _entity.RaisePredictiveEvent(
                new InteractInventorySlotEvent(_entity.GetNetEntity(control.Entity.Value), altInteract: false));
            args.Handle();
        }
        else if (args.Function == ContentKeyFunctions.AltActivateItemInWorld)
        {
            if (control.Entity is null)
                return;

            _entity.RaisePredictiveEvent(new InteractInventorySlotEvent(_entity.GetNetEntity(control.Entity.Value), altInteract: true));
            args.Handle();
        }
    }
}
