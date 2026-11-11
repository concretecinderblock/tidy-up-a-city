using NUnit.Framework;
using TidyUp.Story;

namespace TidyUp.Tests
{
    public sealed class MemoryJournalTests
    {
        [Test]
        public void CollectsOnceInOrder()
        {
            var journal = new MemoryJournal();
            Assert.IsTrue(journal.Collect("memory_bakery"));
            Assert.IsFalse(journal.Collect("memory_bakery"));
            Assert.IsTrue(journal.Collect("memory_library"));
            Assert.AreEqual(2, journal.Count);
            Assert.AreEqual("memory_bakery", journal.CollectedIds[0]);
            Assert.IsTrue(journal.Has("memory_library"));
        }

        [Test]
        public void IgnoresEmptyIds()
        {
            var journal = new MemoryJournal();
            Assert.IsFalse(journal.Collect(null));
            Assert.IsFalse(journal.Collect(""));
            Assert.IsFalse(journal.Has(null));
            Assert.AreEqual(0, journal.Count);
        }

        [Test]
        public void CollectedEventFiresOnlyForNewMemories()
        {
            var journal = new MemoryJournal();
            int events = 0;
            journal.Collected += _ => events++;
            journal.Collect("a");
            journal.Collect("a");
            journal.Collect("b");
            Assert.AreEqual(2, events);
        }

        [Test]
        public void LoadRestoresWithoutEventsAndDropsDuplicates()
        {
            var journal = new MemoryJournal();
            int events = 0;
            journal.Collected += _ => events++;
            journal.Load(new[] { "a", "b", "a", "" });
            Assert.AreEqual(2, journal.Count);
            Assert.AreEqual(0, events);
            Assert.IsFalse(journal.Collect("b"));
        }
    }
}
