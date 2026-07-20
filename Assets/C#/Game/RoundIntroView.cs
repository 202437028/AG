using System.Collections;
using TMPro;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// ラウンド開始の演出。「ROUND ○」→「START!!」を順に出す。
    /// </summary>
    public sealed class RoundIntroView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text roundText;  // ROUND 1
        [SerializeField] private TMP_Text startText;  // START!!

        [Header("タイミング")]
        [Tooltip("「ROUND ○」を見せる時間")]
        [SerializeField] private float roundSeconds = 1.0f;
        [Tooltip("「START!!」を見せる時間")]
        [SerializeField] private float startSeconds = 0.8f;

        [Header("サウンド(未割り当ての音は鳴らないだけ)")]
        [SerializeField] private AudioClip seRound;
        [SerializeField] private AudioClip seStart;

        private Coroutine _routine;

        /// <summary>演出の総尺(秒)。GameFlowControllerのフェーズ長に使う。</summary>
        public float TotalSeconds => roundSeconds + startSeconds;

        public void Play(int roundNumber)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (root != null) root.SetActive(true);
            _routine = StartCoroutine(Routine(roundNumber));
        }

        public void Hide()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (root != null) root.SetActive(false);
        }

        private IEnumerator Routine(int roundNumber)
        {
            SetText(roundText, $"ROUND {roundNumber}");
            SetText(startText, "");
            SoundManager.Instance.PlaySe(seRound);
            yield return new WaitForSeconds(roundSeconds);

            SetText(startText, "START!!");
            SoundManager.Instance.PlaySe(seStart);
            yield return new WaitForSeconds(startSeconds);

            _routine = null;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
