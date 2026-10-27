using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// 牌の公開演出。大差なら即決着で結果へ、そうでなければ連打へ進む。
    public sealed class Phase30Reveal : MatchPhaseBase
    {
        private const float FallbackSeconds = 2f; // 演出View未割り当て時の表示時間
        private const float DuckLevel = 0.5f;     // 公開中のBGM音量(正規音量に対する倍率)

        private float _timer;

        public Phase30Reveal(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
            : base(ctx, input, refs, changePhase) { }

        public override HudMode HudMode => HudMode.Reveal;
        public override float HudTimer => _timer;

        public override void Enter()
        {
            // 選択確定の瞬間(ふたりでは2Pの確定時)にHUDを引っ込める。
            // 牌カードが出る前に手札・PAUSEを消し、公開演出を邪魔しないようにする。
            if (Refs.HudVisibility != null) Refs.HudVisibility.HideAll();
            if (Refs.CameraDirector != null) Refs.CameraDirector.EnterBattleMode();

            _timer = Refs.RevealView != null ? PlayRevealView() : FallbackSeconds;
            SoundManager.Instance.DuckForSeconds(DuckLevel, _timer);

            Input.DisableAll();
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (Refs.RevealView != null) Refs.RevealView.Hide();

            if (Ctx.Matchup.IsImmediate)
            {
                Ctx.RoundWinner = Ctx.Matchup.ImmediateWinner;
                ChangePhase(PhaseId.Result);
            }
            else
            {
                ChangePhase(PhaseId.Mash);
            }
        }

        /// 判定に応じた公開演出を再生し、その尺(秒)を返す。
        private float PlayRevealView()
        {
            var m = Ctx.Matchup;
            if (m.IsImmediate)
            {
                // 大差決着: 勝者に1勝を先取りした状態のスコアを見せる
                int winsA = Ctx.State.WinsA + (m.ImmediateWinner == Player.A ? 1 : 0);
                int winsB = Ctx.State.WinsB + (m.ImmediateWinner == Player.B ? 1 : 0);
                Refs.RevealView.PlayImmediate(Ctx.TileA, Ctx.TileB, $"{winsA} - {winsB}");
                return Refs.RevealView.ImmediateSeconds;
            }

            string instruction = Ctx.IsVersus
                ? "AとD ←と→を\n連打しろ!!"
                : "AとDを連打しろ!!";
            Refs.RevealView.PlayMashIntro(Ctx.TileA, Ctx.TileB, instruction);
            return Refs.RevealView.MashIntroSeconds;
        }
    }
}
