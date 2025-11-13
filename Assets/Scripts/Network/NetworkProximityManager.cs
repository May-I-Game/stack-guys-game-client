using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network Visibility 관리 - 거리 기반으로 플레이어 동기화 범위 제한
/// 가까운 플레이어만 동기화하여 네트워크 대역폭과 클라이언트 처리 부하 감소
/// </summary>
public class NetworkProximityManager : MonoBehaviour
{
    [Header("Visibility Settings")]
    [Tooltip("이 거리 내의 플레이어만 동기화 (미터)")]
    [SerializeField] private float visibilityRange = 80f;

    [Tooltip("가시성 체크 주기 (초) - 낮을수록 정확하지만 CPU 사용량 증가")]
    [SerializeField] private float updateInterval = 0.5f;

    [Tooltip("가시성 변경 시 히스테리시스 거리 (미터) - 깜빡임 방지")]
    [SerializeField] private float hysteresisDistance = 5f;

    [Header("Performance Settings")]
    [Tooltip("프레임당 처리할 최대 가시성 업데이트 수 (0 = 무제한)")]
    [SerializeField] private int maxUpdatesPerFrame = 50;

    [Tooltip("봇 플레이어에 대한 가시성 관리 활성화")]
    [SerializeField] private bool manageBotVisibility = true;

    [Header("Debug")]
    [Tooltip("가시성 변경 로그 출력")]
    [SerializeField] private bool debugLog = true;

    [Tooltip("에디터에서 가시성 범위 기즈모 표시")]
    [SerializeField] private bool showGizmos = true;

    private float nextUpdateTime;
    private Dictionary<ulong, HashSet<ulong>> clientVisibilityCache = new();
    private List<NetworkObject> allNetworkObjects = new();
    private int updateQueueIndex = 0;

    private void Start()
    {
        // NetworkManager가 초기화될 때까지 대기
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[NetworkProximityManager] NetworkManager not found, waiting...");
            Invoke(nameof(Initialize), 1f);
            return;
        }

