using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // 따라갈 타겟 (플레이어)

    [Header("Camera Offset")]
    public Vector3 offset = new Vector3(0f, 5f, -10f); // 카메라와 플레이어 사이 거리

    [Header("Follow Settings")]
    public bool followX = true; // X축 따라가기
    public bool followY = true; // Y축 따라가기
    public bool followZ = true; // Z축 따라가기

    [Header("Smooth Settings")]
    public float smoothSpeed = 5f; // 부드러운 이동 속도 (0 = 즉시 이동, 높을수록 느림)

    private Vector3 velocity = Vector3.zero;

    void Start()
    {
        // 타겟이 설정되지 않았다면 Player 태그로 찾기
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("CameraFollow: 타겟이 설정되지 않았습니다!");
            }
        }

        // 초기 위치 설정
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // 목표 위치 계산
        Vector3 desiredPosition = target.position + offset;

        // 각 축별로 따라갈지 결정
        Vector3 targetPosition = new Vector3(
            followX ? desiredPosition.x : transform.position.x,
            followY ? desiredPosition.y : transform.position.y,
            followZ ? desiredPosition.z : transform.position.z
        );
        // 부드러운 이동
        if (smoothSpeed > 0)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, 1f / smoothSpeed);
        }
        else
        {
            // 즉시 이동
            transform.position = targetPosition;
        }
        // 회전은 고정 (초기 회전값 유지)
        // transform.rotation은 변경하지 않음
    }
}
