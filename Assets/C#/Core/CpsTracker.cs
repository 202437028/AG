using System.Collections.Generic;
using System.Linq;

namespace KaniTactics.Core
{
    /// AIのペイオフ推定に使うためにプレイヤーの連打速度(CPS)の移動平均を測定。
    /// 試合が進んでプレイヤーが疲労すれば推定値もそれに追従する。
    public sealed class CpsTracker
    {
        private readonly float _priorCps;
        private readonly int _windowSize;
        private readonly Queue<float> _samples = new Queue<float>();

        /// priorCps:計測データがないときの事前推定値
        /// windowSize:移動平均のウィンドウ幅(連打回数)
        public CpsTracker(float priorCps, int windowSize = 4)
        {
            _priorCps = priorCps;
            _windowSize = windowSize;
        }

        /// 連打フェーズ1回分のCPSを記録する。
        public void Record(float cps)
        {
            _samples.Enqueue(cps);
            while (_samples.Count > _windowSize)
                _samples.Dequeue();
        }

        /// 現在の推定CPS。未計測なら事前値。
        public float Estimate => _samples.Count == 0 ? _priorCps : _samples.Average();
    }
}
