#if UNITY_SERVER
using UnityEngine;

/// <summary>
/// Headless 서버 최적화
/// RuntimeInitializeOnLoadMethod를 사용하여 자동으로 실행됨
/// </summary>
public static class ServerOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        OptimizeFrameRate();
        OptimizeRendering();

        Debug.Log("[ServerOptimizer] Server optimizations applied");
    }

    /// <summary>
    /// Update Rate 제한 (가장 중요한 최적화)
    /// </summary>
    private static void OptimizeFrameRate()
    {
        // FixedUpdate와 동기화 (50Hz)
        Application.targetFrameRate = 50;

        // VSync 비활성화 (Headless 서버에서 불필요)
        QualitySettings.vSyncCount = 0;

        Debug.Log($"[ServerOptimizer] Target Frame Rate: {Application.targetFrameRate} Hz");
    }

    /// <summary>
    /// 렌더링 관련 최적화 (선택적)
    /// </summary>
    private static void OptimizeRendering()
    {
        // 카메라 비활성화 (Headless 서버에서 불필요)
        DisableCameras();

        // 오디오 비활성화 (Headless 서버에서 불필요)
        DisableAudio();
    }

    private static void DisableCameras()
    {
        var cameras = Object.FindObjectsOfType<Camera>(true);
        foreach (var cam in cameras)
        {
            cam.enabled = false;
        }

        if (cameras.Length > 0)
        {
            Debug.Log($"[ServerOptimizer] Disabled {cameras.Length} cameras");
        }
    }

    private static void DisableAudio()
    {
        AudioListener.volume = 0f;

        var audioSources = Object.FindObjectsOfType<AudioSource>(true);
        foreach (var audio in audioSources)
        {
            audio.enabled = false;
        }

        if (audioSources.Length > 0)
        {
            Debug.Log($"[ServerOptimizer] Disabled {audioSources.Length} audio sources");
        }
    }
}
#endif
