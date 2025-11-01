using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float rotationSpeed = 10f;

    [Header("Jump Settings")]
    public float jumpForce = 3f;
    public float diveForce = 4f; // 다이브할 때 앞으로 가는 힘
    public float diveDownForce = 1f; // 다이브할 때 아래로 가는 힘

    [Header("Grap Settings")]
    public float grabRange = 1f; // 잡기 범위
    public float holdHeight = 0.5f; // 머리 위 높이
    public float holdDistance = 0.1f; // 플레이어 앞쪽 거리
    public float throwForce = 5f; // 던지기 힘
    public int escapeRequiredJumps = 5; // 탈출에 필요한 점프 횟수

    [Header("Animation")]
    public Animator animator;

    private float weakHitDuration = 2f;    // weakHit 애니메이션 길이
    private float strongHitDuration = 2.4f; // StrongHit 애니메이션 길이
    private float diveGroundedDuration = 0.65f; // 다이브 착지 애니메이션 길이

    private Rigidbody rb;

    private NetworkVariable<Vector3> netMoveDirection = new NetworkVariable<Vector3>();
    private NetworkVariable<float> netCurrentSpeed = new NetworkVariable<float>();
    private bool isjumpQueued;

    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>();
    private NetworkVariable<float> netVerticalVelocity = new NetworkVariable<float>();
    private NetworkVariable<bool> netIsDiving = new NetworkVariable<bool>(false); // 공중 다이브 중인지
    private NetworkVariable<bool> netIsDiveGrounded = new NetworkVariable<bool>(false); // 다이브 착지 상태 (이동 불가)
    private bool isHit = false; // 충돌 상태 (이동 불가)
    private bool canDive = false; // 다이브 가능 상태 (점프 중)

    // 잡기 관련 변수
    private NetworkVariable<bool> netIsHolding = new NetworkVariable<bool>(false); // 무언가를 들고 있는지
    private NetworkVariable<bool> netIsGrabbed = new NetworkVariable<bool>(false); // 잡혀있는지
    private NetworkVariable<ulong> netGrabberId = new NetworkVariable<ulong>(0); // 누가 잡고 있는지
    private NetworkVariable<ulong> netHoldingTargetId = new NetworkVariable<ulong>(0); // 누구를 잡고 있는지

    private GameObject holdingObject = null; // 실제로 들고 있는 오브젝트
    private int escapeJumpCount = 0; // 탈출 시도 횟수

    NetworkTransform nt;
    // 최초 스폰 자리 저장 (서버 전용)
    private Vector3 _initialSpawnPosition;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 최초 스폰 위치 저장
            _initialSpawnPosition = transform.position;
        }

        if (IsOwner)
        {
            Camera.main.GetComponent<CameraFollow>().target = this.transform;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        nt = GetComponent<NetworkTransform>();

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

    private void Update()
    {
        if (IsOwner)
        {
            // 로비/게임 중에만 입력 받기
            if (GameManager.instance.IsLobby || GameManager.instance.IsGame)
            {
                HandleInput();
            }

            // 오른쪽 버튼 커서 토글 부분
            ToggleCursorWithRMB();
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (IsServer)
        {
            // 이동 처리
            MovePlayer();
            // 점프 처리
            JumpPlayer();
            // 들기 처리
            HeldPlayer();

            // 애니메이션 업데이트
            SyncAnimationState();
        }
    }

    void HandleInput()
    {
        // WASD 입력 받기
        float horizontal = Input.GetAxisRaw("Horizontal"); // A, D
        float vertical = Input.GetAxisRaw("Vertical");     // W, S

        MovePlayerServerRpc(new Vector3(vertical, 0f, -horizontal).normalized);

        // Space 키로 점프 또는 다이브
        if (Input.GetKeyDown(KeyCode.Space))
        {
            JumpPlayerServerRpc();
        }

        // E 키로 잡기 또는 던지기
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!netIsHolding.Value)
            {
                GrapPlayerServerRpc();
            }
            else
            {
                ThrowPlayerServerRpc();
            }
        }
    }

    #region ServerRPCs
    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 direction)
    {
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || netIsDiveGrounded.Value)
        {
            netMoveDirection.Value = Vector3.zero;
            return;
        }

        netMoveDirection.Value = direction;
        // 기본 이동 속도
        netCurrentSpeed.Value = walkSpeed;
    }

    [ServerRpc]
    private void JumpPlayerServerRpc()
    {
        // 잡혔으면 탈출시도
        if (netIsGrabbed.Value)
        {
            escapeJumpCount++;
            if (escapeJumpCount >= escapeRequiredJumps)
            {
                EscapeFromGrap();
            }
        }

        // 이외에는 점프시도
        else
        {
            // 충돌 중이거나 다이브 착지 중이면 입력 무시
            if (isHit || netIsDiveGrounded.Value)
            {
                return;
            }

            isjumpQueued = true;
        }
    }

    [ServerRpc]
    private void GrapPlayerServerRpc()
    {
        if (isHit || netIsDiveGrounded.Value || netIsHolding.Value || netIsGrabbed.Value)
        {
            return;
        }

        // 범위 내 잡을 수 있는 오브젝트에 잡기시도
        Collider[] colliders = Physics.OverlapSphere(transform.position, grabRange);
        foreach (Collider col in colliders)
        {
            // 자기자신 제외
            if (col.gameObject == this.gameObject) continue;

            // TODO: GrabbableObject 체크
            //GrabbableObject grabbable = col.GetComponent<GrabbableObject>();
            //if (grabbable != null && !grabbable.IsGrabbed())
            //{
            //    GrabObject(col.gameObject);
            //    return;
            //}

            // 다른 플레이어 체크
            PlayerController otherPlayer = col.GetComponent<PlayerController>();
            if (otherPlayer != null && !otherPlayer.netIsGrabbed.Value && !otherPlayer.netIsHolding.Value)
            {
                GrabPlayer(otherPlayer);
                return;
            }
        }
    }

    private void GrabPlayer(PlayerController target)
    {
        holdingObject = target.gameObject;
        netIsHolding.Value = true;
        netHoldingTargetId.Value = target.NetworkObjectId;

        // 상대방 상태 변경
        target.netIsGrabbed.Value = true;
        target.netGrabberId.Value = this.NetworkObjectId;
        target.escapeJumpCount = 0;

        // 상대방 물리 비활성화
        if (target.rb != null)
        {
            target.rb.isKinematic = true;
        }

        SetTriggerClientRpc("Grab");
        target.SetTriggerClientRpc("Grabbed");
        Debug.Log($"[잡기] 플레이어를 잡았습니다: {target.gameObject.name}");
    }

    [ServerRpc]
    private void ThrowPlayerServerRpc()
    {
        // 잡기 중 아니면 입력 무시
        if (!netIsHolding.Value || holdingObject == null)
        {
            return;
        }

        // 던지기 방향 계산 (앞쪽 + 약간 위)
        Vector3 throwDirection = (transform.forward + Vector3.up * 0.5f).normalized;

        // 플레이어를 던지는 경우
        PlayerController targetPlayer = holdingObject.GetComponent<PlayerController>();
        if (targetPlayer != null)
        {
            ThrowPlayer(targetPlayer, throwDirection);
        }

        // TODO: 오브젝트를 던지는 경우
        //else
        //{
        //    ThrowObject(holdingObject, throwDirection);
        //}

        holdingObject = null;
        netIsHolding.Value = false;
        netHoldingTargetId.Value = 0;

        SetTriggerClientRpc("Throw");
        Debug.Log("[잡기] 오브젝트를 던졌습니다");
    }
    #endregion

    void MovePlayer()
    {
        if (netMoveDirection.Value.magnitude >= 0.1f)
        {
            // 이동
            Vector3 movement = netMoveDirection.Value * netCurrentSpeed.Value * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            // 회전
            Quaternion targetRotation = Quaternion.LookRotation(netMoveDirection.Value);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void JumpPlayer()
    {
        if (isjumpQueued)
        {
            // 땅에 있을 때: 점프
            if (netIsGrounded.Value)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                netIsGrounded.Value = false; // 점프 시 강제로 false 설정
                canDive = true; // 점프 후 다이브 가능
            }
            // 공중에 있을 때: 다이브
            else if (canDive && !netIsDiving.Value && !netIsGrabbed.Value)
            {
                DivePlayer();
            }
            isjumpQueued = false;
        }
    }

    void DivePlayer()
    {
        netIsDiving.Value = true;
        canDive = false;

        // 현재 바라보는 방향으로 앞으로 힘 가하기
        Vector3 diveDirection = transform.forward * diveForce + Vector3.down * diveDownForce;
        rb.linearVelocity = Vector3.zero; // 기존 속도 초기화
        rb.AddForce(diveDirection, ForceMode.Impulse);

        // 다이브 애니메이션 실행 (공중)
        SetTriggerClientRpc("Dive");
    }

    // 다이브 착지 처리
    void OnDiveLand()
    {
        if (!netIsDiving.Value) return;

        netIsDiving.Value = false;
        netIsDiveGrounded.Value = true;

        Debug.Log("[다이브 착지] 착지 애니메이션 재생, 조작 불가");

        // 착지 애니메이션 실행
        SetTriggerClientRpc("DiveGrounded");

        // 착지 애니메이션이 끝나면 복구
        StartCoroutine(ResetDiveGroundedState());
    }

    // 다이브 착지 상태 복구
    private System.Collections.IEnumerator ResetDiveGroundedState()
    {
        yield return new WaitForSeconds(diveGroundedDuration);
        netIsDiveGrounded.Value = false;
    }

    private void HeldPlayer()
    {
        if (!netIsHolding.Value || holdingObject == null)
        {
            return;
        }

        // 머리 위 위치 계산
        Vector3 targetPosition = transform.position
            + transform.forward * holdDistance
            + Vector3.up * holdHeight;

        holdingObject.transform.position = targetPosition;

        // 플레이어를 들고 있는 경우 회전도 맞춤
        PlayerController heldPlayer = holdingObject.GetComponent<PlayerController>();
        if (heldPlayer != null)
        {
            holdingObject.transform.rotation = transform.rotation;
        }
    }

    private void ThrowPlayer(PlayerController target, Vector3 throwDirection)
    {
        target.netIsGrabbed.Value = false;
        target.netGrabberId.Value = 0;
        target.escapeJumpCount = 0;

        // 물리 재활성화 및 힘 가하기
        if (target.rb != null)
        {
            target.rb.isKinematic = false;
            target.rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        }
    }

    private void ThrowObject(GameObject target, Vector3 throwDirection)
    {
        // TODO:
        //Rigidbody targetRb = target.GetComponent<Rigidbody>();
        //if (targetRb != null)
        //{
        //    targetRb.isKinematic = false;
        //    targetRb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        //}

        //GrabbableObject grabbable = target.GetComponent<GrabbableObject>();
        //if (grabbable != null)
        //{
        //    grabbable.OnReleased();
        //}
    }

    private void EscapeFromGrap()
    {
        if (netGrabberId.Value == 0) return;

        PlayerController grabbedBy = NetworkManager.Singleton.ConnectedClients[netGrabberId.Value].PlayerObject.GetComponent<PlayerController>();
        if (grabbedBy == null)
        {
            netIsGrabbed.Value = false;
            netGrabberId.Value = 0;
            rb.isKinematic = false;
            return;
        }

        // 잡고 있던 플레이어의 상태 해제
        grabbedBy.holdingObject = null;
        grabbedBy.netIsHolding.Value = false;
        grabbedBy.netHoldingTargetId.Value = 0;

        // 내 상태 해제
        netIsGrabbed.Value = false;
        netGrabberId.Value = 0;
        escapeJumpCount = 0;

        // 물리 재활성화 및 점프
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.up * jumpForce * 1.5f, ForceMode.Impulse);
        }

        SetTriggerClientRpc("Escape");
        Debug.Log("[탈출] 성공적으로 탈출했습니다!");
    }

    // Collider로 땅 감지
    private void OnCollisionStay(Collision collision)
    {
        if (!IsServer) return;

        // 점프 직후에는 땅 체크 안 함
        //if (canDive && !netIsDiving.Value)
        //{
        //    return;
        //}

        // 충돌한 오브젝트가 아래쪽에 있으면 땅으로 판단
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) // 법선 벡터가 위를 향하면 땅
            {
                // 다이브 중이었다면 착지 처리
                if (netIsDiving.Value)
                {
                    OnDiveLand();
                }

                // 수직 속도가 거의 0이거나 아래로 떨어지는 중일 때만 착지로 판단
                if (rb.linearVelocity.y <= 0.1f)
                {
                    netIsGrounded.Value = true;

                    // 땅에 닿으면 다이브 불가능 상태로 초기화
                    if (canDive)
                    {
                        canDive = false;
                    }
                }

                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!IsServer) return;

        netIsGrounded.Value = false;
    }

    // 특정 물체와 충돌할 때
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        // Tag로 구분하여 다른 애니메이션 재생
        switch (collision.gameObject.tag)
        {
            case "Death":
                // 최초 스폰 자리로 텔레포트
                DoRespawn();
                break;

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

    #region Animation
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
        SetTriggerClientRpc(triggerName);

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

    // NetworkVariable 업데이트
    void SyncAnimationState()
    {
        netVerticalVelocity.Value = rb.linearVelocity.y;
    }

    void UpdateAnimation()
    {
        if (animator != null)
        {
            // 이동 속도를 애니메이터에 전달
            float speed = netMoveDirection.Value.magnitude * netCurrentSpeed.Value;
            animator.SetFloat("Speed", speed);
            // 점프 상태를 애니메이터에 전달
            animator.SetBool("IsGrounded", netIsGrounded.Value);
            // 다이브 상태를 애니메이터에 전달
            animator.SetBool("IsDiving", netIsDiving.Value);
            // 다이브 착지 상태를 애니메이터에 전달
            animator.SetBool("IsDiveGrounded", netIsDiveGrounded.Value);
            // 수직 속도를 애니메이터에 전달 (점프/낙하 애니메이션용)
            animator.SetFloat("VerticalVelocity", netVerticalVelocity.Value);

            // 잡힌 상태를 애니메이터에 전달
            animator.SetBool("IsGrabbed", netIsGrabbed.Value);
        }
    }
    #endregion

    // 서버 권위 리스폰
    public void DoRespawn()
    {
        if (!IsServer) return;

        ReleaseGrab();

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 캐릭터 텔레포트
        if (nt != null)
        {
            nt.Teleport(_initialSpawnPosition, Quaternion.identity, transform.localScale);
        }

        // 이동/점프 관련 상태 최소 초기화
        netMoveDirection.Value = Vector3.zero;
        netCurrentSpeed.Value = 0f;
        netIsGrounded.Value = true;
        netIsDiving.Value = false;
        netIsDiveGrounded.Value = false;
        canDive = false;
        isjumpQueued = false;
        isHit = false;

        // 애니메이터도 각 클라에서 리셋
        ResetDiveAnimClientRpc();
    }

    public void DoRespawn(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

        ReleaseGrab();

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 캐릭터 텔레포트
        if (nt != null)
        {
            nt.Teleport(pos, rot, transform.localScale);
        }

        // 이동/점프 관련 상태 최소 초기화
        netMoveDirection.Value = Vector3.zero;
        netCurrentSpeed.Value = 0f;
        netIsGrounded.Value = true;
        netIsDiving.Value = false;
        netIsDiveGrounded.Value = false;
        canDive = false;
        isjumpQueued = false;
        isHit = false;

        // 애니메이터도 각 클라에서 리셋
        ResetDiveAnimClientRpc();
    }

    private void ReleaseGrab()
    {
        // 내가 무언가를 들고 있었다면
        if (netIsHolding.Value && holdingObject != null)
        {
            PlayerController heldPlayer = holdingObject.GetComponent<PlayerController>();
            if (heldPlayer != null)
            {
                heldPlayer.netIsGrabbed.Value = false;
                heldPlayer.netGrabberId.Value = 0;
                if (heldPlayer.rb != null)
                {
                    heldPlayer.rb.isKinematic = false;
                }
            }
            //TODO:
            //else
            //{
            //    GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
            //    if (grabbable != null)
            //    {
            //        grabbable.OnReleased();
            //    }
            //}

            holdingObject = null;
        }

        // 내가 잡혀있었다면
        if (netIsGrabbed.Value)
        {
            PlayerController grabbedBy = NetworkManager.Singleton.ConnectedClients[netGrabberId.Value].PlayerObject.GetComponent<PlayerController>();
            if (grabbedBy != null)
            {
                grabbedBy.holdingObject = null;
                grabbedBy.netIsHolding.Value = false;
                grabbedBy.netHoldingTargetId.Value = 0;
            }
        }

        netIsHolding.Value = false;
        netHoldingTargetId.Value = 0;
        netIsGrabbed.Value = false;
        netGrabberId.Value = 0;
        escapeJumpCount = 0;
    }

    // 오른쪽 버튼 클릭시 커서 토글
    public void ToggleCursorWithRMB()
    {
        if (!IsClient) return;

        if (Input.GetMouseButtonDown(1)) // RMB 클릭 시
        {
            bool willUnlock = (Cursor.lockState == CursorLockMode.Locked);
            Cursor.lockState = willUnlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = willUnlock;
        }
    }

    #region ClientRPCs
    [ClientRpc]
    void SetTriggerClientRpc(string triggerName)
    {
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    [ClientRpc]
    private void ResetDiveAnimClientRpc()
    {
        if (animator == null) return;

        animator.Rebind();                                  // 바인딩 초기화
    }
    #endregion
}
