using NUnit.Framework;
using TidyUp.Items;
using UnityEngine;

namespace TidyUp.Tests
{
    public sealed class SlotRulesTests
    {
        private static readonly string[] Baguette = { "bread_long" };

        [Test]
        public void Accepts_MatchingSlotType()
        {
            var slot = new SlotDefinition(new[] { "bread_long" });
            Assert.IsTrue(SlotRules.Accepts(slot, "bread_baguette", Baguette));
        }

        [Test]
        public void Rejects_WrongSlotType()
        {
            var slot = new SlotDefinition(new[] { "pastry" });
            Assert.IsFalse(SlotRules.Accepts(slot, "bread_baguette", Baguette));
        }

        [Test]
        public void Accepts_AnyOfSeveralTypes()
        {
            var slot = new SlotDefinition(new[] { "pastry", "bread_long" });
            Assert.IsTrue(SlotRules.Accepts(slot, "bread_baguette", Baguette));
        }

        [Test]
        public void ExactItemId_MustMatch()
        {
            var slot = new SlotDefinition(new[] { "bread_long" }, exactItemId: "bread_baguette_seeded");
            Assert.IsFalse(SlotRules.Accepts(slot, "bread_baguette", Baguette));
            Assert.IsTrue(SlotRules.Accepts(slot, "bread_baguette_seeded", Baguette));
        }

        [Test]
        public void ExactItemId_WithoutTypes_AcceptsThatItemOnly()
        {
            var slot = new SlotDefinition(null, exactItemId: "crown");
            Assert.IsTrue(SlotRules.Accepts(slot, "crown", new string[0]));
            Assert.IsFalse(SlotRules.Accepts(slot, "teacup", new string[0]));
        }

        [Test]
        public void EmptySlotDefinition_AcceptsNothing()
        {
            Assert.IsFalse(SlotRules.Accepts(new SlotDefinition(), "bread_baguette", Baguette));
        }

        [Test]
        public void NullOrEmptyItem_IsRejected()
        {
            var slot = new SlotDefinition(new[] { "bread_long" });
            Assert.IsFalse(SlotRules.Accepts(slot, (ItemDefinition)null));
            Assert.IsFalse(SlotRules.Accepts(slot, "", Baguette));
        }

        [Test]
        public void Accepts_ItemDefinitionAsset()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure("bread_loaf", "Sourdough Loaf", "bakery", new[] { "bread_loaf" }, MechanicFamily.ShelfSort, ItemRarity.Common, false);
            try
            {
                Assert.IsTrue(SlotRules.Accepts(new SlotDefinition(new[] { "bread_loaf" }), item));
                Assert.IsFalse(SlotRules.Accepts(new SlotDefinition(new[] { "pastry" }), item));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void PerfectAlignment_WithinTolerance()
        {
            var slot = new SlotDefinition(new[] { "x" }, rotationTolerance: 5f);
            Assert.IsTrue(SlotRules.IsPerfectAlignment(slot, Quaternion.identity, Quaternion.Euler(0f, 4f, 0f)));
            Assert.IsFalse(SlotRules.IsPerfectAlignment(slot, Quaternion.identity, Quaternion.Euler(0f, 6f, 0f)));
        }

        [Test]
        public void PerfectAlignment_DisabledWhenToleranceZero()
        {
            var slot = new SlotDefinition(new[] { "x" });
            Assert.IsFalse(SlotRules.IsPerfectAlignment(slot, Quaternion.identity, Quaternion.identity));
        }
    }
}
