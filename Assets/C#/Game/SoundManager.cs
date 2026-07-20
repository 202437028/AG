using System.Collections;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// BGM/SE再生の一元管理。初回アクセス時に自動生成され、シーンをまたいで生存する。
    /// クリップは各呼び出し側(GameFlowController / TitleMenuController)が持ち、ここは再生だけを担う。
    /// ダッキング: SE再生中はBGMを一時的に下げ、終わったら自動で戻す(DuckForSeconds)。
    /// 試合勝敗ジングルのような「その場面が続く間ずっと無音でいい」音は DuckMute で完全ミュートし、
    /// 次にPlayBgmが呼ばれるまで自動では戻さない。
    /// </summary>
    public sealed class SoundManager : MonoBehaviour
    {
        private const string BgmVolumeKey = "BgmVolume";
        private const string SeVolumeKey = "SeVolume";

        private static SoundManager _instance;

        public static SoundManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("SoundManager");
                    _instance = go.AddComponent<SoundManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AudioSource _bgmSource;
        private AudioSource _seSource;
        private AudioSource _loopSeSource;
        private Coroutine _duckRoutine;

        public float BgmVolume { get; private set; }
        public float SeVolume { get; private set; }

        private void Awake()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;

            _seSource = gameObject.AddComponent<AudioSource>();
            _seSource.playOnAwake = false;

            _loopSeSource = gameObject.AddComponent<AudioSource>();
            _loopSeSource.loop = true;
            _loopSeSource.playOnAwake = false;

            BgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, 0.8f);
            SeVolume = PlayerPrefs.GetFloat(SeVolumeKey, 0.8f);
            _seSource.volume = SeVolume;
            _loopSeSource.volume = SeVolume;
            _bgmSource.volume = BgmVolume;
        }

        /// <summary>
        /// BGMを再生する。ダッキングで下がっていた音量は必ずここで正規音量に戻る
        /// (試合勝敗ジングルでミュートした後、次の試合開始で自動的に元へ戻すため)。
        /// 同じクリップが既に再生中なら再生位置は維持し、音量だけ戻す。
        /// </summary>
        public void PlayBgm(AudioClip clip)
        {
            if (clip == null) { StopBgm(); return; }
            if (_duckRoutine != null) { StopCoroutine(_duckRoutine); _duckRoutine = null; }
            _bgmSource.volume = BgmVolume;
            if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
            _bgmSource.clip = clip;
            _bgmSource.Play();
        }

        public void StopBgm() => _bgmSource.Stop();

        /// <summary>単発SE。nullは無視(未割り当てでも安全)。</summary>
        public void PlaySe(AudioClip clip)
        {
            if (clip == null) return;
            _seSource.PlayOneShot(clip);
        }

        /// <summary>ループSE(連打中など)。同じクリップ再生中なら何もしない。</summary>
        public void PlayLoopSe(AudioClip clip)
        {
            if (clip == null) return;
            if (_loopSeSource.clip == clip && _loopSeSource.isPlaying) return;
            _loopSeSource.clip = clip;
            _loopSeSource.Play();
        }

        public void StopLoopSe() => _loopSeSource.Stop();

        /// <summary>
        /// BGMを一時的に下げて、durationSeconds後に自動で元の音量へ戻す。
        /// duckLevelは正規音量に対する倍率(0.5=半分、0=無音)。公開・ラウンド勝敗の演出向け。
        /// </summary>
        public void DuckForSeconds(float duckLevel, float durationSeconds, float fadeSeconds = 0.15f)
        {
            if (_duckRoutine != null) StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckRoutine(duckLevel, durationSeconds, fadeSeconds));
        }

        /// <summary>
        /// BGMを完全ミュートにし、自動では戻さない。試合勝敗ジングル向け
        /// (次にPlayBgmが呼ばれた瞬間に正規音量へ復帰する)。
        /// </summary>
        public void DuckMute(float fadeSeconds = 0.15f)
        {
            if (_duckRoutine != null) { StopCoroutine(_duckRoutine); _duckRoutine = null; }
            StartCoroutine(FadeBgm(0f, fadeSeconds));
        }

        private IEnumerator DuckRoutine(float duckLevel, float holdSeconds, float fadeSeconds)
        {
            yield return FadeBgm(BgmVolume * duckLevel, fadeSeconds);
            float remain = holdSeconds - fadeSeconds;
            if (remain > 0f) yield return new WaitForSeconds(remain);
            yield return FadeBgm(BgmVolume, fadeSeconds);
            _duckRoutine = null;
        }

        private IEnumerator FadeBgm(float target, float duration)
        {
            if (duration <= 0f) { _bgmSource.volume = target; yield break; }
            float start = _bgmSource.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            _bgmSource.volume = target;
        }

        public void SetBgmVolume(float value)
        {
            BgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
            if (_duckRoutine == null) _bgmSource.volume = BgmVolume;
        }

        public void SetSeVolume(float value)
        {
            SeVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SeVolumeKey, SeVolume);
            _seSource.volume = SeVolume;
            _loopSeSource.volume = SeVolume;
        }
    }
}
