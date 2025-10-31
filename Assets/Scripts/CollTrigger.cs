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
            Debug.Log("성공");
            if (other.CompareTag(playerTag))
                deathSystem.Die(playerTag);
        }
    }

    //private void OnTriggerExit(Collider other)
    //{
    //    if (other.CompareTag(playerTag))
    //}
}
