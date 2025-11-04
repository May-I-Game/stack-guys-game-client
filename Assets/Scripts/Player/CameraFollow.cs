// CameraFollow.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public float teleportDistance = 10f;                        // 이 거리 이상이면 즉시 텔레포트

    [Header("Camera Offset")]
    public Vector3 offset = new Vector3(0f, 7f, -15f);

    [Header("Follow Settings")]
    public bool followX = true;
    public bool followY = true;
    public bool followZ = true;

    [Header("Smooth Settings")]
    public float smoothSpeed = 5f;                              // 위치 보간 속도(클수록 빠르게 근접, 0=즉시 이동)
    public float rotationSmoothSpeed = 10f;                     // 회전 보간 속도(클수록 빠르게 목표 각도로 붙음)

    [Header("Rotation Settings")]
    [Range(-60f, 75f)]
    public float fixedPitch = 20f;                              // 기본 수직 각도

    [Range(0.05f, 1f)] public float topRegionPercent = 0.60f;   // 화면 상단 퍼센트 (카메라 드래그 영역)
    public bool ignoreUIOnStart = true;                         // UI 위에서 시작한 드래그 무시
    public float dragYawSensitivity = 0.2f;                     // 좌우 드래그 → Yaw
    public bool allowPitchDrag = true;                          // 상하 드래그로 Pitch 조절 허용할지
    public float dragPitchSensitivity = 0.12f;                  // 상하 드래그 → Pitch 변화량
    public float pitchMin = -60f, pitchMax = 75f;               // Pitch 범위

    // 내부 상태
    private Vector3 velocity = Vector3.zero;
    private float currentYaw = 0f;
    private float smoothYaw = 0f;
    private float yawVelocity = 0f;

    // 드래그 상태 - 터치 ID 추가
    private bool dragActive = false;
    private Vector2 prevTouchPos;
    private int cameraTouchId = -1;                             // 카메라 드래그 전용 터치 ID

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

    // 화면 상단 N%에서 터치 드래그로 회전 (멀티터치 지원)
    void HandleTopRegionDrag()
    {
        // 터치 입력이 있으면 터치 우선 처리
        if (Input.touchCount > 0)
        {
            HandleTouchDrag();
        }
        // 터치가 없으면 마우스 입력 처리 (PC 브라우저/에디터)
        else
        {
            HandleMouseDrag();

            // 마우스 입력도 없으면 드래그 상태 해제
            if (!Input.GetMouseButton(0) && cameraTouchId != -1)
            {
                cameraTouchId = -1;
                dragActive = false;
            }
        }
    }

    // 터치 입력으로 카메라 드래그 처리
    void HandleTouchDrag()
    {
        // 모든 터치 검사
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // 터치 시작
            if (touch.phase == TouchPhase.Began)
            {
                // 드래그 시작 조건: 할당된 터치 없음 & "상단 영역" & (옵션) UI 위 아님
                if (cameraTouchId == -1 && IsTouchInTopRegion(touch.position))
                {
                    if (!ignoreUIOnStart || !IsPointerOverUI())
                    {
                        cameraTouchId = touch.fingerId;
                        dragActive = true;
                        prevTouchPos = touch.position;
                    }
                }
            }
            // 터치 중 (터치 이동)
            else if (touch.phase == TouchPhase.Moved)
            {
                // 할당된 터치만 처리
                if (touch.fingerId == cameraTouchId && dragActive)
                {
                    Vector2 delta = touch.position - prevTouchPos;

                    // 좌우 → Yaw
                    currentYaw += delta.x * dragYawSensitivity;

                    // 상하 → Pitch (옵션)
                    if (allowPitchDrag)
                    {
                        fixedPitch = Mathf.Clamp(
                            fixedPitch - delta.y * dragPitchSensitivity,
                            pitchMin,
                            pitchMax
                        );
                    }

                    prevTouchPos = touch.position;
                }
            }
            // 터치 종료
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (touch.fingerId == cameraTouchId)
                {
                    cameraTouchId = -1;
                    dragActive = false;
                }
            }
        }
    }

    // 에디터 테스트용 마우스 드래그 처리
    void HandleMouseDrag()
    {
        Vector3 mp = Input.mousePosition;

        // 드래그 시작 조건: 좌클릭 눌린 프레임 & "상단 영역" & (옵션) UI 위 아님
        if (Input.GetMouseButtonDown(0))
        {
            if (cameraTouchId == -1 && IsTouchInTopRegion(mp))
            {
                if (!ignoreUIOnStart || !IsPointerOverUI())
                {
                    cameraTouchId = 0; // 마우스는 터치 ID 0으로 처리
                    dragActive = true;
                    prevTouchPos = mp;
                }
            }
        }
        // 드래그 중
        else if (Input.GetMouseButton(0) && cameraTouchId == 0 && dragActive)
        {
            Vector2 delta = (Vector2)mp - prevTouchPos;

            // 좌우 → Yaw
            currentYaw += delta.x * dragYawSensitivity;

            // 상하 → Pitch (옵션)
            if (allowPitchDrag)
            {
                fixedPitch = Mathf.Clamp(
                    fixedPitch - delta.y * dragPitchSensitivity,
                    pitchMin,
                    pitchMax
                );
            }

            prevTouchPos = mp;
        }
        // 드래그 종료
        else if (Input.GetMouseButtonUp(0) && cameraTouchId == 0)
        {
            cameraTouchId = -1;
            dragActive = false;
        }
    }

    // 터치 위치가 상단 영역에 있는지 확인 (조이스틱 영역 제외)
    bool IsTouchInTopRegion(Vector2 screenPosition)
    {
        // 화면 하단 30%를 제외한 나머지 영역 (조이스틱 영역과 겹치지 않음)
        float bottomThreshold = Screen.height * 0.3f; // 조이스틱 영역
        return screenPosition.y >= bottomThreshold
               && screenPosition.y <= Screen.height
               && screenPosition.x >= 0
               && screenPosition.x <= Screen.width;
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
        // Yaw 회전만 적용한 기본 방향
        Quaternion yawRotation = Quaternion.Euler(0f, smoothYaw, 0f);
        Vector3 horizontalOffset = yawRotation * new Vector3(offset.x, 0f, offset.z);

        // 수평 거리 계산
        float horizontalDistance = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);

        // Pitch에 따른 높이 계산 (삼각함수 사용)
        float pitchRadians = fixedPitch * Mathf.Deg2Rad;
        float verticalOffset = offset.y + horizontalDistance * Mathf.Sin(pitchRadians);

        return target.position + horizontalOffset + Vector3.up * verticalOffset;
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
        // 현재 카메라와 목표 위치 사이의 거리 계산
        float distance = Vector3.Distance(transform.position, targetPosition);

        // 거리가 임계값보다 크면 즉시 텔레포트
        if (distance > teleportDistance)
        {
            transform.position = targetPosition;
            velocity = Vector3.zero; // 속도 초기화
        }
        // 가까우면 부드럽게 따라감
        else if (smoothSpeed > 0f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                1f / smoothSpeed
            );
        }
        // smoothSpeed가 0 이하일 때의 안전장치
        else
        {
            transform.position = targetPosition;
        }
    }

    // 플레이어 머리 높이 방향을 바라보도록 카메라 회전을 설정
    void LookAtTarget()
    {
        // 타겟을 바라봄 (높이는 타겟 중심)
        Vector3 lookTarget = target.position + Vector3.up * 1f;

        // 카메라 → 타겟 방향 계산
        Vector3 direction = lookTarget - transform.position;

        // 타겟 방향으로 회전 (높이 보정 없이 정확히 바라봄)
        if (direction.sqrMagnitude > 0.001f)  // 0으로 나누기 방지
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
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

    // 상단 70%에 HUD/버튼/슬라이더 같은 UI 있으면 드래그 무시
    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
}