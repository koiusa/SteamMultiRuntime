using Koiusa.Input;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// インプットプロファイルを複数のプレイヤーコントローラーに一括割り当てするヘルパークラス
    /// </summary>
    public static class InputConfigInitializer
    {
        /// <summary>
        /// 指定されたプロファイルを複数のプレイヤーコントローラーに割り当てる
        /// </summary>
        public static void AssignProfile(
            LocalPlayerController localController,
            ServerDrivenPlayerController serverController,
            InputActionsConfig profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (localController != null)
            {
                localController.SetInputConfig(profile);
            }

            if (serverController != null)
            {
                serverController.SetInputConfig(profile);
            }
        }

        /// <summary>
        /// ローカルプレイヤーのみにプロファイルを割り当てる
        /// </summary>
        public static void AssignProfileToLocalOnly(
            LocalPlayerController localController,
            InputActionsConfig profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (localController != null)
            {
                localController.SetInputConfig(profile);
            }
        }

        /// <summary>
        /// サーバー駆動プレイヤーのみにプロファイルを割り当てる
        /// </summary>
        public static void AssignProfileToServerDrivenOnly(
            ServerDrivenPlayerController serverController,
            InputActionsConfig profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile is null.");
                return;
            }

            if (serverController != null)
            {
                serverController.SetInputConfig(profile);
            }
        }
    }
}
