using UnityEngine;
using Unity.Netcode;

public class GoalFlag : NetworkBehaviour
{
    private bool hasFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        // 서버만 도착 체크 (클라이언트는 Trigger는 감지하지만 처리는 서버가 함)
        if (!IsServer) return;

        // 태그 및 게임상태 확인
        if (hasFinished || !GameManager.instance.IsGame || !other.CompareTag("Player")) return;

        // 플레이어 여부 확인
        if (!other.TryGetComponent<PlayerController>(out var player)) return;

        // 서버에서 직접 처리 (Owner 체크 불필요, 서버가 모든 플레이어를 감지)
        // PlayerPrefs는 각 클라이언트 로컬이므로, 여기서는 ClientId만 전달
        // GameManager에서 플레이어 이름을 관리해야 함
        string playerName = player.GetPlayerName();
        Debug.Log($"도착 완료!! 플레이어: {playerName}");
        GameManager.instance.PlayerReachedGoal(playerName, player.OwnerClientId);

        hasFinished = true;
    }
}