using UnityEngine;
using UnityEngine.UI;

namespace KaniTactics.Game
{
    /// <summary>
    /// 遊び方パネルのページ送り。各ページはGameObject(スクショ画像1枚など)として並べ、
    /// 次へ/戻るで1枚ずつ切り替える。先頭では「戻る」、末尾では「次へ」が自動で押せなくなる。
    /// タイトルのメニュー操作を止めないよう、開いている間はメインパネルを隠す。
    /// </summary>
    public sealed class HowToPlayPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;      // 遊び方パネル全体
        [SerializeField] private GameObject titleMainPanel; // 開いている間隠すタイトルのメインパネル
        [Tooltip("説明ページを順に割り当てる(1ページ=1オブジェクト)")]
        [SerializeField] private GameObject[] pages;

        [Header("ボタン(端で自動的に押せなくする)")]
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("サウンド(任意)")]
        [SerializeField] private AudioClip seClick;

        private int _index;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        /// <summary>「あそびかた」ボタン用。パネルを開いて1ページ目を表示する。</summary>
        public void Open()
        {
            SoundManager.Instance.PlaySe(seClick);
            if (titleMainPanel != null) titleMainPanel.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(true);
            _index = 0;
            Refresh();
        }

        /// <summary>「閉じる」ボタン用。タイトルのメインへ戻る。</summary>
        public void Close()
        {
            SoundManager.Instance.PlaySe(seClick);
            if (panelRoot != null) panelRoot.SetActive(false);
            if (titleMainPanel != null) titleMainPanel.SetActive(true);
        }

        /// <summary>「次へ」ボタン用。</summary>
        public void Next()
        {
            if (_index >= pages.Length - 1) return;
            SoundManager.Instance.PlaySe(seClick);
            _index++;
            Refresh();
        }

        /// <summary>「戻る」ボタン用。</summary>
        public void Prev()
        {
            if (_index <= 0) return;
            SoundManager.Instance.PlaySe(seClick);
            _index--;
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < pages.Length; i++)
                if (pages[i] != null) pages[i].SetActive(i == _index);

            // 端ではボタンを無効化(押せない見た目になる)
            if (prevButton != null) prevButton.interactable = _index > 0;
            if (nextButton != null) nextButton.interactable = _index < pages.Length - 1;
        }
    }
}
