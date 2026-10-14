using UnityEngine;

namespace KaniTactics.Game
{
    /// <summary>
    /// フェーズが使うシーン上の参照(View・演出・サウンド)の束。
    /// 値はGameFlowControllerのInspector設定から詰める。フェーズごとに長い引数リストを渡さないための入れ物。
    /// </summary>
    public sealed class MatchSceneRefs
    {
        // ---- View・演出(未割り当てならnull。各フェーズ側でnullチェックする) ----
        public MashGaugeView MashGauge;
        public CrabClashView CrabStage;
        public RevealView RevealView;
        public RoundIntroView RoundIntroView;
        public CameraDirector CameraDirector;
        public HudVisibilityDirector HudVisibility;

        // ---- ポーズ ----
        public GameObject PausePanel;
        public PauseStatusView PauseStatus;

        // ---- シーン遷移 ----
        public string TitleSceneName;

        // ---- サウンド(未割り当ての音は鳴らないだけ) ----
        public AudioClip SeConfirm;
        public AudioClip SeTap;
        public AudioClip SeRoundWin;
        public AudioClip SeMatchWin;
    }
}