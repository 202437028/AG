namespace KaniTactics.Core
{
    /// <summary>下剋上マッチの定義。低い牌が有利になる特殊マッチアップ。</summary>
    public readonly struct GekokujoRule
    {
        public readonly int Low;   // 低い側の牌
        public readonly int High;  // 高い側の牌
        public readonly int Steps; // 低い側の有利段数

        public GekokujoRule(int low, int high, int steps)
        {
            Low = low;
            High = high;
            Steps = steps;
        }
    }

    /// <summary>
    /// ゲームバランスの全数値。UnityEngine非依存。
    /// Game層の RuleConfigAsset.ToConfig() から生成して各ロジックに注入する。
    /// 数値をコードにハードコードすることを禁止し、必ずここを経由する。
    /// </summary>
    public sealed class RuleConfig
    {
        /// <summary>ρ: 有利1段あたり、不利側に要求する連打増加率(0.08 = +8%)。暫定値、M5で確定。</summary>
        public float AdvantageStepRate = 0.08f;

        /// <summary>連打フェーズの秒数。</summary>
        public float MashSeconds = 10f;

        /// <summary>この数字差以内なら連打フェーズへ(超えたら即決着)。</summary>
        public int MashTriggerMaxDiff = 2;

        /// <summary>総ラウンド数。</summary>
        public int TotalRounds = 9;

        /// <summary>この勝利数に達したら早期終了。</summary>
        public int WinsToClinch = 5;

        /// <summary>牌選択の制限時間(秒)。時間切れは選択中の牌で確定。</summary>
        public float SelectTimeLimitSeconds = 30f;

        /// <summary>CPU難易度ごとの連打速度(連打/秒)。イージー/ノーマル/ハード/名人。</summary>
        public float[] CpuCps = { 5f, 7f, 9f, 16f };

        /// <summary>下剋上マッチ定義。デフォルト: 1vs9(2段) / 1vs8(1段) / 2vs9(1段)。</summary>
        public GekokujoRule[] Gekokujo =
        {
            new GekokujoRule(1, 9, 2),
            new GekokujoRule(1, 8, 1),
            new GekokujoRule(2, 9, 1),
        };
    }
}