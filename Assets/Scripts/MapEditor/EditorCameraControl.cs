using UnityEngine;

public class EditorCameraControl : MonoBehaviour
{
    // 카메라 이동 속도
    public float moveSpeed = 10f;
    public float shiftSpeed = 20f;
    public float sensitivity = 2f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        Debug.Log("--- EDITOR MODE ACTIVATED ---");

        // 자유롭게 볼 수 있도록 커서 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // 커서 잠금
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (!Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        if (!Cursor.visible)
        {
            // --- 카메라 회전 (마우스) ---
            rotationX -= Input.GetAxis("Mouse Y") * sensitivity;
            rotationY += Input.GetAxis("Mouse X") * sensitivity;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f); // 상하 각도 제한
            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);

            // --- 카메라 이동 (키보드) ---
            float speed = Input.GetKey(KeyCode.LeftShift) ? shiftSpeed : moveSpeed;

            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

            transform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}