using Koiusa.Input;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// インプットプロファイルを複数のプレイヤーコントローラーに一括割り当てするヘルパークラス
    /// </summary>
    public static class InputProfileInitializer
    {
        /// <summary>
        /// 指定されたプロファイルを複数のプレイヤーコントローラーに割り当てる
        /// </summary>
        public static void AssignProfile(
            LocalPlayerController localController,
            ServerDrivenPlayerController serverController,
            InputActionAssetProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (localController != null)
            {
                localController.SetInputProfile(profile);
            }

            if (serverController != null)
            {
                serverController.SetInputProfile(profile);
            }
        }

        /// <summary>
        /// ローカルプレイヤーのみにプロファイルを割り当てる
        /// </summary>
        public static void AssignProfileToLocalOnly(
            LocalPlayerController localController,
            InputActionAssetProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (localController != null)
            {
                localController.SetInputProfile(profile);
            }
        }

        /// <summary>
        /// サーバー駆動プレイヤーのみにプロファイルを割り当てる
        /// </summary>
        public static void AssignProfileToServerDrivenOnly(
            ServerDrivenPlayerController serverController,
            InputActionAssetProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (serverController != null)
            {
                serverController.SetInputProfile(profile);
            }
        }
    }
}
