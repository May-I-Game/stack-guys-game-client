using UnityEngine;

public class GoalFlag : MonoBehaviour
{
    private static bool gameEnded = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !gameEnded)
        {
            string playerName = PlayerPrefs.GetString("player_name", "Player");

            // ������ �÷��̾� ��� ����
            DisablePlayer(other.gameObject);

            // ���� �Ŵ������� ���� �˸�
            GameEndManager gameManager = Object.FindFirstObjectByType<GameEndManager>();
            if (gameManager != null)
            {
                gameManager.PlayerReachedGoal(playerName);
            }
            else
            {
                Debug.LogError("GameManager�� ã�� �� �����ϴ�! Hierarchy�� GameManager�� �ִ��� Ȯ���ϼ���.");
            }
        }
    }

    public static void DisablePlayer(GameObject player)
    {
        // ThirdPersonController ��Ȱ��ȭ
        var controller = player.GetComponent<StarterAssets.ThirdPersonController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // StarterAssetsInputs ��Ȱ��ȭ
        var input = player.GetComponent<StarterAssets.StarterAssetsInputs>();
        if (input != null)
        {
            input.enabled = false;
        }

        // CharacterController ��Ȱ��ȭ
        var charController = player.GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }

#if ENABLE_INPUT_SYSTEM
        // PlayerInput ��Ȱ��ȭ
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