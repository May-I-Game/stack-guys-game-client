using System.Collections.Generic;
using UnityEngine;

public class EditorPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float rotationSpeed = 10f;

    [Header("Jump Settings")]
    public float jumpForce = 3f;
    public float diveForce = 4f; // 다이브할 때 앞으로 가는 힘
    public float diveDownForce = 1f; // 다이브할 때 아래로 가는 힘

    [Header("Collision")]
    public float groundCheckDist = 0.1f;
    private RaycastHit[] groundHits = new RaycastHit[3];
    public LayerMask groundLayerMask = -1; // 땅으로 인식할 레이어 (최적화용, -1 = 모든 레이어)
    public float bounceForce = 5f; // 튕겨나가는 힘
    public int groundCheckInterval = 2;  // 2프레임마다 체크 (50Hz → 25Hz)

    [Header("Animation")]
    public Animator animator;

    private Rigidbody rb;
    private CapsuleCollider col;

    private Vector2 moveDir = Vector2.zero;
    private Vector2 lastSentInput = Vector2.zero;  // 실제로 서버에 전송한 마지막 입력

    // GC 최적화: WaitForSeconds 캐싱
    private bool isJumpQueued;
    private Vector3 deathPosition;  // 죽은 위치 저장용

    public GameObject bodyPrefab;

    // TODO : 애니메이션 종료시에 함수 호출해서
    // netIsDiveGrounded 말고 private bool 변수 사용해서 서버만 동기화하도록
    private float diveGroundedDuration = 0.65f; // 다이브 착지 애니메이션 길이

    private bool isMove;
    private bool isGrounded;
    private bool isDiving;
    private bool isDiveGrounded;
    private bool isDeath;

    private bool isHit = false; // 충돌 상태 (이동 불가)
    private bool canDive = false; // 다이브 가능 상태 (점프 중)

    private int respawnIdx = 0;
    public List<Transform> respawnAreas = new List<Transform>();

    //시네마틱 동기화를 위한 사용자 입력 무시 변수
    public bool inputEnabled = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
    }

    private void Start()
    {
        Camera.main.GetComponent<CameraFollow>().target = this.transform;

        // Animator가 설정되지 않았다면 자동으로 찾기
        animator = animator != null ? animator : GetComponent<Animator>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        //본인이 아닌 캐릭터, 혹은 input이 비활성화 되어있을 때는 애니메이션만 최신화
        if (!inputEnabled)
        {
            UpdateAnimation();
            return;
        }

        MovePlayerInput();

        // Space 키로 점프 또는 다이브 (PC만)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpPlayerInput();
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // 죽었으면 처리 무시
        if (isDeath) return;

        // 땅 체크
        GroundCheck();
        // 이동 처리
        PlayerMove();

        // 점프 요청이 있으면
        if (isJumpQueued)
        {
            // 점프 처리
            PlayerJump();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    private void MovePlayerInput()
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiveGrounded)
        {
            moveDir = Vector2.zero;
            return;
        }

        // ============ PC: 기존 키보드 입력 ============ // WASD 입력 받기
        float horizontal = Input.GetAxisRaw("Horizontal"); // A, D
        float vertical = Input.GetAxisRaw("Vertical");     // W, S

        // ============ 카메라에 영향을 받는 이동 ===========
        Transform cam = EditorManager.Instance.playerCam.transform;
        Vector2 currentInput;
        // GC 최적화: Vector2 재사용 (매 프레임 할당 방지)
        Vector2 foward = new Vector2(cam.forward.x, cam.forward.z);
        foward.Normalize();

        Vector2 right = new Vector2(cam.right.x, cam.right.z);
        right.Normalize();

        // 입력(Vertical=앞/뒤, Horizontal=좌/우)을 카메라 기준으로 합성
        currentInput = foward * vertical + right * horizontal;

        // ===== 입력 동기화 최적화 =====
        float inputDelta = Vector2.Distance(currentInput, lastSentInput);

        // 대각선 과입력(√2) 보정
        if (currentInput.sqrMagnitude > 1f) currentInput.Normalize();

        // 이동 방향 임계값 체크: 방향 변화가 크거나 멈출 때만 동기화
        Vector2 directionDelta = currentInput - moveDir;
        if (directionDelta.magnitude >= 0.1f || currentInput == Vector2.zero)
        {
            moveDir = currentInput;
        }

        lastSentInput = currentInput;
    }

    private void JumpPlayerInput()
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiveGrounded)
        {
            return;
        }

        isJumpQueued = true;
    }

    // 애니메이션 이벤트에서 호출됨
    public void RespawnPlayer()
    {
        // ServerRpc 호출 (시체 생성 + 텔레포트)
        RespawnPlayerServerRpc();
    }

    // 시체 생성 + 텔레포트 실행
    private void RespawnPlayerServerRpc()
    {
        DoRespawnTeleport();
    }

    // 애니메이션이 끝날때 호출되는 함수
    public void ResetHitStateServerRpc()
    {
        //이제 이동 가능
        isHit = false;
    }

    public void ResetStateServerRpc()
    {
        ResetPlayerState();
    }

    protected void PlayerMove()
    {
        // 이동 요청이 있으면
        if (moveDir.magnitude >= 0.1f)
        {
            // 이동
            Vector3 movement = new Vector3(
                moveDir.x,
                0,
                moveDir.y
            ) * walkSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // 회전
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            transform.rotation = targetRotation;

            isMove = true;
        }
        else
        {
            isMove = false;
        }
    }

    protected void PlayerJump()
    {
        // 땅에 있을 때: 점프
        if (isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false; // 점프 시 강제로 false 설정
            canDive = true; // 점프 후 다이브 가능
        }
        // 공중에 있을 때: 다이브
        else if (canDive && !isDiving)
        {
            PlayerDive();
        }

        isJumpQueued = false;
    }

    private void PlayerDive()
    {
        isDiving = true;
        canDive = false;

        // 현재 바라보는 방향으로 앞으로 힘 가하기
        Vector3 diveDirection = transform.forward * diveForce + Vector3.down * diveDownForce;
        rb.linearVelocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(diveDirection, ForceMode.Impulse);

        // 다이브 애니메이션 실행 (공중)
        animator.SetTrigger("Dive");
    }

    // 다이브 착지 처리
    private void OnDiveLand()
    {
        if (!isDiving) return;

        isDiving = false;
        isDiveGrounded = true;

        Debug.Log("[다이브 착지] 착지 애니메이션 재생, 조작 불가");

        // 착지 애니메이션이 끝나면 복구
        StartCoroutine(ResetDiveGroundedState());
    }

    // 다이브 착지 상태 복구
    private System.Collections.IEnumerator ResetDiveGroundedState()
    {
        yield return new WaitForSeconds(diveGroundedDuration);
        isDiveGrounded = false;
    }

    public void PlayerDeath()
    {
        if (isDeath) return;

        // 죽은 위치 저장 (시체 생성용)
        deathPosition = transform.position;

        isDeath = true;

        // 인풋벡터 초기화
        moveDir = Vector2.zero;

        animator.SetTrigger("Death");
    }

    // 시체 생성 + 리스폰
    private void DoRespawnTeleport()
    {
        // 시체 생성 (리스폰 시점에 생성하여 자연스러움)
        if (bodyPrefab != null)
        {
            GameObject bodyInstance = Instantiate(bodyPrefab, deathPosition, transform.rotation);

            // Layer 설정: DeadBody (Layer 10) - 거리 기반 컬링 적용
            SetLayerRecursively(bodyInstance, 10);
        }

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 캐릭터 텔레포트
        transform.position = respawnAreas[respawnIdx].position;
        transform.rotation = respawnAreas[respawnIdx].rotation;

        ResetPlayerState();
    }

    private void ResetPlayerState()
    {
        // 이동/점프 관련 상태 최소 초기화
        moveDir = Vector2.zero;
        isJumpQueued = false;
        isGrounded = true;
        isDiving = false;
        isDiveGrounded = false;
        isDeath = false;
        canDive = false;
        isHit = false;

        // 애니메이터도 각 클라에서 리셋
        animator.Rebind();
    }

    // 오브젝트와 자식들의 레이어를 재귀적으로 설정
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // 충돌관리 로직
    protected void GroundCheck()
    {
        // 프레임 스키핑: Unity 전역 프레임 카운터 사용 (모든 플레이어가 동기화됨)
        if (Time.frameCount % groundCheckInterval != 0) return;

        // 캐싱된 계산 (매번 계산하지 않도록)
        float offsetDist = col.height / 2f - col.radius;
        Vector3 bottomSphereCenter = col.center + (Vector3.down * offsetDist);
        Vector3 castOrigin = transform.TransformPoint(bottomSphereCenter);
        float scale = transform.localScale.y;
        float scaledRadius = col.radius * scale * 0.95f;
        float scaledDistance = groundCheckDist * scale;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin,
            scaledRadius,
            Vector3.down,
            groundHits,
            scaledDistance,
            groundLayerMask  // LayerMask로 필터링 (Physics 쿼리 최적화)
        );

        isGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];

            // 자기자신 제외
            if (hit.collider == null || hit.collider == col) continue;
            // 경사로/벽면 제외 (0.7 = 약 45도 경사)
            if (hit.normal.y < 0.7f) continue;

            // Debug.Log($"{hit.collider.name}을 땅으로 감지!!");
            isGrounded = true;
            break;
        }

        // 착지 시 처리 (최적화: 조건을 미리 체크)
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            if (isDiving)
            {
                OnDiveLand();
            }

            if (canDive)
            {
                canDive = false;
            }
        }
    }

    // 특정 물체와 충돌할 때
    private void OnCollisionEnter(Collision collision)
    {
        // Tag로 구분하여 다른 애니메이션 재생
        switch (collision.gameObject.tag)
        {
            case "Ocean":
            case "Death":
                // 캐릭터가 가지고 있는 리스폰 인덱스로 이동
                PlayerDeath();
                break;

            case "weakObstacles":
                // 충돌 지점의 평균 법선 벡터 계산
                Vector3 avgNormal = Vector3.zero;
                foreach (ContactPoint contact in collision.contacts)
                {
                    avgNormal += contact.normal;
                }
                avgNormal /= collision.contacts.Length;

                // 장애물에 부딪힘
                PlayHitAnimation("weakHit");
                BouncePlayer(avgNormal, bounceForce);
                break;

            case "StrongObstacles":
                // 가시에 부딪힘
                PlayHitAnimation("StrongHit");
                break;

            default:
                // 매칭되지 않은 Tag
                // Debug.Log($"[경고] 매칭되지 않은 Tag: {collision.gameObject.tag}");
                break;
        }
    }

    // 플레이어 튕겨나가기 함수
    private void BouncePlayer(Vector3 normal, float force)
    {
        // 현재 속도 초기화
        rb.linearVelocity = Vector3.zero;

        // 법선 방향으로 힘 가하기 (위쪽 방향 추가)
        Vector3 bounceDirection = (normal + Vector3.up * 0.3f).normalized;
        rb.AddForce(bounceDirection * force, ForceMode.Impulse);

        // Debug.Log($"[튕겨나가기] 방향: {bounceDirection}, 힘: {force}");
    }

    // 애니메이션 재생 함수
    private void PlayHitAnimation(string triggerName)
    {
        if (isHit || animator == null)
        {
            return;
        }

        // Animator Controller에 해당 Parameter가 있는지 확인
        bool hasParameter = false;
        foreach (var param in animator.parameters)
        {
            if (param.name == triggerName && param.type == AnimatorControllerParameterType.Trigger)
            {
                hasParameter = true;
                break;
            }
        }

        if (!hasParameter)
        {
            //디버깅 animator에 해당하는 parameter가 없을 경우
            Debug.Log("현재 Animator Parameters:");
            foreach (var param in animator.parameters)
            {
                Debug.Log($"  - {param.name} ({param.type})");
            }
            return;
        }

        // 이동 차단 및 Trigger 실행
        isHit = true;
        animator.SetTrigger(triggerName);
    }

    protected void UpdateAnimation()
    {
        if (animator != null)
        {
            // 이동 상태를 애니메이터에 전달
            animator.SetBool("IsMoving", isMove);
            // 점프 상태를 애니메이터에 전달
            animator.SetBool("IsGrounded", isGrounded);
            // 다이브 상태를 애니메이터에 전달
            animator.SetBool("IsDiving", isDiving);
            // 다이브 착지 상태를 애니메이터에 전달
            animator.SetBool("IsDiveGrounded", isDiveGrounded);
        }
    }
}