using UnityEngine;

public class GoalFlag : MonoBehaviour
{
    private static bool gameEnded = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !gameEnded)
        {
            string playerName = PlayerPrefs.GetString("player_name", "Player");

            // 골인한 플레이어 즉시 정지
            DisablePlayer(other.gameObject);

            // 게임 매니저에게 골인 알림
            GameEndManager gameManager = Object.FindFirstObjectByType<GameEndManager>();
            if (gameManager != null)
            {
                gameManager.PlayerReachedGoal(playerName);
            }
            else
            {
                Debug.LogError("GameManager를 찾을 수 없습니다! Hierarchy에 GameManager가 있는지 확인하세요.");
            }
        }
    }

    public static void DisablePlayer(GameObject player)
    {
        // ThirdPersonController 비활성화
        var controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // StarterAssetsInputs 비활성화
        var input = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        if (input != null)
        {
            input.enabled = false;
        }

        // CharacterController 비활성화
        var charController = player.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        // PlayerInput 비활성화
        var playerInput = player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
#endif
    }

    public static void ResetGame()
    {
        gameEnded = false;
    }
}