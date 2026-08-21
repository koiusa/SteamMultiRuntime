using Koiusa.KeyConfig;
using NUnit.Framework;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class KeyConfigBindingNavigationTests
    {
        [Test]
        public void HorizontalMove_SkipsDisabledButtonInRequestedDirection()
        {
            var availability = new[] { true, false, true, true };

            Assert.That(KeyConfigBindingNavigation.FindAdjacentColumn(availability, 0, 1), Is.EqualTo(2));
            Assert.That(KeyConfigBindingNavigation.FindAdjacentColumn(availability, 2, -1), Is.EqualTo(0));
        }

        [Test]
        public void HorizontalMove_WrapsWithoutSelectingDisabledButton()
        {
            var availability = new[] { true, false, true, true };

            Assert.That(KeyConfigBindingNavigation.FindAdjacentColumn(availability, 0, -1), Is.EqualTo(3));
            Assert.That(KeyConfigBindingNavigation.FindAdjacentColumn(availability, 3, 1), Is.EqualTo(0));
        }
    }
}
