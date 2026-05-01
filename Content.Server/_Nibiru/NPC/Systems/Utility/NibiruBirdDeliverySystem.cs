// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Utility;

public sealed class NibiruBirdDeliverySystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruPigeonPostComponent, InteractHandEvent>(OnPostInteract);
        SubscribeLocalEvent<NibiruBirdComponent, NibiruBirdSelectPostMessage>(OnPostSelected);
    }

    private void OnPostInteract(EntityUid uid, NibiruPigeonPostComponent component, InteractHandEvent args)
    {
        // Если игрок ведет птицу, привязываем её к этому отделению
        if (TryComp<NibiruAnimalCommanderComponent>(args.User, out var commander) && commander.CurrentAnimal != null)
        {
            var animal = commander.CurrentAnimal.Value;
            if (TryComp<NibiruBirdComponent>(animal, out var bird))
            {
                if (!bird.KnownPosts.Contains(uid))
                {
                    bird.KnownPosts.Add(uid);
                    _popup.PopupEntity(Loc.GetString("nibiru-bird-post-learned", ("post", component.PostName)), animal, args.User);
                    args.Handled = true;
                }
            }
        }
    }

    public void OpenUi(EntityUid player, EntityUid birdUid)
    {
        if (!TryComp<NibiruBirdComponent>(birdUid, out var bird))
            return;

        var posts = new Dictionary<NetEntity, string>();
        foreach (var postUid in bird.KnownPosts)
        {
            if (TryComp<NibiruPigeonPostComponent>(postUid, out var post))
            {
                posts[GetNetEntity(postUid)] = post.PostName;
            }
        }

        _ui.SetUiState(birdUid, NibiruBirdDeliveryUiKey.Key, new NibiruBirdDeliveryUiState(posts));
        _ui.OpenUi(birdUid, NibiruBirdDeliveryUiKey.Key, player);
    }

    private void OnPostSelected(EntityUid uid, NibiruBirdComponent component, NibiruBirdSelectPostMessage args)
    {
        var postUid = GetEntity(args.Post);
        if (!EntityManager.EntityExists(postUid))
            return;

        // Птица улетает (исчезает и телепортируется через время)
        _popup.PopupEntity(Loc.GetString("nibiru-bird-delivery-depart"), uid);
        
        var targetPos = _transform.GetMapCoordinates(postUid);
        
        // Логика полета: телепортация через время (например, 1 сек на 10 метров)
        var currentPos = _transform.GetMapCoordinates(uid);
        var dist = (targetPos.Position - currentPos.Position).Length();
        var delay = Math.Clamp(dist * 0.1f, 2f, 30f);

        // Временно скрываем птицу в голубятне назначения
        var containerSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<Robust.Server.Containers.ContainerSystem>();
        var container = containerSystem.EnsureContainer<Robust.Shared.Containers.Container>(postUid, "bird_delivery_cage");
        containerSystem.Insert(uid, container);
        
        Timer.Spawn(TimeSpan.FromSeconds(delay), () => {
            if (EntityManager.Deleted(uid)) return;
            
            containerSystem.Remove(uid, container);
            _transform.SetMapCoordinates(uid, targetPos);
            _popup.PopupEntity(Loc.GetString("nibiru-bird-delivery-arrive"), uid);
        });
    }
}
