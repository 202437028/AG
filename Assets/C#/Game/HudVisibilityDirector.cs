using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// フェーズごとのHUD表示制御。演出の見せ場で不要なUIを引っ込め、視認性を上げる。
    /// Canvasにアタッチし、各UIオブジェクトを割り当てる(未割り当ての枠は単に無視される)。
    /// </summary>
    public sealed class HudVisibilityDirector : MonoBehaviour
    {
        [Tooltip("ラウンド・スコア表示")]
        [SerializeField] private GameObject headerText;
        [Tooltip("残り時間表示(MainText)")]
        [SerializeField] private GameObject timerText;
        [Tooltip("操作説明・状況表示(SubText)")]
        [SerializeField] private GameObject subText;
        [Tooltip("相手の手札の列(右上)")]
        [SerializeField] private GameObject opponentRow;
        [Tooltip("自分の手札の列(左下)")]
        [SerializeField] private GameObject selfRow;
        [Tooltip("PAUSEボタン")]
        [SerializeField] private GameObject pauseButton;

        /// <summary>選択フェーズ・ラウンド開始時: 全部表示。</summary>
        public void ShowAll() => Apply(true, true, true, true, true, true);

        /// <summary>判定・カウントダウン中: 演出以外を全部隠す。</summary>
        public void HideAll() => Apply(false, false, false, false, false, false);

        /// <summary>連打フェーズ: ヘッダ・残り時間・操作説明のみ表示(手札・PAUSEは隠す)。</summary>
        public void ShowMashOnly() => Apply(true, true, true, false, false, false);

        private void Apply(bool header, bool timer, bool sub, bool opponent, bool self, bool pause)
        {
            SetActive(headerText, header);
            SetActive(timerText, timer);
            SetActive(subText, sub);
            SetActive(opponentRow, opponent);
            SetActive(selfRow, self);
            SetActive(pauseButton, pause);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }
    }
}