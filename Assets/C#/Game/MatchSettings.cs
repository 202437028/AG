namespace KaniTactics.Game
{
    /// <summary>
    /// シーンをまたいで持ち回す試合設定。タイトル画面で設定し、GameシーンのGameFlowControllerが読む。
    /// Configuredがfalseのとき(エディタでGameシーンを直接再生した場合など)は
    /// GameFlowControllerのインスペクタ値がそのまま使われる。
    /// </summary>
    public static class MatchSettings
    {
        public static bool Configured;
        public static GameFlowController.GameMode Mode;
        public static int CpuDifficulty;
    }
}
