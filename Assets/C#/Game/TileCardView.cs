using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// 公開演出用の牌カード1枚。TileRowViewと同じ「漢数字+蟹」の二色刷りを単体で表示する。
    /// </summary>
    public sealed class TileCardView : MonoBehaviour
    {
        [Tooltip("カード全体のルート(未指定ならこのオブジェクト自身)。段階公開で表示/非表示を切り替える")]
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private TMP_Text label;
        [Tooltip("数字の色。1P側=赤、2P側=青に設定する")]
        [SerializeField] private Color numberColor = new Color(0.76f, 0.15f, 0.13f);
        [SerializeField] private Color crabColor = new Color(0.10f, 0.10f, 0.10f);
        [SerializeField] private string suffix = "蟹";
        [SerializeField] private bool vertical = true;

        private static readonly string[] Kanji =
            { "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        private GameObject Root => cardRoot != null ? cardRoot : gameObject;

        /// <summary>カードの表示/非表示。段階的な公開演出に使う。</summary>
        public void SetVisible(bool visible) => Root.SetActive(visible);

        public void SetTile(int tile)
        {
            if (label == null || tile < 1 || tile > 9) return;
            label.richText = true;
            label.color = Color.white;
            string numHex = ColorUtility.ToHtmlStringRGBA(numberColor);
            string crabHex = ColorUtility.ToHtmlStringRGBA(crabColor);
            string sep = vertical ? "\n" : "";
            label.text =
                $"<color=#{numHex}>{Kanji[tile - 1]}</color>{sep}<color=#{crabHex}>{suffix}</color>";
        }
    }
}
