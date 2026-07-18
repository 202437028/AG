using UnityEngine;
using UnityEngine.UI;

namespace KaniTactics.Game
{
    /// <summary>
    /// 綱引き型の鍔迫り合いゲージ。左=プレイヤー、右=CPU。
    /// 実際の勝敗値(連打数×倍率)の比率を滑らかに追従して表示し、
    /// 連打数そのものは見せない(ブラックボックス化)。
    /// DQMBV風の装飾(押し込みエフェクト・振動など)は今後このクラスに足していく。
    /// </summary>
    public sealed class MashGaugeView : MonoBehaviour
    {
        [Tooltip("プレイヤー側の塗り。Image Type: Filled / Horizontal / Origin: Left")]
        [SerializeField] private Image playerFill;

        [Tooltip("境界に置くマーカー(任意)。アンカーは親の中央に設定")]
        [SerializeField] private RectTransform clashMarker;

        [Tooltip("表示の追従速度。大きいほど機敏、小さいほど重い押し合いに見える")]
        [SerializeField] private float smoothSpeed = 6f;

        [Tooltip("中央からのズレの誇張倍率。1=実比率のまま、大きいほど劣勢/優勢が大袈裟に見える")]
        [SerializeField, Range(1f, 5f)] private float exaggeration = 2.5f;

        private float _current = 0.5f;
        private float _target = 0.5f;

        /// <summary>連打フェーズ開始時に呼ぶ。中央で拮抗した状態から始める。</summary>
        public void Show()
        {
            _current = _target = 0.5f;
            ApplyVisual();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>ゲージの目標位置。0=CPU側に押し切られた状態、1=プレイヤー側の完勝、0.5=拮抗。</summary>
        public void SetTarget(float normalized)
        {
            float centered = (Mathf.Clamp01(normalized) - 0.5f) * exaggeration;
            _target = Mathf.Clamp01(0.5f + centered);
        }

        private void Update()
        {
            _current = Mathf.Lerp(_current, _target, smoothSpeed * Time.deltaTime);
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (playerFill != null)
                playerFill.fillAmount = _current;

            if (clashMarker != null)
            {
                var parent = (RectTransform)clashMarker.parent;
                float width = parent.rect.width;
                clashMarker.anchoredPosition =
                    new Vector2((_current - 0.5f) * width, clashMarker.anchoredPosition.y);
            }
        }
    }
}
