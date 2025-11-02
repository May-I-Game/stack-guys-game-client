using UnityEngine;

public class GoalFlag : MonoBehaviour
{
    private bool hasFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        // 태그 및 게임상태 확인
        if (hasFinished || !GameManager.instance.IsGame || !other.CompareTag("Player")) return;

        // 플레이어 여부 및 내거인지 확인
        var player = other.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;

        string playerName = PlayerPrefs.GetString("player_name", "Player");
        GameManager.instance.PlayerReachedGoalServerRpc(playerName, player.OwnerClientId);

        hasFinished = true;
    }
}