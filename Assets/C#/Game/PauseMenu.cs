using UnityEngine;

namespace KaniTactics.Game
{
    /// ポーズメニューのボタン中継(BAMBOO OF CHICKENから移植・再構成)。
    /// 開閉・Escトグル・タイマー停止・入力遮断はGameFlowControllerの責務なので、
    /// このクラスは3つのボタンをControllerへ橋渡しし、タイトル遷移だけを担当する。
    /// 旧版にあったTime.timeScale制御は使わない(選択フェーズ限定ポーズのため不要で、
    /// 止めるとカニの待機アニメまで固まる)。
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;

        [Tooltip("タイトルシーン名。Day 2で作成し、File > Build Settings の Scenes In Build に追加すること")]
        [SerializeField] private string titleSceneName = "Title";

        /// 「PAUSEをとじる」ボタン用。
        public void OnCloseButton() => gameFlow.ResumeFromPause();

        /// 「リスタート」ボタン用。試合を最初から仕切り直す。
        public void OnRestartButton() => SceneLoader.Instance.FadeAction(gameFlow.RestartMatch);

        /// 「タイトルへ」ボタン用。
        public void OnTitleButton() => SceneLoader.Instance.LoadScene(titleSceneName);
    }
}
