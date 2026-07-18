using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// グレーボックスHUD。表示はこのViewだけが担当し、GameFlowControllerは表示の実装を知らない。
    /// M4ではこのクラスを本番の演出Viewに差し替える(Controllerは無傷で済む)。
    /// セットアップ: Canvas配下にTextMeshProのテキストを4つ置き、各フィールドに割り当てる。
    /// </summary>
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text headerText;  // ラウンド・スコア
        [SerializeField] private TMP_Text handsText;   // 両者の手札
        [SerializeField] private TMP_Text mainText;    // フェーズごとのメイン表示
        [SerializeField] private TMP_Text subText;     // 操作説明・補足

        public void Render(string header, string hands, string main, string sub)
        {
            headerText.text = header;
            handsText.text = hands;
            mainText.text = main;
            subText.text = sub;
        }
    }
}
