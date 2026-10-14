namespace KaniTactics.Game
{
    /// <summary>フェーズの実行順。上から順に進む(ファイル名の番号と対応)</summary>
    public enum PhaseId
    {
        RoundIntro, // Phase10
        Select,     // Phase20
        Reveal,     // Phase30
        Mash,       // Phase40
        Result,     // Phase50
        MatchEnd,   // Phase60
    }

    /// <summary>
    /// 試合の各フェーズ(各コーナーの担当者)が実装する共通の窓口。
    /// GameFlowControllerがEnter → Tick(毎フレーム) → Exit の順に呼ぶ。
    /// </summary>
    public interface IMatchPhase
    {
        /// <summary>フェーズ開始時に1回だけ呼ばれる</summary>
        void Enter();

        /// <summary>毎フレーム呼ばれる</summary>
        void Tick(float deltaTime);

        /// <summary>次のフェーズへ切り替わる直前に1回だけ呼ばれる</summary>
        void Exit();

        // ---- HUD表示用(MatchHudPresenterに渡す) ----
        HudMode HudMode { get; }
        float HudTimer { get; }
        int? CursorTile { get; }
    }
}
