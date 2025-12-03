//using Content.Shared.Buckle.Components;
//using Content.Shared.Movement.Components;
//using Content.Shared.Movement.Systems;

//namespace Content.Shared._Nibiru.Vehicle;

///// <summary>
///// Система интеграции транспорта с компонентом Strap для совместимости
///// </summary>
//public sealed class MountStrapIntegrationSystem : EntitySystem
//{
//    //[Dependency] private readonly SharedVehicleSystem _mount = default!;

//    public override void Initialize()
//    {
//        base.Initialize();

//        // Когда кто-то пристёгивается к сущности с MountComponent
//        SubscribeLocalEvent<VehicleComponent, StrappedEvent>(OnBuckleChanged);

//        // Когда кто-то отстёгивается от сущности с MountComponent
//        SubscribeLocalEvent<VehicleComponent, UnstrappedEvent>(OnUnbuckleChanged);
//    }

//    private void OnBuckleChanged(EntityUid uid, VehicleComponent component, ref StrappedEvent args)
//    {
//        // Если кто-то пристегнулся, пытаемся установить его как всадника
//        if (args.Buckle != null)
//        {
//            // Проверяем, есть ли уже всадник
//            if (component.RiderSlot.ContainedEntity == null)
//            {
                
//                var riderComp = EnsureComp<RiderComponent>(args.Buckle.Owner);
//                riderComp.Mount = uid;
//            }
//        }
//    }

//    private void OnUnbuckleChanged(EntityUid uid, VehicleComponent component, ref UnstrappedEvent args)
//    {
//        // Если кто-то отстегнулся, удаляем его как всадника
//        if (args.Buckle != null)
//        {
//            RemComp<RiderComponent>(args.Buckle.Owner);
//        }
//    }
//}
