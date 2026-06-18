using Content.Shared._Nibiru.ModularCraft;
using Robust.Client.GameObjects;

namespace Content.Client._Nibiru.ModularCraft;

public sealed class ModularCraftBoundUserInterface : BoundUserInterface
{
    private UI.ModularCraftMenu? _menu;

    public ModularCraftBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = new UI.ModularCraftMenu();
        _menu.OpenCentered();
        _menu.OnClose += Close;

        _menu.OnItemTypeSelected += type =>
        {
            SendMessage(new ModularCraftSelectTypeMessage(type));
        };

        _menu.OnSlotChanged += (part, mod, mat) =>
        {
            SendMessage(new ModularCraftSelectSlotMessage(part, mod, mat));
        };

        _menu.OnCraftPressed += () =>
        {
            SendMessage(new ModularCraftDoCraftMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ModularCraftBUIState craftState)
        {
            _menu?.UpdateState(craftState);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _menu?.Dispose();
        }
    }
}
