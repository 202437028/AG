using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// ポーズメニューのボタン中継(BAMBOO OF CHICKENから移植・再構成)。
    /// 開閉・Escトグル・タイマー停止・入力遮断はGameFlowControllerの責務なので、
    /// このクラスは3つのボタンをControllerへ橋渡しし、タイトル遷移だけを担当する。
    /// 旧版にあったTime.timeScale制御は使わない(選択フェーズ限定ポーズのため不要で、
    /// 止めるとカニの待機アニメまで固まる)。
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;

        [Tooltip("タイトルシーン名。Day 2で作成し、File > Build Settings の Scenes In Build に追加すること")]
        [SerializeField] private string titleSceneName = "Title";

        /// <summary>「PAUSEをとじる」ボタン用。</summary>
        public void OnCloseButton() => gameFlow.ResumeFromPause();

        /// <summary>「リスタート」ボタン用。試合を最初から仕切り直す。</summary>
        public void OnRestartButton() => SceneLoader.Instance.FadeAction(gameFlow.RestartMatch);

        /// <summary>「タイトルへ」ボタン用。</summary>
        public void OnTitleButton() => SceneLoader.Instance.LoadScene(titleSceneName);
    }
}
