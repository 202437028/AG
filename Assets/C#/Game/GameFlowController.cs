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

        private enum Phase { RoundIntro, Select, Reveal, Mash, Result, MatchEnd }

        [SerializeField] private RuleConfigAsset configAsset;
        [SerializeField] private MatchHudView hud;
        [SerializeField] private MashGaugeView mashGauge;
        [SerializeField] private CrabClashView crabStage;
        [Tooltip("公開フェーズの演出(任意。未割り当てなら従来通り2秒の簡易表示)")]
        [SerializeField] private RevealView revealView;
        [Tooltip("ラウンド開始演出 ROUND○/FIGHT!!(任意)")]
        [SerializeField] private RoundIntroView roundIntroView;
        [Tooltip("カメラ演出(任意)。選択フェーズだけカメラが動く")]
        [SerializeField] private CameraDirector cameraDirector;
        [Tooltip("フェーズごとのHUD表示制御(任意)")]
        [SerializeField] private HudVisibilityDirector hudVisibility;
        [SerializeField] private GameMode mode = GameMode.SoloCpu;
        [Tooltip("SoloCpu時のみ使用。0=イージー(5cps) 1=ノーマル(7) 2=ハード(9) 3=名人(16)")]
        [SerializeField, Range(0, 3)] private int cpuDifficulty = 1;
        [Tooltip("ポーズメニューのルートオブジェクト(BAMBOO OF CHICKEN移植分)。選択フェーズ中のみ開ける")]
        [SerializeField] private GameObject pausePanel;
        [Tooltip("ポーズ画面の戦績表示(任意)")]
        [SerializeField] private PauseStatusView pauseStatus;
        [Tooltip("試合終了画面からEscで戻るタイトルシーン名")]
        [SerializeField] private string titleSceneName = "Title";

        [Header("サウンド(未割り当ての音は鳴らないだけ)")]
        [SerializeField] private AudioClip bgmBattle;
        [SerializeField] private AudioClip seConfirm;   // 牌決定
        [SerializeField] private AudioClip seReveal;    // 公開
        [SerializeField] private AudioClip seTap;       // 連打の打鍵1回ごと(A/D共通)
        [SerializeField] private AudioClip seRoundWin;  // ラウンド勝利(P1視点)
        [SerializeField] private AudioClip seRoundLose; // ラウンド敗北(P1視点)
        [SerializeField] private AudioClip seMatchWin;  // 試合勝利(P1視点)
        [SerializeField] private AudioClip seMatchLose; // 試合敗北(P1視点)

        private bool _paused;
        private bool _transitioning; // ラウンド間のフェード中

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

        private void OnDestroy()
        {
            if (_controls == null) return;
            _controls.Disable(); // 有効なままのMapを残すとリーク警告が出る
            _controls.Dispose();
        }

        private void Start()
        {
            // タイトル画面経由なら選択されたモード・難易度で上書き
            // (エディタでGameシーンを直接再生した場合はインスペクタ値のまま)
            if (MatchSettings.Configured)
            {
                mode = MatchSettings.Mode;
                cpuDifficulty = MatchSettings.CpuDifficulty;
            }
            StartMatch();
        }

        private void StartMatch()
        {
            SetPaused(false); // ポーズパネルがアクティブ保存されていた場合の保険
            SoundManager.Instance.PlayBgm(bgmBattle);
            _config = configAsset.ToConfig();
            _state = new MatchState(_config);
            if (!IsVersus)
            {
                _brain = new CpuBrain(_config, cpuDifficulty, new System.Random());
                _cpsTracker = new CpsTracker(_config.PlayerCpsPrior);
            }
            _mashCount = 0;
            _cpsLog.Clear();
            EnterRoundIntro();
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

        /// <summary>ラウンド開始演出(ROUND○ / FIGHT!!)を挟んでから選択フェーズへ入る。</summary>
        private void EnterRoundIntro()
        {
            _transitioning = false;
            _displayRound = _state.RoundNumber;
            if (mashGauge != null) mashGauge.Hide();
            if (crabStage != null) crabStage.ReturnHome();
            if (cameraDirector != null) cameraDirector.EnterSelectMode();
            if (hudVisibility != null) hudVisibility.ShowAll(); // 演出で隠したUIをここで戻す
            SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);

            if (roundIntroView != null)
            {
                roundIntroView.Play(_displayRound);
                _phaseTimer = roundIntroView.TotalSeconds;
                _phase = Phase.RoundIntro;
            }
            else
            {
                EnterSelect(); // 演出未割り当てならそのまま選択へ
            }
        }

        private void UpdateRoundIntro()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;
            if (roundIntroView != null) roundIntroView.Hide();
            EnterSelect();
        }

        private void EnterSelect()
        {
            _displayRound = _state.RoundNumber;
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
                case Phase.RoundIntro: UpdateRoundIntro(); break;
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
                    $"{_state.WinsA} - {_state.WinsB}",
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
            SoundManager.Instance.PlaySe(seConfirm);
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

            SoundManager.Instance.PlaySe(seReveal);
            if (cameraDirector != null) cameraDirector.EnterBattleMode();
            _phase = Phase.Reveal;

            if (revealView != null)
            {
                if (_matchup.IsImmediate)
                {
                    // 大差決着: 勝者に1勝を先取りした状態のスコアを見せる
                    int winsA = _state.WinsA + (_matchup.ImmediateWinner == Player.A ? 1 : 0);
                    int winsB = _state.WinsB + (_matchup.ImmediateWinner == Player.B ? 1 : 0);
                    revealView.PlayImmediate(_tileA, _tileB, $"{winsA} - {winsB}");
                    _phaseTimer = revealView.ImmediateSeconds;
                }
                else
                {
                    string instruction = IsVersus
                        ? "AとD ←と→を\n連打しろ!!"
                        : "AとDを連打しろ!!";
                    revealView.PlayMashIntro(_tileA, _tileB, instruction);
                    _phaseTimer = revealView.MashIntroSeconds;
                }
                SoundManager.Instance.DuckForSeconds(0.5f, _phaseTimer);
            }
            else
            {
                _phaseTimer = 2f; // 従来動作(演出なし)
                SoundManager.Instance.DuckForSeconds(0.5f, 2f);
            }

            SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        // ---- Reveal ----

        private void UpdateReveal()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (revealView != null) revealView.Hide();

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
                if (hudVisibility != null) hudVisibility.ShowMashOnly();
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
            if (p1.TapLeft.WasPressedThisFrame() && _tapsA.RegisterTap(0)) SoundManager.Instance.PlaySe(seTap);
            if (p1.TapRight.WasPressedThisFrame() && _tapsA.RegisterTap(1)) SoundManager.Instance.PlaySe(seTap);

            if (IsVersus)
            {
                var p2 = _controls.MashP2;
                if (p2.TapLeft.WasPressedThisFrame() && _tapsB.RegisterTap(0)) SoundManager.Instance.PlaySe(seTap);
                if (p2.TapRight.WasPressedThisFrame() && _tapsB.RegisterTap(1)) SoundManager.Instance.PlaySe(seTap);
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
            SoundManager.Instance.PlaySe(_roundWinner == Player.A ? seRoundWin : seRoundLose);
            SoundManager.Instance.DuckForSeconds(0.35f, 2f); // Resultフェーズ(2秒)の間だけ絞る
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
                SoundManager.Instance.PlaySe(_state.MatchWinner == Player.A ? seMatchWin : seMatchLose);
                SoundManager.Instance.DuckMute(); // 試合終了ジングルの間はBGMを完全ミュート(次の試合開始で自動復帰)
                _phase = Phase.MatchEnd;
                SwitchMaps(select: false, mashP1: false, mashP2: false, system: true);
            }
            else if (!_transitioning)
            {
                // ラウンドの繋ぎ目を暗転で挟む(暗転中に次ラウンドの準備が走る)
                _transitioning = true;
                SceneLoader.Instance.FadeAction(EnterRoundIntro);
            }
        }

        private void UpdateMatchEnd()
        {
            if (_controls.System.Restart.WasPressedThisFrame())
                SceneLoader.Instance.FadeAction(StartMatch);

            if (_controls.System.ToTitle.WasPressedThisFrame())
                SceneLoader.Instance.LoadScene(titleSceneName);
        }

        // ---- 表示用テキストの構築(Viewへ渡すだけ。描画はMatchHudViewの責務) ----

        private string NameOf(Player p)
            => IsVersus ? (p == Player.A ? "P1" : "P2")
                        : (p == Player.A ? "あなた" : "CPU");

        private void RenderHud()
        {
            if (hud == null) return;

            string header = $"Round {_displayRound} / スコア {NameOf(Player.A)} {_state.WinsA} - {_state.WinsB} {NameOf(Player.B)}";
            // 手札表示は「今選んでいない側=相手」のみ。自分の手札は選択カーソル(MainText/左下)で見えるため重複させない。
            // ローカル2Pでは選択者が交代するたびに、公開される手札も自動で入れ替わる。
            Player opponent = _selecting == Player.A ? Player.B : Player.A;
            var opponentHand = opponent == Player.A ? _state.HandA : _state.HandB;
            string hands = $"{NameOf(opponent)}の手札: {TileList(opponentHand)}";
            string main, sub;

            switch (_phase)
            {
                case Phase.RoundIntro:
                    main = "";
                    sub = "";
                    break;

                case Phase.Select:
                    // MainTextは残り時間のみ
                    main = $"{Mathf.CeilToInt(Mathf.Max(_phaseTimer, 0f))}";
                    sub = IsVersus
                        ? $"{NameOf(_selecting)} の番 / 1-9キー・←→で選び Enterで確定(相手は画面から目を離すこと!)"
                        : "1-9キー直接 / ←→で移動 / Enterで確定";
                    break;

                case Phase.Reveal:
                    // 演出View割り当て時は牌・見出し・カウントダウンをそちらが表示するため、HUDは空にする
                    main = "";
                    sub = revealView != null ? "" : MatchupView();
                    break;

                case Phase.Mash:
                    // MainTextは残り時間のみ。連打数は出さない(ブラックボックス化)
                    main = $"{_phaseTimer:F1}";
                    sub = IsVersus
                        ? $"P1: A/D、P2: ←/→ を交互に! {MatchupView()}"
                        : $"AとDを交互に! {MatchupView()}";
                    break;

                case Phase.Result:
                    main = "";
                    sub = $"このラウンドは {NameOf(_roundWinner.Value)} の勝ち!";
                    break;

                case Phase.MatchEnd:
                default:
                    var w = _state.MatchWinner;
                    main = "";
                    sub = $"試合終了! 勝者: {(w.HasValue ? NameOf(w.Value) : "引き分け")}  ({_state.WinsA} - {_state.WinsB})\nRキーで再戦 / Escでタイトルへ";
                    break;
            }

            hud.Render(header, hands, main, sub);

            // 牌の列で表示する場合の更新(未割り当てなら何も起きない)。選択中はカーソル位置を強調
            int? cursorTile = null;
            if (_phase == Phase.Select && !_paused && _handSorted != null && _handSorted.Count > 0)
                cursorTile = _handSorted[_cursor];
            var selfHand = _selecting == Player.A ? _state.HandA : _state.HandB;
            hud.RenderTileRows(selfHand, opponentHand, cursorTile);
        }

        private static string TileList(IReadOnlyCollection<int> hand)
            => string.Join(" ", hand.OrderBy(t => t));

        private string MatchupView()
        {
            if (_matchup.IsImmediate) return "大差 → 即決着";
            if (_matchup.FavoredSide == null) return "同数 → 等倍の連打勝負";
            return $"{NameOf(_matchup.FavoredSide.Value)}が{_matchup.AdvantageSteps}段有利(要求倍率 ×{_matchup.RequiredRatio:F3})";
        }
    }
}
