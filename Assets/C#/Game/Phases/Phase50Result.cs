namespace KaniTactics.Game
{
    /// ラウンドの勝敗演出。勝者に1勝を加え、試合が終わっていれば試合終了へ、
    /// 続くなら暗転を挟んで次のラウンド開始へ戻る。
    public sealed class Phase50Result : MatchPhaseBase
    {
        private const float DisplaySeconds = 2f;
        private const float DuckLevel = 0.35f; // 勝敗表示中のBGM音量(正規音量に対する倍率)

        private float _timer;
        private bool _transitioning; // 次ラウンドへの暗転中(フェードを二重に呼ばないため)

        public Phase50Result(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
            : base(ctx, input, refs, changePhase) { }

        public override HudMode HudMode => HudMode.Result;
        public override float HudTimer => _timer;

        public override void Enter()
        {
            _transitioning = false;
            Ctx.State.AwardWin(Ctx.RoundWinner.Value);
            _timer = DisplaySeconds;

            SoundManager.Instance.PlaySe(Refs.SeRoundWin);                  // 勝敗共通
            SoundManager.Instance.DuckForSeconds(DuckLevel, DisplaySeconds); // このフェーズの間だけBGMを絞る
            if (Refs.MashGauge != null) Refs.MashGauge.Hide();
            if (Refs.CrabStage != null) Refs.CrabStage.PlayResult(Ctx.RoundWinner.Value);
            Input.DisableAll();
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (Ctx.State.IsOver)
            {
                ChangePhase(PhaseId.MatchEnd);
            }
            else if (!_transitioning)
            {
                // ラウンドの繋ぎ目を暗転で挟む(暗転中に次ラウンドの準備が走る)
                _transitioning = true;
                SceneLoader.Instance.FadeAction(() => ChangePhase(PhaseId.RoundIntro));
            }
        }
    }
}
