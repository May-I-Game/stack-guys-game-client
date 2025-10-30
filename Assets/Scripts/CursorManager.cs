using UnityEngine;

public class CursorManager : MonoBehaviour
{
    // �� �����Ҷ� ���콺 Ŀ�� ����
    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // �� ������ ���콺 Ŀ�� �ѱ�
    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
