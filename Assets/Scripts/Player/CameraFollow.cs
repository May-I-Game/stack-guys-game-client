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
    public bool enableRotation = true;
    [Range(5f, 85f)]
    public float fixedPitch = 20f;          // 기본 수직 각도

    [Header("Locked Sensitivity (기존 마우스 이동용)")]
    public float lockedYawGain = 8f;
    [Range(0.2f, 1f)]
    public float lockedResponseGamma = 0.5f;

    // 새 드래그 회전 옵션
    [Header("Top-Drag Rotate (새 입력 방식)")]
    public bool rotateByTopDrag = true;             // 상단 드래그로 회전
    [Range(0.05f, 0.6f)] public float topRegionPercent = 0.30f; // 화면 상단 퍼센트
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
        InitializeCamera();
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (enableRotation)
        {
            if (rotateByTopDrag)
                HandleTopRegionDrag();
            else
                HandlePointerLockRotation(); // 필요시 기존 방식 유지

            SmoothRotation();
        }

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

        if (enableRotation)
        {
            Vector3 dir = (transform.position - target.position).normalized;
            currentYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            smoothYaw = currentYaw;
        }
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

    // 기존 포인터락 방식(우클릭 토글 후 Mouse X)
    void HandlePointerLockRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float dx = Input.GetAxisRaw("Mouse X");
        float ax = Mathf.Abs(dx);

        if (ax > Mathf.Epsilon)
        {
            float boosted = Mathf.Sign(dx) * Mathf.Pow(ax, lockedResponseGamma);
            currentYaw += boosted * lockedYawGain;
        }
    }

    void SmoothRotation()
    {
        smoothYaw = Mathf.SmoothDampAngle(
            smoothYaw,
            currentYaw,
            ref yawVelocity,
            1f / rotationSmoothSpeed
        );
    }

    void UpdateCameraTransform()
    {
        Vector3 desired = CalculateDesiredPosition();
        Vector3 targetPos = ApplyFollowSettings(desired);
        MoveCamera(targetPos);
        LookAtTarget();
    }

    Vector3 CalculateDesiredPosition()
    {
        if (enableRotation)
        {
            Quaternion rot = Quaternion.Euler(fixedPitch, smoothYaw, 0f);
            Vector3 rotatedOffset = rot * offset;
            return target.position + rotatedOffset;
        }
        else
        {
            return target.position + offset;
        }
    }

    Vector3 ApplyFollowSettings(Vector3 desiredPosition)
    {
        return new Vector3(
            followX ? desiredPosition.x : transform.position.x,
            followY ? desiredPosition.y : transform.position.y,
            followZ ? desiredPosition.z : transform.position.z
        );
    }

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

    void LookAtTarget()
    {
        if (!enableRotation) return;
        Vector3 lookTarget = target.position + Vector3.up * 1f;
        transform.LookAt(lookTarget);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null && enableRotation)
        {
            Vector3 dir = (transform.position - target.position).normalized;
            currentYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            smoothYaw = currentYaw;
        }
    }

    // UI 위 포인터 여부(시작 시점만 체크)
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
}
