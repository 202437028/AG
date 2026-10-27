using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// HUDの表示モード。フェーズごとに出す文言が変わる
    public enum HudMode { Blank, Select, Reveal, Mash, Result, MatchEnd }

    /// HUDに出す文字列と色の組み立て専用(掲示板係)。
    /// MatchContextを読んで文言を作り、MatchHudViewへ渡すだけ。描画はView側の責務。
    public sealed class MatchHudPresenter
    {
        private readonly MatchHudView _hud;
        private readonly Color _color1P;
        private readonly Color _color2P;

        public MatchHudPresenter(MatchHudView hud, Color color1P, Color color2P)
        {
            _hud = hud;
            _color1P = color1P;
            _color2P = color2P;
        }

        /// <param name="timer">残り時間(SelectとMashで表示)</param>
        /// <param name="cursorTile">選択中の牌(選択中以外はnull)</param>
        /// <param name="revealShownByView">公開演出Viewが表示を担当しているか</param>
        public void Render(MatchContext ctx, HudMode mode, float timer, int? cursorTile, bool revealShownByView)
        {
            if (_hud == null || ctx == null) return;

            var state = ctx.State;
            string header = $"Round {ctx.DisplayRound}   {state.WinsA} - {state.WinsB}";

            // 手札表示は「今選んでいない側=相手」のみ。自分の手札は選択カーソルで見えるため重複させない。
            // ローカル2Pでは選択者が交代するたびに、公開される手札も自動で入れ替わる。
            Player opponent = ctx.Selecting.Opponent();
            var opponentHand = state.HandOf(opponent);
            string hands = $"{NameOf(ctx, opponent)}の手札: {TileList(opponentHand)}";

            BuildPhaseText(ctx, mode, timer, revealShownByView, out string main, out string sub);
            _hud.Render(header, hands, main, sub);

            // 試合終了の案内(専用テキスト)。それ以外では空を渡して隠す
            if (mode == HudMode.MatchEnd)
            {
                var w = state.MatchWinner;
                _hud.RenderMatchEnd(
                    $"試合終了!  勝者: {(w.HasValue ? NameOf(ctx, w.Value) : "引き分け")}  ({state.WinsA} - {state.WinsB})\n" +
                    "Rキーで再戦 / Escでタイトルへ");
            }
            else
            {
                _hud.RenderMatchEnd(null);
            }

            // 1P=赤 / 2P=青 をプレイヤーに固定。手番が変わると自分/相手の色も入れ替わる。
            // ソロは常にプレイヤー=A=赤、CPU=B=青。
            _hud.RenderTileRows(state.HandOf(ctx.Selecting), opponentHand, cursorTile,
                                ColorOf(ctx.Selecting), ColorOf(opponent));
        }

        private void BuildPhaseText(MatchContext ctx, HudMode mode, float timer, bool revealShownByView,
                                    out string main, out string sub)
        {
            switch (mode)
            {
                case HudMode.Select:
                    main = $"残り {Mathf.CeilToInt(Mathf.Max(timer, 0f))} 秒";
                    sub = ctx.IsVersus
                        ? $"{NameOf(ctx, ctx.Selecting)} の番 / 1-9キー・←→で選び Enterで確定(相手は画面から目を離すこと!)"
                        : "1-9キー直接 / ←→で移動 / Enterで確定";
                    break;

                case HudMode.Reveal:
                    // 演出Viewが牌・見出し・カウントダウンを出す場合はHUDを空にする
                    main = "";
                    sub = revealShownByView ? "" : MatchupText(ctx);
                    break;

                case HudMode.Mash:
                    // 連打数は出さない(ブラックボックス化)
                    main = $"残り {timer:F1} 秒";
                    sub = ctx.IsVersus
                        ? $"P1: A/D、P2: ←/→ を交互に! {MatchupText(ctx)}"
                        : $"AとDを交互に! {MatchupText(ctx)}";
                    break;

                case HudMode.Result:
                    main = "";
                    sub = $"このラウンドは {NameOf(ctx, ctx.RoundWinner.Value)} の勝ち!";
                    break;

                case HudMode.Blank:
                case HudMode.MatchEnd: // 案内は専用テキストに出す
                default:
                    main = "";
                    sub = "";
                    break;
            }
        }

        private Color ColorOf(Player p) => p == Player.A ? _color1P : _color2P;

        private static string NameOf(MatchContext ctx, Player p)
            => ctx.IsVersus ? (p == Player.A ? "P1" : "P2")
                            : (p == Player.A ? "あなた" : "CPU");

        private static string TileList(IReadOnlyCollection<int> hand)
            => string.Join(" ", hand.OrderBy(t => t));

        private static string MatchupText(MatchContext ctx)
        {
            var m = ctx.Matchup;
            if (m.IsImmediate) return "大差 → 即決着";
            if (m.FavoredSide == null) return "同数 → 等倍の連打勝負";
            return $"{NameOf(ctx, m.FavoredSide.Value)}が{m.AdvantageSteps}段有利(要求倍率 ×{m.RequiredRatio:F3})";
        }
    }
}
