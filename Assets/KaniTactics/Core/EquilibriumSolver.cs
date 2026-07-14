namespace KaniTactics.Core
{
    /// <summary>
    /// 2人ゼロサムゲームの混合戦略ナッシュ均衡を虚構遊び(fictitious play)で近似する。
    /// 各反復で「相手の過去の手の経験分布」への最適反応を選び続けると、
    /// 選択回数の割合が均衡ミックスに収束する(ゼロサムゲームでの収束は既知の結果)。
    /// </summary>
    public static class EquilibriumSolver
    {
        /// <summary>
        /// 行側(最大化プレイヤー)の混合戦略を返す。
        /// payoff[i, j] = 行側が手iを、列側が手jを出したときの行側の勝率。
        /// </summary>
        public static float[] SolveRowStrategy(float[,] payoff, int iterations = 600)
        {
            int rows = payoff.GetLength(0);
            int cols = payoff.GetLength(1);

            var rowCount = new int[rows];
            var colCount = new int[cols];
            rowCount[0]++;
            colCount[0]++;

            for (int it = 1; it < iterations; it++)
            {
                // 行側: 列の経験分布に対する期待勝率が最大の手
                int bestRow = 0;
                double bestRowValue = double.MinValue;
                for (int i = 0; i < rows; i++)
                {
                    double v = 0;
                    for (int j = 0; j < cols; j++) v += payoff[i, j] * colCount[j];
                    if (v > bestRowValue) { bestRowValue = v; bestRow = i; }
                }

                // 列側: 行の経験分布に対する行側勝率が最小の手
                int bestCol = 0;
                double bestColValue = double.MaxValue;
                for (int j = 0; j < cols; j++)
                {
                    double v = 0;
                    for (int i = 0; i < rows; i++) v += payoff[i, j] * rowCount[i];
                    if (v < bestColValue) { bestColValue = v; bestCol = j; }
                }

                rowCount[bestRow]++;
                colCount[bestCol]++;
            }

            var mix = new float[rows];
            for (int i = 0; i < rows; i++)
                mix[i] = (float)rowCount[i] / iterations;
            return mix;
        }
    }
}
