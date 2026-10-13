using System;
using UnityEngine.InputSystem;

namespace KaniTactics.Game
{
    /// <summary>
    /// 試合中の入力の窓口(受付係)。
    /// KaniTacticsControlsを保持し、フェーズに応じたAction Mapの切替と、
    /// 連打の1打ごとの通知(イベント方式)を担当する。
    /// </summary>
    public sealed class MatchInput : IDisposable
    {
        /// <summary>P1の連打1打ごとに通知。引数 0=左(A) 1=右(D)</summary>
        public event Action<int> TapP1;
        /// <summary>P2の連打1打ごとに通知。引数 0=左(←) 1=右(→)</summary>
        public event Action<int> TapP2;

        private readonly KaniTacticsControls _controls;
        private readonly InputAction[] _digitActions;

        public MatchInput()
        {
            _controls = new KaniTacticsControls();

            var s = _controls.Select;
            _digitActions = new InputAction[]
            {
                s.Digit1, s.Digit2, s.Digit3, s.Digit4, s.Digit5,
                s.Digit6, s.Digit7, s.Digit8, s.Digit9
            };

            // 連打はイベント方式。1フレーム内に複数回押されても、押した順に1打ずつ届く。
            // Mapが無効なフェーズではイベント自体が飛ばない(SwitchMapsで制御)。
            _controls.MashP1.TapLeft.performed += _ => TapP1?.Invoke(0);
            _controls.MashP1.TapRight.performed += _ => TapP1?.Invoke(1);
            _controls.MashP2.TapLeft.performed += _ => TapP2?.Invoke(0);
            _controls.MashP2.TapRight.performed += _ => TapP2?.Invoke(1);
        }

        // ---- Action Mapの切替(フェーズ遷移の唯一の入力管理点) ----

        public void SwitchMaps(bool select, bool mashP1, bool mashP2, bool system)
        {
            if (select) { _controls.Select.Enable(); _controls.Pause.Enable(); }
            else { _controls.Select.Disable(); _controls.Pause.Disable(); }

            if (mashP1) _controls.MashP1.Enable(); else _controls.MashP1.Disable();
            if (mashP2) _controls.MashP2.Enable(); else _controls.MashP2.Disable();
            if (system) _controls.System.Enable(); else _controls.System.Disable();
        }

        /// <summary>ポーズ中に牌選択の入力だけを止める(Pauseマップは生かしたまま)</summary>
        public void SetSelectEnabled(bool enabled)
        {
            if (enabled) _controls.Select.Enable();
            else _controls.Select.Disable();
        }

        // ---- 選択フェーズ ----

        public bool DigitPressed(int digit) => _digitActions[digit - 1].WasPressedThisFrame();
        public bool MoveLeftPressed => _controls.Select.MoveLeft.WasPressedThisFrame();
        public bool MoveRightPressed => _controls.Select.MoveRight.WasPressedThisFrame();
        public bool ConfirmPressed => _controls.Select.Confirm.WasPressedThisFrame();
        public bool PausePressed => _controls.Pause.Pause.WasPressedThisFrame();

        // ---- 試合終了画面 ----

        public bool RestartPressed => _controls.System.Restart.WasPressedThisFrame();
        public bool ToTitlePressed => _controls.System.ToTitle.WasPressedThisFrame();

        public void Dispose()
        {
            _controls.Disable(); // 有効なままのMapを残すとリーク警告が出る
            _controls.Dispose();
        }
    }
}
