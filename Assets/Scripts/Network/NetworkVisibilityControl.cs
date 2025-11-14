using Unity.Netcode;
using UnityEngine;

public class NetworkVisibilityControl : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float visibleRange = 60f;
    [SerializeField] private float hysteresisDistance = 5f;
    [SerializeField] private float updateInterval = 0.5f;

    private float lastCheckTime;

    public override void OnNetworkSpawn()
    {
        // 서버에서만 체크 로직을 덮어씀
        if (IsServer)
        {
            NetworkObject.CheckObjectVisibility = CheckVisibility;
        }
    }

    private void Update()
    {
        if (!IsServer || Time.time - lastCheckTime < updateInterval) return;
        lastCheckTime = Time.time;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            UpdateVisibility(clientId);
        }
    }

    private bool CheckVisibility(ulong clientId)
    {
        // 대상 클라이언트가 존재하는지 확인
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            return false;

        // 대상 클라이언트의 캐릭터 확인
        var clientPlayer = client.PlayerObject;
        if (clientPlayer == null) return false;

        // 자기 자신은 무조건 보여야 함
        if (clientId == OwnerClientId) return true;

        // 제곱으로 거리 계산
        float sqrDistance = (transform.position - clientPlayer.transform.position).sqrMagnitude;

        // 히스테리시스 로직 적용
        // 현재 이 클라이언트가 나를 보고 있는 상태인가?
        bool isCurrentlyVisible = NetworkObject.IsNetworkVisibleTo(clientId);

        if (isCurrentlyVisible)
        {
            // 이미 보고 있다면: 나갈 때는 좀 더 멀어져야 안 보임 (여유 공간)
            float exitRange = visibleRange + hysteresisDistance;
            return sqrDistance < exitRange * exitRange;
        }
        else
        {
            // 안 보고 있었다면: 들어올 때는 확실히 들어와야 보임
            return sqrDistance < visibleRange * visibleRange;
        }
    }

    private void UpdateVisibility(ulong clientId)
    {
        bool shouldBeVisible = CheckVisibility(clientId);
        bool isCurrentlyVisible = NetworkObject.IsNetworkVisibleTo(clientId);
        if (shouldBeVisible != isCurrentlyVisible)
        {
            if (shouldBeVisible)
            {
                NetworkObject.NetworkShow(clientId);
            }
            else
            {
                NetworkObject.NetworkHide(clientId);
            }
        }
    }
}