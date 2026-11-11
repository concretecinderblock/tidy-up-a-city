using NUnit.Framework;
using TidyUp.Locations;

namespace TidyUp.Tests
{
    public sealed class RestorationMathTests
    {
        [Test]
        public void EaseHitsEndpointsAndIsClamped()
        {
            Assert.AreEqual(0f, RestorationMath.Ease(0f), 1e-6f);
            Assert.AreEqual(1f, RestorationMath.Ease(1f), 1e-6f);
            Assert.AreEqual(0.5f, RestorationMath.Ease(0.5f), 1e-6f);
            Assert.AreEqual(0f, RestorationMath.Ease(-1f), 1e-6f);
            Assert.AreEqual(1f, RestorationMath.Ease(2f), 1e-6f);
        }

        [Test]
        public void ThresholdReachedAtExactlyFullCompletion()
        {
            Assert.IsTrue(RestorationMath.IsReached(1f, 1f));
            float accumulated = 0f;
            for (int i = 0; i < 12; i++) accumulated += 1f / 12f; // float drift must not block the 100% stage
            Assert.IsTrue(RestorationMath.IsReached(accumulated, 1f));
            Assert.IsFalse(RestorationMath.IsReached(11f / 12f, 1f));
            Assert.IsTrue(RestorationMath.IsReached(0.25f, 0.25f));
            Assert.IsFalse(RestorationMath.IsReached(0.24f, 0.25f));
        }

        [Test]
        public void PopInOvershootsThenSettles()
        {
            Assert.AreEqual(0f, RestorationMath.PopIn(0f), 1e-5f);
            Assert.AreEqual(1f, RestorationMath.PopIn(1f), 1e-5f);
            Assert.Greater(RestorationMath.PopIn(0.75f), 1f);
        }
    }
}
