using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// 牌の選択。カーソル移動・数字キー直接指定・Enter確定・制限時間切れ・ポーズを扱う。
    /// ローカル2Pでは 1P → 暗転 → 2P の順に選び、ソロではCPUが同時に選ぶ。
    /// 確定したら勝敗の組み合わせを判定して公開へ進む。
    public sealed class Phase20Select : MatchPhaseBase
    {
        private List<int> _handSorted;
        private int _cursor;
        private float _timer;
        private bool _paused;
        private bool _switchingPlayer; // ローカル2Pの手番交代フェード中

        public Phase20Select(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
            : base(ctx, input, refs, changePhase) { }

        public override HudMode HudMode => HudMode.Select;
        public override float HudTimer => _timer;
        public override int? CursorTile =>
            !_paused && _handSorted != null && _handSorted.Count > 0
                ? _handSorted[_cursor]
                : (int?)null;

        public override void Enter()
        {
            Ctx.DisplayRound = Ctx.State.RoundNumber;
            BeginSelection(Player.A);
        }

        public override void Tick(float deltaTime)
        {
            // ポーズの開閉(選択フェーズ中のみ。連打中は「連打逃げ」防止のためPauseマップ自体が無効)
            if (Input.PausePressed)
            {
                SetPaused(!_paused);
                return;
            }
            if (_paused || _switchingPlayer) return; // ポーズ中・手番交代フェード中はタイマーも入力も止まる

            _timer -= deltaTime;

            // 数字キー直接指定(手札にある牌のみ有効)
            for (int d = 1; d <= 9; d++)
            {
                if (Input.DigitPressed(d) && _handSorted.Contains(d))
                {
                    _cursor = _handSorted.IndexOf(d);
                    Confirm();
                    return;
                }
            }

            if (Input.MoveLeftPressed)
                _cursor = (_cursor - 1 + _handSorted.Count) % _handSorted.Count;
            if (Input.MoveRightPressed)
                _cursor = (_cursor + 1) % _handSorted.Count;

            // Enter確定、または時間切れ=選択中の牌で確定
            if (Input.ConfirmPressed || _timer <= 0f)
                Confirm();
        }

        /// ポーズ状態の切替。ポーズメニューのボタンからもGameFlowController経由で呼ばれる。
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (Refs.PausePanel != null) Refs.PausePanel.SetActive(paused);
            if (paused && Refs.PauseStatus != null)
            {
                Refs.PauseStatus.Render(
                    $"{Ctx.State.WinsA} - {Ctx.State.WinsB}",
                    Ctx.State.HandA, Ctx.State.HandB);
            }
            // ポーズ中は牌選択の入力を殺し、Pauseマップ(Esc)だけ生かす
            Input.SetSelectEnabled(!paused);
        }

        private void BeginSelection(Player who)
        {
            Ctx.Selecting = who;
            _handSorted = Ctx.State.HandOf(who).OrderBy(t => t).ToList();
            _cursor = Random.Range(0, _handSorted.Count); // 初期カーソルはランダム(固定デフォルト牌の防止)
            _timer = Ctx.Config.SelectTimeLimitSeconds;
            Input.SwitchMaps(select: true, mashP1: false, mashP2: false, system: false);
        }

        private void Confirm()
        {
            SoundManager.Instance.PlaySe(Refs.SeConfirm);
            int picked = _handSorted[_cursor];

            if (Ctx.IsVersus)
            {
                if (Ctx.Selecting == Player.A)
                {
                    Ctx.TileA = picked;
                    // 暗転を挟んで2Pへ交代。暗転中に手札表示が1P用→2P用へ入れ替わる
                    _switchingPlayer = true;
                    Input.DisableAll();
                    SceneLoader.Instance.FadeAction(() =>
                    {
                        BeginSelection(Player.B);
                        _switchingPlayer = false;
                    });
                    return;
                }
                Ctx.TileB = picked;
            }
            else
            {
                Ctx.TileA = picked;
                // CPUはプレイヤーの選択を知らずに残り手札だけを見て決める(同時出しの再現)
                Ctx.TileB = Ctx.Brain.SelectTile(Ctx.State, Ctx.MashCount, Ctx.CpsTracker.Estimate);
            }

            Ctx.State.ConsumeTiles(Ctx.TileA, Ctx.TileB);
            Ctx.Matchup = MatchupJudge.Judge(Ctx.TileA, Ctx.TileB, Ctx.Config);
            ChangePhase(PhaseId.Reveal);
        }
    }
}
