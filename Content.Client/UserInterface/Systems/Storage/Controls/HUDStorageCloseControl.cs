namespace Content.Client.UserInterface.Systems.Storage.Controls;

public class HUDStorageCloseControl : HUDItemGridControl
{
    public HUDStorageCloseControl()
    {
        IoCManager.InjectDependencies(this);

        Name = Loc.GetString("slotbutton-storage-close");
        Size = (DefaultButtonSize, DefaultButtonSize);
        CanEmitSound = false;
        Texture = VPUIManager.GetThemeTexture("Storage/close");
    }
}
