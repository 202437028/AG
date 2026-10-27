using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// 10秒間の連打勝負。プレイヤーの打鍵はMatchInputのイベントで1打ずつ受け取る(FPS非依存)。
    /// ソロではCPUの連打を実効CPSから積算する。時間切れで勝敗判定、完全同点なら再連打。
    public sealed class Phase40Mash : MatchPhaseBase
    {
        private readonly TapCounter _tapsA = new TapCounter();
        private readonly TapCounter _tapsB = new TapCounter(); // LocalVersusのP2用
        private float _cpuTapsF;                               // SoloCpuのCPU用
        private float _cpuCpsThisMash;
        private float _timer;

        public Phase40Mash(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                           System.Action<PhaseId> changePhase)
            : base(ctx, input, refs, changePhase) { }

        public override HudMode HudMode => HudMode.Mash;
        public override float HudTimer => _timer;

        private int CurrentTapsB => Ctx.IsVersus ? _tapsB.Count : Mathf.FloorToInt(_cpuTapsF);

        public override void Enter()
        {
            // 連打イベントの購読はこのフェーズの間だけ
            Input.TapP1 += OnTapP1;
            Input.TapP2 += OnTapP2;

            BeginBout();
            if (Refs.HudVisibility != null) Refs.HudVisibility.ShowMashOnly();
            Input.SwitchMaps(select: false, mashP1: true, mashP2: Ctx.IsVersus, system: false);
        }

        public override void Tick(float deltaTime)
        {
            // プレイヤーの打鍵はOnTapで数え済み。ここではCPUの連打と時間経過だけを扱う
            if (!Ctx.IsVersus)
                _cpuTapsF += _cpuCpsThisMash * deltaTime;

            float gauge = GaugePosition();
            if (Refs.MashGauge != null) Refs.MashGauge.SetTarget(gauge);
            if (Refs.CrabStage != null) Refs.CrabStage.SetTarget(gauge);

            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (!Ctx.IsVersus) RecordCps();
            Ctx.MashCount++; // CPU疲労が1段進む(Versusでは未使用)

            var winner = MashResolver.Resolve(_tapsA.Count, CurrentTapsB, Ctx.Matchup);
            if (winner == null)
            {
                BeginBout(); // 完全同点 → 中央から仕切り直して再連打
                return;
            }

            Ctx.RoundWinner = winner;
            ChangePhase(PhaseId.Result);
        }

        public override void Exit()
        {
            Input.TapP1 -= OnTapP1;
            Input.TapP2 -= OnTapP2;
        }

        /// 1回分の連打勝負を開始する(初回と再連打で共通)
        private void BeginBout()
        {
            _tapsA.Reset();
            _tapsB.Reset();
            _cpuTapsF = 0f;
            if (!Ctx.IsVersus)
                _cpuCpsThisMash = Ctx.Brain.EffectiveCps(Ctx.MashCount); // 疲労込みの実効CPS
            _timer = Ctx.Config.MashSeconds;
            if (Refs.MashGauge != null) Refs.MashGauge.Show();
            if (Refs.CrabStage != null) Refs.CrabStage.BeginClash();
        }

        private void OnTapP1(int key) => RegisterTap(_tapsA, key);
        private void OnTapP2(int key) => RegisterTap(_tapsB, key);

        /// key: 0=左(A/←) 1=右(D/→)。同じキーの連続はTapCounter側で弾かれる。
        private void RegisterTap(TapCounter counter, int key)
        {
            if (_timer <= 0f) return; // 時間切れ後の打鍵は数えない
            if (counter.RegisterTap(key)) SoundManager.Instance.PlaySe(Refs.SeTap);
        }

        private void RecordCps()
        {
            float cps = _tapsA.Count / Ctx.Config.MashSeconds;
            Ctx.CpsLog.Add(cps);
            Ctx.CpsTracker.Record(cps); // AIのプレイヤーCPS推定を更新
            Debug.Log($"[CPS計測] Round {Ctx.DisplayRound}: あなた {_tapsA.Count}打 = {cps:F2}cps ／ CPU実効 {_cpuCpsThisMash:F2}cps");
        }

        /// ゲージ位置(0=B側優勢の端、1=A側優勢の端)。
        /// 勝敗判定と同じ重み(有利側の連打数×要求倍率)の比率なので、演出と結果が食い違わない。
        private float GaugePosition()
        {
            float weightedA = _tapsA.Count;
            float weightedB = CurrentTapsB;

            var m = Ctx.Matchup;
            if (m.FavoredSide == Player.A) weightedA *= m.RequiredRatio;
            else if (m.FavoredSide == Player.B) weightedB *= m.RequiredRatio;

            float total = weightedA + weightedB;
            return total <= 0f ? 0.5f : weightedA / total;
        }
    }
}
