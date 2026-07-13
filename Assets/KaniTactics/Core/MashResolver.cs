namespace KaniTactics.Core
{
    /// <summary>連打フェーズの結果から勝者を決める。</summary>
    public static class MashResolver
    {
        /// <summary>
        /// 勝敗ルール:
        /// ・有利側あり: 不利側の連打数が「有利側の連打数 × RequiredRatio」を厳密に超えれば逆転勝ち。
        ///   同値以下は有利側の防衛勝ち(同点は有利側が守り切る)。
        /// ・同数(有利なし): 連打数が多い方の勝ち。完全同数は null を返す(引き分け → 呼び出し側で再連打)。
        /// </summary>
        public static Player? Resolve(int tapsA, int tapsB, in MatchupResult matchup)
        {
            // 即決着の結果が渡された場合は安全側でそのまま返す(通常は呼ばれない想定)
            if (matchup.IsImmediate)
                return matchup.ImmediateWinner;

            // 同数: 等倍勝負
            if (matchup.FavoredSide == null)
            {
                if (tapsA == tapsB) return null;
                return tapsA > tapsB ? Player.A : Player.B;
            }

            Player favored = matchup.FavoredSide.Value;
            Player unfavored = favored == Player.A ? Player.B : Player.A;
            int tapsFavored = favored == Player.A ? tapsA : tapsB;
            int tapsUnfavored = favored == Player.A ? tapsB : tapsA;

            return tapsUnfavored > tapsFavored * matchup.RequiredRatio
                ? unfavored
                : favored;
        }
    }
}
