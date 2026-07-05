/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Damage.Systems;

namespace Content.Shared._CE.ZLevels.Damage.FallingDamage;

public sealed class CEFallingDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly Content.Shared.Blocking.BlockingSystem _blocking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallingDamageComponent, CEZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<CEFallingDamageComponent> ent, ref CEZFellOnMeEvent args)
    {
        if (args.Cancelled)
            return;

        // Check if target is blocking overhead with a shield
        if (_blocking.IsBlockingOverhead(ent.Owner, out var blocking))
        {
            var audio = EntityManager.System<Robust.Shared.Audio.Systems.SharedAudioSystem>();
            audio.PlayPvs(blocking.BlockSound, ent.Owner);
            args.Cancel();
            return;
        }

        _damage.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
