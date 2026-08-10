using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests
{
    [TestFixture]
    public sealed class InventoryHelpersTest : GameTest
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: entity
  name: InventoryStunnableDummy
  id: InventoryStunnableDummy
  components:
  - type: Inventory
  - type: ContainerContainer
  - type: MobState

- type: entity
  name: InventoryPantsDummy
  id: InventoryPantsDummy
  components:
  - type: Clothing
    slots: [pants]

- type: entity
  name: InventoryIDCardDummy
  id: InventoryIDCardDummy
  components:
  - type: Clothing
    QuickEquip: false
    slots:
    - idcard
  - type: Pda
";
        [Test]
        public async Task SpawnItemInSlotTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var sEntities = server.ResolveDependency<IEntityManager>();
            var systemMan = sEntities.EntitySysManager;

            await server.WaitAssertion(() =>
            {
                var human = sEntities.SpawnEntity("InventoryStunnableDummy", MapCoordinates.Nullspace);
                var invSystem = systemMan.GetEntitySystem<InventorySystem>();

                Assert.Multiple(() =>
                {
                    Assert.That(invSystem.HasSlot(human, "pants"));
                    Assert.That(invSystem.HasSlot(human, "id"));
                });

                Assert.That(invSystem.SpawnItemInSlot(human, "pants", "InventoryPantsDummy", true));

#pragma warning disable NUnit2045
                Assert.That(invSystem.TryGetSlotEntity(human, "pants", out var pants));
                Assert.That(sEntities.GetComponent<MetaDataComponent>(pants.Value).EntityPrototype is
                {
                    ID: "InventoryPantsDummy"
                });
#pragma warning restore NUnit2045

                systemMan.GetEntitySystem<StunSystem>().TryUpdateStunDuration(human, TimeSpan.FromSeconds(1f));

#pragma warning disable NUnit2045
                Assert.That(invSystem.SpawnItemInSlot(human, "id", "InventoryIDCardDummy", true), Is.False);

                Assert.That(invSystem.TryGetSlotEntity(human, "item", out _), Is.False);

                Assert.That(invSystem.SpawnItemInSlot(human, "id", "InventoryIDCardDummy", true, true));
                Assert.That(invSystem.TryGetSlotEntity(human, "id", out var idUid));
                Assert.That(sEntities.GetComponent<MetaDataComponent>(idUid.Value).EntityPrototype is
                {
                    ID: "InventoryIDCardDummy"
                });
#pragma warning restore NUnit2045
                sEntities.DeleteEntity(human);
            });
        }
    }
}
