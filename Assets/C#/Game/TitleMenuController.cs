using UnityEngine;
using UnityEngine.SceneManagement;

namespace KaniTactics.Game
{
    /// <summary>
    /// タイトル画面のメニュー制御。
    /// メインパネル(ソロ/ふたりで/終了)と難易度パネル(4段階+戻る)を切り替え、
    /// 選択結果をMatchSettingsに書いてGameシーンへ遷移する。
    /// </summary>
    public sealed class TitleMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject mainPanel;       // ソロ / ふたりで / 終了
        [SerializeField] private GameObject difficultyPanel; // イージー〜名人 + 戻る
        [SerializeField] private string gameSceneName = "Game";

        [Header("隠し要素: 名人モード")]
        [Tooltip("難易度パネル内の名人ボタン。未解放時は非表示になる")]
        [SerializeField] private GameObject meijinButton;
        [Tooltip("解放演出のルート(テキスト・エフェクト等、任意)。解放の瞬間にアクティブ化される")]
        [SerializeField] private GameObject unlockEffect;

        private const string MeijinPrefKey = "MeijinUnlocked";

        [Header("サウンド(未割り当ての音は鳴らないだけ)")]
        [SerializeField] private AudioClip bgmTitle;
        [SerializeField] private AudioClip seClick;   // ボタン決定
        [SerializeField] private AudioClip seUnlock;  // 名人解放

        /// <summary>名人モードが解放済みか(PlayerPrefsで永続化)。</summary>
        public static bool IsMeijinUnlocked => PlayerPrefs.GetInt(MeijinPrefKey, 0) == 1;

        private void Start()
        {
            SoundManager.Instance.PlayBgm(bgmTitle);
            ShowMain();
            if (meijinButton != null) meijinButton.SetActive(IsMeijinUnlocked);
            if (unlockEffect != null) unlockEffect.SetActive(false);
        }

        /// <summary>隠しコマンド成立時にTitleSecretCommandから呼ばれる。</summary>
        public void UnlockMeijin()
        {
            if (IsMeijinUnlocked) return;
            PlayerPrefs.SetInt(MeijinPrefKey, 1);
            PlayerPrefs.Save();
            SoundManager.Instance.PlaySe(seUnlock);
            if (meijinButton != null) meijinButton.SetActive(true);
            if (unlockEffect != null) unlockEffect.SetActive(true);
        }

        private void ShowMain()
        {
            if (mainPanel != null) mainPanel.SetActive(true);
            if (difficultyPanel != null) difficultyPanel.SetActive(false);
        }

        /// <summary>「ソロでプレイ」ボタン → 難易度選択へ。</summary>
        public void OnSoloButton()
        {
            SoundManager.Instance.PlaySe(seClick);
            if (mainPanel != null) mainPanel.SetActive(false);
            if (difficultyPanel != null) difficultyPanel.SetActive(true);
        }

        /// <summary>難易度ボタン用。OnClickの引数に 0=イージー 1=ノーマル 2=ハード 3=名人 を渡す。</summary>
        public void OnDifficultyButton(int difficulty)
        {
            SoundManager.Instance.PlaySe(seClick);
            MatchSettings.Configured = true;
            MatchSettings.Mode = GameFlowController.GameMode.SoloCpu;
            MatchSettings.CpuDifficulty = Mathf.Clamp(difficulty, 0, 3);
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>「ふたりでプレイ」ボタン → ローカル2Pで即開始。</summary>
        public void OnVersusButton()
        {
            SoundManager.Instance.PlaySe(seClick);
            MatchSettings.Configured = true;
            MatchSettings.Mode = GameFlowController.GameMode.LocalVersus;
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>難易度パネルの「戻る」ボタン。</summary>
        public void OnBackButton()
        {
            SoundManager.Instance.PlaySe(seClick);
            ShowMain();
        }

        /// <summary>「ゲーム終了」ボタン。エディタ再生中は何も起きない(ビルドでのみ有効)。</summary>
        public void OnQuitButton() => Application.Quit();
    }
}
