using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Factions;

    [RegisterComponent]
    public sealed partial class FactionComponent : Component
    {
        [DataField("factionName")]
        public string FactionName { get; set; } = string.Empty;

        [DataField("isCreator")]
        public bool IsCreator { get; set; } = false;
		
		/// <summary>
        /// All of the recipe packs that the faction type has by default
        /// </summary>
        //[DataField]
        //public List<ProtoId<ConstructionPackPrototype>> StaticPacks = new();

        //[DataField("members")]
        //public List<EntityUid> Members { get; set; } = new();
    }
