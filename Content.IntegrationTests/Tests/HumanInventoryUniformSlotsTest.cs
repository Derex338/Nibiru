using Content.IntegrationTests.Fixtures;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests
{
    // Tests the behavior of InventoryComponent.
    // i.e. the interaction between uniforms and the pocket/ID slots.
    // and also how big items don't fit in pockets.
    [TestFixture]
    public sealed class HumanInventoryUniformSlotsTest : GameTest
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: entity
  name: HumanUniformDummy
  id: HumanUniformDummy
  components:
  - type: Inventory
  - type: ContainerContainer

- type: entity
  name: UniformDummy
  id: UniformDummy
  components:
  - type: Clothing
    slots: [pants]
  - type: Item
    size: Tiny

- type: entity
  name: IDCardDummy
  id: IDCardDummy
  components:
  - type: Clothing
    slots:
    - idcard
  - type: Item
    size: Tiny
  - type: IdCard

- type: entity
  name: FlashlightDummy
  id: FlashlightDummy
  components:
  - type: Item
    size: Tiny

- type: entity
  name: ToolboxDummy
  id: ToolboxDummy
  components:
  - type: Item
    size: Huge
";
        [Test]
        public async Task Test()
        {
            var pair = Pair;
            var server = pair.Server;
            var testMap = await pair.CreateTestMap();
            var coordinates = testMap.GridCoords;

            EntityUid human = default;
            EntityUid uniform = default;
            EntityUid idCard = default;
            EntityUid pocketItem = default;

            InventorySystem invSystem = default!;
            var mapSystem = server.System<SharedMapSystem>();
            var entityMan = server.ResolveDependency<IEntityManager>();

            await server.WaitAssertion(() =>
            {
                invSystem = entityMan.System<InventorySystem>();

                human = entityMan.SpawnEntity("HumanUniformDummy", coordinates);
                uniform = entityMan.SpawnEntity("UniformDummy", coordinates);
                idCard = entityMan.SpawnEntity("IDCardDummy", coordinates);
                pocketItem = entityMan.SpawnEntity("FlashlightDummy", coordinates);
                var tooBigItem = entityMan.SpawnEntity("ToolboxDummy", coordinates);


                Assert.Multiple(() =>
                {
                    Assert.That(invSystem.CanEquip(human, uniform, "pants", out _));

                    Assert.That(invSystem.CanEquip(human, idCard, "id", out _), Is.False);
                    Assert.That(invSystem.CanEquip(human, pocketItem, "pocket1", out _), Is.False);
                    Assert.That(invSystem.CanEquip(human, tooBigItem, "pocket2", out _), Is.False);
                });

                Assert.Multiple(() =>
                {
                    Assert.That(invSystem.TryEquip(human, uniform, "pants"));
                    Assert.That(invSystem.TryEquip(human, idCard, "id"));
                });

#pragma warning disable NUnit2045
                Assert.That(invSystem.CanEquip(human, tooBigItem, "pocket1", out _), Is.False);
                Assert.That(invSystem.TryEquip(human, pocketItem, "pocket1"));
#pragma warning restore NUnit2045

                Assert.Multiple(() =>
                {
                    Assert.That(IsDescendant(idCard, human, entityMan));
                    Assert.That(IsDescendant(pocketItem, human, entityMan));
                });

                Assert.That(invSystem.TryUnequip(human, "pants"));
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsDescendant(uniform, human, entityMan), Is.False);
                    Assert.That(IsDescendant(idCard, human, entityMan), Is.False);
                    Assert.That(IsDescendant(pocketItem, human, entityMan), Is.False);

                    Assert.That(!invSystem.TryGetSlotEntity(human, "pants", out _));
                    Assert.That(!invSystem.TryGetSlotEntity(human, "id", out _));
                    Assert.That(!invSystem.TryGetSlotEntity(human, "pocket1", out _));
                });

                mapSystem.DeleteMap(testMap.MapId);
            });
        }

        private static bool IsDescendant(EntityUid descendant, EntityUid parent, IEntityManager entManager)
        {
            var xforms = entManager.GetEntityQuery<TransformComponent>();
            var tmpParent = xforms.GetComponent(descendant).ParentUid;
            while (tmpParent.IsValid())
            {
                if (tmpParent == parent)
                {
                    return true;
                }

                tmpParent = xforms.GetComponent(tmpParent).ParentUid;
            }

            return false;
        }
    }
}
