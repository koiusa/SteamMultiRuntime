using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    public static class InputActionResolver
    {
        public static InputAction Resolve(InputActionReference assetSource, string actionPath)
        {
            var asset = assetSource?.action?.actionMap?.asset;
            return asset?.FindAction(actionPath, throwIfNotFound: false);
        }
    }
}
