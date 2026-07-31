using Koiusa.SteamMultiRuntime.Player.UI;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class ActorHealthOverlayUiDocumentTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.35f, 0.35f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void ApplyFillAmountUsesClampedLeftAnchoredScale(float input, float expected)
        {
            var fill = new VisualElement();

            ActorHealthOverlayUiDocument.ApplyFillAmount(fill, input);

            Assert.That(fill.style.width.value.value, Is.EqualTo(100f));
            Assert.That(fill.style.transformOrigin.value.x.value, Is.EqualTo(0f));
            Assert.That(fill.style.scale.value.value.x, Is.EqualTo(expected));
        }

        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, false)]
        [TestCase(true, true, true, false)]
        [TestCase(true, true, false, true)]
        public void OverheadHealthVisibilityExcludesLocalAndUnresolvedPlayers(
            bool hasOwnership,
            bool isOwnershipResolved,
            bool isLocalOwner,
            bool expected)
        {
            Assert.That(
                ActorHealthUiRouter.CanShowOverhead(hasOwnership, isOwnershipResolved, isLocalOwner),
                Is.EqualTo(expected));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public void PlayerNameVisibilityRequiresAvailableLivingPlayer(
            bool identityAvailable,
            bool isAlive,
            bool expected)
        {
            Assert.That(
                PlayerNameOverlayUiDocument.ShouldDisplayName(identityAvailable, isAlive),
                Is.EqualTo(expected));
        }
    }
}
