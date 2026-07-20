using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// カメラ演出。選択フェーズは対戦ステージの周りを緩やかに振り(ポケモン対戦のカメラワーク風)、
    /// 連打フェーズは固定位置に戻って落ち着かせる。
    /// Main Cameraにアタッチし、Targetに対戦ステージ(CrabStage)を割り当てる。
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        [Tooltip("注視するステージ(CrabStageなど)")]
        [SerializeField] private Transform target;
        [Tooltip("連打フェーズで戻る固定カメラ位置。未指定なら起動時のカメラ位置を使う")]
        [SerializeField] private Transform battleAnchor;

        [Header("選択フェーズのカメラワーク")]
        [Tooltip("ステージからの水平距離")]
        [SerializeField] private float orbitRadius = 6f;
        [Tooltip("カメラの高さ")]
        [SerializeField] private float orbitHeight = 2.5f;
        [Tooltip("左右に振る角度(±この値)")]
        [SerializeField] private float sweepAngle = 35f;
        [Tooltip("振りの速さ(小さいほどゆっくり)")]
        [SerializeField] private float sweepSpeed = 0.25f;
        [Tooltip("注視点の高さオフセット")]
        [SerializeField] private float lookHeight = 1f;

        [Header("共通")]
        [Tooltip("目標位置への追従速度")]
        [SerializeField] private float followSpeed = 2.5f;

        private Vector3 _anchorPos;
        private Quaternion _anchorRot;
        private bool _selectMode = true;
        private float _sweepTime;

        private void Awake()
        {
            if (battleAnchor != null)
            {
                _anchorPos = battleAnchor.position;
                _anchorRot = battleAnchor.rotation;
            }
            else
            {
                _anchorPos = transform.position;
                _anchorRot = transform.rotation;
            }
        }

        /// <summary>選択フェーズ: ステージ周りを振るカメラワークに入る。</summary>
        public void EnterSelectMode() => _selectMode = true;

        /// <summary>連打・演出フェーズ: 固定カメラに戻る。</summary>
        public void EnterBattleMode() => _selectMode = false;

        private void LateUpdate()
        {
            Vector3 desiredPos;
            Quaternion desiredRot;

            if (_selectMode && target != null)
            {
                _sweepTime += Time.deltaTime * sweepSpeed;
                // サイン波で左右にゆっくり振る(往復するのでポケモン対戦のような揺れになる)
                float angle = Mathf.Sin(_sweepTime * Mathf.PI * 2f) * sweepAngle;
                var offset = Quaternion.Euler(0f, angle, 0f) *
                             new Vector3(0f, orbitHeight, -orbitRadius);
                desiredPos = target.position + offset;

                var lookAt = target.position + Vector3.up * lookHeight;
                desiredRot = Quaternion.LookRotation(lookAt - desiredPos);
            }
            else
            {
                desiredPos = _anchorPos;
                desiredRot = _anchorRot;
            }

            float k = followSpeed * Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, desiredPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, k);
        }
    }
}
