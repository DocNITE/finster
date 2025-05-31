using Content.Client._ViewportGui.ViewportUserInterface;
using Content.Client._ViewportGui.ViewportUserInterface.UI;

public class HUDStorageContainer : HUDControl
{
    [Dependency] private readonly IViewportUserInterfaceManager _vpUIManager = default!;

    public HUDStorageContainer()
    {
        IoCManager.InjectDependencies(this);
    }
}
