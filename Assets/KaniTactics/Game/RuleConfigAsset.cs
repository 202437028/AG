using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// RuleConfig のインスペクタ編集用ラッパー。
    /// Project右クリック → Create → KaniTactics → RuleConfig でアセットを作成し、
    /// 使用側は ToConfig() で Core 用の RuleConfig に変換して渡す。
    /// </summary>
    [CreateAssetMenu(fileName = "RuleConfig", menuName = "KaniTactics/RuleConfig")]
    public sealed class RuleConfigAsset : ScriptableObject
    {
        [System.Serializable]
        public struct GekokujoEntry
        {
            public int low;   // 低い側の牌
            public int high;  // 高い側の牌
            public int steps; // 低い側の有利段数
        }

        [Header("連打バランス")]
        [Tooltip("ρ: 有利1段あたり不利側に要求する連打増加率(0.08 = +8%)")]
        [SerializeField] private float advantageStepRate = 0.08f;
        [SerializeField] private float mashSeconds = 10f;
        [Tooltip("この数字差以内なら連打フェーズ(超えたら即決着)")]
        [SerializeField] private int mashTriggerMaxDiff = 2;

        [Header("試合構成")]
        [SerializeField] private int totalRounds = 9;
        [SerializeField] private int winsToClinch = 5;
        [SerializeField] private float selectTimeLimitSeconds = 30f;

        [Header("CPU難易度(連打/秒) — イージー/ノーマル/ハード/名人")]
        [SerializeField] private float[] cpuCps = { 5f, 7f, 9f, 16f };

        [Header("CPU AI")]
        [Tooltip("連打フェーズ1回ごとのCPU側CPS減衰率(疲労モデル)")]
        [SerializeField, Range(0f, 0.3f)] private float cpuFatigueRatePerMash = 0.05f;
        [Tooltip("難易度ごとの均衡手の採用率(残りは完全ランダム)")]
        [SerializeField] private float[] cpuEquilibriumRate = { 0f, 0.5f, 0.85f, 1f };
        [Tooltip("プレイヤーCPSの事前推定値(計測前にAIが使う)")]
        [SerializeField] private float playerCpsPrior = 8f;
        [Tooltip("AIが想定するプレイヤー連打の変動係数(実測 約15%)")]
        [SerializeField] private float aiPlayerCpsCv = 0.15f;
        [Tooltip("均衡ソルバー(虚構遊び)の反復回数")]
        [SerializeField] private int fictitiousPlayIterations = 600;

        [Header("下剋上マッチ定義")]
        [SerializeField] private GekokujoEntry[] gekokujo =
        {
            new GekokujoEntry { low = 1, high = 9, steps = 2 },
            new GekokujoEntry { low = 1, high = 8, steps = 1 },
            new GekokujoEntry { low = 2, high = 9, steps = 1 },
        };

        /// <summary>Core用の設定オブジェクトに変換する。</summary>
        public RuleConfig ToConfig()
        {
            var rules = new GekokujoRule[gekokujo.Length];
            for (int i = 0; i < gekokujo.Length; i++)
            {
                rules[i] = new GekokujoRule(gekokujo[i].low, gekokujo[i].high, gekokujo[i].steps);
            }

            return new RuleConfig
            {
                AdvantageStepRate = advantageStepRate,
                MashSeconds = mashSeconds,
                MashTriggerMaxDiff = mashTriggerMaxDiff,
                TotalRounds = totalRounds,
                WinsToClinch = winsToClinch,
                SelectTimeLimitSeconds = selectTimeLimitSeconds,
                CpuCps = cpuCps,
                CpuFatigueRatePerMash = cpuFatigueRatePerMash,
                CpuEquilibriumRate = cpuEquilibriumRate,
                PlayerCpsPrior = playerCpsPrior,
                AiPlayerCpsCv = aiPlayerCpsCv,
                FictitiousPlayIterations = fictitiousPlayIterations,
                Gekokujo = rules,
            };
        }
    }
}
