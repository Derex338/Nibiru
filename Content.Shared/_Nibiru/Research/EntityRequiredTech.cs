using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Research
{
    [DataDefinition]
    public sealed partial class EntityRequiredTech 
    {
		[DataField("entity")]
        public /*ProtoId<EntityPrototype>*/ EntProtoId EntityPrototypeId { get; private set; }

        public bool EntityValid(EntityUid uid)
        {
            return true;
        }

        public bool EntityValid(EntityUid entity, [NotNullWhen(true)] out StackComponent? stack)
        {
            if (IoCManager.Resolve<IEntityManager>().TryGetComponent(entity, out StackComponent? otherStack))
                stack = otherStack;
            else
                stack = null;

            return stack != null;
        }
    }
}