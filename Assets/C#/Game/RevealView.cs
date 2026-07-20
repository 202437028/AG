using System.Collections;
using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// 公開フェーズの演出。1P → 2P の順に牌を1枚ずつ見せ(ドン! ドン!)、判定を出す(ドドン!)。
    /// ・連打時: 「連打バトル発生!」→ 操作説明 → 3・2・1・開始めいッッ
    /// ・大差時: 「大差勝ち」+ スコア
    /// 所要時間はGameFlowControllerと共有するためプロパティで公開している。
    /// </summary>
    public sealed class RevealView : MonoBehaviour
    {
        [SerializeField] private GameObject root;          // 演出全体のルート(表示/非表示の切替対象)
        [SerializeField] private TileCardView cardA;       // 左: P1/あなた
        [SerializeField] private TileCardView cardB;       // 右: P2/CPU
        [SerializeField] private TMP_Text headlineText;    // 「連打バトル発生!」「大差勝ち」
        [SerializeField] private TMP_Text instructionText; // 操作説明・スコア
        [SerializeField] private TMP_Text countdownText;   // 3 / 2 / 1 / 開始めいッッ
        [Tooltip("演出中にHUDを引っ込めるためのディレクター(任意)")]
        [SerializeField] private HudVisibilityDirector hudVisibility;

        [Header("タイミング")]
        [Tooltip("牌を1枚見せてから次の動作までの間(1P→2P→判定の各間隔)")]
        [SerializeField] private float cardRevealStep = 0.6f;
        [Tooltip("見出し+操作説明を見せる時間")]
        [SerializeField] private float headlineSeconds = 1.0f;
        [Tooltip("3→2→1→開始 の各間隔")]
        [SerializeField] private float countdownStep = 0.6f;
        [Tooltip("大差決着時に判定を見せる時間")]
        [SerializeField] private float immediateHoldSeconds = 1.2f;

        [Header("カウントダウン文言")]
        [SerializeField] private string startWord = "開始めいッッ";

        [Header("サウンド(未割り当ての音は鳴らないだけ)")]
        [Tooltip("牌が1枚出た瞬間(ドン!)。1P・2Pの両方で鳴る")]
        [SerializeField] private AudioClip seTileReveal;
        [Tooltip("判定が出た瞬間 = 「連打バトル発生!」「大差勝ち」(ドドン!)")]
        [SerializeField] private AudioClip seJudge;
        [Tooltip("カウントダウンの3・2・1(木魚)")]
        [SerializeField] private AudioClip seCountdownTick;
        [Tooltip("「開始めいッッ」(ビブラスラップ)")]
        [SerializeField] private AudioClip seStart;

        private Coroutine _routine;

        /// <summary>連打突入演出の総尺(秒)。GameFlowControllerのRevealフェーズ長に使う。</summary>
        public float MashIntroSeconds => cardRevealStep * 2f + headlineSeconds + countdownStep * 4f;

        /// <summary>大差決着演出の総尺(秒)。</summary>
        public float ImmediateSeconds => cardRevealStep * 2f + immediateHoldSeconds;

        /// <summary>連打突入の演出を再生する。instructionはモード別の操作説明。</summary>
        public void PlayMashIntro(int tileA, int tileB, string instruction)
        {
            Begin();
            _routine = StartCoroutine(MashIntroRoutine(tileA, tileB, instruction));
        }

        /// <summary>大差決着の演出を再生する。</summary>
        public void PlayImmediate(int tileA, int tileB, string score)
        {
            Begin();
            _routine = StartCoroutine(ImmediateRoutine(tileA, tileB, score));
        }

        public void Hide()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (root != null) root.SetActive(false);
        }

        private void Begin()
        {
            if (_routine != null) StopCoroutine(_routine);
            if (root != null) root.SetActive(true);
            if (cardA != null) cardA.SetVisible(false);
            if (cardB != null) cardB.SetVisible(false);
            SetText(headlineText, "");
            SetText(instructionText, "");
            SetText(countdownText, "");
        }

        /// <summary>1P → 2P の順に牌を1枚ずつ公開する(各ドン!)。</summary>
        private IEnumerator RevealCards(int tileA, int tileB)
        {
            if (cardA != null) { cardA.SetTile(tileA); cardA.SetVisible(true); }
            SoundManager.Instance.PlaySe(seTileReveal); // 1P(ドン!)
            yield return new WaitForSeconds(cardRevealStep);

            if (cardB != null) { cardB.SetTile(tileB); cardB.SetVisible(true); }
            SoundManager.Instance.PlaySe(seTileReveal); // 2P(ドン!)
            yield return new WaitForSeconds(cardRevealStep);
        }

        private IEnumerator MashIntroRoutine(int tileA, int tileB, string instruction)
        {
            yield return RevealCards(tileA, tileB);

            SetText(headlineText, "連打バトル発生!");
            SetText(instructionText, instruction);
            SoundManager.Instance.PlaySe(seJudge); // 判定(ドドン!)
            if (hudVisibility != null) hudVisibility.HideAll(); // 判定の瞬間から演出だけを見せる
            yield return new WaitForSeconds(headlineSeconds);

            // カウントダウンからは数字だけを残す(牌・見出し・操作説明を畳む)
            if (cardA != null) cardA.SetVisible(false);
            if (cardB != null) cardB.SetVisible(false);
            SetText(headlineText, "");
            SetText(instructionText, "");

            foreach (var word in new[] { "3", "2", "1", startWord })
            {
                SetText(countdownText, word);
                // 3・2・1は木魚、開始の合図だけビブラスラップ
                SoundManager.Instance.PlaySe(word == startWord ? seStart : seCountdownTick);
                yield return new WaitForSeconds(countdownStep);
            }

            SetText(countdownText, "");
            _routine = null;
        }

        private IEnumerator ImmediateRoutine(int tileA, int tileB, string score)
        {
            yield return RevealCards(tileA, tileB);

            SetText(headlineText, "大差勝ち");
            SetText(instructionText, score);
            SoundManager.Instance.PlaySe(seJudge); // 判定(ドドン!)
            if (hudVisibility != null) hudVisibility.HideAll(); // 判定の瞬間から演出だけを見せる
            yield return new WaitForSeconds(immediateHoldSeconds);

            _routine = null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