        Initialize();
    }

    private void Initialize()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkProximityManager] NetworkManager not found!");
            enabled = false;
            return;
        }

        // 서버에서만 활성화
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[NetworkProximityManager] Not a server, disabling");
            enabled = false;
            return;
        }

        // 새 클라이언트 접속 시 기존 오브젝트를 보이게 설정
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        Debug.Log($"[NetworkProximityManager] Started - Range: {visibilityRange}m, Interval: {updateInterval}s");
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    /// <summary>
    /// 새 클라이언트가 접속하면 모든 기존 NetworkObject를 보이게 설정
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 모든 NetworkObject를 새 클라이언트에게 보이게 설정
        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in allPlayers)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                try
                {
                    netObj.NetworkShow(clientId);
                }
                catch (System.Exception e)
                {
                    if (debugLog)
                    {
                        Debug.LogWarning($"[Proximity] Failed to show object {netObj.NetworkObjectId} to new client {clientId}: {e.Message}");
                    }
                }
            }
        }

        if (debugLog)
        {
            Debug.Log($"[NetworkProximityManager] New client {clientId} connected, initialized visibility");
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            UpdatePlayerVisibility();
        }
    }

    /// <summary>
    /// 모든 클라이언트에 대해 가시성 업데이트
    /// </summary>
    private void UpdatePlayerVisibility()
    {
        // 모든 NetworkObject 수집 (플레이어 + 봇)
        allNetworkObjects.Clear();
        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                // 봇 제외 옵션 처리
                if (!manageBotVisibility)
                {
                    var botIdentity = player.GetComponent<NetworkBotIdentity>();
                    if (botIdentity != null && botIdentity.IsBot)
                        continue;
                }

                allNetworkObjects.Add(netObj);
            }
        }

        if (allNetworkObjects.Count == 0) return;

        var clients = NetworkManager.Singleton.ConnectedClientsList;
        int updatesThisFrame = 0;

        // 각 클라이언트에 대해 가시성 계산
        for (int i = 0; i < clients.Count; i++)
        {
            // 프레임당 업데이트 제한 (순환 처리)
            if (maxUpdatesPerFrame > 0 && updatesThisFrame >= maxUpdatesPerFrame)
            {
                updateQueueIndex = (updateQueueIndex + 1) % clients.Count;
                break;
            }

            int clientIndex = (updateQueueIndex + i) % clients.Count;
            var client = clients[clientIndex];

            if (client.PlayerObject == null) continue;

            UpdateVisibilityForClient(client);
            updatesThisFrame++;
        }

        updateQueueIndex = (updateQueueIndex + updatesThisFrame) % Mathf.Max(clients.Count, 1);
    }

    /// <summary>
    /// 특정 클라이언트에 대한 가시성 업데이트
    /// </summary>
    private void UpdateVisibilityForClient(NetworkClient client)
    {
        ulong clientId = client.ClientId;

        // 연결 상태 확인 - 연결되지 않은 클라이언트는 스킵
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            return;
        }

        Vector3 clientPosition = client.PlayerObject.transform.position;

        // 캐시 초기화
        if (!clientVisibilityCache.ContainsKey(clientId))
        {
            clientVisibilityCache[clientId] = new HashSet<ulong>();
        }

        var visibleObjects = clientVisibilityCache[clientId];
        var newVisibleObjects = new HashSet<ulong>();

        // 각 NetworkObject에 대해 가시성 판단
        foreach (var netObj in allNetworkObjects)
        {
            if (netObj == null || !netObj.IsSpawned) continue;

            ulong targetId = netObj.NetworkObjectId;

            // 자기 자신은 항상 보임
            if (netObj.OwnerClientId == clientId)
            {
                newVisibleObjects.Add(targetId);
                continue;
            }

            float distance = Vector3.Distance(clientPosition, netObj.transform.position);
            bool wasVisible = visibleObjects.Contains(targetId);

            // 히스테리시스 적용 (깜빡임 방지)
            float threshold = wasVisible
                ? visibilityRange + hysteresisDistance
                : visibilityRange;

            if (distance <= threshold)
            {
                newVisibleObjects.Add(targetId);

                // 새로 보이게 되는 경우
                if (!wasVisible)
                {
                    try
                    {
                        netObj.NetworkShow(clientId);

                        if (debugLog)
                        {
                            // Debug.Log($"[Proximity] Show Object {targetId} to Client {clientId} (dist: {distance:F1}m)");
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (debugLog)
                        {
                            Debug.LogWarning($"[Proximity] Failed to show Object {targetId}: {e.Message}");
                        }
                    }
                }
            }
            else
            {
                // 보이지 않게 되는 경우
                if (wasVisible)
                {
                    try
                    {
                        netObj.NetworkHide(clientId);

                        if (debugLog)
                        {
                            // Debug.Log($"[Proximity] Hide Object {targetId} from Client {clientId} (dist: {distance:F1}m)");
                        }
                    }
                    catch (System.Exception e)
                    {
                        if (debugLog)
                        {
                            Debug.LogWarning($"[Proximity] Failed to hide Object {targetId}: {e.Message}");
                        }
                    }
                }
            }
        }

        clientVisibilityCache[clientId] = newVisibleObjects;
    }

    /// <summary>
    /// 클라이언트 연결 해제 시 캐시 정리
    /// </summary>
    public void OnClientDisconnected(ulong clientId)
    {
        if (clientVisibilityCache.ContainsKey(clientId))
        {
            clientVisibilityCache.Remove(clientId);

            if (debugLog)
            {
                Debug.Log($"[Proximity] Removed cache for disconnected client {clientId}");
            }
        }
    }

    /// <summary>
    /// 특정 클라이언트에게 보이는 오브젝트 수 반환 (디버그용)
    /// </summary>
    public int GetVisibleObjectCount(ulong clientId)
    {
        if (clientVisibilityCache.TryGetValue(clientId, out var visible))
        {
            return visible.Count;
        }
        return 0;
    }

    /// <summary>
    /// 전체 통계 반환 (디버그용)
    /// </summary>
    public string GetStats()
    {
        int totalObjects = allNetworkObjects.Count;
        int totalClients = NetworkManager.Singleton.ConnectedClientsIds.Count;
        int totalVisibilityPairs = 0;

        foreach (var kvp in clientVisibilityCache)
        {
            totalVisibilityPairs += kvp.Value.Count;
        }

        float avgVisiblePerClient = totalClients > 0 ? (float)totalVisibilityPairs / totalClients : 0;
        float reductionPercent = totalObjects > 0
            ? (1f - avgVisiblePerClient / totalObjects) * 100f
            : 0f;

        return $"Objects: {totalObjects} | Clients: {totalClients} | " +
               $"Avg Visible: {avgVisiblePerClient:F1} ({reductionPercent:F1}% reduction)";
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 각 클라이언트의 가시성 범위 표시
        var clients = NetworkManager.Singleton.ConnectedClientsList;
        if (clients == null) return;

        foreach (var client in clients)
        {
            if (client.PlayerObject == null) continue;

            Vector3 pos = client.PlayerObject.transform.position;

            // 가시성 범위
            Gizmos.color = new Color(0, 1, 0, 0.1f);
            DrawCircle(pos, visibilityRange, 32);

            // 히스테리시스 범위
            Gizmos.color = new Color(1, 1, 0, 0.05f);
            DrawCircle(pos, visibilityRange + hysteresisDistance, 32);
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
#endif
}
