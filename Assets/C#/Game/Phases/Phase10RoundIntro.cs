using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// ラウンド開始演出(ROUND○ / FIGHT!!)。
    /// 前ラウンドの演出を片づけて初期状態に戻し、演出が終わったら選択へ進む。演出が無ければ即座に選択へ。
    public sealed class Phase10RoundIntro : MatchPhaseBase
    {
        private float _timer;

        public Phase10RoundIntro(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                                 System.Action<PhaseId> changePhase)
            : base(ctx, input, refs, changePhase) { }

        public override HudMode HudMode => HudMode.Blank;

        public override void Enter()
        {
            Ctx.DisplayRound = Ctx.State.RoundNumber;
            // 手番を1Pへ戻す。この処理は暗転中に走るので、手札の色が明転前に1P基準へ切り替わる
            Ctx.Selecting = Player.A;

            if (Refs.MashGauge != null) Refs.MashGauge.Hide();
            if (Refs.CrabStage != null) Refs.CrabStage.ReturnHome();
            if (Refs.CameraDirector != null) Refs.CameraDirector.EnterSelectMode();
            if (Refs.HudVisibility != null) Refs.HudVisibility.ShowAll(); // 演出で隠したUIをここで戻す
            Input.DisableAll();

            if (Refs.RoundIntroView != null)
            {
                Refs.RoundIntroView.Play(Ctx.DisplayRound);
                _timer = Refs.RoundIntroView.TotalSeconds;
            }
            else
            {
                ChangePhase(PhaseId.Select); // 演出未割り当てならそのまま選択へ
            }
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (Refs.RoundIntroView != null) Refs.RoundIntroView.Hide();
            ChangePhase(PhaseId.Select);
        }
    }
}
