using System.Linq;
using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// 試合終了。ジングルを鳴らし、Rで再戦 / Escでタイトルへ戻る。
    /// </summary>
    public sealed class Phase60MatchEnd : IMatchPhase
    {
        private readonly MatchContext _ctx;
        private readonly MatchInput _input;
        private readonly MatchSceneRefs _refs;
        private readonly System.Action _restartMatch;

        public Phase60MatchEnd(MatchContext ctx, MatchInput input, MatchSceneRefs refs,
                               System.Action restartMatch)
        {
            _ctx = ctx;
            _input = input;
            _refs = refs;
            _restartMatch = restartMatch;
        }

        public HudMode HudMode => HudMode.MatchEnd;
        public float HudTimer => 0f;
        public int? CursorTile => null;

        public void Enter()
        {
            var log = _ctx.CpsLog;
            if (log.Count > 0)
                Debug.Log($"[CPS計測] 平均 {log.Average():F2} / 最小 {log.Min():F2} / 最大 {log.Max():F2} (連打{log.Count}回)");

            SoundManager.Instance.PlaySe(_refs.SeMatchWin); // 勝敗共通
            SoundManager.Instance.DuckMute();               // ジングルの間はBGMを完全ミュート(次の試合開始で自動復帰)
            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: true);
        }

        public void Tick(float deltaTime)
        {
            if (_input.RestartPressed)
                SceneLoader.Instance.FadeAction(_restartMatch);

            if (_input.ToTitlePressed)
                SceneLoader.Instance.LoadScene(_refs.TitleSceneName);
        }

        public void Exit() { }
    }
}
