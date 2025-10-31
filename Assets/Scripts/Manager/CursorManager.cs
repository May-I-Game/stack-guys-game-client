using UnityEngine;

public class CursorManager : MonoBehaviour
{
    // 씬 진입할때 마우스 커서 끄기
    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 씬 나갈때 마우스 커서 켜기
    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
