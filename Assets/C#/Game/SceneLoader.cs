using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KaniTactics.Game
{
    /// <summary>
    /// 黒フェードを挟んだシーン遷移。初回アクセス時にCanvasごと自動生成され、シーンをまたいで生存する。
    /// シーンに何も置く必要はなく、SceneManager.LoadSceneの呼び出しをこれに差し替えるだけでよい。
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        private static SceneLoader _instance;

        public static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SceneLoader");
                    _instance = go.AddComponent<SceneLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Tooltip("暗転・明転にかける秒数")]
        private const float FadeSeconds = 0.25f;

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

        /// <summary>暗転 → シーン読み込み → 明転。連打で多重呼び出ししても1回だけ実行される。</summary>
        public void LoadScene(string sceneName)
        {
            if (_busy) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"シーン '{sceneName}' がBuild Settingsに未登録です。");
                return;
            }
            StartCoroutine(LoadRoutine(sceneName));
        }

        /// <summary>暗転 → 処理を実行 → 明転。シーンを変えないリスタートなどに使う。</summary>
        public void FadeAction(Action action)
        {
            if (_busy) return;
            StartCoroutine(ActionRoutine(action));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            yield return Fade(1f);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;

            yield return Fade(0f);
            _group.blocksRaycasts = false;
            _busy = false;
        }

        private IEnumerator ActionRoutine(Action action)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            yield return Fade(1f);

            action?.Invoke();
            yield return null; // 1フレーム置いて画面の更新を待つ

            yield return Fade(0f);
            _group.blocksRaycasts = false;
            _busy = false;
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
