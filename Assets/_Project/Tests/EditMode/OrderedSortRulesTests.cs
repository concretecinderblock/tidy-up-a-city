using NUnit.Framework;
using TidyUp.Mechanics.OrderedSort;

namespace TidyUp.Tests
{
    public sealed class OrderedSortRulesTests
    {
        [Test]
        public void NumbersSortNumerically()
        {
            Assert.Less(OrderedSortRules.CompareKeys("2", "10"), 0);
            Assert.Greater(OrderedSortRules.CompareKeys("38", "4"), 0);
            Assert.AreEqual(0, OrderedSortRules.CompareKeys("7", "7.0"));
        }

        [Test]
        public void TextSortsIgnoringCaseAndAccents()
        {
            Assert.Less(OrderedSortRules.CompareKeys("abernathy", "Bellweather"), 0);
            Assert.AreEqual(0, OrderedSortRules.CompareKeys("Brontë", "bronte"));
        }

        [Test]
        public void SortAscendingAndDescending()
        {
            string[] keys = { "Moss", "Abernathy", "Kettle" };
            CollectionAssert.AreEqual(new[] { "Abernathy", "Kettle", "Moss" }, OrderedSortRules.Sort(keys, k => k, SortDirection.Ascending));
            CollectionAssert.AreEqual(new[] { "38", "24", "20" }, OrderedSortRules.Sort(new[] { "20", "38", "24" }, k => k, SortDirection.Descending));
        }

        [Test]
        public void SortIsStableForEqualKeys()
        {
            var items = new[] { ("a", "1"), ("b", "1"), ("c", "0") };
            var sorted = OrderedSortRules.Sort(items, i => i.Item2, SortDirection.Ascending);
            Assert.AreEqual("c", sorted[0].Item1);
            Assert.AreEqual("a", sorted[1].Item1);
            Assert.AreEqual("b", sorted[2].Item1);
        }

        [Test]
        public void DirectionWithinARow()
        {
            Assert.AreEqual(OrderHint.FurtherRight, OrderedSortRules.Direction(2, 7, 10));
            Assert.AreEqual(OrderHint.FurtherLeft, OrderedSortRules.Direction(7, 2, 10));
            Assert.AreEqual(OrderHint.Here, OrderedSortRules.Direction(4, 4, 10));
        }

        [Test]
        public void DirectionAcrossRows()
        {
            // Reading order: row 0 is the top shelf.
            Assert.AreEqual(OrderHint.ShelfBelow, OrderedSortRules.Direction(9, 10, 10));
            Assert.AreEqual(OrderHint.ShelfAbove, OrderedSortRules.Direction(10, 9, 10));
        }

        [Test]
        public void DirectionInvalidInput()
        {
            Assert.AreEqual(OrderHint.None, OrderedSortRules.Direction(-1, 3, 10));
            Assert.AreEqual(OrderHint.None, OrderedSortRules.Direction(1, 3, 0));
        }

        [Test]
        public void DescribeMessages()
        {
            Assert.AreEqual("Kettle goes further right.", OrderedSortRules.Describe("Kettle", OrderHint.FurtherRight));
            Assert.AreEqual("Kettle belongs on the shelf below.", OrderedSortRules.Describe("Kettle", OrderHint.ShelfBelow));
            Assert.AreEqual("Kettle doesn't go here.", OrderedSortRules.Describe("Kettle", OrderHint.None));
        }
    }
}
