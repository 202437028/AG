namespace KaniTactics.Game
{
    /// フェーズの実行順。上から順に進む(ファイル名の番号と対応)
    public enum PhaseId
    {
        RoundIntro, // Phase10
        Select,     // Phase20
        Reveal,     // Phase30
        Mash,       // Phase40
        Result,     // Phase50
        MatchEnd,   // Phase60
    }

    /// 試合の各フェーズ(各コーナーの担当者)が実装する共通の窓口。
    /// GameFlowControllerがEnter → Tick(毎フレーム) → Exit の順に呼ぶ。
    public interface IMatchPhase
    {
        /// フェーズ開始時に1回だけ呼ばれる
        void Enter();

        /// 毎フレーム呼ばれる
        void Tick(float deltaTime);

        /// 次のフェーズへ切り替わる直前に1回だけ呼ばれる
        void Exit();

        // ---- HUD表示用(MatchHudPresenterに渡す) ----
        HudMode HudMode { get; }
        float HudTimer { get; }
        int? CursorTile { get; }
    }
}
