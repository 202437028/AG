namespace KaniTactics.Core
{
    /// <summary>
    /// 交互連打カウンタ。直前と同じキーの入力は加算しない(押しっぱなし・連続同一キー対策)。
    /// キー入力の検出はGame層の責務、カウントの有効判定はCore層の責務。
    /// </summary>
    public sealed class TapCounter
    {
        private const int NoKey = -1;
        private int _lastKeyId = NoKey;

        /// <summary>有効打の累計。</summary>
        public int Count { get; private set; }

        /// <summary>
        /// 打鍵を登録する。直前と異なるキーなら加算して true、同一キーなら無効で false。
        /// 最初の1打はどちらのキーでも有効。
        /// </summary>
        public bool RegisterTap(int keyId)
        {
            if (keyId == _lastKeyId)
                return false;

            _lastKeyId = keyId;
            Count++;
            return true;
        }

        /// <summary>次の連打フェーズに向けてリセットする。</summary>
        public void Reset()
        {
            _lastKeyId = NoKey;
            Count = 0;
        }
    }
}
