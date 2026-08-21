using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig.Tests
{
    public sealed class ProductionInputBindingTests
    {
        private const string AssetPath =
            "Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions";

        [Test]
        public void UiNavigate_KeepsWasdAndArrowKeysAsSeparateComposites()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null);
            var navigate = asset.FindAction("UI/Navigate", true);
            var roots = navigate.bindings
                .Select((binding, index) => (binding, index))
                .Where(item => item.binding.isComposite &&
                    (item.binding.name == "WASD" || item.binding.name == "Arrow Keys"))
                .ToArray();

            Assert.That(roots.Select(item => item.binding.name),
                Is.EqualTo(new[] { "WASD", "Arrow Keys" }));
            Assert.That(GetPartPaths(navigate, roots[0].index), Is.EqualTo(new[]
            {
                "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d"
            }));
            Assert.That(GetPartPaths(navigate, roots[1].index), Is.EqualTo(new[]
            {
                "<Keyboard>/upArrow", "<Keyboard>/downArrow",
                "<Keyboard>/leftArrow", "<Keyboard>/rightArrow"
            }));
        }

        [Test]
        public void PlayerMovement_UsesIndividuallyRebindableDirectionActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null);

            AssertDirectionBinding(asset, "Player/MoveUp", "<Keyboard>/w");
            AssertDirectionBinding(asset, "Player/MoveDown", "<Keyboard>/s");
            AssertDirectionBinding(asset, "Player/MoveLeft", "<Keyboard>/a");
            AssertDirectionBinding(asset, "Player/MoveRight", "<Keyboard>/d");

            var analogMove = asset.FindAction("Player/Move", true);
            Assert.That(analogMove.bindings.Any(binding => binding.isComposite), Is.False);
            Assert.That(analogMove.bindings.Any(binding =>
                binding.path.StartsWith("<Keyboard>/")), Is.False);
        }

        private static void AssertDirectionBinding(InputActionAsset asset, string actionPath, string expectedPath)
        {
            var action = asset.FindAction(actionPath, true);
            Assert.That(action.bindings.Count, Is.EqualTo(1), actionPath);
            Assert.That(action.bindings[0].isComposite, Is.False, actionPath);
            Assert.That(action.bindings[0].isPartOfComposite, Is.False, actionPath);
            Assert.That(action.bindings[0].path, Is.EqualTo(expectedPath), actionPath);
        }

        private static string[] GetPartPaths(InputAction action, int rootIndex) => action.bindings
            .Skip(rootIndex + 1)
            .TakeWhile(binding => binding.isPartOfComposite)
            .Select(binding => binding.path)
            .ToArray();
    }
}
