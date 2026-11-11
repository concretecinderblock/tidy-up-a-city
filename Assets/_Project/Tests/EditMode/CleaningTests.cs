using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TidyUp.Core.Save;
using TidyUp.Locations;
using TidyUp.Mechanics.Cleaning;
using UnityEngine;

namespace TidyUp.Tests
{
    public sealed class CleaningTests
    {
        [TearDown]
        public void TearDown() => LocationStateStore.Clear();

        [Test]
        public void FixturesGetAQuickHandfulOfDustBunnies()
        {
            Assert.AreEqual(4, CleaningRules.BunnyCount(12));
            Assert.AreEqual(5, CleaningRules.BunnyCount(240));
            Assert.AreEqual(6, CleaningRules.BunnyCount(672));
            Assert.AreEqual(6, CleaningRules.BunnyCount(5000), "never more than six");
        }

        [Test]
        public void WipeMarkerBouncesBetweenTheEnds()
        {
            var bar = new TimingBar();
            bar.Start(3, new System.Random(1));
            for (int i = 0; i < 500; i++)
            {
                bar.Advance(0.037f);
                Assert.That(bar.Marker, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void WipeOnlyCountsPressesInTheZone()
        {
            var bar = new TimingBar();
            bar.Start(3, new System.Random(5));
            int frames = 0;
            // Press only when outside: never a hit.
            while (frames++ < 400)
            {
                bar.Advance(1f / 60f);
                if (!bar.InZone) Assert.IsFalse(bar.Press());
            }
            Assert.AreEqual(0, bar.Hits);
        }

        [Test]
        public void AGoodWipeTakesOnlyAFewSeconds()
        {
            var bar = new TimingBar();
            bar.Start(3, new System.Random(7));
            float seconds = 0f;
            float zoneBefore = bar.ZoneStart;
            while (!bar.Done && seconds < 30f)
            {
                bar.Advance(1f / 60f);
                seconds += 1f / 60f;
                if (bar.InZone && bar.Press()) Assert.AreNotEqual(zoneBefore, bar.ZoneStart + (bar.Done ? 1f : 0f), "zone moves after a hit");
                zoneBefore = bar.ZoneStart;
            }
            Assert.IsTrue(bar.Done);
            Assert.Less(seconds, 4f, "a perfect player finishes three wipes in a few seconds");
            Assert.IsFalse(bar.Press(), "no hits after done");
        }

        [Test]
        public void CleanedFlagsSurviveTheSessionStore()
        {
            LocationStateStore.SaveCleaned("bakery", new List<bool> { true, false, true });
            List<KeyValuePair<string, bool[]>> snapshot = LocationStateStore.CleanedSnapshot();
            snapshot[0].Value[1] = true; // copy, not the stored array
            Assert.AreEqual(2, CleaningRules.CountTrue(LocationStateStore.GetCleaned("bakery")));

            LocationStateStore.RestoreCleaned(new[] { new KeyValuePair<string, bool[]>("library", new[] { true }) });
            Assert.IsNull(LocationStateStore.GetCleaned("bakery"));
            Assert.AreEqual(1, CleaningRules.CountTrue(LocationStateStore.GetCleaned("library")));

            LocationStateStore.Clear();
            Assert.IsNull(LocationStateStore.GetCleaned("library"));
        }

        [Test]
        public void CleanedFlagsRoundTripThroughASaveFile()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "TidyUpCleaningTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new SaveStorage(folder);
                var data = new SaveData { savedAtUtc = DateTime.UtcNow.ToString("o") };
                data.locations.Add(new LocationSave { id = "bakery", name = "Bakery", filled = new[] { true }, cleaned = new[] { true, false } });
                storage.Write(1, data);
                Assert.IsTrue(storage.TryRead(1, out SaveData read, out string error), error);
                CollectionAssert.AreEqual(new[] { true, false }, read.FindLocation("bakery").cleaned);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
