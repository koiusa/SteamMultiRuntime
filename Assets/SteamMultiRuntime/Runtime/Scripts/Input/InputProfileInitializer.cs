using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// インプットプロファイルを複数のプレイヤーコントローラーに一括割り当てするヘルパークラス
    /// </summary>
    public static class InputProfileInitializer
    {
        /// <summary>
        /// Resources フォルダからデフォルトプロファイルをロードして割り当てる
        /// </summary>
        public static void InitializeFromResources(
            LocalPlayerController localController,
            ServerDrivenPlayerController serverController,
            string resourcePath = "InputProfiles/DefaultProfile")
        {
            var profile = Resources.Load<PlayerInputActionsProfile>(resourcePath);
            if (profile == null)
            {
                Debug.LogError($"Failed to load InputProfile from Resources: {resourcePath}");
                return;
            }

            AssignProfile(localController, serverController, profile);
        }

        /// <summary>
        /// 指定されたプロファイルを複数のプレイヤーコントローラーに割り当てる
        /// </summary>
        public static void AssignProfile(
            LocalPlayerController localController,
            ServerDrivenPlayerController serverController,
            PlayerInputActionsProfile profile)
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
            PlayerInputActionsProfile profile)
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
            PlayerInputActionsProfile profile)
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
