using System;

namespace KaniTactics.Core
{
    public enum Player { A, B }

    /// <summary>牌公開後の判定結果。「即決着」か「連打突入」のどちらか。</summary>
    public readonly struct MatchupResult
    {
        /// <summary>連打なしの即決着か。</summary>
        public readonly bool IsImmediate;

        /// <summary>即決着時の勝者。IsImmediate == false のときは無効値。</summary>
        public readonly Player ImmediateWinner;

        /// <summary>連打時の有利側。同数(段数0)のときは null。</summary>
        public readonly Player? FavoredSide;

        /// <summary>有利段数(0 = 同数の等倍勝負)。</summary>
        public readonly int AdvantageSteps;

        /// <summary>不利側がこの倍率を「超えれば」逆転勝ち。同数時は 1.0。</summary>
        public readonly float RequiredRatio;

        private MatchupResult(bool isImmediate, Player immediateWinner,
                              Player? favoredSide, int steps, float ratio)
        {
            IsImmediate = isImmediate;
            ImmediateWinner = immediateWinner;
            FavoredSide = favoredSide;
            AdvantageSteps = steps;
            RequiredRatio = ratio;
        }

        public static MatchupResult Immediate(Player winner)
            => new MatchupResult(true, winner, null, 0, 1f);

        public static MatchupResult Mash(Player? favoredSide, int steps, float ratio)
            => new MatchupResult(false, default, favoredSide, steps, ratio);
    }

    /// <summary>公開された2枚の牌から勝敗判定の分岐を決める。</summary>
    public static class MatchupJudge
    {
        /// <summary>
        /// 判定順序: ①下剋上(最優先) ②同数 ③差がMashTriggerMaxDiff超なら即決着 ④それ以外は高い側有利の連打。
        /// </summary>
        public static MatchupResult Judge(int tileA, int tileB, RuleConfig config)
        {
            ValidateTile(tileA, nameof(tileA));
            ValidateTile(tileB, nameof(tileB));

            // ① 下剋上マッチ(通常判定より優先)
            foreach (var g in config.Gekokujo)
            {
                if (tileA == g.Low && tileB == g.High)
                    return MatchupResult.Mash(Player.A, g.Steps, RatioOf(g.Steps, config));
                if (tileB == g.Low && tileA == g.High)
                    return MatchupResult.Mash(Player.B, g.Steps, RatioOf(g.Steps, config));
            }

            int diff = tileA - tileB;

            // ② 同数 → 等倍の純連打
            if (diff == 0)
                return MatchupResult.Mash(null, 0, 1f);

            int absDiff = Math.Abs(diff);
            Player higher = diff > 0 ? Player.A : Player.B;

            // ③ 大差 → 即決着
            if (absDiff > config.MashTriggerMaxDiff)
                return MatchupResult.Immediate(higher);

            // ④ 接戦 → 高い側有利の連打
            return MatchupResult.Mash(higher, absDiff, RatioOf(absDiff, config));
        }

        private static float RatioOf(int steps, RuleConfig config)
            => (float)Math.Pow(1.0 + config.AdvantageStepRate, steps);

        private static void ValidateTile(int tile, string paramName)
        {
            if (tile < 1 || tile > 9)
                throw new ArgumentOutOfRangeException(paramName, tile, "牌は1〜9である必要があります。");
        }
    }
}
