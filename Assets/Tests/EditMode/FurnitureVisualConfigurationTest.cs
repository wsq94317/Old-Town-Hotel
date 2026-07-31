using NUnit.Framework;
using UnityEngine;

namespace OldTownHotel.Tests.EditMode
{
    [TestFixture]
    public class FurnitureVisualConfigurationTest
    {
        [Test]
        public void CatalogAndSceneSlot_ReserveFourOptionsWithoutFakeConfiguredModels()
        {
            var catalog = ScriptableObject.CreateInstance<FurnitureVisualCatalogSO>();
            var placeholder = new GameObject("Placeholder");
            var alternate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            alternate.name = "Alternate";
            var sceneObject = new GameObject("Lobby Sofa");
            try
            {
                catalog.EnsureDefaultEntry(FurnitureCatalog.Sofa, placeholder);
                Assert.That(catalog.Entries.Count, Is.EqualTo(1));
                Assert.That(catalog.Entries[0].variants.Length,
                    Is.GreaterThanOrEqualTo(FurnitureVisualCatalogSO.ReservedSlotsPerKind));
                Assert.That(catalog.ConfiguredVariantsFor(FurnitureCatalog.Sofa).Count, Is.EqualTo(1));

                SceneFurnitureSlot slot = sceneObject.AddComponent<SceneFurnitureSlot>();
                slot.ConfigureDefault("Floor1/LobbySofa#0", placeholder);
                Assert.That(slot.HasDefaultConfiguration("Floor1/LobbySofa#0", placeholder), Is.True);
                Assert.That(slot.Options.Length,
                    Is.GreaterThanOrEqualTo(SceneFurnitureSlot.ReservedOptionCount));
                Assert.That(slot.TrySelect(0), Is.True);
                Assert.That(slot.TrySelect(1), Is.False,
                    "Empty reserved slots must not instantiate a missing model.");

                slot.Options[1].prefab = alternate;
                Assert.That(slot.TrySelect(1), Is.True);
                Assert.That(sceneObject.transform.Find("FurnitureVariant_1"), Is.Not.Null);
                Assert.That(slot.TrySelect(0), Is.True);
                Assert.That(sceneObject.transform.Find("FurnitureVariant_1"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sceneObject);
                Object.DestroyImmediate(alternate);
                Object.DestroyImmediate(placeholder);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
