using UnityEngine;

namespace KaniTactics.Game
{
    /// 牌の表記「一蟹」〜「九蟹」を萬子風の二色刷り(数字色+蟹色)のリッチテキストで組み立てる。
    /// TileRowView(手札の列)とTileCardView(公開演出のカード)で共用する。
    public static class TileLabel
    {
        private static readonly string[] Kanji =
            { "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        /// <param name="tile">牌(1〜9)</param>
        /// <param name="vertical">麻雀牌のように縦書き(一の下に蟹)にするか</param>
        public static string Format(int tile, Color numberColor, Color crabColor, string suffix, bool vertical)
        {
            string numHex = ColorUtility.ToHtmlStringRGBA(numberColor);
            string crabHex = ColorUtility.ToHtmlStringRGBA(crabColor);
            string sep = vertical ? "\n" : "";
            return $"<color=#{numHex}>{Kanji[tile - 1]}</color>{sep}<color=#{crabHex}>{suffix}</color>";
        }
    }
}
