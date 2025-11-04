using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    private Transform currentPlatform;
    private Vector3 lastPlatformPosition;
    private Quaternion lastPlatformRotation;
    private Rigidbody rb;
    private bool isOnPlatform = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (currentPlatform != null && isOnPlatform)
        {
            // 발판의 회전량 계산
            Quaternion rotationDelta = currentPlatform.rotation *
                                       Quaternion.Inverse(lastPlatformRotation);

            // 플레이어의 상대 위치 계산
            Vector3 localPos = rb.position - lastPlatformPosition;
            Vector3 newLocalPos = rotationDelta * localPos;

            // 새로운 위치 계산
            Vector3 newPosition = currentPlatform.position + newLocalPos;

            // 이동량 계산해서 적용
            Vector3 platformMovement = newPosition - rb.position;
            rb.MovePosition(rb.position + platformMovement);

            // 플레이어 회전도 적용 (캐릭터가 발판 방향 따라가게 하려면)
            // rb.MoveRotation(rotationDelta * rb.rotation);

            // 현재 값 저장
            lastPlatformPosition = currentPlatform.position;
            lastPlatformRotation = currentPlatform.rotation;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            // 위쪽 면에 닿았는지 확인 (바닥만 체크)
            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    currentPlatform = collision.transform;
                    lastPlatformPosition = currentPlatform.position;
                    lastPlatformRotation = currentPlatform.rotation;
                    isOnPlatform = true;
                    break;
                }
            }
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform") && currentPlatform == collision.transform)
        {
            // 계속 위에 있는지 확인
            bool stillOnTop = false;
            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f)
                {
                    stillOnTop = true;
                    break;
                }
            }

            if (!stillOnTop)
            {
                isOnPlatform = false;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform") && currentPlatform == collision.transform)
        {
            currentPlatform = null;
            isOnPlatform = false;
        }
    }
}
