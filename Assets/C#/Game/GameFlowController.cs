using System.Collections.Generic;
using UnityEngine;

namespace KaniTactics.Game
{
    /// 1試合の進行役(司会者)。フェーズの生成と切り替えだけを担当し、中身の処理は各フェーズに任せる。
    ///   流れ: Phase10RoundIntro → 20Select → 30Reveal → 40Mash → 50Result → 60MatchEnd
    ///   入力はMatchInput、共有データはMatchContext、HUD文言はMatchHudPresenterに委譲する。
    /// モード:
    ///   SoloCpu     … A=プレイヤー、B=CPU(均衡AI+疲労モデル)
    ///   LocalVersus … A=P1、B=P2。選択は交互(手番外は画面を見ない運用)、連打は同時(P1: A/D、P2: ←/→)
    public sealed class GameFlowController : MonoBehaviour
    {
        public enum GameMode { SoloCpu, LocalVersus }

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

        private MatchInput _input;
        private MatchHudPresenter _presenter;
        private MatchSceneRefs _refs;
        private MatchContext _ctx;

        private readonly Dictionary<PhaseId, IMatchPhase> _phases = new Dictionary<PhaseId, IMatchPhase>();
        private IMatchPhase _current;
        private Phase20Select _select; // ポーズメニューのボタンから操作するため個別に保持

        private void Awake()
        {
            _input = new MatchInput();
            _presenter = new MatchHudPresenter(hud, color1P, color2P);
            _refs = new MatchSceneRefs
            {
                MashGauge = mashGauge,
                CrabStage = crabStage,
                RevealView = revealView,
                RoundIntroView = roundIntroView,
                CameraDirector = cameraDirector,
                HudVisibility = hudVisibility,
                PausePanel = pausePanel,
                PauseStatus = pauseStatus,
                TitleSceneName = titleSceneName,
                SeConfirm = seConfirm,
                SeTap = seTap,
                SeRoundWin = seRoundWin,
                SeMatchWin = seMatchWin,
            };
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

        private void Update()
        {
            if (_current == null) return;

            _current.Tick(Time.deltaTime);
            _presenter.Render(_ctx, _current.HudMode, _current.HudTimer, _current.CursorTile, revealView != null);
        }

        /// 試合を最初から始める。フェーズは試合ごとに新しいMatchContextで作り直す。
        private void StartMatch()
        {
            SoundManager.Instance.PlayBgm(bgmBattle);
            _ctx = new MatchContext(configAsset.ToConfig(), mode, cpuDifficulty);
            BuildPhases();

            _select.SetPaused(false); // ポーズパネルがアクティブ保存されていた場合の保険
            _current = null;          // 前の試合のフェーズはExitを呼ばずに捨てる
            ChangePhase(PhaseId.RoundIntro);
        }

        private void BuildPhases()
        {
            _phases.Clear();
            _select = new Phase20Select(_ctx, _input, _refs, ChangePhase);

            _phases[PhaseId.RoundIntro] = new Phase10RoundIntro(_ctx, _input, _refs, ChangePhase);
            _phases[PhaseId.Select]     = _select;
            _phases[PhaseId.Reveal]     = new Phase30Reveal(_ctx, _input, _refs, ChangePhase);
            _phases[PhaseId.Mash]       = new Phase40Mash(_ctx, _input, _refs, ChangePhase);
            _phases[PhaseId.Result]     = new Phase50Result(_ctx, _input, _refs, ChangePhase);
            _phases[PhaseId.MatchEnd]   = new Phase60MatchEnd(_ctx, _input, _refs, ChangePhase, StartMatch);
        }

        /// フェーズ切り替えの唯一の入口。旧フェーズのExit → 新フェーズのEnter の順に呼ぶ。
        private void ChangePhase(PhaseId next)
        {
            _current?.Exit();
            _current = _phases[next];
            _current.Enter();
        }

        // ---- ポーズメニューのボタン用 ----

        /// 「再開」ボタン用。
        public void ResumeFromPause() => _select?.SetPaused(false);

        /// 「最初から」ボタン用。試合を仕切り直す。
        public void RestartMatch()
        {
            _select?.SetPaused(false);
            StartMatch();
        }
    }
}