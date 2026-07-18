using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 1試合ループ(Select → Reveal → Mash → Result)の進行と状態管理。表示はMatchHudViewに委譲。
    /// モード:
    ///   SoloCpu     … A=プレイヤー、B=CPU(均衡AI+疲労モデル)
    ///   LocalVersus … A=P1、B=P2。選択は交互(手番外は画面を見ない運用)、連打は同時(P1: A/D、P2: ←/→)
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public enum GameMode { SoloCpu, LocalVersus }

        private enum Phase { Select, Reveal, Mash, Result, MatchEnd }

        [SerializeField] private RuleConfigAsset configAsset;
        [SerializeField] private MatchHudView hud;
        [SerializeField] private MashGaugeView mashGauge;
        [SerializeField] private CrabClashView crabStage;
        [SerializeField] private GameMode mode = GameMode.SoloCpu;
        [Tooltip("SoloCpu時のみ使用。0=イージー(5cps) 1=ノーマル(7) 2=ハード(9) 3=名人(16)")]
        [SerializeField, Range(0, 3)] private int cpuDifficulty = 1;
        [Tooltip("ポーズメニューのルートオブジェクト(BAMBOO OF CHICKEN移植分)。選択フェーズ中のみ開ける")]
        [SerializeField] private GameObject pausePanel;
        [Tooltip("ポーズ画面の戦績表示(任意)")]
        [SerializeField] private PauseStatusView pauseStatus;

        private bool _paused;

        private RuleConfig _config;
        private MatchState _state;
        private Phase _phase;
        private float _phaseTimer;
        private int _displayRound;

        private bool IsVersus => mode == GameMode.LocalVersus;

        // 入力(Action Map)
        private KaniTacticsControls _controls;
        private InputAction[] _digitActions;

        // 選択フェーズ
        private Player _selecting; // いま選んでいる側(LocalVersusで交互になる)
        private List<int> _handSorted;
        private int _cursor;

        // ラウンドデータ
        private int _tileA, _tileB;
        private MatchupResult _matchup;
        private Player? _roundWinner;

        // 連打フェーズ
        private readonly TapCounter _tapsA = new TapCounter();
        private readonly TapCounter _tapsB = new TapCounter(); // LocalVersusのP2用
        private float _cpuTapsF;                               // SoloCpuのCPU用

        // CPS計測(SoloCpuのみ。M5のρ確定用データ)
        private readonly List<float> _cpsLog = new List<float>();

        // CPU AI(SoloCpuのみ)
        private CpuBrain _brain;
        private CpsTracker _cpsTracker;
        private int _mashCount;
        private float _cpuCpsThisMash;

        private void Awake()
        {
            _controls = new KaniTacticsControls();
            var s = _controls.Select;
            _digitActions = new InputAction[]
            {
                s.Digit1, s.Digit2, s.Digit3, s.Digit4, s.Digit5,
                s.Digit6, s.Digit7, s.Digit8, s.Digit9
            };
        }

        private void OnDestroy() => _controls?.Dispose();

        private void Start() => StartMatch();

        private void StartMatch()
        {
            _config = configAsset.ToConfig();
            _state = new MatchState(_config);
            if (!IsVersus)
            {
                _brain = new CpuBrain(_config, cpuDifficulty, new System.Random());
                _cpsTracker = new CpsTracker(_config.PlayerCpsPrior);
            }
            _mashCount = 0;
            _cpsLog.Clear();
            EnterSelect();
        }

        // ---- Action Mapの切替(フェーズ遷移の唯一の入力管理点) ----

        private void SwitchMaps(bool select, bool mashP1, bool mashP2, bool system)
        {
            if (select) { _controls.Select.Enable(); _controls.Pause.Enable(); }
            else { _controls.Select.Disable(); _controls.Pause.Disable(); }

            if (mashP1) _controls.MashP1.Enable(); else _controls.MashP1.Disable();
            if (mashP2) _controls.MashP2.Enable(); else _controls.MashP2.Disable();
            if (system) _controls.System.Enable(); else _controls.System.Disable();
        }

        private void EnterSelect()
        {
            _displayRound = _state.RoundNumber;
            if (mashGauge != null) mashGauge.Hide();
            if (crabStage != null) crabStage.ReturnHome();
            BeginSelection(Player.A);
        }

        private void BeginSelection(Player who)
        {
            _selecting = who;
            var hand = who == Player.A ? _state.HandA : _state.HandB;
            _handSorted = hand.OrderBy(t => t).ToList();
            _cursor = Random.Range(0, _handSorted.Count); // 初期カーソルはランダム(固定デフォルト牌の防止)
            _phaseTimer = _config.SelectTimeLimitSeconds;
            _phase = Phase.Select;
            SwitchMaps(select: true, mashP1: false, mashP2: false, system: false);
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Select: UpdateSelect(); break;
                case Phase.Reveal: UpdateReveal(); break;
                case Phase.Mash: UpdateMash(); break;
                case Phase.Result: UpdateResult(); break;
                case Phase.MatchEnd: UpdateMatchEnd(); break;
            }

            RenderHud();
        }

        // ---- Select ----

        private void UpdateSelect()
        {
            // ポーズの開閉(選択フェーズ中のみ。連打中は「連打逃げ」防止のためPauseマップ自体が無効)
            if (_controls.Pause.Pause.WasPressedThisFrame())
            {
                SetPaused(!_paused);
                return;
            }
            if (_paused) return; // ポーズ中はタイマーも入力も止まる

            var sel = _controls.Select;
            _phaseTimer -= Time.deltaTime;

            // 数字キー直接指定(手札にある牌のみ有効)
            for (int d = 1; d <= 9; d++)
            {
                if (_digitActions[d - 1].WasPressedThisFrame() && _handSorted.Contains(d))
                {
                    _cursor = _handSorted.IndexOf(d);
                    ConfirmSelection();
                    return;
                }
            }

            if (sel.MoveLeft.WasPressedThisFrame())
                _cursor = (_cursor - 1 + _handSorted.Count) % _handSorted.Count;
            if (sel.MoveRight.WasPressedThisFrame())
                _cursor = (_cursor + 1) % _handSorted.Count;

            // Enter確定、または時間切れ=選択中の牌で確定
            if (sel.Confirm.WasPressedThisFrame() || _phaseTimer <= 0f)
                ConfirmSelection();
        }

        /// <summary>ポーズ状態の切替。UIボタン(再開)からはResumeFromPauseを呼ぶ。</summary>
        private void SetPaused(bool paused)
        {
            _paused = paused;
            if (pausePanel != null) pausePanel.SetActive(paused);
            if (paused && pauseStatus != null)
            {
                pauseStatus.Render(
                    $"{NameOf(Player.A)} {_state.WinsA} - {_state.WinsB} {NameOf(Player.B)}",
                    _state.HandA, _state.HandB);
            }
            // ポーズ中は牌選択の入力を殺し、Pauseマップ(Esc)だけ生かす
            if (paused) _controls.Select.Disable();
            else _controls.Select.Enable();
        }

        /// <summary>ポーズメニューの「再開」ボタン用。</summary>
        public void ResumeFromPause() => SetPaused(false);

        /// <summary>ポーズメニューの「最初から」ボタン用。試合を仕切り直す。</summary>
        public void RestartMatch()
        {
            SetPaused(false);
            StartMatch();
        }

        private void ConfirmSelection()
        {
            int picked = _handSorted[_cursor];

            if (IsVersus)
            {
                if (_selecting == Player.A)
                {
                    _tileA = picked;
                    BeginSelection(Player.B); // 交代してP2の選択へ
                    return;
                }
                _tileB = picked;
            }
            else
            {
                _tileA = picked;
                // CPUはプレイヤーの選択を知らずに残り手札だけを見て決める(同時出しの再現)
                _tileB = _brain.SelectTile(_state, _mashCount, _cpsTracker.Estimate);
            }

            _state.ConsumeTiles(_tileA, _tileB);
            _matchup = MatchupJudge.Judge(_tileA, _tileB, _config);

            _phase = Phase.Reveal;
            _phaseTimer = 2f; // 公開を2秒見せて自動遷移
            SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
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
                _tapsA.Reset();
                _tapsB.Reset();
                _cpuTapsF = 0f;
                if (!IsVersus)
                    _cpuCpsThisMash = _brain.EffectiveCps(_mashCount); // 疲労込みの実効CPS
                _phaseTimer = _config.MashSeconds;
                _phase = Phase.Mash;
                if (mashGauge != null) mashGauge.Show();
                if (crabStage != null) crabStage.BeginClash();
                SwitchMaps(select: false, mashP1: true, mashP2: IsVersus, system: false);
            }
        }

        // ---- Mash ----

        private int CurrentTapsB => IsVersus ? _tapsB.Count : Mathf.FloorToInt(_cpuTapsF);

        private void UpdateMash()
        {
            var p1 = _controls.MashP1;
            if (p1.TapLeft.WasPressedThisFrame()) _tapsA.RegisterTap(0);
            if (p1.TapRight.WasPressedThisFrame()) _tapsA.RegisterTap(1);

            if (IsVersus)
            {
                var p2 = _controls.MashP2;
                if (p2.TapLeft.WasPressedThisFrame()) _tapsB.RegisterTap(0);
                if (p2.TapRight.WasPressedThisFrame()) _tapsB.RegisterTap(1);
            }
            else
            {
                _cpuTapsF += _cpuCpsThisMash * Time.deltaTime;
            }

            if (mashGauge != null) mashGauge.SetTarget(GaugePosition());
            if (crabStage != null) crabStage.SetTarget(GaugePosition());
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (!IsVersus)
            {
                float cps = _tapsA.Count / _config.MashSeconds;
                _cpsLog.Add(cps);
                _cpsTracker.Record(cps);   // AIのプレイヤーCPS推定を更新
                Debug.Log($"[CPS計測] Round {_displayRound}: あなた {_tapsA.Count}打 = {cps:F2}cps ／ CPU実効 {_cpuCpsThisMash:F2}cps");
            }
            _mashCount++; // CPU疲労が1段進む(Versusでは未使用)

            var winner = MashResolver.Resolve(_tapsA.Count, CurrentTapsB, _matchup);
            if (winner == null)
            {
                // 完全同点 → 再連打
                _tapsA.Reset();
                _tapsB.Reset();
                _cpuTapsF = 0f;
                if (!IsVersus)
                    _cpuCpsThisMash = _brain.EffectiveCps(_mashCount);
                _phaseTimer = _config.MashSeconds;
                if (mashGauge != null) mashGauge.Show(); // 中央から仕切り直し
                if (crabStage != null) crabStage.BeginClash();
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
            if (mashGauge != null) mashGauge.Hide();
            if (crabStage != null) crabStage.PlayResult(_roundWinner.Value);
            SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        /// <summary>
        /// ゲージ位置(0=B側優勢の端、1=A側優勢の端)。
        /// 勝敗判定と同じ重み(有利側の連打数×要求倍率)の比率なので、演出と結果が食い違わない。
        /// </summary>
        private float GaugePosition()
        {
            float weightedA = _tapsA.Count;
            float weightedB = CurrentTapsB;

            if (_matchup.FavoredSide == Player.A) weightedA *= _matchup.RequiredRatio;
            else if (_matchup.FavoredSide == Player.B) weightedB *= _matchup.RequiredRatio;

            float total = weightedA + weightedB;
            return total <= 0f ? 0.5f : weightedA / total;
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
                SwitchMaps(select: false, mashP1: false, mashP2: false, system: true);
            }
            else
            {
                EnterSelect();
            }
        }

        private void UpdateMatchEnd()
        {
            if (_controls.System.Restart.WasPressedThisFrame())
                StartMatch();
        }

        // ---- 表示用テキストの構築(Viewへ渡すだけ。描画はMatchHudViewの責務) ----

        private string NameOf(Player p)
            => IsVersus ? (p == Player.A ? "P1" : "P2")
                        : (p == Player.A ? "あなた" : "CPU");

        private void RenderHud()
        {
            if (hud == null) return;

            string header = $"Round {_displayRound} / スコア {NameOf(Player.A)} {_state.WinsA} - {_state.WinsB} {NameOf(Player.B)}";
            string hands = $"{NameOf(Player.A)}の手札: {TileList(_state.HandA)}\n{NameOf(Player.B)}の手札: {TileList(_state.HandB)}";
            string main, sub;

            switch (_phase)
            {
                case Phase.Select:
                    main = $"◆ {NameOf(_selecting)} の選択フェーズ(残り {_phaseTimer:F0} 秒)\n{CursorView()}";
                    sub = IsVersus
                        ? "1-9キー直接 / ←→で移動 / Enterで確定(相手は画面から目を離すこと!)"
                        : "1-9キー直接 / ←→で移動 / Enterで確定";
                    break;

                case Phase.Reveal:
                    main = $"◆ 公開!  {NameOf(Player.A)}: {_tileA}  vs  {NameOf(Player.B)}: {_tileB}";
                    sub = MatchupView();
                    break;

                case Phase.Mash:
                    // 連打数は表示しない(ブラックボックス化)。状況はゲージだけで伝える
                    main = $"◆ 連打フェーズ!(残り {_phaseTimer:F1} 秒)";
                    sub = IsVersus
                        ? $"P1: A/D、P2: ←/→ を交互に! {MatchupView()}"
                        : $"AとDを交互に! {MatchupView()}";
                    break;

                case Phase.Result:
                    main = $"◆ このラウンドは {NameOf(_roundWinner.Value)} の勝ち!";
                    sub = "";
                    break;

                case Phase.MatchEnd:
                default:
                    var w = _state.MatchWinner;
                    main = $"◆ 試合終了! 勝者: {(w.HasValue ? NameOf(w.Value) : "引き分け")}  ({_state.WinsA} - {_state.WinsB})";
                    sub = "Rキーで再戦";
                    break;
            }

            hud.Render(header, hands, main, sub);

            // 牌の画像表示(TileRowView割り当て時のみ有効)。選択中はカーソル位置を強調
            int? cursorTile = null;
            if (_phase == Phase.Select && !_paused && _handSorted != null && _handSorted.Count > 0)
                cursorTile = _handSorted[_cursor];
            hud.RenderTileRows(_state.HandA, _state.HandB, cursorTile, _selecting);
        }

        private static string TileList(IReadOnlyCollection<int> hand)
            => string.Join(" ", hand.OrderBy(t => t));

        private string CursorView()
            => string.Join(" ", _handSorted.Select((t, i) => i == _cursor ? $"[{t}]" : $" {t} "));

        private string MatchupView()
        {
            if (_matchup.IsImmediate) return "大差 → 即決着";
            if (_matchup.FavoredSide == null) return "同数 → 等倍の連打勝負";
            return $"{NameOf(_matchup.FavoredSide.Value)}が{_matchup.AdvantageSteps}段有利(要求倍率 ×{_matchup.RequiredRatio:F3})";
        }
    }
}
