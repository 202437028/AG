using System.Collections.Generic;
using System.Linq;

namespace KaniTactics.Core
{
    /// <summary>
    /// プレイヤーの連打速度(CPS)の移動平均。AIのpayoff推定に使う。
    /// 直近ウィンドウの平均なので、試合が進んでプレイヤーが疲労すれば推定値も自然に追従する。
    /// </summary>
    public sealed class CpsTracker
    {
        private readonly float _priorCps;
        private readonly int _windowSize;
        private readonly Queue<float> _samples = new Queue<float>();

        /// <param name="priorCps">計測データがないときの事前推定値(例: 8)</param>
        /// <param name="windowSize">移動平均のウィンドウ幅(連打回数)</param>
        public CpsTracker(float priorCps, int windowSize = 4)
        {
            _priorCps = priorCps;
            _windowSize = windowSize;
        }

        /// <summary>連打フェーズ1回分のCPSを記録する。</summary>
        public void Record(float cps)
        {
            _samples.Enqueue(cps);
            while (_samples.Count > _windowSize)
                _samples.Dequeue();
        }

        /// <summary>現在の推定CPS。未計測なら事前値。</summary>
        public float Estimate => _samples.Count == 0 ? _priorCps : _samples.Average();
    }
}
