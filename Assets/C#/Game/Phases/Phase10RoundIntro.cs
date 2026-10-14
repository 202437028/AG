using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// ラウンド開始演出(ROUND○ / FIGHT!!)。
    /// 前ラウンドの演出を片づけて初期状態に戻し、演出が終わったら選択へ進む。演出が無ければ即座に選択へ。
    /// </summary>
    public sealed class Phase10RoundIntro : IMatchPhase
    {
        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action<PhaseId> _changePhase;

        private float _timer;

        public Phase10RoundIntro(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                                 System.Action<PhaseId> changePhase)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _changePhase = changePhase;
        }

        public HudMode HudMode => HudMode.Blank;
        public float HudTimer => 0f;
        public int? CursorTile => null;

        public void Enter()
        {
            _ctx.DisplayRound = _ctx.State.RoundNumber;
            // 手番を1Pへ戻す。この処理は暗転中に走るので、手札の色が明転前に1P基準へ切り替わる
            _ctx.Selecting = Player.A;

            if (_refs.MashGauge != null) _refs.MashGauge.Hide();
            if (_refs.CrabStage != null) _refs.CrabStage.ReturnHome();
            if (_refs.CameraDirector != null) _refs.CameraDirector.EnterSelectMode();
            if (_refs.HudVisibility != null) _refs.HudVisibility.ShowAll(); // 演出で隠したUIをここで戻す
            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);

            if (_refs.RoundIntroView != null)
            {
                _refs.RoundIntroView.Play(_ctx.DisplayRound);
                _timer = _refs.RoundIntroView.TotalSeconds;
            }
            else
            {
                _changePhase(PhaseId.Select); // 演出未割り当てならそのまま選択へ
            }
        }

        public void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (_refs.RoundIntroView != null) _refs.RoundIntroView.Hide();
            _changePhase(PhaseId.Select);
        }

        public void Exit() { }
    }
}
