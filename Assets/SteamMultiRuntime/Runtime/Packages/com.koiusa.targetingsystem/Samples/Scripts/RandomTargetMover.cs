using UnityEngine;

namespace Koiusa.TargetingSystem.Sample
{
    /// <summary>
    /// テスト用途で、ターゲットの位置をランダムに移動させるコンポーネント。
    /// ITargetable を実装したオブジェクトに対応します。
    /// </summary>
    [DisallowMultipleComponent]
    public class RandomTargetMover : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("次の目標地点を決める間隔（秒）")]
        private float moveInterval = 2f;

        [SerializeField]
        [Tooltip("現在位置から移動できる最大距離（各軸）")]
        private Vector3 rangeSize = new Vector3(10f, 0f, 10f);

        [SerializeField]
        [Tooltip("移動速度（Units/秒）")]
        private float moveSpeed = 3f;

        private Vector3 targetPosition;
        private float nextMoveTime;

        private void OnEnable()
        {
            targetPosition = transform.position;
            nextMoveTime = Time.time + moveInterval;
        }

        private void Update()
        {
            if (Time.time >= nextMoveTime)
            {
                PickNextTarget();
                nextMoveTime = Time.time + moveInterval;
            }

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }

        /// <summary>
        /// 現在位置を基準にランダムなオフセットを加えた目標地点を決定します。
        /// </summary>
        private void PickNextTarget()
        {
            var randomOffset = new Vector3(
                Random.Range(-rangeSize.x, rangeSize.x),
                Random.Range(-rangeSize.y, rangeSize.y),
                Random.Range(-rangeSize.z, rangeSize.z)
            );

            targetPosition = transform.position + randomOffset;
        }

        /// <summary>
        /// 移動間隔を設定します。
        /// </summary>
        public void SetMoveInterval(float interval)
        {
            moveInterval = Mathf.Max(0.1f, interval);
            nextMoveTime = Time.time + moveInterval;
        }
    }
}
