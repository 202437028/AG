using System;
using System.Collections.Generic;
using System.Linq;

namespace KaniTactics.Core
{
    /// <summary>
    /// 1試合の状態(両者の残り手札・勝利数・ラウンド進行)。
    /// フェーズ進行やUIはGame層の責務で、ここは状態と遷移ルールだけを持つ。
    /// </summary>
    public sealed class MatchState
    {
        private readonly RuleConfig _config;
        private readonly HashSet<int> _handA;
        private readonly HashSet<int> _handB;

        public int WinsA { get; private set; }
        public int WinsB { get; private set; }

        /// <summary>現在のラウンド番号(1始まり)。</summary>
        public int RoundNumber => WinsA + WinsB + 1;

        public IReadOnlyCollection<int> HandA => _handA;
        public IReadOnlyCollection<int> HandB => _handB;

        public MatchState(RuleConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _handA = new HashSet<int>(Enumerable.Range(1, 9));
            _handB = new HashSet<int>(Enumerable.Range(1, 9));
        }

        public bool HasTile(Player player, int tile)
            => (player == Player.A ? _handA : _handB).Contains(tile);

        /// <summary>両者の出した牌を手札から消費する。手札にない牌は例外。</summary>
        public void ConsumeTiles(int tileA, int tileB)
        {
            if (!_handA.Remove(tileA))
                throw new InvalidOperationException($"プレイヤーAの手札に {tileA} がありません。");
            if (!_handB.Remove(tileB))
                throw new InvalidOperationException($"プレイヤーBの手札に {tileB} がありません。");
        }

        /// <summary>ラウンドの勝者に1勝を加算する。</summary>
        public void AwardWin(Player winner)
        {
            if (IsOver)
                throw new InvalidOperationException("試合は既に終了しています。");

            if (winner == Player.A) WinsA++;
            else WinsB++;
        }

        /// <summary>規定勝利数への到達、または全ラウンド消化で試合終了。</summary>
        public bool IsOver =>
            WinsA >= _config.WinsToClinch ||
            WinsB >= _config.WinsToClinch ||
            WinsA + WinsB >= _config.TotalRounds;

        /// <summary>試合の勝者。未終了または同勝利数(偶数ラウンド設定時のみ起こり得る)は null。</summary>
        public Player? MatchWinner
        {
            get
            {
                if (!IsOver) return null;
                if (WinsA == WinsB) return null;
                return WinsA > WinsB ? Player.A : Player.B;
            }
        }
    }
}
