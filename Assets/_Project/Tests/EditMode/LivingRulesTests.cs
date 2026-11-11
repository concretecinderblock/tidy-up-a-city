using NUnit.Framework;
using TidyUp.Mechanics.LivingItems;
using UnityEngine;

namespace TidyUp.Tests
{
    public sealed class LivingRulesTests
    {
        [Test]
        public void FleeDirectionPointsAwayFromPlayerOnTheGround()
        {
            Vector3 d = LivingRules.FleeDirection(new Vector3(1f, 0.2f, 0f), new Vector3(0f, 1.7f, 0f), 0f);
            Assert.AreEqual(1f, d.x, 1e-5f);
            Assert.AreEqual(0f, d.y, 1e-5f);
            Assert.AreEqual(1f, d.magnitude, 1e-5f);
        }

        [Test]
        public void FleeDirectionHandlesPlayerOnTop()
        {
            Vector3 d = LivingRules.FleeDirection(Vector3.zero, Vector3.up, 0f);
            Assert.AreEqual(1f, d.magnitude, 1e-5f);
        }

        [Test]
        public void ClampKeepsPointsInsideTheShrunkArea()
        {
            var area = new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));
            Vector3 p = LivingRules.ClampToArea(new Vector3(5f, 0.3f, -9f), area, 0.5f);
            Assert.AreEqual(1.5f, p.x, 1e-5f);
            Assert.AreEqual(-1.5f, p.z, 1e-5f);
            Assert.AreEqual(0.3f, p.y, 1e-5f);
        }

        [Test]
        public void HopArcStartsAndEndsOnTheGroundAndPeaksInTheMiddle()
        {
            var a = new Vector3(0f, 0f, 0f);
            var b = new Vector3(2f, 0f, 0f);
            Assert.AreEqual(a, LivingRules.HopArc(a, b, 0.5f, 0f));
            Assert.AreEqual(b, LivingRules.HopArc(a, b, 0.5f, 1f));
            Assert.AreEqual(0.5f, LivingRules.HopArc(a, b, 0.5f, 0.5f).y, 1e-5f);
        }

        [Test]
        public void ShyVisibilityFadesWithDistance()
        {
            Assert.AreEqual(1f, LivingRules.ShyVisibility(1f, 2f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, LivingRules.ShyVisibility(3f, 2f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, LivingRules.ShyVisibility(2.25f, 2f, 0.5f), 1e-5f);
        }

        [Test]
        public void FloatOffsetStaysWithinRadiusAndBob()
        {
            for (float t = 0f; t < 60f; t += 0.37f)
            {
                Vector3 o = LivingRules.FloatOffset(t, 0.5f, 0.08f, 3f);
                Assert.LessOrEqual(Mathf.Abs(o.x), 0.5f + 1e-5f);
                Assert.LessOrEqual(Mathf.Abs(o.y), 0.08f + 1e-5f);
            }
        }
    }
}
