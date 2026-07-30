using Koiusa.SteamMultiRuntime.Player.UI;
using NUnit.Framework;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class WorldSpaceUiOverlayCameraTests
    {
        [Test]
        public void FindHighestUnnamedUserLayer_WhenAllUserLayersAreNamed_ReturnsNoLayer()
        {
            var layer = WorldSpaceUiOverlayCamera.FindHighestUnnamedUserLayer(_ => "Used");

            Assert.That(layer, Is.EqualTo(-1));
        }

        [Test]
        public void FindHighestUnnamedUserLayer_SelectsHighestAvailableUserLayer()
        {
            var layer = WorldSpaceUiOverlayCamera.FindHighestUnnamedUserLayer(
                candidate => candidate == 24 || candidate == 12 ? string.Empty : "Used");

            Assert.That(layer, Is.EqualTo(24));
        }

        [Test]
        public void Register_WhenNoLayerCanBeResolved_PreservesSurfaceAndCameraState()
        {
            var surface = new GameObject("WorldSpaceUiSurface");
            var cameraObject = new GameObject("GameCamera");
            var camera = cameraObject.AddComponent<Camera>();
            surface.layer = 5;
            camera.cullingMask = 0x13579BDF;
            var cameraCountBefore = Camera.allCameras.Length;

            try
            {
                WorldSpaceUiOverlayCamera.Register(surface, surface.layer, () => false);

                Assert.That(surface.layer, Is.EqualTo(5));
                Assert.That(camera.cullingMask, Is.EqualTo(0x13579BDF));
                Assert.That(Camera.allCameras.Length, Is.EqualTo(cameraCountBefore));
            }
            finally
            {
                WorldSpaceUiOverlayCamera.Unregister(surface);
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void AdapterReplacement_ReleasesAppliedAdapterBeforeRebuildingCameraState()
        {
            var previousAdapter = WorldSpaceUiOverlayCameraAdapterRegistry.Current;
            var surface = new GameObject("WorldSpaceUiSurface");
            var cameraObject = new GameObject("GameCamera");
            var sourceCamera = cameraObject.AddComponent<Camera>();
            var originalLayer = 5;
            var originalCullingMask = 0x13579BDF;
            surface.layer = originalLayer;
            sourceCamera.cullingMask = originalCullingMask;
            var firstAdapter = new RecordingAdapter(sourceCamera);
            var secondAdapter = new RecordingAdapter(sourceCamera);
            var surfaceRegistered = false;

            try
            {
                WorldSpaceUiOverlayCameraAdapterRegistry.Register(firstAdapter);
                WorldSpaceUiOverlayCamera.Register(surface, originalLayer);
                surfaceRegistered = true;

                Assert.That(firstAdapter.ConfigureCount, Is.EqualTo(1));
                Assert.That(firstAdapter.ReleaseCount, Is.Zero);
                Assert.That(firstAdapter.LastOverlay, Is.Not.Null);
                var firstOverlay = firstAdapter.LastOverlay;

                WorldSpaceUiOverlayCameraAdapterRegistry.Register(secondAdapter);

                Assert.That(firstAdapter.ReleaseCount, Is.EqualTo(1));
                Assert.That(secondAdapter.ConfigureCount, Is.EqualTo(1));
                Assert.That(secondAdapter.LastOverlay, Is.Not.Null);
                Assert.That(secondAdapter.LastOverlay, Is.Not.SameAs(firstOverlay));

                WorldSpaceUiOverlayCamera.Unregister(surface);
                surfaceRegistered = false;

                Assert.That(secondAdapter.ReleaseCount, Is.EqualTo(1));
                Assert.That(surface.layer, Is.EqualTo(originalLayer));
                Assert.That(sourceCamera.cullingMask, Is.EqualTo(originalCullingMask));
            }
            finally
            {
                if (surfaceRegistered)
                    WorldSpaceUiOverlayCamera.Unregister(surface);

                WorldSpaceUiOverlayCameraAdapterRegistry.Unregister(secondAdapter);
                WorldSpaceUiOverlayCameraAdapterRegistry.Unregister(firstAdapter);
                if (previousAdapter != null)
                    WorldSpaceUiOverlayCameraAdapterRegistry.Register(previousAdapter);

                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class RecordingAdapter : IWorldSpaceUiOverlayCameraAdapter
        {
            private readonly Camera sourceCamera;

            public RecordingAdapter(Camera sourceCamera)
            {
                this.sourceCamera = sourceCamera;
            }

            public int ConfigureCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public Camera LastOverlay { get; private set; }

            public bool IsSourceCamera(Camera camera) => camera == sourceCamera;

            public bool Configure(Camera source, Camera overlay)
            {
                ConfigureCount++;
                LastOverlay = overlay;
                return true;
            }

            public void Release(Camera source, Camera overlay)
            {
                ReleaseCount++;
            }
        }
    }
}
