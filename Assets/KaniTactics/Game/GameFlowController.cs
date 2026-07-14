using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// M2グレーボックス: 1試合ループ(Select → Reveal → Mash → Result)。
    /// 進行と状態管理のみを担当し、表示はMatchHudViewに委譲する。
    /// CPUは完全ランダム。M3でAI、M4で入力(Action Map)と演出を差し替える。
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        private enum Phase { Select, Reveal, Mash, Result, MatchEnd }

        [SerializeField] private RuleConfigAsset configAsset;
        [SerializeField] private MatchHudView hud;
        [Tooltip("0=イージー(5cps) 1=ノーマル(7) 2=ハード(9) 3=名人(16)")]
        [SerializeField, Range(0, 3)] private int cpuDifficulty = 1;

        private RuleConfig _config;
        private MatchState _state;
        private Phase _phase;
        private float _phaseTimer;
        private int _displayRound;

        // 選択フェーズ
        private List<int> _handSorted;
        private int _cursor;

        // ラウンドデータ(A=プレイヤー, B=CPU)
        private int _tileA, _tileB;
        private MatchupResult _matchup;
        private Player? _roundWinner;

        // 連打フェーズ
        private readonly TapCounter _playerTaps = new TapCounter();
        private float _cpuTapsF;

        // CPS計測(M5のρ確定用データ)
        private readonly List<float> _cpsLog = new List<float>();

        // CPU AI(M3): 均衡ブレイン・プレイヤーCPS推定・疲労カウント
        private CpuBrain _brain;
        private CpsTracker _cpsTracker;
        private int _mashCount;
        private float _cpuCpsThisMash;

        private void Start() => StartMatch();

        private void StartMatch()
        {
            _config = configAsset.ToConfig();
            _state = new MatchState(_config);
            _brain = new CpuBrain(_config, cpuDifficulty, new System.Random());
            _cpsTracker = new CpsTracker(_config.PlayerCpsPrior);
            _mashCount = 0;
            _cpsLog.Clear();
            EnterSelect();
        }

        private void EnterSelect()
        {
            _displayRound = _state.RoundNumber;
            _handSorted = _state.HandA.OrderBy(t => t).ToList();
            _cursor = Random.Range(0, _handSorted.Count); // 初期カーソルはランダム(固定デフォルト牌の防止)
            _phaseTimer = _config.SelectTimeLimitSeconds;
            _phase = Phase.Select;
        }

        private void Update()
        {
            if (Keyboard.current != null)
            {
                switch (_phase)
                {
                    case Phase.Select: UpdateSelect(); break;
                    case Phase.Reveal: UpdateReveal(); break;
                    case Phase.Mash: UpdateMash(); break;
                    case Phase.Result: UpdateResult(); break;
                    case Phase.MatchEnd: UpdateMatchEnd(); break;
                }
            }

            RenderHud();
        }

        // ---- Select ----

        private void UpdateSelect()
        {
            var kb = Keyboard.current;
            _phaseTimer -= Time.deltaTime;

            // 数字キー直接指定(手札にある牌のみ有効)
            for (int d = 1; d <= 9; d++)
            {
                if (WasDigitPressed(kb, d) && _handSorted.Contains(d))
                {
                    _cursor = _handSorted.IndexOf(d);
                    ConfirmSelection();
                    return;
                }
            }

            if (kb.leftArrowKey.wasPressedThisFrame)
                _cursor = (_cursor - 1 + _handSorted.Count) % _handSorted.Count;
            if (kb.rightArrowKey.wasPressedThisFrame)
                _cursor = (_cursor + 1) % _handSorted.Count;

            // Enter確定、または時間切れ=選択中の牌で確定
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || _phaseTimer <= 0f)
                ConfirmSelection();
        }

        private static bool WasDigitPressed(Keyboard kb, int digit)
        {
            var key = Key.Digit1 + (digit - 1); // Digit1〜Digit9は連続値
            return kb[key].wasPressedThisFrame;
        }

        private void ConfirmSelection()
        {
            _tileA = _handSorted[_cursor];
            // CPUはプレイヤーの選択を知らずに残り手札だけを見て決める(同時出しの再現)
            _tileB = _brain.SelectTile(_state, _mashCount, _cpsTracker.Estimate);
            _state.ConsumeTiles(_tileA, _tileB);
            _matchup = MatchupJudge.Judge(_tileA, _tileB, _config);

            _phase = Phase.Reveal;
            _phaseTimer = 2f; // 公開を2秒見せて自動遷移
        }

        // ---- Reveal ----

        private void UpdateReveal()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (_matchup.IsImmediate)
            {
                _roundWinner = _matchup.ImmediateWinner;
                EnterResult();
            }
            else
            {
                _playerTaps.Reset();
                _cpuTapsF = 0f;
                _cpuCpsThisMash = _brain.EffectiveCps(_mashCount); // 疲労込みの実効CPS
                _phaseTimer = _config.MashSeconds;
                _phase = Phase.Mash;
            }
        }

        // ---- Mash ----

        private void UpdateMash()
        {
            var kb = Keyboard.current;
            if (kb.aKey.wasPressedThisFrame) _playerTaps.RegisterTap(0);
            if (kb.dKey.wasPressedThisFrame) _playerTaps.RegisterTap(1);

            _cpuTapsF += _cpuCpsThisMash * Time.deltaTime;
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            float cps = _playerTaps.Count / _config.MashSeconds;
            _cpsLog.Add(cps);
            _cpsTracker.Record(cps);   // AIのプレイヤーCPS推定を更新
            _mashCount++;              // CPU疲労が1段進む
            Debug.Log($"[CPS計測] Round {_displayRound}: あなた {_playerTaps.Count}打 = {cps:F2}cps ／ CPU実効 {_cpuCpsThisMash:F2}cps");

            var winner = MashResolver.Resolve(_playerTaps.Count, Mathf.FloorToInt(_cpuTapsF), _matchup);
            if (winner == null)
            {
                // 完全同点 → 再連打(疲労は1段進んだ状態で)
                _playerTaps.Reset();
                _cpuTapsF = 0f;
                _cpuCpsThisMash = _brain.EffectiveCps(_mashCount);
                _phaseTimer = _config.MashSeconds;
                return;
            }

            _roundWinner = winner;
            EnterResult();
        }

        // ---- Result ----

        private void EnterResult()
        {
            _state.AwardWin(_roundWinner.Value);
            _phase = Phase.Result;
            _phaseTimer = 2f;
        }

        private void UpdateResult()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (_state.IsOver)
            {
                if (_cpsLog.Count > 0)
                    Debug.Log($"[CPS計測] 平均 {_cpsLog.Average():F2} / 最小 {_cpsLog.Min():F2} / 最大 {_cpsLog.Max():F2} (連打{_cpsLog.Count}回)");
                _phase = Phase.MatchEnd;
            }
            else
            {
                EnterSelect();
            }
        }

        private void UpdateMatchEnd()
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
                StartMatch();
        }

        // ---- 表示用テキストの構築(Viewへ渡すだけ。描画はMatchHudViewの責務) ----

        private void RenderHud()
        {
            if (hud == null) return;

            if (Keyboard.current == null)
            {
                hud.Render(
                    "入力エラー",
                    "",
                    "Keyboardが取得できません。",
                    "Project Settings → Player → Active Input Handling を「Input System Package」か「Both」に。");
                return;
            }

            string header = $"Round {_displayRound} ／ スコア あなた {_state.WinsA} - {_state.WinsB} CPU";
            string hands = $"あなたの手札: {TileList(_state.HandA)}\nCPU の手札 : {TileList(_state.HandB)}";
            string main, sub;

            switch (_phase)
            {
                case Phase.Select:
                    main = $"◆ 選択フェーズ(残り {_phaseTimer:F0} 秒)\n{CursorView()}";
                    sub = "1〜9キー直接 / ←→で移動 / Enterで確定";
                    break;

                case Phase.Reveal:
                    main = $"◆ 公開!  あなた: {_tileA}  vs  CPU: {_tileB}";
                    sub = MatchupView();
                    break;

                case Phase.Mash:
                    main = $"◆ 連打フェーズ!(残り {_phaseTimer:F1} 秒)\nあなた: {_playerTaps.Count}打   CPU: {Mathf.FloorToInt(_cpuTapsF)}打";
                    sub = $"AとDを交互に! {MatchupView()}";
                    break;

                case Phase.Result:
                    main = $"◆ このラウンドは {(_roundWinner == Player.A ? "あなた" : "CPU")} の勝ち!";
                    sub = "";
                    break;

                case Phase.MatchEnd:
                default:
                    var w = _state.MatchWinner;
                    main = $"◆ 試合終了! 勝者: {(w == Player.A ? "あなた" : "CPU")}  ({_state.WinsA} - {_state.WinsB})";
                    sub = "Rキーで再戦";
                    break;
            }

            hud.Render(header, hands, main, sub);
        }

        private static string TileList(IReadOnlyCollection<int> hand)
            => string.Join(" ", hand.OrderBy(t => t));

        private string CursorView()
            => string.Join(" ", _handSorted.Select((t, i) => i == _cursor ? $"[{t}]" : $" {t} "));

        private string MatchupView()
        {
            if (_matchup.IsImmediate) return "大差 → 即決着";
            if (_matchup.FavoredSide == null) return "同数 → 等倍の連打勝負";
            string side = _matchup.FavoredSide == Player.A ? "あなた" : "CPU";
            return $"{side}が{_matchup.AdvantageSteps}段有利(要求倍率 ×{_matchup.RequiredRatio:F3})";
        }
    }
}
