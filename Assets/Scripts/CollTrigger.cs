using UnityEngine;
using UnityEngine.Events;

public class CollTrigger : MonoBehaviour
{
    [SerializeField]
    string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        PlayerDeathSystem deathSystem = other.GetComponent<PlayerDeathSystem>();

        if (deathSystem != null && !deathSystem.IsDead())
        {
            if (other.CompareTag(playerTag))
                deathSystem.Die(playerTag);
        }
    }

    //private void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag(playerTag))
    //}
}
