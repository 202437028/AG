namespace KaniTactics.Game
{
    /// <summary>
    /// ラウンドの勝敗演出。勝者に1勝を加え、試合が終わっていれば試合終了へ、
    /// 続くなら暗転を挟んで次のラウンド開始へ戻る。
    /// </summary>
    public sealed class Phase50Result : IMatchPhase
    {
        private const float DisplaySeconds = 2f;

        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action<PhaseId> _changePhase;

        private float _timer;
        private bool _transitioning; // 次ラウンドへの暗転中(フェードを二重に呼ばないため)

        public Phase50Result(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _changePhase = changePhase;
        }

        public HudMode HudMode => HudMode.Result;
        public float HudTimer => _timer;
        public int? CursorTile => null;

        public void Enter()
        {
            _transitioning = false;
            _ctx.State.AwardWin(_ctx.RoundWinner.Value);
            _timer = DisplaySeconds;

            SoundManager.Instance.PlaySe(_refs.SeRoundWin);              // 勝敗共通
            SoundManager.Instance.DuckForSeconds(0.35f, DisplaySeconds); // このフェーズの間だけBGMを絞る
            if (_refs.MashGauge != null) _refs.MashGauge.Hide();
            if (_refs.CrabStage != null) _refs.CrabStage.PlayResult(_ctx.RoundWinner.Value);
            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        public void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (_ctx.State.IsOver)
            {
                _changePhase(PhaseId.MatchEnd);
            }
            else if (!_transitioning)
            {
                // ラウンドの繋ぎ目を暗転で挟む(暗転中に次ラウンドの準備が走る)
                _transitioning = true;
                SceneLoader.Instance.FadeAction(() => _changePhase(PhaseId.RoundIntro));
            }
        }

        public void Exit() { }
    }
}
