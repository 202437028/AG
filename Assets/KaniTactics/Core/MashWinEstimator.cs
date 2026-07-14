using System;

namespace KaniTactics.Core
{
    /// <summary>
    /// 連打フェーズの勝率を推定する(AIのpayoff計算用)。
    /// モデル: プレイヤーの連打数は平均μ・変動係数cvの正規分布、CPUの連打数は決定値。
    /// </summary>
    public static class MashWinEstimator
    {
        /// <summary>プレイヤー(Player.A)が勝つ確率を返す。</summary>
        /// <param name="matchup">MatchupJudge.Judge(プレイヤー牌, CPU牌, config) の結果</param>
        /// <param name="playerMeanTaps">プレイヤーの平均連打数(推定CPS × 連打秒数)</param>
        /// <param name="playerCv">プレイヤー連打の変動係数(例: 0.15 = ±15%)</param>
        /// <param name="cpuTaps">CPUの連打数(実効CPS × 連打秒数)</param>
        public static float PlayerWinProb(in MatchupResult matchup,
                                          float playerMeanTaps, float playerCv, float cpuTaps)
        {
            if (matchup.IsImmediate)
                return matchup.ImmediateWinner == Player.A ? 1f : 0f;

            float sigma = Math.Max(playerCv * playerMeanTaps, 1e-3f);

            // プレイヤーが勝つための必要連打数(MashResolverの規則と対応)
            float threshold;
            if (matchup.FavoredSide == Player.A)
                threshold = cpuTaps / matchup.RequiredRatio;  // 有利側: CPUに要求倍率を超えられなければ勝ち
            else if (matchup.FavoredSide == Player.B)
                threshold = cpuTaps * matchup.RequiredRatio;  // 不利側: 要求倍率を超えれば勝ち
            else
                threshold = cpuTaps;                          // 同数: 上回れば勝ち

            return 1f - NormalCdf((threshold - playerMeanTaps) / sigma);
        }

        /// <summary>標準正規分布の累積分布関数(Abramowitz-Stegun 7.1.26 近似)。</summary>
        public static float NormalCdf(float z)
        {
            double az = Math.Abs(z);
            double t = 1.0 / (1.0 + 0.2316419 * az);
            double d = 0.3989422804 * Math.Exp(-az * az / 2.0);
            double p = d * t * (0.319381530 + t * (-0.356563782 +
                       t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return (float)(z >= 0 ? 1.0 - p : p);
        }
    }
}
