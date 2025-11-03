// CameraFollow.cs
using UnityEngine;
using UnityEngine.EventSystems; // UI 위 포인터 검사용 (옵션)

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;

    [Header("Camera Offset")]
    public Vector3 offset = new Vector3(0f, 7f, -15f);

    [Header("Follow Settings")]
    public bool followX = true;
    public bool followY = true;
    public bool followZ = true;

    [Header("Smooth Settings")]
    public float smoothSpeed = 5f;          // 위치 보간 속도(클수록 빠르게 근접, 0=즉시 이동)
    public float rotationSmoothSpeed = 10f; // 회전 보간 속도(클수록 빠르게 목표 각도로 붙음)

    [Header("Rotation Settings")]
    [Range(5f, 85f)]
    public float fixedPitch = 20f;          // 기본 수직 각도

    [Range(0.05f, 0.6f)] public float topRegionPercent = 0.50f; // 화면 상단 퍼센트
    public bool ignoreUIOnStart = true;             // UI 위에서 시작한 드래그 무시
    public float dragYawSensitivity = 0.2f;         // 좌우 드래그 → Yaw
    public bool allowPitchDrag = false;             // 상하 드래그로 Pitch 조절 허용할지
    public float dragPitchSensitivity = 0.12f;      // 상하 드래그 → Pitch 변화량
    public float pitchMin = 5f, pitchMax = 75f;     // Pitch 범위

    // 내부 상태
    private Vector3 velocity = Vector3.zero;
    private float currentYaw = 0f;
    private float smoothYaw = 0f;
    private float yawVelocity = 0f;

    // 드래그 상태
    private bool dragActive = false;
    private Vector3 prevMousePos;

    void Start()
    {
        // 초기 카메라 설정 (타겟/오프셋/초기 yaw 등 계산)
        InitializeCamera();
    }

    // LateUpdate는 플레이어 이동/애니메이션이 끝난 뒤 카메라가 따라붙도록 해줌
    void LateUpdate()
    {
        if (target == null) return;

        HandleTopRegionDrag();

        SmoothRotation();

        UpdateCameraTransform();
    }

    void InitializeCamera()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null) target = player.transform;
            else
            {
                Debug.LogWarning("CameraFollow: 타겟이 설정되지 않았습니다!");
                return;
            }
        }

        transform.position = target.position + offset;

        Vector3 dir = (transform.position - target.position).normalized;
        currentYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        smoothYaw = currentYaw;
    }

    // 새 입력: 화면 상단 N%에서 좌클릭 드래그로 회전
    void HandleTopRegionDrag()
    {
        Vector3 mp = Input.mousePosition;

        // 드래그 시작 조건: 좌클릭 눌린 프레임 & "상단 영역" & (옵션) UI 위 아님
        if (Input.GetMouseButtonDown(0))
        {
            bool inTopRegion = mp.y >= Screen.height * (1f - topRegionPercent) && mp.y <= Screen.height
                               && mp.x >= 0 && mp.x <= Screen.width;

            if (inTopRegion && (!ignoreUIOnStart || !IsPointerOverUI()))
            {
                dragActive = true;
                prevMousePos = mp;
            }
        }

        // 드래그 중
        if (dragActive && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)(mp - prevMousePos);

            // 좌우 → Yaw
            currentYaw += delta.x * dragYawSensitivity;

            // 상하 → Pitch(옵션)
            if (allowPitchDrag)
            {
                fixedPitch = Mathf.Clamp(fixedPitch - delta.y * dragPitchSensitivity, pitchMin, pitchMax);
            }

            prevMousePos = mp;
        }

        // 드래그 종료
        if (Input.GetMouseButtonUp(0))
        {
            dragActive = false;
        }
    }

    // 현재 yaw를 목표 yaw로 부드럽게 감쇠 보간
    void SmoothRotation()
    {
        smoothYaw = Mathf.SmoothDampAngle(
            smoothYaw,
            currentYaw,
            ref yawVelocity,
            1f / rotationSmoothSpeed
        );
    }

    // 카메라 목표 계산→축별 적용→이동→시선 설정을 한 프레임에 처리
    void UpdateCameraTransform()
    {
        Vector3 desired = CalculateDesiredPosition();
        Vector3 targetPos = ApplyFollowSettings(desired);
        MoveCamera(targetPos);
        LookAtTarget();
    }

    // yaw/pitch와 오프셋을 적용해 이상적인 카메라 위치를 계산
    Vector3 CalculateDesiredPosition()
    {
        Quaternion rot = Quaternion.Euler(fixedPitch, smoothYaw, 0f);
        Vector3 rotatedOffset = rot * offset;
        return target.position + rotatedOffset;

    }

    // 축별 추적 플래그에 따라 각 축 좌표를 갱신하거나 유지
    Vector3 ApplyFollowSettings(Vector3 desiredPosition)
    {
        return new Vector3(
            followX ? desiredPosition.x : transform.position.x,
            followY ? desiredPosition.y : transform.position.y,
            followZ ? desiredPosition.z : transform.position.z
        );
    }

    // SmoothDamp(>0) 또는 즉시 스냅(=0)으로 목표 위치로 이동
    void MoveCamera(Vector3 targetPosition)
    {
        if (smoothSpeed > 0f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                1f / smoothSpeed
            );
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    // 플레이어 머리 높이 방향을 바라보도록 카메라 회전을 설정
    void LookAtTarget()
    {
        Vector3 lookTarget = target.position + Vector3.up * 1f;
        transform.LookAt(lookTarget);
    }

    // 새 타겟을 지정하고 현재 시점 기준으로 yaw 초기값을 재설정
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            Vector3 dir = (transform.position - target.position).normalized;
            currentYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            smoothYaw = currentYaw;
        }
    }

    // 상단 50%에 HUD/버튼/슬라이더 같은 UI 있으면 드래그 무시
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
}
