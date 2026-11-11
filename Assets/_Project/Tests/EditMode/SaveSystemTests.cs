using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TidyUp.Core.Save;
using TidyUp.Locations;
using UnityEngine;

namespace TidyUp.Tests
{
    public sealed class SaveSystemTests
    {
        private string _dir;
        private SaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "TidyUpSaveTests_" + Guid.NewGuid().ToString("N"));
            _storage = new SaveStorage(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            LocationStateStore.Clear();
        }

        private static SaveData Sample(DateTime savedAt)
        {
            var data = new SaveData
            {
                savedAtUtc = savedAt.ToString("o"),
                playTimeSeconds = 840f,
                currentLocationId = "library",
                hasStreetPosition = false,
            };
            data.locations.Add(new LocationSave { id = "bakery", name = "Bakery", filled = new[] { true, true, false } });
            data.locations.Add(new LocationSave { id = "library", name = "Library", filled = Array.Empty<bool>() });
            data.memories.Add("memory_bakery");
            return data;
        }

        [Test]
        public void WriteThenReadRoundTrips()
        {
            _storage.Write(2, Sample(new DateTime(2026, 11, 10, 16, 0, 0, DateTimeKind.Utc)));
            Assert.IsTrue(_storage.Exists(2));
            Assert.IsTrue(_storage.TryRead(2, out SaveData read, out string error), error);

            Assert.AreEqual("library", read.currentLocationId);
            Assert.AreEqual(840f, read.playTimeSeconds);
            Assert.AreEqual(2, read.locations.Count);
            CollectionAssert.AreEqual(new[] { true, true, false }, read.FindLocation("bakery").filled);
            Assert.AreEqual(2, read.TotalPlaced);
            CollectionAssert.AreEqual(new[] { "memory_bakery" }, read.memories);
        }

        [Test]
        public void OverwriteReplacesTheSlot()
        {
            _storage.Write(1, Sample(DateTime.UtcNow));
            SaveData second = Sample(DateTime.UtcNow);
            second.currentLocationId = string.Empty;
            _storage.Write(1, second);
            _storage.TryRead(1, out SaveData read, out _);
            Assert.AreEqual(string.Empty, read.currentLocationId);
            Assert.IsFalse(File.Exists(_storage.PathFor(1) + ".tmp"));
        }

        [Test]
        public void EmptySlotReadsAsEmpty()
        {
            Assert.IsFalse(_storage.Exists(3));
            Assert.IsFalse(_storage.TryRead(3, out SaveData data, out string error));
            Assert.IsNull(data);
            Assert.AreEqual("Empty slot.", error);
        }

        [Test]
        public void DamagedFileIsReportedNotThrown()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_storage.PathFor(1), "{ this is not json");
            Assert.IsFalse(_storage.TryRead(1, out _, out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void NewerVersionIsRefused()
        {
            SaveData future = Sample(DateTime.UtcNow);
            future.version = SaveData.CurrentVersion + 1;
            _storage.Write(1, future);
            Assert.IsFalse(_storage.TryRead(1, out _, out string error));
            StringAssert.Contains("newer version", error);
        }

        [Test]
        public void DeleteEmptiesTheSlot()
        {
            _storage.Write(1, Sample(DateTime.UtcNow));
            Assert.IsTrue(_storage.Delete(1));
            Assert.IsFalse(_storage.Exists(1));
            Assert.IsFalse(_storage.Delete(1));
        }

        [Test]
        public void MostRecentSlotPicksTheLatestSave()
        {
            Assert.AreEqual(-1, _storage.MostRecentSlot());
            _storage.Write(1, Sample(new DateTime(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc)));
            _storage.Write(3, Sample(new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc)));
            _storage.Write(2, Sample(new DateTime(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual(3, _storage.MostRecentSlot());
        }

        [Test]
        public void InvalidSlotsAreRejected()
        {
            Assert.IsFalse(SaveStorage.IsValidSlot(0));
            Assert.IsFalse(SaveStorage.IsValidSlot(SaveStorage.SlotCount + 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _storage.Write(0, Sample(DateTime.UtcNow)));
        }

        [Test]
        public void LocationStateSnapshotAndRestore()
        {
            LocationStateStore.Save("bakery", new List<bool> { true, false, true });
            List<KeyValuePair<string, bool[]>> snapshot = LocationStateStore.Snapshot();
            snapshot[0].Value[1] = true; // snapshot is a copy
            Assert.AreEqual(2, LocationStateStore.PlacedCount("bakery"));

            LocationStateStore.Restore(new[] { new KeyValuePair<string, bool[]>("library", new[] { true }) });
            Assert.IsNull(LocationStateStore.Get("bakery"));
            Assert.AreEqual(1, LocationStateStore.PlacedCount("library"));
        }
    }
}
