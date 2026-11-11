using NUnit.Framework;
using TidyUp.Core;

namespace TidyUp.Tests
{
    public sealed class CarryInventoryTests
    {
        private sealed class Thing
        {
            public readonly string Name;
            public Thing(string name) => Name = name;
            public override string ToString() => Name;
        }

        [Test]
        public void RespectsCapacity()
        {
            var inventory = new CarryInventory<Thing>(10);
            for (int i = 0; i < 10; i++) Assert.IsTrue(inventory.TryAdd(new Thing("t" + i)));
            Assert.IsTrue(inventory.IsFull);
            Assert.IsFalse(inventory.TryAdd(new Thing("overflow")));
            Assert.AreEqual(10, inventory.Count);
        }

        [Test]
        public void RejectsNullAndDuplicates()
        {
            var inventory = new CarryInventory<Thing>(10);
            var a = new Thing("a");
            Assert.IsFalse(inventory.TryAdd(null));
            Assert.IsTrue(inventory.TryAdd(a));
            Assert.IsFalse(inventory.TryAdd(a));
            Assert.AreEqual(1, inventory.Count);
        }

        [Test]
        public void NewestPickupIsSelected()
        {
            var inventory = new CarryInventory<Thing>(10);
            var a = new Thing("a");
            var b = new Thing("b");
            inventory.TryAdd(a);
            inventory.TryAdd(b);
            Assert.AreSame(b, inventory.Selected);
        }

        [Test]
        public void RemovingSelected_KeepsAValidSelection()
        {
            var inventory = new CarryInventory<Thing>(10);
            var a = new Thing("a");
            var b = new Thing("b");
            var c = new Thing("c");
            inventory.TryAdd(a);
            inventory.TryAdd(b);
            inventory.TryAdd(c);

            inventory.Remove(c);
            Assert.AreSame(b, inventory.Selected);
            inventory.Remove(a);
            Assert.AreSame(b, inventory.Selected);
            inventory.Remove(b);
            Assert.IsNull(inventory.Selected);
            Assert.AreEqual(-1, inventory.SelectedIndex);
        }

        [Test]
        public void RemovingBeforeSelected_KeepsSameItemSelected()
        {
            var inventory = new CarryInventory<Thing>(10);
            var a = new Thing("a");
            var b = new Thing("b");
            inventory.TryAdd(a);
            inventory.TryAdd(b);
            inventory.Remove(a);
            Assert.AreSame(b, inventory.Selected);
        }

        [Test]
        public void CycleWrapsBothWays()
        {
            var inventory = new CarryInventory<Thing>(10);
            var a = new Thing("a");
            var b = new Thing("b");
            var c = new Thing("c");
            inventory.TryAdd(a);
            inventory.TryAdd(b);
            inventory.TryAdd(c); // selected c

            inventory.Cycle(+1);
            Assert.AreSame(a, inventory.Selected);
            inventory.Cycle(-1);
            Assert.AreSame(c, inventory.Selected);
            inventory.Cycle(-1);
            Assert.AreSame(b, inventory.Selected);
        }

        [Test]
        public void CapacityUpgrade_NeverDropsItems()
        {
            var inventory = new CarryInventory<Thing>(10);
            for (int i = 0; i < 10; i++) inventory.TryAdd(new Thing("t" + i));
            inventory.SetCapacity(20);
            Assert.IsFalse(inventory.IsFull);
            inventory.SetCapacity(5);
            Assert.AreEqual(10, inventory.Capacity);
        }

        [Test]
        public void ChangedEventFires()
        {
            var inventory = new CarryInventory<Thing>(10);
            int changes = 0;
            inventory.Changed += () => changes++;
            var a = new Thing("a");
            inventory.TryAdd(a);
            inventory.Remove(a);
            Assert.AreEqual(2, changes);
        }
    }
}
