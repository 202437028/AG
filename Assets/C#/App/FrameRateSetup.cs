using UnityEngine;

public static class FrameRateSetup
{
    private const int TargetFps = 60;

    /// 起動時にVSyncを切り、フレームレート上限を60に固定する
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
    }
}