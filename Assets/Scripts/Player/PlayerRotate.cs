using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    private Transform currentPlatform;
    private Vector3 lastPlatformPosition;
    private Quaternion lastPlatformRotation;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (currentPlatform != null)
        {
            // 발판의 회전량 계산
            Quaternion rotationDelta = currentPlatform.rotation *
                                       Quaternion.Inverse(lastPlatformRotation);

            // 플레이어의 상대 위치
            Vector3 localPos = rb.position - lastPlatformPosition;
            Vector3 newLocalPos = rotationDelta * localPos;

            // 새로운 위치
            Vector3 newPosition = currentPlatform.position + newLocalPos;

            // Rigidbody로 이동 (물리 엔진 고려)
            rb.MovePosition(newPosition);
            rb.MoveRotation(rotationDelta * rb.rotation);

            // 현재 값 저장
            lastPlatformPosition = currentPlatform.position;
            lastPlatformRotation = currentPlatform.rotation;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            currentPlatform = collision.transform;
            lastPlatformPosition = currentPlatform.position;
            lastPlatformRotation = currentPlatform.rotation;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            currentPlatform = null;
        }
    }
}
