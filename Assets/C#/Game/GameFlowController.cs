using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// 1試合ループ(RoundIntro → Select → Reveal → Mash → Result → MatchEnd)の進行役。
    /// 入力はMatchInput、共有データはMatchContext、HUD文言はMatchHudPresenterに委譲する。
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

        [Header("プレイヤー色(牌の数字色)")]
        [SerializeField] private Color color1P = new Color(0.76f, 0.15f, 0.13f); // 赤
        [SerializeField] private Color color2P = new Color(0.16f, 0.31f, 0.79f); // 青
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
        [SerializeField] private AudioClip seTap;       // 連打の打鍵1回ごと(A/D共通)
        [Tooltip("ラウンド決着の音。勝敗どちらでも鳴る")]
        [SerializeField] private AudioClip seRoundWin;
        [Tooltip("試合終了の音。勝敗どちらでも鳴る")]
        [SerializeField] private AudioClip seMatchWin;

        // 協力者
        private MatchInput _input;
        private MatchHudPresenter _presenter;
        private MatchContext _ctx;

        // 進行状態
        private Phase _phase;
        private float _phaseTimer;
        private bool _paused;
        private bool _transitioning;      // ラウンド間のフェード中
        private bool _switchingPlayer;    // ローカル2Pの手番交代フェード中

        // 選択フェーズ
        private List<int> _handSorted;
        private int _cursor;

        // 連打フェーズ
        private readonly TapCounter _tapsA = new TapCounter();
        private readonly TapCounter _tapsB = new TapCounter(); // LocalVersusのP2用
        private float _cpuTapsF;                               // SoloCpuのCPU用
        private float _cpuCpsThisMash;

        // CPS計測(SoloCpuのみ。M5のρ確定用データ)
        private readonly List<float> _cpsLog = new List<float>();

        private bool IsVersus => _ctx.IsVersus;

        private void Awake()
        {
            _input = new MatchInput();
            _input.TapP1 += key => OnTap(_tapsA, key);
            _input.TapP2 += key => OnTap(_tapsB, key);
            _presenter = new MatchHudPresenter(hud, color1P, color2P);
        }

        private void OnDestroy() => _input?.Dispose();

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
            _ctx = new MatchContext(configAsset.ToConfig(), mode, cpuDifficulty);
            _cpsLog.Clear();
            EnterRoundIntro();
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

        // ---- RoundIntro ----

        /// <summary>ラウンド開始演出(ROUND○ / FIGHT!!)を挟んでから選択フェーズへ入る。</summary>
        private void EnterRoundIntro()
        {
            _transitioning = false;
            _ctx.DisplayRound = _ctx.State.RoundNumber;
            // 手番を1Pへ戻す。この処理は暗転中に走るので、手札の色が明転前に1P基準へ切り替わる
            _ctx.Selecting = Player.A;
            _handSorted = _ctx.State.HandA.OrderBy(t => t).ToList();
            if (mashGauge != null) mashGauge.Hide();
            if (crabStage != null) crabStage.ReturnHome();
            if (cameraDirector != null) cameraDirector.EnterSelectMode();
            if (hudVisibility != null) hudVisibility.ShowAll(); // 演出で隠したUIをここで戻す
            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);

            if (roundIntroView != null)
            {
                roundIntroView.Play(_ctx.DisplayRound);
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

        // ---- Select ----

        private void EnterSelect()
        {
            _ctx.DisplayRound = _ctx.State.RoundNumber;
            BeginSelection(Player.A);
        }

        private void BeginSelection(Player who)
        {
            _ctx.Selecting = who;
            var hand = who == Player.A ? _ctx.State.HandA : _ctx.State.HandB;
            _handSorted = hand.OrderBy(t => t).ToList();
            _cursor = Random.Range(0, _handSorted.Count); // 初期カーソルはランダム(固定デフォルト牌の防止)
            _phaseTimer = _ctx.Config.SelectTimeLimitSeconds;
            _phase = Phase.Select;
            _input.SwitchMaps(select: true, mashP1: false, mashP2: false, system: false);
        }

        private void UpdateSelect()
        {
            // ポーズの開閉(選択フェーズ中のみ。連打中は「連打逃げ」防止のためPauseマップ自体が無効)
            if (_input.PausePressed)
            {
                SetPaused(!_paused);
                return;
            }
            if (_paused || _switchingPlayer) return; // ポーズ中・手番交代フェード中はタイマーも入力も止まる

            _phaseTimer -= Time.deltaTime;

            // 数字キー直接指定(手札にある牌のみ有効)
            for (int d = 1; d <= 9; d++)
            {
                if (_input.DigitPressed(d) && _handSorted.Contains(d))
                {
                    _cursor = _handSorted.IndexOf(d);
                    ConfirmSelection();
                    return;
                }
            }

            if (_input.MoveLeftPressed)
                _cursor = (_cursor - 1 + _handSorted.Count) % _handSorted.Count;
            if (_input.MoveRightPressed)
                _cursor = (_cursor + 1) % _handSorted.Count;

            // Enter確定、または時間切れ=選択中の牌で確定
            if (_input.ConfirmPressed || _phaseTimer <= 0f)
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
                    $"{_ctx.State.WinsA} - {_ctx.State.WinsB}",
                    _ctx.State.HandA, _ctx.State.HandB);
            }
            // ポーズ中は牌選択の入力を殺し、Pauseマップ(Esc)だけ生かす
            _input.SetSelectEnabled(!paused);
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
                if (_ctx.Selecting == Player.A)
                {
                    _ctx.TileA = picked;
                    // 暗転を挟んで2Pへ交代。暗転中に手札表示が1P用→2P用へ入れ替わる
                    _switchingPlayer = true;
                    _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
                    SceneLoader.Instance.FadeAction(() =>
                    {
                        BeginSelection(Player.B);
                        _switchingPlayer = false;
                    });
                    return;
                }
                _ctx.TileB = picked;
            }
            else
            {
                _ctx.TileA = picked;
                // CPUはプレイヤーの選択を知らずに残り手札だけを見て決める(同時出しの再現)
                _ctx.TileB = _ctx.Brain.SelectTile(_ctx.State, _ctx.MashCount, _ctx.CpsTracker.Estimate);
            }

            _ctx.State.ConsumeTiles(_ctx.TileA, _ctx.TileB);
            _ctx.Matchup = MatchupJudge.Judge(_ctx.TileA, _ctx.TileB, _ctx.Config);

            // 選択確定の瞬間(ふたりでは2Pの確定時)にHUDを引っ込める。
            // 牌カードが出る前に手札・PAUSEを消し、公開演出を邪魔しないようにする。
            if (hudVisibility != null) hudVisibility.HideAll();

            if (cameraDirector != null) cameraDirector.EnterBattleMode();
            _phase = Phase.Reveal;

            if (revealView != null)
            {
                var m = _ctx.Matchup;
                if (m.IsImmediate)
                {
                    // 大差決着: 勝者に1勝を先取りした状態のスコアを見せる
                    int winsA = _ctx.State.WinsA + (m.ImmediateWinner == Player.A ? 1 : 0);
                    int winsB = _ctx.State.WinsB + (m.ImmediateWinner == Player.B ? 1 : 0);
                    revealView.PlayImmediate(_ctx.TileA, _ctx.TileB, $"{winsA} - {winsB}");
                    _phaseTimer = revealView.ImmediateSeconds;
                }
                else
                {
                    string instruction = IsVersus
                        ? "AとD ←と→を\n連打しろ!!"
                        : "AとDを連打しろ!!";
                    revealView.PlayMashIntro(_ctx.TileA, _ctx.TileB, instruction);
                    _phaseTimer = revealView.MashIntroSeconds;
                }
                SoundManager.Instance.DuckForSeconds(0.5f, _phaseTimer);
            }
            else
            {
                _phaseTimer = 2f; // 従来動作(演出なし)
                SoundManager.Instance.DuckForSeconds(0.5f, 2f);
            }

            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        // ---- Reveal ----

        private void UpdateReveal()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (revealView != null) revealView.Hide();

            if (_ctx.Matchup.IsImmediate)
            {
                _ctx.RoundWinner = _ctx.Matchup.ImmediateWinner;
                EnterResult();
            }
            else
            {
                _tapsA.Reset();
                _tapsB.Reset();
                _cpuTapsF = 0f;
                if (!IsVersus)
                    _cpuCpsThisMash = _ctx.Brain.EffectiveCps(_ctx.MashCount); // 疲労込みの実効CPS
                _phaseTimer = _ctx.Config.MashSeconds;
                _phase = Phase.Mash;
                if (hudVisibility != null) hudVisibility.ShowMashOnly();
                if (mashGauge != null) mashGauge.Show();
                if (crabStage != null) crabStage.BeginClash();
                _input.SwitchMaps(select: false, mashP1: true, mashP2: IsVersus, system: false);
            }
        }

        // ---- Mash ----

        private int CurrentTapsB => IsVersus ? _tapsB.Count : Mathf.FloorToInt(_cpuTapsF);

        /// <summary>
        /// 連打1打ごとにMatchInputのイベントから呼ばれる。key: 0=左(A/←) 1=右(D/→)。
        /// 同じキーの連続はTapCounter側で弾かれる。
        /// </summary>
        private void OnTap(TapCounter counter, int key)
        {
            // 念のための二重ガード(Mapの有効/無効と合わせて、連打フェーズ外の入力を無視する)
            if (_phase != Phase.Mash || _phaseTimer <= 0f) return;
            if (counter.RegisterTap(key)) SoundManager.Instance.PlaySe(seTap);
        }

        private void UpdateMash()
        {
            // プレイヤーの打鍵はOnTap(イベント)で数え済み。ここではCPUの連打と時間経過だけを扱う
            if (!IsVersus)
                _cpuTapsF += _cpuCpsThisMash * Time.deltaTime;

            if (mashGauge != null) mashGauge.SetTarget(GaugePosition());
            if (crabStage != null) crabStage.SetTarget(GaugePosition());
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (!IsVersus)
            {
                float cps = _tapsA.Count / _ctx.Config.MashSeconds;
                _cpsLog.Add(cps);
                _ctx.CpsTracker.Record(cps);   // AIのプレイヤーCPS推定を更新
                Debug.Log($"[CPS計測] Round {_ctx.DisplayRound}: あなた {_tapsA.Count}打 = {cps:F2}cps ／ CPU実効 {_cpuCpsThisMash:F2}cps");
            }
            _ctx.MashCount++; // CPU疲労が1段進む(Versusでは未使用)

            var winner = MashResolver.Resolve(_tapsA.Count, CurrentTapsB, _ctx.Matchup);
            if (winner == null)
            {
                // 完全同点 → 再連打
                _tapsA.Reset();
                _tapsB.Reset();
                _cpuTapsF = 0f;
                if (!IsVersus)
                    _cpuCpsThisMash = _ctx.Brain.EffectiveCps(_ctx.MashCount);
                _phaseTimer = _ctx.Config.MashSeconds;
                if (mashGauge != null) mashGauge.Show(); // 中央から仕切り直し
                if (crabStage != null) crabStage.BeginClash();
                return;
            }

            _ctx.RoundWinner = winner;
            EnterResult();
        }

        /// <summary>
        /// ゲージ位置(0=B側優勢の端、1=A側優勢の端)。
        /// 勝敗判定と同じ重み(有利側の連打数×要求倍率)の比率なので、演出と結果が食い違わない。
        /// </summary>
        private float GaugePosition()
        {
            float weightedA = _tapsA.Count;
            float weightedB = CurrentTapsB;

            var m = _ctx.Matchup;
            if (m.FavoredSide == Player.A) weightedA *= m.RequiredRatio;
            else if (m.FavoredSide == Player.B) weightedB *= m.RequiredRatio;

            float total = weightedA + weightedB;
            return total <= 0f ? 0.5f : weightedA / total;
        }

        // ---- Result ----

        private void EnterResult()
        {
            _ctx.State.AwardWin(_ctx.RoundWinner.Value);
            _phase = Phase.Result;
            _phaseTimer = 2f;
            SoundManager.Instance.PlaySe(seRoundWin); // 勝敗共通
            SoundManager.Instance.DuckForSeconds(0.35f, 2f); // Resultフェーズ(2秒)の間だけ絞る
            if (mashGauge != null) mashGauge.Hide();
            if (crabStage != null) crabStage.PlayResult(_ctx.RoundWinner.Value);
            _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: false);
        }

        private void UpdateResult()
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer > 0f) return;

            if (_ctx.State.IsOver)
            {
                if (_cpsLog.Count > 0)
                    Debug.Log($"[CPS計測] 平均 {_cpsLog.Average():F2} / 最小 {_cpsLog.Min():F2} / 最大 {_cpsLog.Max():F2} (連打{_cpsLog.Count}回)");
                SoundManager.Instance.PlaySe(seMatchWin); // 勝敗共通
                SoundManager.Instance.DuckMute(); // ジングルの間はBGMを完全ミュート(次の試合開始で自動復帰)
                _phase = Phase.MatchEnd;
                _input.SwitchMaps(select: false, mashP1: false, mashP2: false, system: true);
            }
            else if (!_transitioning)
            {
                // ラウンドの繋ぎ目を暗転で挟む(暗転中に次ラウンドの準備が走る)
                _transitioning = true;
                SceneLoader.Instance.FadeAction(EnterRoundIntro);
            }
        }

        // ---- MatchEnd ----

        private void UpdateMatchEnd()
        {
            if (_input.RestartPressed)
                SceneLoader.Instance.FadeAction(StartMatch);

            if (_input.ToTitlePressed)
                SceneLoader.Instance.LoadScene(titleSceneName);
        }

        // ---- HUD(文言の組み立てはMatchHudPresenterの責務) ----

        private void RenderHud()
        {
            int? cursorTile = null;
            if (_phase == Phase.Select && !_paused && _handSorted != null && _handSorted.Count > 0)
                cursorTile = _handSorted[_cursor];

            _presenter.Render(_ctx, ToHudMode(_phase), _phaseTimer, cursorTile, revealView != null);
        }

        private static HudMode ToHudMode(Phase phase) => phase switch
        {
            Phase.Select => HudMode.Select,
            Phase.Reveal => HudMode.Reveal,
            Phase.Mash => HudMode.Mash,
            Phase.Result => HudMode.Result,
            Phase.MatchEnd => HudMode.MatchEnd,
            _ => HudMode.Blank,
        };
    }
}