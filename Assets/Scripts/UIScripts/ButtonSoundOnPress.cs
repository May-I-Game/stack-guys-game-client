using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonSoundOnPress : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private AudioSource audioSource;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.PlayOneShot(audioSource.clip);
        }
    }
}