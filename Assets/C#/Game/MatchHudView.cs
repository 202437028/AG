using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// グレーボックスHUD。表示はこのViewだけが担当し、GameFlowControllerは表示の実装を知らない。
    /// 手札のTileRowViewを割り当てると、手札のテキスト表示(handsText)は自動で消え、画像表示に切り替わる。
    /// </summary>
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerText;  // ラウンド・スコア
        [SerializeField] private TMP_Text handsText;   // 両者の手札(テキスト版。TileRow割り当てで不使用に)
        [SerializeField] private TMP_Text mainText;    // フェーズごとのメイン表示
        [SerializeField] private TMP_Text subText;     // 操作説明・補足

        [Header("牌の画像表示(両方割り当てるとhandsTextの代わりに使われる)")]
        [SerializeField] private TileRowView handRowA;
        [SerializeField] private TileRowView handRowB;

        private bool UseTileRows => handRowA != null && handRowB != null;

        public void Render(string header, string hands, string main, string sub)
        {
            headerText.text = header;
            mainText.text = main;
            subText.text = sub;

            if (handsText != null)
            {
                handsText.gameObject.SetActive(!UseTileRows);
                if (!UseTileRows) handsText.text = hands;
            }
        }

        /// <summary>牌の画像列を更新する。highlightTileは選択カーソル(highlightSideの側の列にだけ効く)。</summary>
        public void RenderTileRows(IReadOnlyCollection<int> handA,
                                   IReadOnlyCollection<int> handB,
                                   int? highlightTile, Player highlightSide)
        {
            if (!UseTileRows) return;
            handRowA.Render(handA, highlightSide == Player.A ? highlightTile : null);
            handRowB.Render(handB, highlightSide == Player.B ? highlightTile : null);
        }
    }
}
