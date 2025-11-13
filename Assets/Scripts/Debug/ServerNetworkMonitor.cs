using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버 전용 네트워크 대역폭 모니터 - 로그로 통계 출력
/// ProximityManager 효과 측정 및 네트워크 최적화 분석용
/// NetworkBehaviour 대신 MonoBehaviour 사용 (NetworkManager와 같은 GameObject에 추가 가능)
///
/// 참고: Unity Netcode에서 직접 대역폭 정보를 제공하지 않으므로,
/// NetworkObject 수, 클라이언트 수, Proximity 통계 등을 기반으로 추정합니다.
/// </summary>
public class ServerNetworkMonitor : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("통계 출력 주기 (초)")]
    [SerializeField] private float logInterval = 5f;

    [Tooltip("상세 로그 출력 (Proximity 통계 포함)")]
    [SerializeField] private bool detailedLog = true;

    [Header("Bandwidth Estimation")]
    [Tooltip("NetworkObject당 예상 대역폭 (bytes/sec)")]
    [SerializeField] private float estimatedBytesPerObject = 100f;

    [Tooltip("클라이언트당 오버헤드 (bytes/sec)")]
    [SerializeField] private float estimatedBytesPerClient = 50f;

    private float nextLogTime;
    private int sampleCount;

    // 통계
    private int totalNetworkObjects;
    private int totalClients;
    private float estimatedTotalBandwidth;

    private void Start()
    {
        // NetworkManager가 초기화될 때까지 대기
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[ServerNetworkMonitor] NetworkManager not found, waiting...");
            Invoke(nameof(Initialize), 1f);
            return;
        }

        Initialize();
    }

    private void Initialize()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[ServerNetworkMonitor] NetworkManager not found!");
            enabled = false;
            return;
        }

        // 서버에서만 활성화
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[ServerNetworkMonitor] Not a server, disabling monitor");
            enabled = false;
            return;
        }

        Debug.Log("=================================================");
        Debug.Log("[ServerNetworkMonitor] Started - Logging network stats");
        Debug.Log($"[ServerNetworkMonitor] Log interval: {logInterval}s");
        Debug.Log($"[ServerNetworkMonitor] Detailed log: {detailedLog}");
        Debug.Log("[ServerNetworkMonitor] Note: Bandwidth is estimated based on NetworkObject/Client counts");
        Debug.Log("=================================================");

        nextLogTime = Time.time + logInterval;
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + logInterval;
            LogNetworkStats();
        }
    }

    private void LogNetworkStats()
    {
        if (NetworkManager.Singleton == null) return;

        sampleCount++;

        // 클라이언트 및 오브젝트 수
        int clientCount = NetworkManager.Singleton.ConnectedClients.Count;
        int networkObjectCount = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Length;

        // 대역폭 추정 (NetworkObject 수와 클라이언트 수 기반)
        // 각 NetworkObject는 모든 클라이언트에게 업데이트를 전송
        float estimatedOutgoingPerSec = (networkObjectCount * estimatedBytesPerObject) +
                                       (clientCount * estimatedBytesPerClient);
        float estimatedIncomingPerSec = clientCount * estimatedBytesPerClient;

        // KB/s로 변환
        float estimatedOutgoingKBps = estimatedOutgoingPerSec / 1024f;
        float estimatedIncomingKBps = estimatedIncomingPerSec / 1024f;

        // 누적
        totalNetworkObjects += networkObjectCount;
        totalClients += clientCount;
        estimatedTotalBandwidth += estimatedOutgoingKBps;

        // === 기본 로그 ===
        Debug.Log("=================================================");
        Debug.Log($"[ServerNetworkMonitor] Sample #{sampleCount} (Time: {Time.time:F1}s)");
        Debug.Log($"[Network]   Clients: {clientCount} | NetworkObjects: {networkObjectCount}");
        Debug.Log($"[Estimated] Out: ~{estimatedOutgoingKBps:F2} KB/s | In: ~{estimatedIncomingKBps:F2} KB/s");
        Debug.Log($"[Average]   Objects: {totalNetworkObjects / sampleCount:F1} | Clients: {totalClients / sampleCount:F1}");
        Debug.Log($"[Avg Est]   Bandwidth: ~{estimatedTotalBandwidth / sampleCount:F2} KB/s");

        // === 상세 로그 (옵션) ===
        if (detailedLog)
        {
            LogDetailedStats(clientCount, networkObjectCount);
        }

        Debug.Log("=================================================");
    }

    private void LogDetailedStats(int clientCount, int networkObjectCount)
    {
        // ProximityManager 통계
        var proximityManager = FindAnyObjectByType<NetworkProximityManager>();
        if (proximityManager != null)
        {
            string proximityStats = proximityManager.GetStats();
            Debug.Log($"[Proximity] {proximityStats}");

            // Proximity 효과 계산
            if (clientCount > 0)
            {
                // 전체 오브젝트 대비 평균 가시 오브젝트 비율로 대역폭 절감 추정
                float avgVisiblePerClient = 0f;
                foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                {
                    avgVisiblePerClient += proximityManager.GetVisibleObjectCount(id);
                }
                avgVisiblePerClient /= clientCount;

                float visibilityRatio = networkObjectCount > 0 ? avgVisiblePerClient / networkObjectCount : 1f;
                float estimatedSavings = (1f - visibilityRatio) * 100f;

                Debug.Log($"[Proximity Effect] Avg Visible: {avgVisiblePerClient:F1}/{networkObjectCount} " +
                         $"(~{estimatedSavings:F1}% bandwidth saved)");
            }
        }
        else
        {
            Debug.Log("[Proximity] NetworkProximityManager not found (disabled or not initialized)");
        }

        // 클라이언트별 추정 대역폭
        if (clientCount > 0)
        {
            float avgPerClient = (networkObjectCount * estimatedBytesPerObject + estimatedBytesPerClient) / 1024f;
            Debug.Log($"[PerClient] Estimated: ~{avgPerClient:F2} KB/s per client");
        }
    }

    /// <summary>
    /// 최종 요약 출력 (서버 종료 시 또는 컴포넌트 제거 시)
    /// </summary>
    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || sampleCount == 0) return;

        Debug.Log("=================================================");
        Debug.Log("[ServerNetworkMonitor] ===== FINAL SUMMARY =====");
        Debug.Log($"[Duration]      {sampleCount * logInterval:F1} seconds ({sampleCount} samples)");
        Debug.Log($"[Avg Objects]   {totalNetworkObjects / sampleCount:F1} NetworkObjects");
        Debug.Log($"[Avg Clients]   {totalClients / sampleCount:F1} Clients");
        Debug.Log($"[Avg Bandwidth] ~{estimatedTotalBandwidth / sampleCount:F2} KB/s (estimated)");

        // Proximity 최종 통계
        var proximityManager = FindAnyObjectByType<NetworkProximityManager>();
        if (proximityManager != null)
        {
            string finalStats = proximityManager.GetStats();
            Debug.Log($"[Final Proximity] {finalStats}");
        }

        Debug.Log("=================================================");
    }
}
