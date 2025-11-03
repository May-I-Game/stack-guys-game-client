using UnityEngine;

public class PlayerRotate : MonoBehaviour
{
    private Transform platform;                 // 현재 밟고 있는 플랫폼
    private Quaternion lastPlatformRotation;    // 이전 프레임의 플랫폼 회전 상태
    private Vector3 platformCenter;             // 플랫폼 중심 (보통 transform.position)
    private bool onPlatform = false;

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            platform = collision.transform;
            lastPlatformRotation = platform.rotation;
            platformCenter = platform.position;
            onPlatform = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Platform"))
        {
            platform = null;
            onPlatform = false;
        }
    }

    void LateUpdate()
    {
        if (onPlatform && platform != null)
        {
            // 플랫폼의 회전 변화량 계산
            Quaternion rotationDelta = platform.rotation * Quaternion.Inverse(lastPlatformRotation);

            // 플레이어를 플랫폼 중심 기준으로 회전시킨 위치 계산
            Vector3 relativePos = transform.position - platformCenter;
            relativePos = rotationDelta * relativePos;

            // 플레이어 위치 갱신 (회전만 적용)
            transform.position = platformCenter + relativePos;

            // 다음 프레임을 위한 기록
            lastPlatformRotation = platform.rotation;
            platformCenter = platform.position;
        }
    }
}
