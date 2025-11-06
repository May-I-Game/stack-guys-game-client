using Unity.Netcode;
using UnityEngine;

public class ObserverCameraControl : MonoBehaviour
{
    // 옵저버 카메라 이동 속도
    public float moveSpeed = 10f;
    public float shiftSpeed = 20f;
    public float sensitivity = 2f;

    private bool isObserver = false;
    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        if (NetworkManager.Singleton.IsClient)
        {
            // LocalClientId에 할당된 PlayerObject가 없으면 옵저버
            if (NetworkManager.Singleton.LocalClient != null &&
                NetworkManager.Singleton.LocalClient.PlayerObject == null)
            {
                isObserver = true;
                Debug.Log("--- OBSERVER MODE ACTIVATED ---");

                Camera.main.GetComponent<CameraFollow>().enabled = false;

                // 옵저버는 자유롭게 볼 수 있도록 커서 잠금
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void Update()
    {
        if (!isObserver) return;

        // 커서 잠금
        if (Input.GetKeyDown(KeyCode.Mouse1))
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