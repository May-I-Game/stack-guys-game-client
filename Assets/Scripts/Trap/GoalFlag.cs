using UnityEngine;

public class GoalFlag : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !GameManager.instance.gameEnded)
        {
            string playerName = PlayerPrefs.GetString("player_name", "Player");

            DisablePlayer(other.gameObject);

            GameManager.instance.PlayerReachedGoal(playerName);
        }
    }

    private void DisablePlayer(GameObject player)
    {
        var controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
    }
}