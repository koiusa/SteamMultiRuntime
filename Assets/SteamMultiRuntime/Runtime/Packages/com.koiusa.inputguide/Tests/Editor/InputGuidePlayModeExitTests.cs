using System.Collections;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Koiusa.InputGuide.Tests
{
    public sealed class InputGuidePlayModeExitTests
    {
        [UnityTest]
        public IEnumerator DisablingRuntimeOverlayAfterPanelDetachDoesNotThrow()
        {
            yield return new EnterPlayMode();

            var scene = SceneManager.CreateScene("InputGuidePlayModeExitTest");
            var prefab = Resources.Load<GameObject>("System/InputGuideOverlay");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
            yield return null;

            var document = instance.GetComponent<UnityEngine.UIElements.UIDocument>();
            var navigation = instance.GetComponent<InputGuideNavigationController>();
            Assert.That(document, Is.Not.Null);
            Assert.That(navigation, Is.Not.Null);

            document.enabled = false;
            navigation.enabled = false;
            yield return null;

            Object.DestroyImmediate(instance);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ExitingPlayModeWithRuntimeSystemDoesNotThrow()
        {
            yield return new EnterPlayMode();

            var prefab = Resources.Load<GameObject>("System/System");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            var keyConfigPanel = instance.transform.Find("KeyConfigPanel");
            Assert.That(keyConfigPanel, Is.Not.Null);
            keyConfigPanel.gameObject.SetActive(true);
            yield return null;
            yield return null;

            yield return new ExitPlayMode();
        }
    }
}
