using Unity.Netcode;
using UnityEngine;

public class PlayerMove : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float rotationSpeed = 10f;
    private float currentSpeed;

    [Header("Jump Settings")]
    public float jumpForce = 5f;
    public float diveForce = 10f; // 다이브할 때 앞으로 가는 힘
    public float diveDownForce = 3f; // 다이브할 때 아래로 가는 힘

    [Header("Animation")]
    public Animator animator;

    private float weakHitDuration = 2f;    // weakHit 애니메이션 길이
    private float strongHitDuration = 2.4f; // StrongHit 애니메이션 길이
    private float diveGroundedDuration = 0.867f; // 다이브 착지 애니메이션 길이

    private Rigidbody rb;

    private Vector3 moveDirection;
    private bool isjumpQueued;

    private bool isGrounded;
    private bool isHit = false; // 충돌 상태 (이동 불가)
    private bool isDiving = false; // 공중 다이브 중인지
    private bool isDiveGrounded = false; // 다이브 착지 상태 (이동 불가)
    private bool canDive = false; // 다이브 가능 상태 (점프 중)

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Animator가 설정되지 않았다면 자동으로 찾기
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }
    }

    void Update()
    {
        if (IsOwner)
        {
            // 입력 받기
            HandleInput();
        }
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            // 이동 처리
            MovePlayer();
            // 점프 처리
            JumpPlayer();

            // 애니메이션 업데이트
            UpdateAnimation();
        }
    }

    void HandleInput()
    {
        // WASD 입력 받기
        float horizontal = Input.GetAxisRaw("Horizontal"); // A, D
        float vertical = Input.GetAxisRaw("Vertical");     // W, S

        MovePlayerServerRpc(new Vector3(horizontal, 0f, vertical).normalized);

        // Space 키로 점프 또는 다이브
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpPlayerServerRpc();
        }
    }

    #region ServerRPCs
    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 direction)
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiveGrounded)
        {
            moveDirection = Vector3.zero;
            return;
        }

        moveDirection = direction;
        // 기본 이동 속도
        currentSpeed = walkSpeed;
    }

    [ServerRpc]
    private void JumpPlayerServerRpc()
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || isDiveGrounded)
        {
            return;
        }

        isjumpQueued = true;
    }
    #endregion

    void MovePlayer()
    {
        if (moveDirection.magnitude >= 0.1f)
        {
            // 이동
            Vector3 movement = moveDirection * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // 회전
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void JumpPlayer()
    {
        if (isjumpQueued)
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
                DivePlayer();
            }
            isjumpQueued = false;
        }
    }

    void DivePlayer()
    {
        isDiving = true;
        canDive = false;

        // 현재 바라보는 방향으로 앞으로 힘 가하기
        Vector3 diveDirection = transform.forward * diveForce + Vector3.down * diveDownForce;
        rb.linearVelocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(diveDirection, ForceMode.Impulse);

        // 다이브 애니메이션 실행 (공중)
        if (animator != null)
        {
            animator.SetTrigger("Dive");
        }
    }

    // 다이브 착지 처리
    void OnDiveLand()
    {
        if (!isDiving) return;

        isDiving = false;
        isDiveGrounded = true;

        Debug.Log("[다이브 착지] 착지 애니메이션 재생, 조작 불가");

        // 착지 애니메이션 실행
        if (animator != null)
        {
            animator.SetTrigger("DiveGrounded");
        }

        // 착지 애니메이션이 끝나면 복구
        StartCoroutine(ResetDiveGroundedState());
    }

    // 다이브 착지 상태 복구
    private System.Collections.IEnumerator ResetDiveGroundedState()
    {
        yield return new WaitForSeconds(diveGroundedDuration);
        isDiveGrounded = false;
    }

    // Collider로 땅 감지
    private void OnCollisionStay(Collision collision)
    {
        if (!IsServer) return;

        // 점프 직후에는 땅 체크 안 함
        if (canDive && !isDiving)
        {
            return;
        }

        // 충돌한 오브젝트가 아래쪽에 있으면 땅으로 판단
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) // 법선 벡터가 위를 향하면 땅
            {
                // 다이브 중이었다면 착지 처리
                if (isDiving)
                {
                    OnDiveLand();
                }

                isGrounded = true;

                // 땅에 닿으면 다이브 불가능 상태로 초기화
                if (canDive)
                {
                    canDive = false;
                }

                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsServer) return;

        isGrounded = false;
    }

    // 특정 물체와 충돌할 때
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Tag로 구분하여 다른 애니메이션 재생
        switch (collision.gameObject.tag)
        {
            case "weakObstacles":
                // 장애물에 부딪힘
                PlayHitAnimation("weakHit", weakHitDuration);
                break;

            case "OtherPlayer":
                // 다른플레이어
                PlayHitAnimation("weakHit", weakHitDuration);
                break;

            case "StrongObstacles":
                // 가시에 부딪힘
                PlayHitAnimation("StrongHit", strongHitDuration);
                break;

            default:
                // 매칭되지 않은 Tag
                Debug.Log($"[경고] 매칭되지 않은 Tag: {collision.gameObject.tag}");
                break;
        }
    }

    // 애니메이션 재생 함수
    private void PlayHitAnimation(string triggerName, float duration)
    {
        if (animator == null)
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

        // 지정된 시간만큼 대기 후 이동 재개
        StartCoroutine(ResetHitState(duration));
    }

    // 애니메이션이 끝나면 이동 가능하도록 복구
    private System.Collections.IEnumerator ResetHitState(float duration)
    {
        //이동 차단 duration초 동안 이동 불가
        yield return new WaitForSeconds(duration);

        isHit = false;
        //이제 이동 가능
    }

    void UpdateAnimation()
    {
        if (animator != null)
        {
            // 이동 속도를 애니메이터에 전달
            float speed = moveDirection.magnitude * currentSpeed;
            animator.SetFloat("Speed", speed);

            // 점프 상태를 애니메이터에 전달
            animator.SetBool("IsGrounded", isGrounded);

            // 다이브 상태를 애니메이터에 전달
            animator.SetBool("IsDiving", isDiving);

            // 다이브 착지 상태를 애니메이터에 전달
            animator.SetBool("IsDiveGrounded", isDiveGrounded);

            // 수직 속도를 애니메이터에 전달 (점프/낙하 애니메이션용)
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
        }
    }

}
