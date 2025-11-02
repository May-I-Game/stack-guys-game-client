// 📁 Billboard.cs (Y축 고정 버전)
using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private bool lockY = true; // Y축 고정 여부
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera == null) return;

        if (lockY)
        {
            // Y축은 고정하고 X, Z만 회전 (수평 회전만)
            Vector3 direction = mainCamera.transform.position - transform.position;
            direction.y = 0; // Y축 무시

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(-direction);
            }
        }
        else
        {
            // 완전히 카메라를 향함
            transform.LookAt(mainCamera.transform);
            transform.Rotate(0, 180, 0); // 뒤집기
        }
    }
}