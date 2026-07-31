using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// サーバー主体の物理オブジェクト同期コンポーネント。
    /// サーバーが物理演算を実行し、Position/Rotation/Velocityをクライアントへ配信する。
    /// クライアントはKinematicに設定し、受信した状態へ補間する。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class ServerDrivenNetworkRigidbody : NetworkBehaviour
    {
        private static readonly List<ServerDrivenNetworkRigidbody> serverInstances = new();
        private static readonly List<ServerDrivenNetworkRigidbody> interactionInstances = new();
        internal static IReadOnlyList<ServerDrivenNetworkRigidbody> ServerInstances => serverInstances;
        internal static IReadOnlyList<ServerDrivenNetworkRigidbody> InteractionInstances => interactionInstances;
        internal bool CanReceiveAuthoritativeCrowdContact => !IsSpawned || IsServer;

        [Header("Sync Settings")]
        [Tooltip("クライアント側の補間速度（大きいほどサーバー状態に素早く追従）")]
        [SerializeField] private float interpolationSpeed = 15f;

        [Tooltip("この距離以上ずれていたら補間せず即スナップする")]
        [SerializeField] private float snapThreshold = 3f;

        private Rigidbody rb;
        private Collider[] interactionColliders;
        internal Rigidbody Body => rb;
        internal Collider[] InteractionColliders => interactionColliders;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            serverInstances.Clear();
            interactionInstances.Clear();
        }

        // サーバーが書き込み、全員が読み取る
        private readonly NetworkVariable<Vector3> netPosition = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Quaternion> netRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> netVelocity = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector3> netAngularVelocity = new(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> netIsSleeping = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            interactionColliders = GetComponentsInChildren<Collider>(true);
            if (!interactionInstances.Contains(this))
                interactionInstances.Add(this);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                // クライアントはKinematicにして物理演算をサーバーへ一任する
                rb.isKinematic = true;
                SnapToServerState();
            }
            else if (!serverInstances.Contains(this))
            {
                serverInstances.Add(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            serverInstances.Remove(this);
            base.OnNetworkDespawn();
        }

        private void OnDestroy()
        {
            serverInstances.Remove(this);
            interactionInstances.Remove(this);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                BroadcastPhysicsState();
            }
            else
            {
                InterpolateToServerState();
            }
        }

        /// <summary>
        /// サーバー：現在の物理状態をNetworkVariableへ書き出す。
        /// </summary>
        private void BroadcastPhysicsState()
        {
            netPosition.Value = rb.position;
            netRotation.Value = rb.rotation;
            netVelocity.Value = rb.linearVelocity;
            netAngularVelocity.Value = rb.angularVelocity;
            netIsSleeping.Value = rb.IsSleeping();
        }

        /// <summary>
        /// クライアント：受信したサーバー状態へ補間する。
        /// 静止中はスナップ、大きなずれはスナップ、それ以外は線形補間。
        /// </summary>
        private void InterpolateToServerState()
        {
            var serverPos = netPosition.Value;
            var serverRot = netRotation.Value;
            var distance = Vector3.Distance(rb.position, serverPos);

            // 静止中 or 大きくずれている場合はスナップ
            if (netIsSleeping.Value || distance > snapThreshold)
            {
                SnapToServerState();
                return;
            }

            // サーバーの速度を使ってデッドレコニング補正しながら補間
            var predictedPos = serverPos + netVelocity.Value * Time.fixedDeltaTime;
            var t = interpolationSpeed * Time.fixedDeltaTime;

            rb.MovePosition(Vector3.Lerp(rb.position, predictedPos, t));
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, serverRot, t));
        }

        /// <summary>
        /// サーバー状態へ即座にスナップする。
        /// </summary>
        private void SnapToServerState()
        {
            rb.MovePosition(netPosition.Value);
            rb.MoveRotation(netRotation.Value);
        }
    }
}
