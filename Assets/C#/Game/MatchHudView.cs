using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// 対戦画面のHUD。表示はこのViewだけが担当し、GameFlowControllerは表示の実装を知らない。
    /// 各テキストは未割り当て(None)でも安全に動く(その表示が省略されるだけ)。
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerText;  // ラウンド・スコア
        [Tooltip("相手の手札(テキスト版)。牌の列を使う場合は割り当て不要")]
        [SerializeField] private TMP_Text handsText;
        [SerializeField] private TMP_Text mainText;    // 残り時間
        [SerializeField] private TMP_Text subText;     // 操作説明・補足(未使用なら空でよい)

        [Header("試合終了の案内(専用テキスト)")]
        [Tooltip("試合終了時だけ表示される。勝者と「Rで再戦 / Escでタイトルへ」を出す")]
        [SerializeField] private TMP_Text matchEndText;

        [Header("牌の列で手札を表示する場合(任意)")]
        [Tooltip("相手の手札の列。割り当てるとhandsTextは非表示になる")]
        [SerializeField] private TileRowView opponentRow;
        [Tooltip("自分の手札の列(左下)。カーソル強調もここに出る")]
        [SerializeField] private TileRowView selfRow;

        private void Awake()
        {
            // 試合終了の案内は最初は隠しておく
            if (matchEndText != null) matchEndText.gameObject.SetActive(false);
        }

        public void Render(string header, string hands, string main, string sub)
        {
            if (headerText != null) headerText.text = header;
            if (mainText != null) mainText.text = main;
            if (subText != null) subText.text = sub;

            if (handsText != null)
            {
                bool useRow = opponentRow != null;
                handsText.gameObject.SetActive(!useRow);
                if (!useRow) handsText.text = hands;
            }
        }

        /// 試合終了の案内を出す。messageがnullまたは空なら非表示になる。
        public void RenderMatchEnd(string message)
        {
            if (matchEndText == null) return;

            bool show = !string.IsNullOrEmpty(message);
            if (matchEndText.gameObject.activeSelf != show)
                matchEndText.gameObject.SetActive(show);
            if (show) matchEndText.text = message;
        }

        /// 牌の列を更新する。selfHandは選択中プレイヤーの手札、opponentHandは相手の手札。
        /// highlightTileは自分側の列にだけ効く(選択カーソル)。
        /// 数字色は手番に応じて自分/相手の列で切り替える(1P=赤 / 2P=青)。
        public void RenderTileRows(IReadOnlyCollection<int> selfHand,
                                   IReadOnlyCollection<int> opponentHand,
                                   int? highlightTile,
                                   Color selfColor, Color opponentColor)
        {
            if (selfRow != null)
            {
                selfRow.SetNumberColor(selfColor);
                selfRow.Render(selfHand, highlightTile);
            }
            if (opponentRow != null)
            {
                opponentRow.SetNumberColor(opponentColor);
                opponentRow.Render(opponentHand);
            }
        }
    }
}