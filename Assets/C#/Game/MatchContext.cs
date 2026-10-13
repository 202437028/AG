using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 試合中にフェーズ間で共有するデータ(共有のメモ帳)。
    /// 試合ごとに1つ生成し、再戦時は作り直す。
    /// </summary>
    public sealed class MatchContext
    {
        // ---- 試合全体で変わらないもの ----
        public GameFlowController.GameMode Mode { get; }
        public bool IsVersus => Mode == GameFlowController.GameMode.LocalVersus;
        public RuleConfig Config { get; }
        public MatchState State { get; }

        // ---- CPU(SoloCpuのみ。LocalVersusではnull) ----
        public CpuBrain Brain { get; }
        public CpsTracker CpsTracker { get; }
        /// <summary>この試合で行った連打の回数。CPU疲労の段数に使う</summary>
        public int MashCount { get; set; }

        // ---- ラウンドごとに書き換わるもの ----
        /// <summary>HUDに出すラウンド番号</summary>
        public int DisplayRound { get; set; }
        /// <summary>いま牌を選んでいる側(LocalVersusで交互になる)</summary>
        public Player Selecting { get; set; }
        public int TileA { get; set; }
        public int TileB { get; set; }
        public MatchupResult Matchup { get; set; }
        public Player? RoundWinner { get; set; }

        public MatchContext(RuleConfig config, GameFlowController.GameMode mode, int cpuDifficulty)
        {
            Config = config;
            Mode = mode;
            State = new MatchState(config);
            MashCount = 0;

            if (!IsVersus)
            {
                Brain = new CpuBrain(config, cpuDifficulty, new System.Random());
                CpsTracker = new CpsTracker(config.PlayerCpsPrior);
            }
        }
    }
}
