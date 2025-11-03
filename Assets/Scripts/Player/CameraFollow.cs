using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target; // 따라갈 타겟(플레이어 Transform)

    [Header("Camera Offset")]
    public Vector3 offset = new Vector3(0f, 7f, -15f); // 타겟 기준 기본 오프셋(위로 +Y, 뒤로 -Z)

    [Header("Follow Settings")]
    public bool followX = true; // X축 위치를 따라갈지 여부
    public bool followY = true; // Y축 위치를 따라갈지 여부
    public bool followZ = true; // Z축 위치를 따라갈지 여부

    [Header("Smooth Settings")]
    public float smoothSpeed = 5f;          // 위치 보간 속도(클수록 빠르게 근접, 0이면 즉시 이동)
    public float rotationSmoothSpeed = 10f; // 회전 보간 속도(클수록 빠르게 목표 각도로 붙음)

    [Header("Rotation Settings")]
    public bool enableRotation = true;  // 회전 기능 켜기/끄기
    public float fixedPitch = 20f;        // 수직 고정 각도(위/아래 시점 고정)

    [Header("Locked Sensitivity Boost")]
    public float lockedYawGain = 8f;          // 잠금 상태 회전 증폭(값을 크게 → 더 많이 회전)
    [Range(0.2f, 1f)]
    public float lockedResponseGamma = 0.5f;  // 1=선형, 0.5=제곱근(작은 변화도 크게 증폭)

    // 내부 상태값들
    private Vector3 velocity = Vector3.zero; // SmoothDamp에 필요한 속도 누적 버퍼
    private float currentYaw = 0f;           // 입력으로 누적되는 실제 목표 Yaw(제한/랩 없음 → 360도+ 회전 가능)
    private float smoothYaw = 0f;            // 카메라가 따라가는 보간된 Yaw
    private float yawVelocity = 0f;          // SmoothDampAngle 내부에서 사용하는 각속도 버퍼

    void Start()
    {
        InitializeCamera();
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (enableRotation)
        {
            HandleRotationInput(); // 커서 잠금 상태일 때만 회전 입력
            SmoothRotation();      // smoothYaw가 currentYaw로 부드럽게 수렴
        }

        UpdateCameraTransform();   // 공전 위치 계산 → 축별 반영 → 위치 보간 → 타겟을 바라보도록 회전
    }

    // 타겟 탐색/초기 위치/초기 회전 각도 셋업
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
            Vector3 direction = (transform.position - target.position).normalized;
            currentYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            smoothYaw = currentYaw;
        }
    }

    // 마우스 X 입력 → currentYaw 누적 (잠금 상태에서만)
    // 작은 변화량도 크게 반영되도록 비선형(감마) + 게인 적용
    void HandleRotationInput()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float dx = Input.GetAxisRaw("Mouse X");     // 프레임당 마우스 델타
        float ax = Mathf.Abs(dx);

        if (ax > Mathf.Epsilon)
        {
            // 감마 < 1 → 작은 델타를 더 키움 (예: 0.5 = sqrt)
            float boosted = Mathf.Sign(dx) * Mathf.Pow(ax, lockedResponseGamma);

            // 최종 게인 적용
            currentYaw += boosted * lockedYawGain;  // 360° 제한 없음
        }
    }

    // smoothYaw가 currentYaw로 부드럽게 따라가도록 보간
    void SmoothRotation()
    {
        smoothYaw = Mathf.SmoothDampAngle(
            smoothYaw,
            currentYaw,
            ref yawVelocity,
            1f / rotationSmoothSpeed
        );
    }

    // 오비트된 목표 위치 계산 → 축별 따라가기 적용 → 위치 보간 → 타겟을 바라보도록 회전
    void UpdateCameraTransform()
    {
        Vector3 desiredPosition = CalculateDesiredPosition();
        Vector3 targetPosition = ApplyFollowSettings(desiredPosition);
        MoveCamera(targetPosition);
        LookAtTarget();
    }

    // 타겟 주위를 공전한 카메라의 목표 위치를 계산한다
    Vector3 CalculateDesiredPosition()
    {
        if (enableRotation)
        {
            Quaternion rotation = Quaternion.Euler(fixedPitch, smoothYaw, 0f);
            Vector3 rotatedOffset = rotation * offset;
            return target.position + rotatedOffset;
        }
        else
        {
            return target.position + offset;
        }
    }

    // 축별 따라가기 옵션(followX/Y/Z)에 따라 최종 목표 위치를 만듬
    Vector3 ApplyFollowSettings(Vector3 desiredPosition)
    {
        return new Vector3(
            followX ? desiredPosition.x : transform.position.x,
            followY ? desiredPosition.y : transform.position.y,
            followZ ? desiredPosition.z : transform.position.z
        );
    }

    // 부드럽게 목표 위치로 이동(smoothSpeed<=0이면 즉시 이동)
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

    // 카메라가 타겟(머리 높이)을 바라보도록 회전
    void LookAtTarget()
    {
        if (!enableRotation) return;
        Vector3 lookTarget = target.position + Vector3.up * 1f;
        transform.LookAt(lookTarget);
    }

    // 런타임에 타겟을 교체하고, 초기 Yaw를 재계산해 자연스럽게 잇는다
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null && enableRotation)
        {
            Vector3 direction = (transform.position - target.position).normalized;
            currentYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            smoothYaw = currentYaw;
        }
    }
}
