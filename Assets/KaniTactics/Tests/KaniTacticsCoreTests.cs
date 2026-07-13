using System;
using NUnit.Framework;
using KaniTactics.Core;

namespace KaniTactics.Tests
{
    public class MatchupJudgeTests
    {
        private static RuleConfig DefaultConfig() => new RuleConfig(); // ρ=0.08, 差2以内で連打

        // ---- 下剋上マッチ ----

        [Test]
        public void 下剋上_1vs9_は1側が2段有利の連打になる()
        {
            var r = MatchupJudge.Judge(1, 9, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.AreEqual(Player.A, r.FavoredSide);
            Assert.AreEqual(2, r.AdvantageSteps);
            Assert.AreEqual(Math.Pow(1.08, 2), r.RequiredRatio, 1e-5);
        }

        [Test]
        public void 下剋上_9vs1_でも1を持つ側が有利になる()
        {
            var r = MatchupJudge.Judge(9, 1, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.AreEqual(Player.B, r.FavoredSide);
            Assert.AreEqual(2, r.AdvantageSteps);
        }

        [TestCase(1, 8)]
        [TestCase(2, 9)]
        public void 下剋上_1vs8と2vs9_は低い側が1段有利(int low, int high)
        {
            var r = MatchupJudge.Judge(low, high, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.AreEqual(Player.A, r.FavoredSide);
            Assert.AreEqual(1, r.AdvantageSteps);
            Assert.AreEqual(1.08f, r.RequiredRatio, 1e-5);
        }

        [Test]
        public void 下剋上でない大差_1vs5_は即決着で高い側が勝つ()
        {
            var r = MatchupJudge.Judge(1, 5, DefaultConfig());

            Assert.IsTrue(r.IsImmediate);
            Assert.AreEqual(Player.B, r.ImmediateWinner);
        }

        // ---- 通常判定と境界値 ----

        [Test]
        public void 差3は即決着になる()
        {
            var r = MatchupJudge.Judge(6, 3, DefaultConfig());

            Assert.IsTrue(r.IsImmediate);
            Assert.AreEqual(Player.A, r.ImmediateWinner);
        }

        [Test]
        public void 差2は高い側が2段有利の連打になる()
        {
            var r = MatchupJudge.Judge(4, 6, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.AreEqual(Player.B, r.FavoredSide);
            Assert.AreEqual(2, r.AdvantageSteps);
            Assert.AreEqual(Math.Pow(1.08, 2), r.RequiredRatio, 1e-5);
        }

        [Test]
        public void 差1は高い側が1段有利の連打になる()
        {
            var r = MatchupJudge.Judge(7, 6, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.AreEqual(Player.A, r.FavoredSide);
            Assert.AreEqual(1, r.AdvantageSteps);
        }

        [Test]
        public void 同数は有利なし等倍の連打になる()
        {
            var r = MatchupJudge.Judge(5, 5, DefaultConfig());

            Assert.IsFalse(r.IsImmediate);
            Assert.IsNull(r.FavoredSide);
            Assert.AreEqual(0, r.AdvantageSteps);
            Assert.AreEqual(1f, r.RequiredRatio, 1e-6);
        }

        [TestCase(0, 5)]
        [TestCase(10, 5)]
        [TestCase(5, 0)]
        public void 範囲外の牌は例外(int a, int b)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MatchupJudge.Judge(a, b, DefaultConfig()));
        }
    }

    public class MashResolverTests
    {
        private static RuleConfig DefaultConfig() => new RuleConfig();

        [Test]
        public void 不利側が要求倍率を超えれば逆転勝ち()
        {
            // A=9, B=8: Aが1段有利(要求1.08倍)。A=100打なら閾値108打
            var m = MatchupJudge.Judge(9, 8, DefaultConfig());

            Assert.AreEqual(Player.B, MashResolver.Resolve(100, 109, m));
        }

        [Test]
        public void 不利側が閾値ちょうどなら有利側の防衛勝ち()
        {
            var m = MatchupJudge.Judge(9, 8, DefaultConfig());

            Assert.AreEqual(Player.A, MashResolver.Resolve(100, 108, m));
        }

        [Test]
        public void 不利側が閾値未満なら有利側の勝ち()
        {
            var m = MatchupJudge.Judge(9, 8, DefaultConfig());

            Assert.AreEqual(Player.A, MashResolver.Resolve(100, 107, m));
        }

        [Test]
        public void 下剋上では低い側が有利として扱われる()
        {
            // A=1, B=9: Aが2段有利。B(不利側)は A×1.08^2 を超える必要がある
            var m = MatchupJudge.Judge(1, 9, DefaultConfig());

            Assert.AreEqual(Player.A, MashResolver.Resolve(100, 116, m)); // 閾値116.64 → 防衛
            Assert.AreEqual(Player.B, MashResolver.Resolve(100, 117, m)); // 超えたので逆転
        }

        [Test]
        public void 同数マッチは多い方が勝つ()
        {
            var m = MatchupJudge.Judge(5, 5, DefaultConfig());

            Assert.AreEqual(Player.A, MashResolver.Resolve(101, 100, m));
            Assert.AreEqual(Player.B, MashResolver.Resolve(100, 101, m));
        }

        [Test]
        public void 同数マッチの完全同点はnull_再連打()
        {
            var m = MatchupJudge.Judge(5, 5, DefaultConfig());

            Assert.IsNull(MashResolver.Resolve(100, 100, m));
        }
    }

    public class TapCounterTests
    {
        [Test]
        public void 交互の打鍵はすべてカウントされる()
        {
            var c = new TapCounter();
            c.RegisterTap(0); // A
            c.RegisterTap(1); // D
            c.RegisterTap(0); // A

            Assert.AreEqual(3, c.Count);
        }

        [Test]
        public void 同一キーの連続はカウントされない()
        {
            var c = new TapCounter();
            Assert.IsTrue(c.RegisterTap(0));  // A → 有効
            Assert.IsFalse(c.RegisterTap(0)); // A → 無効
            Assert.IsTrue(c.RegisterTap(1));  // D → 有効

            Assert.AreEqual(2, c.Count);
        }

        [Test]
        public void リセット後は同じキーから再開できる()
        {
            var c = new TapCounter();
            c.RegisterTap(0);
            c.Reset();

            Assert.AreEqual(0, c.Count);
            Assert.IsTrue(c.RegisterTap(0));
            Assert.AreEqual(1, c.Count);
        }
    }

    public class MatchStateTests
    {
        private static RuleConfig DefaultConfig() => new RuleConfig(); // 9ラウンド・5勝先取

        [Test]
        public void 初期状態は両者9枚ずつ持つ()
        {
            var s = new MatchState(DefaultConfig());

            Assert.AreEqual(9, s.HandA.Count);
            Assert.AreEqual(9, s.HandB.Count);
            Assert.AreEqual(1, s.RoundNumber);
        }

        [Test]
        public void 消費した牌は手札から消える()
        {
            var s = new MatchState(DefaultConfig());
            s.ConsumeTiles(3, 7);

            Assert.IsFalse(s.HasTile(Player.A, 3));
            Assert.IsFalse(s.HasTile(Player.B, 7));
            Assert.IsTrue(s.HasTile(Player.A, 7)); // Aの7は残っている
        }

        [Test]
        public void 手札にない牌の消費は例外()
        {
            var s = new MatchState(DefaultConfig());
            s.ConsumeTiles(3, 7);

            Assert.Throws<InvalidOperationException>(() => s.ConsumeTiles(3, 1));
        }

        [Test]
        public void 五勝で早期終了し勝者が確定する()
        {
            var s = new MatchState(DefaultConfig());
            for (int i = 0; i < 5; i++) s.AwardWin(Player.A);

            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(Player.A, s.MatchWinner);
        }

        [Test]
        public void 終了後の勝利加算は例外()
        {
            var s = new MatchState(DefaultConfig());
            for (int i = 0; i < 5; i++) s.AwardWin(Player.B);

            Assert.Throws<InvalidOperationException>(() => s.AwardWin(Player.A));
        }

        [Test]
        public void ラウンド番号は勝敗の合計で進む()
        {
            var s = new MatchState(DefaultConfig());
            s.AwardWin(Player.A);
            s.AwardWin(Player.B);

            Assert.AreEqual(3, s.RoundNumber);
        }
    }
}
