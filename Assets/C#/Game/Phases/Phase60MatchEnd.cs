using System.Linq;
using UnityEngine;

namespace KaniTactics.Game
{
    /// 試合終了。ジングルを鳴らし、Rで再戦 / Escでタイトルへ戻る。
    public sealed class Phase60MatchEnd : MatchPhaseBase
    {
        private readonly System.Action _restartMatch;

        public Phase60MatchEnd(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                               System.Action<PhaseId> changePhase, System.Action restartMatch)
            : base(ctx, input, refs, changePhase)
        {
            _restartMatch = restartMatch;
        }

        public override HudMode HudMode => HudMode.MatchEnd;

        public override void Enter()
        {
            var log = Ctx.CpsLog;
            if (log.Count > 0)
                Debug.Log($"[CPS計測] 平均 {log.Average():F2} / 最小 {log.Min():F2} / 最大 {log.Max():F2} (連打{log.Count}回)");

            SoundManager.Instance.PlaySe(Refs.SeMatchWin); // 勝敗共通
            SoundManager.Instance.DuckMute();              // ジングルの間はBGMを完全ミュート(次の試合開始で自動復帰)
            Input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: true);
        }

        public override void Tick(float deltaTime)
        {
            if (Input.RestartPressed)
                SceneLoader.Instance.FadeAction(_restartMatch);

            if (Input.ToTitlePressed)
                SceneLoader.Instance.LoadScene(Refs.TitleSceneName);
        }
    }
}
