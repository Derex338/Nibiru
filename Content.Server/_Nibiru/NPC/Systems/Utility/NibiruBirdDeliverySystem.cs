// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Nibiru.NPC.Systems.Utility;

public sealed class NibiruBirdDeliverySystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruPigeonPostComponent, InteractHandEvent>(OnPostInteract);
        SubscribeLocalEvent<NibiruBirdComponent, NibiruBirdSelectPostMessage>(OnPostSelected);
        SubscribeLocalEvent<NibiruPigeonPostComponent, GetVerbsEvent<Verb>>(AddRenameVerbs);
        SubscribeLocalEvent<NibiruPigeonPostComponent, NibiruRenamePostMessage>(OnRenamePost);
    }

    private void OnPostInteract(EntityUid uid, NibiruPigeonPostComponent component, InteractHandEvent args)
    {
        // Если игрок ведет птицу, привязываем её к этому отделению
        if (TryComp<NibiruAnimalCommanderComponent>(args.User, out var commander) && commander.Animals != null)
        {
            foreach (var animal in commander.Animals)
            {
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
    }

    public void OpenUi(EntityUid player, EntityUid birdUid)
    {
        if (!TryComp<NibiruBirdComponent>(birdUid, out var bird))
            return;

        if (bird.KnownPosts.Count == 0)
        {
            var postQuery = EntityQueryEnumerator<NibiruPigeonPostComponent>();
            while (postQuery.MoveNext(out var postUid, out _))
            {
                bird.KnownPosts.Add(postUid);
            }
        }

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

    private void AddRenameVerbs(EntityUid uid, NibiruPigeonPostComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("nibiru-pigeon-post-rename-verb"),
            Act = () => _ui.OpenUi(uid, NibiruRenamePostUiKey.Key, args.User),
            Icon = new SpriteSpecifier.Texture(new Robust.Shared.Utility.ResPath("/Textures/Interface/VerbIcons/tag.svg.192dpi.png"))
        });
    }

    private void OnRenamePost(EntityUid uid, NibiruPigeonPostComponent component, NibiruRenamePostMessage args)
    {
        if (string.IsNullOrWhiteSpace(args.Name))
            return;

        component.PostName = args.Name;
        _metaData.SetEntityName(uid, args.Name);
        //_popup.PopupEntity(Loc.GetString("nibiru-pigeon-post-renamed", ("name", args.Name)), uid);
    }
}
