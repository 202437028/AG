using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 対戦画面のHUD。表示はこのViewだけが担当し、GameFlowControllerは表示の実装を知らない。
    /// 手札の見せ方は2通りあり、シーン側の割り当てで選べる:
    ///   ・handsText(テキスト)  … 相手の手札のみを1行で表示(画面右上想定)
    ///   ・handRow*(牌の列)     … TileRowViewで牌を並べて表示。割り当てるとhandsTextは自動で隠れる
    /// </summary>
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerText;  // ラウンド・スコア
        [Tooltip("相手の手札(テキスト版)。牌の列を使う場合は割り当て不要")]
        [SerializeField] private TMP_Text handsText;
        [SerializeField] private TMP_Text mainText;    // フェーズごとのメイン表示(自分の選択カーソル)
        [SerializeField] private TMP_Text subText;     // 操作説明・補足

        [Header("牌の列で手札を表示する場合(任意)")]
        [Tooltip("相手の手札の列。割り当てるとhandsTextは非表示になる")]
        [SerializeField] private TileRowView opponentRow;
        [Tooltip("自分の手札の列(左下)。カーソル強調もここに出る")]
        [SerializeField] private TileRowView selfRow;

        public void Render(string header, string hands, string main, string sub)
        {
            headerText.text = header;
            mainText.text = main;
            subText.text = sub;

            if (handsText != null)
            {
                bool useRow = opponentRow != null;
                handsText.gameObject.SetActive(!useRow);
                if (!useRow) handsText.text = hands;
            }
        }

        /// <summary>
        /// 牌の列を更新する。selfHandは選択中プレイヤーの手札、opponentHandは相手の手札。
        /// highlightTileは自分側の列にだけ効く(選択カーソル)。
        /// </summary>
        public void RenderTileRows(IReadOnlyCollection<int> selfHand,
                                   IReadOnlyCollection<int> opponentHand,
                                   int? highlightTile)
        {
            if (selfRow != null) selfRow.Render(selfHand, highlightTile);
            if (opponentRow != null) opponentRow.Render(opponentHand);
        }
    }
}
