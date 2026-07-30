using Koiusa.TargetingSystem.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.TargetingSystem.Sample
{
    public sealed class CurrentTargetingSampleBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var camera = CreateCamera();
            CreateLight();

            var registryObject = new GameObject("Target Registry");
            registryObject.AddComponent<TargetMarkerRegistry>();

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Targeting Controller";
            player.SetActive(false);
            player.transform.position = Vector3.zero;
            var context = player.AddComponent<TargetingContextProvider>();
            var candidates = player.AddComponent<RegistryTargetCandidateSource>();
            var policy = player.AddComponent<ViewportTargetPolicy>();
            var controller = player.AddComponent<TargetingController>();
            controller.Configure(context, candidates, new MonoBehaviour[] { policy }, new MonoBehaviour[] { policy });

            var input = player.AddComponent<TargetingCommandInput>();
            input.Configure(controller, Resources.Load<TargetingInputActions>("TargetingSampleInputActionsConfig"));
            player.SetActive(true);

            CreateIndicator(controller, camera);
            CreateTarget("Target A", new Vector3(-3f, 1f, 8f), Color.red);
            CreateTarget("Target B", new Vector3(0f, 1.5f, 10f), Color.yellow);
            CreateTarget("Target C", new Vector3(3f, 1f, 8f), Color.cyan);
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 3f, -8f), Quaternion.Euler(8f, 0f, 0f));
            return cameraObject.AddComponent<Camera>();
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
        }

        private static void CreateIndicator(TargetingController controller, Camera camera)
        {
            var uiObject = new GameObject("Current Target Indicator");
            uiObject.SetActive(false);
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = Resources.Load<PanelSettings>("UI/TargetIndicator Panel Settings");
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/TargetIndicator");

            var theme = uiObject.AddComponent<TargetIndicatorThemeProvider>();
            theme.Configure(
                Resources.Load<VisualTreeAsset>("UI/TargetIndicator"),
                Resources.Load<StyleSheet>("UI/TargetIndicator"));

            var indicator = uiObject.AddComponent<TargetIndicatorController>();
            indicator.SetController(controller);
            indicator.SetCamera(camera);
            uiObject.SetActive(true);
        }

        private static void CreateTarget(string targetName, Vector3 position, Color color)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = targetName;
            target.transform.position = position;
            target.transform.localScale = Vector3.one * 1.5f;
            target.GetComponent<Renderer>().material.color = color;
            target.AddComponent<TargetMarker>();
        }
    }
}
