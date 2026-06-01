using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// 梯子を表す Trigger ボリューム。
    /// ILadderTraversalFeature を持つオブジェクトが侵入・離脱したときに通知する。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class LadderVolume : MonoBehaviour
    {
        /// <summary>梯子の上方向（正規化済み）。デフォルトは World Up。</summary>
        public Vector3 UpDirection => transform.up;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("LadderVolume: Collider を自動的に isTrigger = true に設定しました。", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var feature = other.GetComponentInParent<ILadderTraversalFeature>();
            feature?.NotifyEnterLadder(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var feature = other.GetComponentInParent<ILadderTraversalFeature>();
            feature?.NotifyExitLadder(this);
        }
    }
}
