using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Research
{
    [DataDefinition]
    public sealed partial class MaterialRequiredTech 
    {
        [DataField("material")]
        public ProtoId<StackPrototype> MaterialPrototypeId { get; private set; }

        [DataField] public int Amount { get; private set; } = 1;
		[DataField("store")] public string Store { get; private set; } = string.Empty;

        public void DoExamine(ExaminedEvent examinedEvent)
        {
            var material = IoCManager.Resolve<IPrototypeManager>().Index(MaterialPrototypeId);
            var materialName = Loc.GetString(material.Name, ("amount", Amount));

            examinedEvent.PushMarkup(Loc.GetString("construction-insert-material-entity", ("amount", Amount), ("materialName", materialName)));
        }

        public bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
        {
            return entityManager.TryGetComponent(uid, out StackComponent? stack) && stack.StackTypeId == MaterialPrototypeId && stack.Count >= Amount;
        }

        public bool EntityValid(EntityUid entity, [NotNullWhen(true)] out StackComponent? stack)
        {
            if (IoCManager.Resolve<IEntityManager>().TryGetComponent(entity, out StackComponent? otherStack) && otherStack.StackTypeId == MaterialPrototypeId && otherStack.Count >= Amount)
                stack = otherStack;
            else
                stack = null;

            return stack != null;
        }
    }
}