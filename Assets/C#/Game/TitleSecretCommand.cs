using UnityEngine;
using UnityEngine.InputSystem;
using KaniTactics.Core;

namespace KaniTactics.Game
{
    /// <summary>
    /// タイトル画面の隠しコマンド検知。AとDを交互に規定回数連打すると名人モードが解放される。
    /// 判定はゲーム本編と同じTapCounter(交互連打のみ有効)を使い回す。
    /// TitleMenuControllerと同じシーンに置き、参照を割り当てる。
    /// </summary>
    public sealed class TitleSecretCommand : MonoBehaviour
    {
        [SerializeField] private TitleMenuController title;
        [Tooltip("解放に必要な交互連打数(牌の最大数に合わせて九)")]
        [SerializeField] private int requiredTaps = 9;
        [Tooltip("この秒数以内に打ち切らないとカウントがリセットされる")]
        [SerializeField] private float timeWindow = 4f;

        private readonly TapCounter _taps = new TapCounter();
        private float _timer;

        private void Update()
        {
            if (TitleMenuController.IsMeijinUnlocked) return; // 解放済みなら何もしない

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.aKey.wasPressedThisFrame && _taps.RegisterTap(0)) { if (title != null) title.PlaySecretTap(); }
            if (kb.dKey.wasPressedThisFrame && _taps.RegisterTap(1)) { if (title != null) title.PlaySecretTap(); }

            if (_taps.Count > 0)
            {
                _timer += Time.deltaTime;
                if (_timer > timeWindow)
                {
                    _taps.Reset();
                    _timer = 0f;
                }
            }

            if (_taps.Count >= requiredTaps)
            {
                _taps.Reset();
                _timer = 0f;
                if (title != null) title.UnlockMeijin();
            }
        }
    }
}
