using UnityEngine;
using Unity.Netcode;

public class GoalFlag : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 서버만 도착 체크
        if (!IsServer) return;

        // 태그 및 게임상태 확인
        if (!GameManager.instance.IsGame || !other.CompareTag("Player")) return;

        // 플레이어 여부 확인
        if (!other.TryGetComponent<PlayerController>(out var player)) return;

        // ===== 중복 체크 추가 (같은 플레이어가 다시 들어오는 것 방지) =====
        string playerName = player.GetPlayerName();

        Debug.Log($"도착 완료!! 플레이어: {playerName}");
        GameManager.instance.PlayerReachedGoal(playerName, player.OwnerClientId);
    }
}