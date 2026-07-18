using System;
using System.Linq;

namespace KaniTactics.Core
{
    /// <summary>
    /// CPU(Player.B)の手選択AI。
    /// ・残り手札同士の勝率行列を作り、混合戦略均衡を解いて抽選する
    /// ・難易度ごとの「均衡手の採用率」で完全ランダムと混合(ハンディ)
    /// ・CPUの連打力は疲労モデルで減衰: 実効CPS = 基準CPS × (1 - 疲労率)^試合内連打回数
    /// </summary>
    public sealed class CpuBrain
    {
        private readonly RuleConfig _config;
        private readonly int _difficulty;
        private readonly Random _rng;

        public CpuBrain(RuleConfig config, int difficulty, Random rng)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (difficulty < 0 || difficulty >= config.CpuCps.Length)
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            _difficulty = difficulty;
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>疲労を織り込んだ現在の実効CPS。連打を重ねるほど減衰する。</summary>
        public float EffectiveCps(int mashCountSoFar)
            => _config.CpuCps[_difficulty]
               * (float)Math.Pow(1.0 - _config.CpuFatigueRatePerMash, mashCountSoFar);

        /// <summary>この試合状況でCPUが出す牌を決める。</summary>
        /// <param name="state">現在の試合状態(両者の残り手札)</param>
        /// <param name="mashCountSoFar">この試合で発生済みの連打フェーズ回数(疲労計算用)</param>
        /// <param name="playerCpsEstimate">プレイヤーの推定CPS(CpsTracker.Estimate)</param>
        public int SelectTile(MatchState state, int mashCountSoFar, float playerCpsEstimate)
        {
            int[] cpuHand = state.HandB.OrderBy(t => t).ToArray();
            if (cpuHand.Length == 1)
                return cpuHand[0];

            // 難易度ハンディ: 均衡手を使わない試行は完全ランダム
            if (_rng.NextDouble() >= _config.CpuEquilibriumRate[_difficulty])
                return cpuHand[_rng.Next(cpuHand.Length)];

            int[] playerHand = state.HandA.OrderBy(t => t).ToArray();
            float cpuTaps = EffectiveCps(mashCountSoFar) * _config.MashSeconds;
            float playerMeanTaps = playerCpsEstimate * _config.MashSeconds;

            // payoff[i, j] = CPUが牌iを、プレイヤーが牌jを出したときのCPUの勝率
            var payoff = new float[cpuHand.Length, playerHand.Length];
            for (int i = 0; i < cpuHand.Length; i++)
            {
                for (int j = 0; j < playerHand.Length; j++)
                {
                    var matchup = MatchupJudge.Judge(playerHand[j], cpuHand[i], _config); // A=プレイヤー視点
                    payoff[i, j] = 1f - MashWinEstimator.PlayerWinProb(
                        matchup, playerMeanTaps, _config.AiPlayerCpsCv, cpuTaps);
                }
            }

            float[] mix = EquilibriumSolver.SolveRowStrategy(payoff, _config.FictitiousPlayIterations);
            return cpuHand[SampleIndex(mix)];
        }

        private int SampleIndex(float[] mix)
        {
            double r = _rng.NextDouble();
            double acc = 0;
            for (int i = 0; i < mix.Length; i++)
            {
                acc += mix[i];
                if (r < acc) return i;
            }
            return mix.Length - 1; // 浮動小数の端数対策
        }
    }
}
