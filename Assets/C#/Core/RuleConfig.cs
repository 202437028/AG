namespace KaniTactics.Core
{
    /// 下剋上マッチの定義。低い牌が有利になる特殊マッチアップ。
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

    /// ゲームバランスの全数値。UnityEngine非依存。
    /// Game層の RuleConfigAsset.ToConfig() から生成して各ロジックに注入する。
    /// 数値をコードにハードコードすることを禁止し、必ずここを経由する。
    public sealed class RuleConfig
    {
        /// ρ: 有利1段あたり、不利側に要求する連打増加率(0.08 = +8%)。実測CVを踏まえ0.12〜0.15へ引き上げ検討中。
        public float AdvantageStepRate = 0.08f;

        /// 連打フェーズの秒数。
        public float MashSeconds = 10f;

        /// この数字差以内なら連打フェーズへ(超えたら即決着)。
        public int MashTriggerMaxDiff = 2;

        /// 総ラウンド数。
        public int TotalRounds = 9;

        /// この勝利数に達したら早期終了。
        public int WinsToClinch = 5;

        /// 牌選択の制限時間(秒)。時間切れは選択中の牌で確定。
        public float SelectTimeLimitSeconds = 30f;

        /// CPU難易度ごとの基準連打速度(連打/秒)。イージー/ノーマル/ハード/名人。
        public float[] CpuCps = { 5f, 7f, 9f, 16f };

        /// CPU疲労: 試合内の連打フェーズ1回ごとにCPSが減衰する率(0.05 = 5%減)。
        public float CpuFatigueRatePerMash = 0.05f;

        /// 難易度ごとの均衡手の採用率(残りは完全ランダム)。イージー/ノーマル/ハード/名人。
        public float[] CpuEquilibriumRate = { 0f, 0.5f, 0.85f, 1f };

        /// プレイヤーCPSの事前推定値(計測データが貯まる前にAIが使う)。
        public float PlayerCpsPrior = 8f;

        /// AIがpayoff推定に使うプレイヤー連打の変動係数(実測 約15%)。
        public float AiPlayerCpsCv = 0.15f;

        /// 虚構遊びの反復回数(均衡ソルバーの精度)。
        public int FictitiousPlayIterations = 600;

        /// 下剋上マッチ定義。デフォルト: 1vs9(2段) / 1vs8(1段) / 2vs9(1段)。
        public GekokujoRule[] Gekokujo =
        {
            new GekokujoRule(1, 9, 2),
            new GekokujoRule(1, 8, 1),
            new GekokujoRule(2, 9, 1),
        };
    }
}
