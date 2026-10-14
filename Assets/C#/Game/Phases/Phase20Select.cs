using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 牌の選択。カーソル移動・数字キー直接指定・Enter確定・制限時間切れ・ポーズを扱う。
    /// ローカル2Pでは 1P → 暗転 → 2P の順に選び、ソロではCPUが同時に選ぶ。
    /// 確定したら勝敗の組み合わせを判定して公開へ進む。
    /// </summary>
    public sealed class Phase20Select : IMatchPhase
    {
        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action<PhaseId> _changePhase;

        private List<int> _handSorted;
        private int _cursor;
        private float _timer;
        private bool _paused;
        private bool _switchingPlayer; // ローカル2Pの手番交代フェード中

        public Phase20Select(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _changePhase = changePhase;
        }

        public HudMode HudMode => HudMode.Select;
        public float HudTimer => _timer;
        public int? CursorTile =>
            !_paused && _handSorted != null && _handSorted.Count > 0
                ? _handSorted[_cursor]
                : (int?)null;

        public void Enter()
        {
            _ctx.DisplayRound = _ctx.State.RoundNumber;
            BeginSelection(Player.A);
        }

        public void Tick(float deltaTime)
        {
            // ポーズの開閉(選択フェーズ中のみ。連打中は「連打逃げ」防止のためPauseマップ自体が無効)
            if (_input.PausePressed)
            {
                SetPaused(!_paused);
                return;
            }
            if (_paused || _switchingPlayer) return; // ポーズ中・手番交代フェード中はタイマーも入力も止まる

            _timer -= deltaTime;

            // 数字キー直接指定(手札にある牌のみ有効)
            for (int d = 1; d <= 9; d++)
            {
                if (_input.DigitPressed(d) && _handSorted.Contains(d))
                {
                    _cursor = _handSorted.IndexOf(d);
                    Confirm();
                    return;
                }
            }

            if (_input.MoveLeftPressed)
                _cursor = (_cursor - 1 + _handSorted.Count) % _handSorted.Count;
            if (_input.MoveRightPressed)
                _cursor = (_cursor + 1) % _handSorted.Count;

            // Enter確定、または時間切れ=選択中の牌で確定
            if (_input.ConfirmPressed || _timer <= 0f)
                Confirm();
        }

        public void Exit() { }

        /// <summary>ポーズ状態の切替。ポーズメニューのボタンからもGameFlowController経由で呼ばれる。</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (_refs.PausePanel != null) _refs.PausePanel.SetActive(paused);
            if (paused && _refs.PauseStatus != null)
            {
                _refs.PauseStatus.Render(
                    $"{_ctx.State.WinsA} - {_ctx.State.WinsB}",
                    _ctx.State.HandA, _ctx.State.HandB);
            }
            // ポーズ中は牌選択の入力を殺し、Pauseマップ(Esc)だけ生かす
            _input.SetSelectEnabled(!paused);
        }

        private void BeginSelection(Player who)
        {
            _ctx.Selecting = who;
            var hand = who == Player.A ? _ctx.State.HandA : _ctx.State.HandB;
            _handSorted = hand.OrderBy(t => t).ToList();
            _cursor = Random.Range(0, _handSorted.Count); // 初期カーソルはランダム(固定デフォルト牌の防止)
            _timer = _ctx.Config.SelectTimeLimitSeconds;
            _input.SwitchMaps(select: true, mashP1: false, mashP2: false, system: false);
        }

        private void Confirm()
        {
            SoundManager.Instance.PlaySe(_refs.SeConfirm);
            int picked = _handSorted[_cursor];

            if (_ctx.IsVersus)
            {
                if (_ctx.Selecting == Player.A)
                {
                    _ctx.TileA = picked;
                    // 暗転を挟んで2Pへ交代。暗転中に手札表示が1P用→2P用へ入れ替わる
                    _switchingPlayer = true;
                    _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
                    SceneLoader.Instance.FadeAction(() =>
                    {
                        BeginSelection(Player.B);
                        _switchingPlayer = false;
                    });
                    return;
                }
                _ctx.TileB = picked;
            }
            else
            {
                _ctx.TileA = picked;
                // CPUはプレイヤーの選択を知らずに残り手札だけを見て決める(同時出しの再現)
                _ctx.TileB = _ctx.Brain.SelectTile(_ctx.State, _ctx.MashCount, _ctx.CpsTracker.Estimate);
            }

            _ctx.State.ConsumeTiles(_ctx.TileA, _ctx.TileB);
            _ctx.Matchup = MatchupJudge.Judge(_ctx.TileA, _ctx.TileB, _ctx.Config);
            _changePhase(PhaseId.Reveal);
        }
    }
}
