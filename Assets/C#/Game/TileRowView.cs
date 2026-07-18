using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaniTactics.Game
{
    /// <summary>
    /// 牌1〜9を「一蟹」〜「九蟹」のテキストで1列表示する汎用View(画像不要)。
    /// 萬子風の二色刷り: 数字の色(1P=赤/2P=青はインスペクタで変更)+ 蟹の色(黒)。
    /// 未使用は指定色、使用済みは灰色、カーソル位置はプレート色と拡大で強調。
    /// </summary>
    public sealed class TileRowView : MonoBehaviour
    {
        [Tooltip("牌1〜9のテキストを左から順に割り当てる(9個)。文字はスクリプトが自動設定する")]
        [SerializeField] private TMP_Text[] slots = new TMP_Text[9];
        [Tooltip("任意: 牌の背景プレート(Image)。割り当てると状態色がプレートに乗る")]
        [SerializeField] private Image[] plates = new Image[9];

        [Header("表記")]
        [Tooltip("数字の後ろに付く字。一蟹、二蟹…")]
        [SerializeField] private string suffix = "蟹";
        [Tooltip("麻雀牌のように縦書き(一の下に蟹)にする")]
        [SerializeField] private bool vertical = true;

        [Header("文字色(萬子風の二色刷り)")]
        [Tooltip("数字の色。1P側=赤、2P側の列ではインスペクタで青に変更する")]
        [SerializeField] private Color numberColor = new Color(0.76f, 0.15f, 0.13f); // 朱色
        [SerializeField] private Color crabColor = new Color(0.10f, 0.10f, 0.10f);   // 墨色
        [Tooltip("使用済み牌の文字色(数字・蟹とも)")]
        [SerializeField] private Color usedTextColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);

        [Header("プレート色(plates割り当て時のみ)")]
        [SerializeField] private Color plateActive = new Color(0.96f, 0.94f, 0.88f, 0.9f); // 牌の白
        [SerializeField] private Color plateUsed = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        [SerializeField] private Color plateHighlight = new Color(1f, 0.85f, 0.2f, 0.95f);

        [Tooltip("カーソル中の牌の拡大率")]
        [SerializeField] private float highlightScale = 1.15f;

        private static readonly string[] Kanji =
            { "一", "二", "三", "四", "五", "六", "七", "八", "九" };

        // テキスト再構築の抑制用(毎フレームのメッシュ再生成を避ける)
        private readonly int[] _lastTextState = new int[9];

        private void Awake()
        {
            for (int i = 0; i < 9; i++) _lastTextState[i] = -1;
            for (int i = 0; i < slots.Length && i < 9; i++)
            {
                if (slots[i] == null) continue;
                slots[i].richText = true;
                slots[i].color = Color.white; // 色は全てリッチテキストタグで制御する
                ApplyText(i, active: true);
            }
        }

        /// <summary>
        /// activeTilesに含まれる牌を通常色、含まれない(使用済み)牌を灰色で表示する。
        /// highlightTileを渡すとその牌をプレート色と拡大で強調(選択カーソル用)。
        /// </summary>
        public void Render(IReadOnlyCollection<int> activeTiles, int? highlightTile = null)
        {
            for (int i = 0; i < slots.Length && i < 9; i++)
            {
                if (slots[i] == null) continue;

                int tile = i + 1;
                bool isActive = activeTiles.Contains(tile);
                bool isHighlighted = highlightTile == tile;

                int textState = isActive ? 1 : 0;
                if (_lastTextState[i] != textState)
                {
                    ApplyText(i, isActive);
                    _lastTextState[i] = textState;
                }

                if (i < plates.Length && plates[i] != null)
                {
                    plates[i].color = isHighlighted ? plateHighlight
                                    : isActive ? plateActive
                                    : plateUsed;
                }

                var scaleTarget = (i < plates.Length && plates[i] != null)
                    ? plates[i].transform
                    : slots[i].transform;
                scaleTarget.localScale = Vector3.one * (isHighlighted ? highlightScale : 1f);
            }
        }

        private void ApplyText(int index, bool active)
        {
            Color num = active ? numberColor : usedTextColor;
            Color crab = active ? crabColor : usedTextColor;
            string numHex = ColorUtility.ToHtmlStringRGBA(num);
            string crabHex = ColorUtility.ToHtmlStringRGBA(crab);
            string sep = vertical ? "\n" : "";
            slots[index].text =
                $"<color=#{numHex}>{Kanji[index]}</color>{sep}<color=#{crabHex}>{suffix}</color>";
        }
    }
}