using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaniTactics.Game
{
    /// 黒フェードを挟んだシーン遷移。初回アクセス時にCanvasごと自動生成され、シーンをまたいで生存する(PersistentSingleton)。
    /// シーンに何も置く必要はなく、SceneManager.LoadSceneの呼び出しをこれに差し替えるだけでよい。
    public sealed class SceneLoader : PersistentSingleton<SceneLoader>
    {
        private const float FadeSeconds = 0.25f; // 暗転・明転にかける秒数

        private CanvasGroup _group;
        private bool _busy;

        private void Awake()
        {
            // フェード用のCanvasを自前で組み立てる(他のUIより手前に出す)
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            var imageGo = new GameObject("Blackout");
            imageGo.transform.SetParent(canvasGo.transform, false);
            var image = imageGo.AddComponent<Image>();
            image.color = Color.black;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// 暗転 → シーン読み込み → 明転。連打で多重呼び出ししても1回だけ実行される。
        public void LoadScene(string sceneName)
        {
            if (_busy) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"シーン '{sceneName}' がBuild Settingsに未登録です。");
                return;
            }
            StartCoroutine(FadeAround(LoadRoutine(sceneName)));
        }

        /// 暗転 → 処理を実行 → 明転。シーンを変えないリスタートなどに使う。
        public void FadeAction(Action action)
        {
            if (_busy) return;
            StartCoroutine(FadeAround(ActionRoutine(action)));
        }

        /// 暗転 → body(暗転中の処理) → 明転。実行中は入力を遮断し、多重実行を防ぐ。
        private IEnumerator FadeAround(IEnumerator body)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            yield return Fade(1f);

            yield return body;

            yield return Fade(0f);
            _group.blocksRaycasts = false;
            _busy = false;
        }

        private static IEnumerator LoadRoutine(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
        }

        private static IEnumerator ActionRoutine(Action action)
        {
            action?.Invoke();
            yield return null; // 1フレーム置いて画面の更新を待つ
        }

        private IEnumerator Fade(float target)
        {
            float start = _group.alpha;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, target, t / FadeSeconds);
                yield return null;
            }
            _group.alpha = target;
        }
    }
}
