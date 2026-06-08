using System.Collections.Generic;
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

        /// <summary>梯子面の法線方向（正規化済み）。プレイヤーが正対する向きのフォールバック基準に使う。</summary>
        public Vector3 PlaneNormal => transform.forward;

        // Enter/Exit をコライダー単位で追跡し、feature 単位の参照カウントで管理する
        private readonly Dictionary<Collider, ILadderTraversalFeature> colliderOwners = new Dictionary<Collider, ILadderTraversalFeature>();
        private readonly Dictionary<ILadderTraversalFeature, int> featureRefCounts = new Dictionary<ILadderTraversalFeature, int>();

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError("LadderVolume: Collider が必要です。", this);
                enabled = false;
                return;
            }

            if (!col.isTrigger)
            {
                Debug.LogError("LadderVolume: Prefab の Collider を isTrigger = true に設定してください。", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            RegisterCollider(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!colliderOwners.TryGetValue(other, out var feature))
            {
                return;
            }

            colliderOwners.Remove(other);

            if (!featureRefCounts.TryGetValue(feature, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                featureRefCounts.Remove(feature);
                feature.NotifyExitLadder(this);
            }
            else
            {
                featureRefCounts[feature] = count - 1;
            }
        }

        private void OnDisable()
        {
            var features = new ILadderTraversalFeature[featureRefCounts.Count];
            featureRefCounts.Keys.CopyTo(features, 0);

            for (var i = 0; i < features.Length; i++)
            {
                features[i]?.NotifyExitLadder(this);
            }

            colliderOwners.Clear();
            featureRefCounts.Clear();
        }

        private void RegisterCollider(Collider other)
        {
            if (colliderOwners.ContainsKey(other))
            {
                return;
            }

            if (!TryResolveFeatureForCollider(other, out var feature))
            {
                return;
            }

            colliderOwners.Add(other, feature);

            if (!featureRefCounts.TryGetValue(feature, out var count))
            {
                featureRefCounts.Add(feature, 1);
                feature.NotifyEnterLadder(this);
            }
            else
            {
                featureRefCounts[feature] = count + 1;
            }
        }

        private static bool TryResolveFeatureForCollider(Collider other, out ILadderTraversalFeature feature)
        {
            feature = other.GetComponentInParent<ILadderTraversalFeature>();
            if (feature == null)
            {
                return false;
            }

            var featureMono = feature as MonoBehaviour;
            if (featureMono == null)
            {
                return false;
            }

            // ルート（feature と同一 GameObject）にある Collider のみを梯子判定対象にする。
            // 子オブジェクトのセンサー/補助コライダーで誤反応しないようにする。
            if (other.transform != featureMono.transform)
            {
                return false;
            }

            return true;
        }
    }
}
