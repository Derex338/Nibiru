using Content.Shared._Nibiru.Factions;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client._Nibiru.Faction.Systems;

/// <summary>
/// Client-сайд система. Когда статуя получает выбранного члена фракции,
/// копирует его спрайт (RSI+state+color) на статую навсегда.
/// </summary>
public sealed partial class FactionStatueVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionStatueComponent, ComponentStartup>(OnStartup);
        //SubscribeLocalEvent<FactionStatueComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(Entity<FactionStatueComponent> ent, ref ComponentStartup args)
    {
        UpdateStatue(ent, ent.Comp);
    }

    private void OnHandleState(Entity<FactionStatueComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateStatue(ent, ent.Comp);
    }

    public void UpdateStatue(EntityUid entity, FactionStatueComponent component)
    {
        if (component.SelectedMember == null)
            return;

        if (!TryComp(entity, out SpriteComponent? statueSprite))
            return;

        var member = GetEntity(component.SelectedMember.Value);
        if (!TryComp(member, out SpriteComponent? memberSprite))
            return;

        // При добавлении старых капченых слоёв не обновляем повторно
        // (SelectedMember уже был выбран ранее — слои уже скопированы)
        if (statueSprite.AllLayers.Count() > 1)
            return;

        CopyLayers(statueSprite, memberSprite);
    }

    /// <summary>
    /// Копирует все видимые слои с мембера на статую, начиная со слоя 1 (0 — постамент).
    /// </summary>
    private void CopyLayers(SpriteComponent statueSprite, SpriteComponent memberSprite)
    {
        int layerIndex = 1;

        foreach (var layer in memberSprite.AllLayers)
        {
            if (!layer.Visible)
                continue;

            var rsi = layer.ActualRsi;
            if (rsi == null)
                continue;

            // Добавляем слой и настраиваем
            var newIdx = statueSprite.AddBlankLayer(layerIndex);
            statueSprite.LayerSetState(newIdx, layer.RsiState, rsi.Path);
            statueSprite.LayerSetColor(newIdx, layer.Color);
            statueSprite.LayerSetVisible(newIdx, true);

            layerIndex++;
        }
    }
}
