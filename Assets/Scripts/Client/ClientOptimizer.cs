#if !UNITY_SERVER
using UnityEngine;

/// <summary>
/// 클라이언트 최적화
/// RuntimeInitializeOnLoadMethod를 사용하여 자동으로 실행됨
/// </summary>
public static class ClientOptimizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // 에디터에서는 무제한으로 유지 (개발 편의성)
        if (Application.isEditor)
        {
            Debug.Log("[ClientOptimizer] Skipped in Editor - FPS unlimited for development");
            return;
        }

        OptimizeFrameRate();
        Debug.Log("[ClientOptimizer] Client optimizations applied");
    }

    /// <summary>
    /// 클라이언트 Frame Rate 최적화
    /// </summary>
    private static void OptimizeFrameRate()
    {
        // 클라이언트 FPS 제한 (배터리/성능 최적화)
        // 0 = 무제한, -1 = 플랫폼 기본값
        Application.targetFrameRate = GetOptimalFrameRate();

        // VSync 설정 (0=끄기, 1=켜기)
        QualitySettings.vSyncCount = 0;

        Debug.Log($"[ClientOptimizer] Target Frame Rate: {Application.targetFrameRate} Hz, VSync: {QualitySettings.vSyncCount}");
    }

    /// <summary>
    /// 플랫폼별 최적 프레임레이트 반환
    /// </summary>
    private static int GetOptimalFrameRate()
    {
        // 모바일: 배터리 절약을 위해 60fps로 제한
        if (Application.isMobilePlatform)
        {
            // 고사양 기기: 60fps
            if (SystemInfo.systemMemorySize >= 4096)
                return 60;
            // 저사양 기기: 30fps
            else
                return 30;
        }

        // PC: 60fps로 제한 (안정적 성능 + 발열/전력 절약)
        return 60;  // 60fps 고정
    }
}
#endif
