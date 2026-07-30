using NUnit.Framework;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class NetworkPlayerSkillDirectionTests
    {
        [TestCase(float.NaN, 0f, 0f)]
        [TestCase(float.PositiveInfinity, 0f, 0f)]
        [TestCase(0f, float.NegativeInfinity, 0f)]
        public void TryNormalizeDirection_RejectsNonFiniteComponents(float x, float y, float z)
        {
            var accepted = NetworkPlayerSkillController.TryNormalizeDirection(
                new Vector3(x, y, z), out var normalizedDirection);

            Assert.That(accepted, Is.False);
            Assert.That(normalizedDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TryNormalizeDirection_RejectsOverflowedMagnitude()
        {
            var accepted = NetworkPlayerSkillController.TryNormalizeDirection(
                new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), out var normalizedDirection);

            Assert.That(accepted, Is.False);
            Assert.That(normalizedDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TryNormalizeDirection_PreservesZeroAsSkillFallbackSignal()
        {
            var accepted = NetworkPlayerSkillController.TryNormalizeDirection(
                Vector3.zero, out var normalizedDirection);

            Assert.That(accepted, Is.True);
            Assert.That(normalizedDirection, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TryNormalizeDirection_NormalizesFiniteNonZeroDirection()
        {
            var accepted = NetworkPlayerSkillController.TryNormalizeDirection(
                new Vector3(3f, 0f, 4f), out var normalizedDirection);

            Assert.That(accepted, Is.True);
            Assert.That(normalizedDirection.x, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(normalizedDirection.y, Is.Zero);
            Assert.That(normalizedDirection.z, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(normalizedDirection.sqrMagnitude, Is.EqualTo(1f).Within(0.00001f));
        }
    }
}
