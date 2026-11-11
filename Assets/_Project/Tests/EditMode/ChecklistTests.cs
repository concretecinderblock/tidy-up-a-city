using NUnit.Framework;
using TidyUp.Locations;

namespace TidyUp.Tests
{
    public sealed class ChecklistTests
    {
        private static readonly string[] Labels = { "Baguettes", "Baguettes", "Loaves", "Pastries", "Pastries", "" };

        [Test]
        public void GroupsSlotsByLabel_InFirstSeenOrder()
        {
            var checklist = new Checklist(Labels);
            Assert.AreEqual(4, checklist.CategoryCount);
            Assert.AreEqual("Baguettes", checklist.GetCategory(0).Label);
            Assert.AreEqual("Loaves", checklist.GetCategory(1).Label);
            Assert.AreEqual("Pastries", checklist.GetCategory(2).Label);
            Assert.AreEqual("Other", checklist.GetCategory(3).Label);
            Assert.AreEqual(2, checklist.GetCategory(0).Total);
            Assert.AreEqual(6, checklist.Total);
        }

        [Test]
        public void RecountTalliesPerCategory()
        {
            var checklist = new Checklist(Labels);
            checklist.Recount(new[] { true, true, false, true, false, false });

            Assert.AreEqual(3, checklist.Placed);
            Assert.IsTrue(checklist.GetCategory(0).IsComplete);
            Assert.IsFalse(checklist.GetCategory(1).IsComplete);
            Assert.AreEqual(0.5f, checklist.GetCategory(2).Ratio, 1e-5f);
        }

        [Test]
        public void RecountReplacesPreviousCounts()
        {
            var checklist = new Checklist(Labels);
            checklist.Recount(new[] { true, true, true, true, true, true });
            checklist.Recount(new[] { false, false, false, false, false, false });
            Assert.AreEqual(0, checklist.Placed);
            Assert.AreEqual(0, checklist.GetCategory(0).Placed);
        }

        [Test]
        public void EmptyCategoryIsNeverComplete()
        {
            var checklist = new Checklist(new string[0]);
            Assert.AreEqual(0, checklist.CategoryCount);
            Assert.AreEqual(0, checklist.Total);
        }

        [Test]
        public void RecountRejectsWrongLength()
        {
            var checklist = new Checklist(Labels);
            Assert.Throws<System.ArgumentException>(() => checklist.Recount(new[] { true }));
        }
    }
}
