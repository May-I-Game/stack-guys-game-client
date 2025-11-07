using UnityEngine;

/// <summary>
/// 서버 프로파일러 자동 실행 매니저
/// GameScene에 빈 GameObject에 이 컴포넌트를 추가하면 자동으로 프로파일링 시작
/// </summary>
public class ServerProfilerManager : MonoBehaviour
{
    [Header("Profiler Settings")]
    [Tooltip("서버에서만 프로파일링 활성화")]
    public bool enableOnServerOnly = true;

    [Tooltip("프로파일링 활성화 여부")]
    public bool isEnabled = true;

    [Tooltip("로그 출력 간격 (초)")]
    public float logInterval = 5f;

    [Tooltip("로그 파일 경로 (비어있으면 자동 생성)")]
    public string logFilePath = @"D:\";

    void Start()
    {
        // 서버에서만 실행
        if (enableOnServerOnly && !Application.isBatchMode)
        {
            Debug.Log("[ServerProfilerManager] 클라이언트에서는 프로파일링 비활성화");
            enabled = false;
            return;
        }

        if (!isEnabled)
        {
            Debug.Log("[ServerProfilerManager] 프로파일링 비활성화됨");
            enabled = false;
            return;
        }

        // 프로파일러 초기화
        ServerPerformanceProfiler.Initialize(isEnabled, logInterval, logFilePath);

        Debug.Log($"[ServerProfilerManager] 서버 프로파일링 시작 (간격: {logInterval}초)");
    }

    void Update()
    {
        // 주기적으로 로그 출력
        ServerPerformanceProfiler.LogStats();
    }

    void OnDestroy()
    {
        // 종료 시 마지막 통계 출력
        ServerPerformanceProfiler.LogStats();
        Debug.Log("[ServerProfilerManager] 프로파일링 종료");
    }
}
