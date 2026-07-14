using System;
using NUnit.Framework;
using KaniTactics.Core;

namespace KaniTactics.Tests
{
    public class CpuFatigueTests
    {
        [Test]
        public void 実効CPSは連打回数に応じて指数減衰する()
        {
            var config = new RuleConfig { CpuCps = new[] { 10f }, CpuFatigueRatePerMash = 0.1f,
                                          CpuEquilibriumRate = new[] { 1f } };
            var brain = new CpuBrain(config, 0, new Random(1));

            Assert.AreEqual(10f, brain.EffectiveCps(0), 1e-4);
            Assert.AreEqual(9f, brain.EffectiveCps(1), 1e-4);
            Assert.AreEqual(8.1f, brain.EffectiveCps(2), 1e-4);
        }

        [Test]
        public void 疲労率0なら減衰しない()
        {
            var config = new RuleConfig { CpuCps = new[] { 16f }, CpuFatigueRatePerMash = 0f,
                                          CpuEquilibriumRate = new[] { 1f } };
            var brain = new CpuBrain(config, 0, new Random(1));

            Assert.AreEqual(16f, brain.EffectiveCps(10), 1e-4);
        }
    }

    public class CpsTrackerTests
    {
        [Test]
        public void 計測前は事前値を返す()
        {
            var t = new CpsTracker(priorCps: 8f);
            Assert.AreEqual(8f, t.Estimate, 1e-4);
        }

        [Test]
        public void 記録後は移動平均を返す()
        {
            var t = new CpsTracker(8f, windowSize: 4);
            t.Record(10f);
            t.Record(14f);
            Assert.AreEqual(12f, t.Estimate, 1e-4);
        }

        [Test]
        public void ウィンドウを超えた古い記録は落ちる()
        {
            var t = new CpsTracker(8f, windowSize: 2);
            t.Record(100f); // 落ちる
            t.Record(10f);
            t.Record(14f);
            Assert.AreEqual(12f, t.Estimate, 1e-4);
        }
    }

    public class MashWinEstimatorTests
    {
        private static RuleConfig Config() => new RuleConfig();

        [Test]
        public void 正規分布CDFの中心は0_5()
        {
            Assert.AreEqual(0.5f, MashWinEstimator.NormalCdf(0f), 1e-3);
        }

        [Test]
        public void 同数マッチで平均連打がCPUと同じなら勝率は約50パーセント()
        {
            var m = MatchupJudge.Judge(5, 5, Config());
            float p = MashWinEstimator.PlayerWinProb(m, playerMeanTaps: 100f, playerCv: 0.15f, cpuTaps: 100f);
            Assert.AreEqual(0.5f, p, 1e-3);
        }

        [Test]
        public void 平均連打が高いほど勝率は単調に上がる()
        {
            var m = MatchupJudge.Judge(5, 5, Config());
            float p1 = MashWinEstimator.PlayerWinProb(m, 90f, 0.15f, 100f);
            float p2 = MashWinEstimator.PlayerWinProb(m, 100f, 0.15f, 100f);
            float p3 = MashWinEstimator.PlayerWinProb(m, 110f, 0.15f, 100f);
            Assert.Less(p1, p2);
            Assert.Less(p2, p3);
        }

        [Test]
        public void プレイヤー有利側なら同連打力でも勝率は50パーセント超()
        {
            var m = MatchupJudge.Judge(9, 8, Config()); // A(プレイヤー)が1段有利
            float p = MashWinEstimator.PlayerWinProb(m, 100f, 0.15f, 100f);
            Assert.Greater(p, 0.5f);
        }

        [Test]
        public void 即決着は0か1を返す()
        {
            var win = MatchupJudge.Judge(9, 3, Config());
            var lose = MatchupJudge.Judge(3, 9, Config());
            Assert.AreEqual(1f, MashWinEstimator.PlayerWinProb(win, 100f, 0.15f, 100f), 1e-6);
            Assert.AreEqual(0f, MashWinEstimator.PlayerWinProb(lose, 100f, 0.15f, 100f), 1e-6);
        }
    }

    public class EquilibriumSolverTests
    {
        [Test]
        public void 混合戦略の合計は1になる()
        {
            var payoff = new float[,] { { 0.5f, 0.7f }, { 0.3f, 0.5f } };
            var mix = EquilibriumSolver.SolveRowStrategy(payoff);

            float sum = 0;
            foreach (var p in mix) sum += p;
            Assert.AreEqual(1f, sum, 1e-3);
        }

        [Test]
        public void 支配戦略にはほぼ全ての重みが乗る()
        {
            // 行0が行1を厳密支配するpayoff
            var payoff = new float[,] { { 0.9f, 0.8f }, { 0.2f, 0.1f } };
            var mix = EquilibriumSolver.SolveRowStrategy(payoff);

            Assert.Greater(mix[0], 0.95f);
        }

        [Test]
        public void ジャンケン型ゲームはほぼ均等ミックスになる()
        {
            // 3すくみ(勝ち0.9/負け0.1/あいこ0.5)
            var payoff = new float[,]
            {
                { 0.5f, 0.9f, 0.1f },
                { 0.1f, 0.5f, 0.9f },
                { 0.9f, 0.1f, 0.5f },
            };
            var mix = EquilibriumSolver.SolveRowStrategy(payoff, iterations: 3000);

            foreach (var p in mix)
                Assert.AreEqual(1f / 3f, p, 0.06f);
        }
    }

    public class CpuBrainTests
    {
        private static RuleConfig Config() => new RuleConfig();

        [Test]
        public void 残り1枚ならその牌を出す()
        {
            var config = Config();
            var state = new MatchState(config);
            // 双方8枚消費して1枚ずつ残す
            int[] a = { 1, 2, 3, 4, 5, 6, 7, 8 };
            int[] b = { 2, 3, 4, 5, 6, 7, 8, 9 };
            for (int i = 0; i < 8; i++) state.ConsumeTiles(a[i], b[i]);

            var brain = new CpuBrain(config, 3, new Random(1)); // 名人=均衡100%
            Assert.AreEqual(1, brain.SelectTile(state, 0, 8f));
        }

        [Test]
        public void 選ばれる牌は必ずCPUの手札に含まれる()
        {
            var config = Config();
            var brain = new CpuBrain(config, 3, new Random(42));
            var state = new MatchState(config);
            state.ConsumeTiles(5, 5); // 双方5を消費

            for (int i = 0; i < 20; i++)
            {
                int tile = brain.SelectTile(state, 0, 12f);
                Assert.IsTrue(state.HasTile(Player.B, tile), $"手札にない牌 {tile} が選ばれた");
            }
        }

        [Test]
        public void 難易度が範囲外なら例外()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CpuBrain(Config(), 4, new Random(1)));
        }
    }
}
