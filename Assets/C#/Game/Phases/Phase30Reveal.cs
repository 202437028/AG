using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 牌の公開演出。大差なら即決着で結果へ、そうでなければ連打へ進む。
    /// </summary>
    public sealed class Phase30Reveal : IMatchPhase
    {
        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action<PhaseId> _changePhase;

        private float _timer;

        public Phase30Reveal(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                             System.Action<PhaseId> changePhase)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _changePhase = changePhase;
        }

        public HudMode HudMode => HudMode.Reveal;
        public float HudTimer => _timer;
        public int? CursorTile => null;

        public void Enter()
        {
            // 選択確定の瞬間(ふたりでは2Pの確定時)にHUDを引っ込める。
            // 牌カードが出る前に手札・PAUSEを消し、公開演出を邪魔しないようにする。
            if (_refs.HudVisibility != null) _refs.HudVisibility.HideAll();
            if (_refs.CameraDirector != null) _refs.CameraDirector.EnterBattleMode();

            if (_refs.RevealView != null)
            {
                var m = _ctx.Matchup;
                if (m.IsImmediate)
                {
                    // 大差決着: 勝者に1勝を先取りした状態のスコアを見せる
                    int winsA = _ctx.State.WinsA + (m.ImmediateWinner == Player.A ? 1 : 0);
                    int winsB = _ctx.State.WinsB + (m.ImmediateWinner == Player.B ? 1 : 0);
                    _refs.RevealView.PlayImmediate(_ctx.TileA, _ctx.TileB, $"{winsA} - {winsB}");
                    _timer = _refs.RevealView.ImmediateSeconds;
                }
                else
                {
                    string instruction = _ctx.IsVersus
                        ? "AとD ←と→を\n連打しろ!!"
                        : "AとDを連打しろ!!";
                    _refs.RevealView.PlayMashIntro(_ctx.TileA, _ctx.TileB, instruction);
                    _timer = _refs.RevealView.MashIntroSeconds;
                }
                SoundManager.Instance.DuckForSeconds(0.5f, _timer);
            }
            else
            {
                _timer = 2f; // 従来動作(演出なし)
                SoundManager.Instance.DuckForSeconds(0.5f, 2f);
            }

            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        public void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (_refs.RevealView != null) _refs.RevealView.Hide();

            if (_ctx.Matchup.IsImmediate)
            {
                _ctx.RoundWinner = _ctx.Matchup.ImmediateWinner;
                _changePhase(PhaseId.Result);
            }
            else
            {
                _changePhase(PhaseId.Mash);
            }
        }

        public void Exit() { }
    }
}
