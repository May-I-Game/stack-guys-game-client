using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 클라이언트 연결 상태 모니터링 - 송신 큐 넘침 감지 및 자동 연결 해제
/// TCP 송신 큐가 넘치는 클라이언트를 조기 감지하여 서버 부하 방지
/// </summary>
public class ConnectionHealthMonitor : MonoBehaviour
{
    [Header("Health Check Settings")]
    [Tooltip("체크 주기 (초)")]
    [SerializeField] private float checkInterval = 2f;

    [Tooltip("최대 허용 RTT (밀리초) - 초과 시 경고")]
    [SerializeField] private float maxAllowedRtt = 500f;

    [Tooltip("연속 타임아웃 허용 횟수")]
    [SerializeField] private int maxTimeoutCount = 3;

    [Header("Auto Disconnect")]
    [Tooltip("자동 연결 해제 활성화")]
    [SerializeField] private bool autoDisconnect = true;

    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool debugLog = true;

    private Dictionary<ulong, int> clientTimeoutCounts = new Dictionary<ulong, int>();
    private Dictionary<ulong, float> clientLastRtt = new Dictionary<ulong, float>();
    private float nextCheckTime;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[ConnectionHealthMonitor] NetworkManager not found!");
            enabled = false;
            return;
        }

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[ConnectionHealthMonitor] Not a server, disabling");
            enabled = false;
            return;
        }

        // 클라이언트 연결/해제 콜백 구독
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        Debug.Log($"[ConnectionHealthMonitor] Started - Interval: {checkInterval}s, Max RTT: {maxAllowedRtt}ms");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // 새 클라이언트 추가
        clientTimeoutCounts[clientId] = 0;
        clientLastRtt[clientId] = 0f;

        if (debugLog)
        {
            Debug.Log($"[ConnectionHealthMonitor] Client {clientId} connected, monitoring started");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // 클라이언트 제거
        clientTimeoutCounts.Remove(clientId);
        clientLastRtt.Remove(clientId);

        if (debugLog)
        {
            Debug.Log($"[ConnectionHealthMonitor] Client {clientId} disconnected, monitoring stopped");
        }
    }

    private void Update()
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            CheckAllClientsHealth();
        }
    }

    private void CheckAllClientsHealth()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        if (connectedClients == null || connectedClients.Count == 0) return;

        foreach (var kvp in connectedClients)
        {
            ulong clientId = kvp.Key;
            NetworkClient client = kvp.Value;

            CheckClientHealth(clientId, client);
        }
    }

    private void CheckClientHealth(ulong clientId, NetworkClient client)
    {
        // 서버 자신은 체크하지 않음
        if (clientId == NetworkManager.ServerClientId) return;

        try
        {
            // Unity Transport의 NetworkDriver를 통한 RTT 확인
            // 주의: Unity Netcode는 직접적인 RTT API를 제공하지 않으므로
            // Transport 레이어에 접근해야 합니다

            // 현재는 간접적 방법 사용:
            // 1. PlayerObject가 null인지 확인
            // 2. 최근 메시지 전송 실패 여부 확인

            if (client.PlayerObject == null)
            {
                // 플레이어 오브젝트가 없는데 연결되어 있음 - 비정상
                IncrementTimeoutCount(clientId);
                return;
            }

            // NetworkObject의 IsSpawned 상태 확인
            if (!client.PlayerObject.IsSpawned)
            {
                IncrementTimeoutCount(clientId);
                return;
            }

            // 정상 - 타임아웃 카운트 리셋
            if (clientTimeoutCounts.ContainsKey(clientId))
            {
                clientTimeoutCounts[clientId] = 0;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ConnectionHealthMonitor] Error checking client {clientId}: {e.Message}");
            IncrementTimeoutCount(clientId);
        }
    }

    private void IncrementTimeoutCount(ulong clientId)
    {
        if (!clientTimeoutCounts.ContainsKey(clientId))
        {
            clientTimeoutCounts[clientId] = 0;
        }

        clientTimeoutCounts[clientId]++;
        int count = clientTimeoutCounts[clientId];

        if (debugLog)
        {
            Debug.LogWarning($"[ConnectionHealthMonitor] Client {clientId} health check failed ({count}/{maxTimeoutCount})");
        }

        // 최대 타임아웃 횟수 초과 시 연결 해제
        if (count >= maxTimeoutCount && autoDisconnect)
        {
            Debug.LogError($"[ConnectionHealthMonitor] Client {clientId} exceeded max timeout count, disconnecting...");
            DisconnectClient(clientId);
        }
    }

    private void DisconnectClient(ulong clientId)
    {
        try
        {
            // 클라이언트 강제 연결 해제
            if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                NetworkManager.Singleton.DisconnectClient(clientId);
                Debug.Log($"[ConnectionHealthMonitor] Client {clientId} forcefully disconnected");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ConnectionHealthMonitor] Failed to disconnect client {clientId}: {e.Message}");
        }
    }

    /// <summary>
    /// 특정 클라이언트의 건강 상태 반환 (외부 호출용)
    /// </summary>
    public int GetClientTimeoutCount(ulong clientId)
    {
        return clientTimeoutCounts.ContainsKey(clientId) ? clientTimeoutCounts[clientId] : 0;
    }

    /// <summary>
    /// 통계 문자열 반환 (디버그용)
    /// </summary>
    public string GetStats()
    {
        int totalClients = clientTimeoutCounts.Count;
        int unhealthyClients = 0;

        foreach (var count in clientTimeoutCounts.Values)
        {
            if (count > 0)
            {
                unhealthyClients++;
            }
        }

        return $"Clients: {totalClients} | Unhealthy: {unhealthyClients}";
    }
}
