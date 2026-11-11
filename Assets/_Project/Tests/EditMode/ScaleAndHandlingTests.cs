using NUnit.Framework;
using TidyUp.Core;
using TidyUp.Locations;
using TidyUp.Muddle;
using UnityEngine;

namespace TidyUp.Tests
{
    public sealed class ScaleAndHandlingTests
    {
        [Test]
        public void CarryTiersFollowItemsPlaced()
        {
            Assert.AreEqual("Hands", CarryTiers.For(0).Name);
            Assert.AreEqual(10, CarryTiers.For(99).Capacity);
            Assert.AreEqual("Basket", CarryTiers.For(100).Name);
            Assert.AreEqual(40, CarryTiers.For(500).Capacity);
            Assert.IsTrue(CarryTiers.For(2000).Unlimited);
            Assert.AreEqual(1, CarryTiers.ToNext(99));
            Assert.AreEqual(0, CarryTiers.ToNext(5000));
        }

        [Test]
        public void AwkwardThingsLieDownWhenScattered()
        {
            Assert.IsTrue(ScatterRules.ShouldLieDown(new Vector3(0.04f, 0.24f, 0.17f)));  // novel
            Assert.IsFalse(ScatterRules.ShouldLieDown(new Vector3(0.28f, 0.15f, 0.18f))); // loaf
            Assert.IsTrue(ScatterRules.ShouldLieDown(new Vector3(0.03f, 0.22f, 0.22f)));  // square picture book
            Assert.IsTrue(ScatterRules.ShouldLieDown(new Vector3(0.04f, 0.15f, 0.15f)));  // board book
            Assert.IsFalse(ScatterRules.ShouldLieDown(new Vector3(0.08f, 0.015f, 0.08f))); // cookie stays flat
            Assert.IsFalse(ScatterRules.ShouldLieDown(new Vector3(0.1f, 0.1f, 0.09f)));    // muffin stands
        }

        [Test]
        public void ScatterGridMeasuresClearanceToNeighbours()
        {
            var grid = new ScatterGrid(0.5f);
            Assert.IsTrue(float.IsPositiveInfinity(grid.Clearance(Vector3.zero, 0.1f)));
            grid.Add(Vector3.zero, 0.1f);
            Assert.AreEqual(0.3f, grid.Clearance(new Vector3(0.5f, 0f, 0f), 0.1f), 1e-4f);
            Assert.Less(grid.Clearance(new Vector3(0.1f, 0f, 0f), 0.1f), 0f, "overlap is negative");
            // Across a cell boundary, and a big neighbour reaching into far cells.
            grid.Add(new Vector3(3f, 0f, 0f), 1f);
            Assert.AreEqual(0.1f, grid.Clearance(new Vector3(4.2f, 0f, 0f), 0.1f), 1e-4f);
            Assert.AreEqual(2, grid.Count);
        }

        [Test]
        public void ScatterGridIgnoresOtherSurfaceHeights()
        {
            var grid = new ScatterGrid(0.5f);
            grid.Add(new Vector3(0f, 0.9f, 0f), 0.2f); // on a table
            Assert.IsTrue(float.IsPositiveInfinity(grid.Clearance(Vector3.zero, 0.2f)), "the floor below is still free");
            grid.Clear();
            Assert.AreEqual(0, grid.Count);
        }

        [Test]
        public void ChecklistIncrementalMatchesFullRecount()
        {
            string[] labels = { "A", "A", "B", "B", "B" };
            var incremental = new Checklist(labels);
            var filled = new bool[labels.Length];
            incremental.Recount(filled);

            var random = new System.Random(3);
            for (int step = 0; step < 200; step++)
            {
                int i = random.Next(labels.Length);
                bool now = random.Next(2) == 0;
                incremental.SetFilled(i, filled[i], now);
                filled[i] = now;

                var full = new Checklist(labels);
                full.Recount(filled);
                Assert.AreEqual(full.Placed, incremental.Placed);
                Assert.AreEqual(full.GetCategory(1).Placed, incremental.GetCategory(1).Placed);
            }
        }
    }
}
