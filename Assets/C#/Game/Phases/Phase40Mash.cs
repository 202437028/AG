using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 10秒間の連打勝負。プレイヤーの打鍵はMatchInputのイベントで1打ずつ受け取る(FPS非依存)。
    /// ソロではCPUの連打を実効CPSから積算する。時間切れで勝敗判定、完全同点なら再連打。
    /// </summary>
    public sealed class Phase40Mash : IMatchPhase
    {
        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action<PhaseId> _changePhase;

        private readonly TapCounter _tapsA = new TapCounter();
        private readonly TapCounter _tapsB = new TapCounter(); // LocalVersusのP2用
        private float _cpuTapsF;                               // SoloCpuのCPU用
        private float _cpuCpsThisMash;
        private float _timer;

        public Phase40Mash(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                           System.Action<PhaseId> changePhase)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _changePhase = changePhase;
        }

        public HudMode HudMode => HudMode.Mash;
        public float HudTimer => _timer;
        public int? CursorTile => null;

        private int CurrentTapsB => _ctx.IsVersus ? _tapsB.Count : Mathf.FloorToInt(_cpuTapsF);

        public void Enter()
        {
            // 連打イベントの購読はこのフェーズの間だけ
            _input.TapP1 += OnTapP1;
            _input.TapP2 += OnTapP2;

            BeginBout();
            if (_refs.HudVisibility != null) _refs.HudVisibility.ShowMashOnly();
            _input.SwitchMaps(select: false, mashP1: true, mashP2: _ctx.IsVersus, system: false);
        }

        public void Tick(float deltaTime)
        {
            // プレイヤーの打鍵はOnTapで数え済み。ここではCPUの連打と時間経過だけを扱う
            if (!_ctx.IsVersus)
                _cpuTapsF += _cpuCpsThisMash * deltaTime;

            float gauge = GaugePosition();
            if (_refs.MashGauge != null) _refs.MashGauge.SetTarget(gauge);
            if (_refs.CrabStage != null) _refs.CrabStage.SetTarget(gauge);

            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (!_ctx.IsVersus) RecordCps();
            _ctx.MashCount++; // CPU疲労が1段進む(Versusでは未使用)

            var winner = MashResolver.Resolve(_tapsA.Count, CurrentTapsB, _ctx.Matchup);
            if (winner == null)
            {
                BeginBout(); // 完全同点 → 中央から仕切り直して再連打
                return;
            }

            _ctx.RoundWinner = winner;
            _changePhase(PhaseId.Result);
        }

        public void Exit()
        {
            _input.TapP1 -= OnTapP1;
            _input.TapP2 -= OnTapP2;
        }

        /// <summary>1回分の連打勝負を開始する(初回と再連打で共通)</summary>
        private void BeginBout()
        {
            _tapsA.Reset();
            _tapsB.Reset();
            _cpuTapsF = 0f;
            if (!_ctx.IsVersus)
                _cpuCpsThisMash = _ctx.Brain.EffectiveCps(_ctx.MashCount); // 疲労込みの実効CPS
            _timer = _ctx.Config.MashSeconds;
            if (_refs.MashGauge != null) _refs.MashGauge.Show();
            if (_refs.CrabStage != null) _refs.CrabStage.BeginClash();
        }

        private void OnTapP1(int key) => RegisterTap(_tapsA, key);
        private void OnTapP2(int key) => RegisterTap(_tapsB, key);

        /// <summary>key: 0=左(A/←) 1=右(D/→)。同じキーの連続はTapCounter側で弾かれる。</summary>
        private void RegisterTap(TapCounter counter, int key)
        {
            if (_timer <= 0f) return; // 時間切れ後の打鍵は数えない
            if (counter.RegisterTap(key)) SoundManager.Instance.PlaySe(_refs.SeTap);
        }

        private void RecordCps()
        {
            float cps = _tapsA.Count / _ctx.Config.MashSeconds;
            _ctx.CpsLog.Add(cps);
            _ctx.CpsTracker.Record(cps); // AIのプレイヤーCPS推定を更新
            Debug.Log($"[CPS計測] Round {_ctx.DisplayRound}: あなた {_tapsA.Count}打 = {cps:F2}cps ／ CPU実効 {_cpuCpsThisMash:F2}cps");
        }

        /// <summary>
        /// ゲージ位置(0=B側優勢の端、1=A側優勢の端)。
        /// 勝敗判定と同じ重み(有利側の連打数×要求倍率)の比率なので、演出と結果が食い違わない。
        /// </summary>
        private float GaugePosition()
        {
            float weightedA = _tapsA.Count;
            float weightedB = CurrentTapsB;

            var m = _ctx.Matchup;
            if (m.FavoredSide == Player.A) weightedA *= m.RequiredRatio;
            else if (m.FavoredSide == Player.B) weightedB *= m.RequiredRatio;

            float total = weightedA + weightedB;
            return total <= 0f ? 0.5f : weightedA / total;
        }
    }
}
