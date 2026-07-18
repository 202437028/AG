using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// カニ2体の演出View。3つのモードを持つ:
    ///   Home   … 定位置でアイドル
    ///   Clash  … 押し合い(ゲージと同じ正規化値で駆動。バーの境界と衝突点が一致する)
    ///   Result … 勝ちカニが跳ね、負けカニが転がる(クリップ不要のプロシージャル演出)
    /// </summary>
    public sealed class CrabClashView : MonoBehaviour
    {
        private enum Mode { Home, Clash, Result }

        [Tooltip("左側のカニ(P1/あなた)")]
        [SerializeField] private Transform crabA;
        [Tooltip("右側のカニ(P2/CPU)")]
        [SerializeField] private Transform crabB;

        [Header("配置")]
        [Tooltip("カメラが逆側から見る配置のとき有効化。押し合いの左右を反転する")]
        [SerializeField] private bool mirrored = false;

        [Header("押し合いの動き")]
        [Tooltip("衝突点が中央から左右に動ける最大距離")]
        [SerializeField] private float pushRange = 2f;
        [Tooltip("衝突点から各カニの中心までの距離(ハサミが噛み合う間合い)")]
        [SerializeField] private float contactGap = 0.8f;
        [Tooltip("位置の追従速度。ゲージのsmoothSpeedと同じ値にすると完全に同期する")]
        [SerializeField] private float smoothSpeed = 6f;

        [Header("踏ん張り演出")]
        [Tooltip("押し合い中の揺れ幅。拮抗しているほど強く揺れる")]
        [SerializeField] private float struggleShake = 0.06f;
        [Tooltip("押し合い中の前傾角度(モデルの向きによっては符号を調整)")]
        [SerializeField] private float tiltDegrees = 8f;

        [Header("勝敗演出")]
        [Tooltip("勝ちカニの跳ねる高さ")]
        [SerializeField] private float hopHeight = 0.45f;
        [Tooltip("勝ちカニが1秒あたりに跳ねる回数")]
        [SerializeField] private float hopsPerSecond = 2.5f;
        [Tooltip("負けカニの転がる角度")]
        [SerializeField] private float rollDegrees = 110f;
        [Tooltip("負けカニの沈み込み量")]
        [SerializeField] private float sinkDepth = 0.15f;

        [Header("アニメーション(任意。未割り当てなら位置演出のみ)")]
        [SerializeField] private Animator animatorA;
        [SerializeField] private Animator animatorB;
        [Tooltip("Animator ControllerのBoolパラメータ名。押し合い中にtrueになる")]
        [SerializeField] private string clashingBoolParam = "Clashing";

        private Vector3 _homeA, _homeB;
        private Quaternion _homeRotA, _homeRotB;
        private float _current = 0.5f;
        private float _target = 0.5f;
        private Mode _mode = Mode.Home;
        private float _resultTime;
        private Player _resultWinner;

        private void Awake()
        {
            if (crabA != null) { _homeA = crabA.localPosition; _homeRotA = crabA.localRotation; }
            if (crabB != null) { _homeB = crabB.localPosition; _homeRotB = crabB.localRotation; }
        }

        /// <summary>連打フェーズ開始。中央で組み合った状態から始める。</summary>
        public void BeginClash()
        {
            _current = _target = 0.5f;
            _mode = Mode.Clash;
            SetClashingAnim(true);
        }

        /// <summary>ラウンド結果の演出を開始する。勝ちカニが跳ね、負けカニが転がる。</summary>
        public void PlayResult(Player winner)
        {
            _resultWinner = winner;
            _resultTime = 0f;
            _mode = Mode.Result;
            SetClashingAnim(false);
        }

        /// <summary>定位置のアイドルへ戻す(選択フェーズ突入時に呼ぶ)。</summary>
        public void ReturnHome()
        {
            _mode = Mode.Home;
            SetClashingAnim(false);
        }

        /// <summary>0=B(右)側が押し切り、1=A(左)側が押し切り、0.5=拮抗。ゲージと同じ値を渡す。</summary>
        public void SetTarget(float normalized) => _target = Mathf.Clamp01(normalized);

        private void SetClashingAnim(bool value)
        {
            if (animatorA != null) animatorA.SetBool(clashingBoolParam, value);
            if (animatorB != null) animatorB.SetBool(clashingBoolParam, value);
        }

        private void Update()
        {
            if (crabA == null || crabB == null) return;

            Vector3 desiredPosA, desiredPosB;
            Quaternion desiredRotA, desiredRotB;
            float dir = mirrored ? -1f : 1f;

            switch (_mode)
            {
                case Mode.Clash:
                {
                    _current = Mathf.Lerp(_current, _target, smoothSpeed * Time.deltaTime);

                    // 0.5=中央。Aが優勢(→1)なほど衝突点がB側へ食い込む
                    float clashX = (_current - 0.5f) * 2f * pushRange * dir;

                    // 拮抗しているほど激しく震える(Perlinノイズでガタつかず滑らかに)
                    float intensity = 1f - Mathf.Abs(_current - 0.5f) * 2f;
                    float t = Time.time * 13f;
                    var jitterA = JitterOffset(t, 0f) * struggleShake * intensity;
                    var jitterB = JitterOffset(t, 7.3f) * struggleShake * intensity;

                    desiredPosA = new Vector3(clashX - contactGap * dir, _homeA.y, _homeA.z) + jitterA;
                    desiredPosB = new Vector3(clashX + contactGap * dir, _homeB.y, _homeB.z) + jitterB;
                    desiredRotA = _homeRotA * Quaternion.Euler(0f, 0f, -tiltDegrees * dir);
                    desiredRotB = _homeRotB * Quaternion.Euler(0f, 0f, tiltDegrees * dir);
                    break;
                }

                case Mode.Result:
                {
                    _resultTime += Time.deltaTime;
                    bool aWon = _resultWinner == Player.A;

                    // 勝者: ホーム位置でピョンピョン跳ねる
                    float hop = Mathf.Abs(Mathf.Sin(_resultTime * Mathf.PI * hopsPerSecond)) * hopHeight;
                    // 敗者: 0.6秒かけて転がって少し沈む(イージング付き)
                    float roll = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_resultTime / 0.6f));

                    var winPosOffset = Vector3.up * hop;
                    var losePosOffset = Vector3.down * (sinkDepth * roll);
                    var loseRot = Quaternion.Euler(0f, 0f, rollDegrees * roll * dir);

                    desiredPosA = _homeA + (aWon ? winPosOffset : losePosOffset);
                    desiredPosB = _homeB + (aWon ? losePosOffset : winPosOffset);
                    desiredRotA = aWon ? _homeRotA : _homeRotA * loseRot;
                    desiredRotB = aWon ? _homeRotB * Quaternion.Inverse(loseRot) : _homeRotB;
                    break;
                }

                case Mode.Home:
                default:
                    desiredPosA = _homeA;
                    desiredPosB = _homeB;
                    desiredRotA = _homeRotA;
                    desiredRotB = _homeRotB;
                    break;
            }

            float k = smoothSpeed * Time.deltaTime;
            // 勝者の跳ねだけは補間せず直接反映(補間すると跳ねが潰れて「浮いてる」だけになる)
            if (_mode == Mode.Result)
            {
                bool aWon = _resultWinner == Player.A;
                if (aWon) { crabA.localPosition = desiredPosA; }
                else { crabB.localPosition = desiredPosB; }
                if (aWon) crabB.localPosition = Vector3.Lerp(crabB.localPosition, desiredPosB, k);
                else crabA.localPosition = Vector3.Lerp(crabA.localPosition, desiredPosA, k);
            }
            else
            {
                crabA.localPosition = Vector3.Lerp(crabA.localPosition, desiredPosA, k);
                crabB.localPosition = Vector3.Lerp(crabB.localPosition, desiredPosB, k);
            }
            crabA.localRotation = Quaternion.Slerp(crabA.localRotation, desiredRotA, k);
            crabB.localRotation = Quaternion.Slerp(crabB.localRotation, desiredRotB, k);
        }

        private static Vector3 JitterOffset(float t, float seed)
        {
            // -0.5〜+0.5のPerlinノイズ2軸(水平と上下)
            float x = Mathf.PerlinNoise(t, seed) - 0.5f;
            float y = Mathf.PerlinNoise(seed, t) - 0.5f;
            return new Vector3(x, y * 0.5f, 0f);
        }
    }
}
