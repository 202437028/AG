namespace KaniTactics.Game
{
    /// 各フェーズ共通の土台。共有データ・入力・シーン参照・フェーズ切替の窓口を持ち、
    /// HUD表示用プロパティとExitには「何もしない」既定値を与える(必要なフェーズだけ上書きする)。
    public abstract class MatchPhaseBase : IMatchPhase
    {
        protected readonly MatchContext Ctx;
        protected readonly MatchInput Input;
        protected readonly MatchSceneRefs Refs;
        protected readonly System.Action<PhaseId> ChangePhase;

        protected MatchPhaseBase(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                                 System.Action<PhaseId> changePhase)
        {
            Ctx = ctx;
            Input = input;
            Refs = refs;
            ChangePhase = changePhase;
        }

        public abstract HudMode HudMode { get; }
        public virtual float HudTimer => 0f;
        public virtual int? CursorTile => null;

        public abstract void Enter();
        public abstract void Tick(float deltaTime);
        public virtual void Exit() { }
    }
}
