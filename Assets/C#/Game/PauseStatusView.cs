using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// ポーズ画面の戦績表示。現在スコアと、両者の牌の使用状況(未使用=明・使用済=暗)を表示する。
    /// ポーズパネルの子に配置し、ポーズを開くたびにGameFlowControllerから最新状態が流し込まれる。
    /// </summary>
    public sealed class PauseStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [Tooltip("上段: プレイヤーA(あなた/P1)の牌")]
        [SerializeField] private TileRowView rowA;
        [Tooltip("下段: プレイヤーB(CPU/P2)の牌")]
        [SerializeField] private TileRowView rowB;

        public void Render(string score,
                           IReadOnlyCollection<int> handA,
                           IReadOnlyCollection<int> handB)
        {
            if (scoreText != null) scoreText.text = score;
            if (rowA != null) rowA.Render(handA);
            if (rowB != null) rowB.Render(handB);
        }
    }
}
