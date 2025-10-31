using UnityEngine;

public class CharacterSelectionManager : MonoBehaviour
{
    public GameObject characterSelectPopup;
    public void PopupOn()
    {
        characterSelectPopup.SetActive(true);
    }
    public void PopupOff()
    {
        characterSelectPopup.SetActive(false);
    }
}
