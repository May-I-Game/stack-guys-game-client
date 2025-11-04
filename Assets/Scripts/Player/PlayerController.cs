using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 4f;
    public float rotationSpeed = 10f;

    [Header("Cursor / Pointer Lock")]
    public bool togglePointerLockWithRMB = true; // 우클릭으로 포인터락 토글
    private bool _pointerLocked = false;

    [Header("Jump Settings")]
    public float jumpForce = 3f;
    public float diveForce = 4f; // 다이브할 때 앞으로 가는 힘
    public float diveDownForce = 1f; // 다이브할 때 아래로 가는 힘

    [Header("Grap Settings")]
    public float grabRange = 1f; // 잡기 범위
    public float holdHeight = 0.6f; // 머리 위 높이
    public float holdDistance = 0.1f; // 플레이어 앞쪽 거리
    public float throwForce = 5f; // 던지기 힘
    public int escapeRequiredJumps = 5; // 탈출에 필요한 점프 횟수

    [Header("Collision")]
    public float bounceForce = 5f; // 튕겨나가는 힘

    [Header("Animation")]
    public Animator animator;

    private Rigidbody rb;
    private PlayerInputHandler inputHandler;

    private bool isJumpQueued;
    private bool isGrabQueued;

    public GameObject bodyPrefab;

    private float diveGroundedDuration = 0.65f; // 다이브 착지 애니메이션 길이

    private NetworkVariable<Vector3> netMoveDirection = new NetworkVariable<Vector3>();
    private NetworkVariable<float> netCurrentSpeed = new NetworkVariable<float>();

    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>();
    private NetworkVariable<float> netVerticalVelocity = new NetworkVariable<float>();
    private NetworkVariable<bool> netIsDiving = new NetworkVariable<bool>(false); // 공중 다이브 중인지
    private NetworkVariable<bool> netIsDiveGrounded = new NetworkVariable<bool>(false); // 다이브 착지 상태 (이동 불가)
    private NetworkVariable<bool> netIsDeath = new NetworkVariable<bool>(false); // 죽었는지?
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

    // 리스폰 구역 Index 값
    public NetworkVariable<int> RespawnId = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private RespawnManager respawnManager;     // 리스폰 리스트를 사용하기 위하여 선언

    //시네마틱 동기화를 위한 사용자 입력 무시 변수
    private bool inputEnabled = true;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Camera.main.GetComponent<CameraFollow>().target = this.transform;
        }
    }

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        inputHandler = GetComponent<PlayerInputHandler>();
        nt = GetComponent<NetworkTransform>();
        respawnManager = FindFirstObjectByType<RespawnManager>();

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
            // 입력 허용시만 요청 처리
            if (inputEnabled)
            {
                MovePlayerServerRpc(inputHandler.MoveInput);

                if (inputHandler.JumpInput)
                {
                    JumpPlayerServerRpc();
                    inputHandler.ResetJumpInput();
                }

                if (inputHandler.GrabInput)
                {
                    GrabPlayerServerRpc();
                    inputHandler.ResetGrabInput();
                }

                // --- 커서/포인터락 토글 & 강제 유지 ---
                //if (togglePointerLockWithRMB)
                //{
                //    HandlePointerLockToggleRMB();
                //}

                //EnforcePointerLock();
            }
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // 서버만 로직 처리
        if (IsServer)
        {
            // 죽었으면 처리 무시
            if (!netIsDeath.Value)
            {
                // 이동 요청이 있으면
                if (netMoveDirection.Value.magnitude >= 0.1f)
                {
                    // 이동 처리
                    PlayerMove();
                }
                // 점프 요청이 있으면
                if (isJumpQueued)
                {
                    // 점프 처리
                    PlayerJump();
                }
                // 잡기 요청이 있으면
                if (isGrabQueued)
                {
                    // 잡기 처리
                    PlayerGrab();
                }

                // 잡고 있으면
                if (netIsHolding.Value && holdingObject != null)
                {
                    // 들기 처리
                    PlayerHeld();
                }

                // 애니메이션 업데이트
                SyncAnimationState();
            }
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    // 클라에서 서버에게 요청할 Rpc 모음
    #region ServerRpcs
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
        // 충돌 중이거나 다이브 착지 중이면 입력 무시
        if (isHit || netIsDiveGrounded.Value)
        {
            return;
        }

        isJumpQueued = true;
    }

    [ServerRpc]
    private void GrabPlayerServerRpc()
    {
        if (isHit || netIsDiveGrounded.Value || netIsGrabbed.Value)
        {
            return;
        }

        isGrabQueued = true;
    }

    [ServerRpc]
    public void RespawnPlayerServerRpc()
    {
        DoRespawn();
    }

    // 애니메이션이 끝날때 호출되는 함수
    [ServerRpc]
    public void ResetHitStateServerRpc()
    {
        //이제 이동 가능
        isHit = false;
    }
    #endregion

    // 서버에서 실제로 실행할 로직
    // 여기에 있는 모든 로직은 서버만 실행해야함!!!!!!!!
    #region ServerLogic
    private void PlayerMove()
    {
        // 이동
        Vector3 movement = netMoveDirection.Value * netCurrentSpeed.Value * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);

        // 회전
        Quaternion targetRotation = Quaternion.LookRotation(netMoveDirection.Value);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }

    private void PlayerJump()
    {
        // 잡혔으면 탈출시도
        if (netIsGrabbed.Value)
        {
            Debug.Log($"[잡기] 플레이어 탈출 시도: {escapeJumpCount}");
            escapeJumpCount++;
            if (escapeJumpCount >= escapeRequiredJumps)
            {
                EscapeFromGrap();
            }
        }

        else
        {
            // 땅에 있을 때: 점프
            if (netIsGrounded.Value)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                netIsGrounded.Value = false; // 점프 시 강제로 false 설정
                canDive = true; // 점프 후 다이브 가능
            }
            // 공중에 있을 때: 다이브
            else if (canDive && !netIsDiving.Value && !netIsHolding.Value)
            {
                PlayerDive();
            }
        }

        isJumpQueued = false;
    }

    private void PlayerDive()
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
    private void OnDiveLand()
    {
        if (!netIsDiving.Value) return;

        netIsDiving.Value = false;
        netIsDiveGrounded.Value = true;

        Debug.Log("[다이브 착지] 착지 애니메이션 재생, 조작 불가");

        // 착지 애니메이션이 끝나면 복구
        StartCoroutine(ResetDiveGroundedState());
    }

    // 다이브 착지 상태 복구
    private System.Collections.IEnumerator ResetDiveGroundedState()
    {
        yield return new WaitForSeconds(diveGroundedDuration);
        netIsDiveGrounded.Value = false;
    }

    private void PlayerGrab()
    {
        // 잡기중이 아니면 잡기시도
        if (!netIsHolding.Value)
        {
            TryGrab();
        }

        // 잡기 중이면 던지기 시도
        else
        {
            TryThrow();
        }

        isGrabQueued = false;
    }

    private void TryGrab()
    {
        // 범위 내 잡을 수 있는 오브젝트에 잡기시도
        Collider[] colliders = Physics.OverlapSphere(transform.position, grabRange);
        foreach (Collider col in colliders)
        {
            // 자기자신 제외
            if (col.gameObject == this.gameObject) continue;

            // 다른 플레이어 체크
            PlayerController otherPlayer = col.GetComponent<PlayerController>();
            if (otherPlayer != null && !otherPlayer.netIsGrabbed.Value && !otherPlayer.netIsHolding.Value)
            {
                GrabPlayer(otherPlayer);
                return;
            }

            // 오브젝트 체크
            GrabbableObject grabbable = col.GetComponent<GrabbableObject>();
            if (grabbable != null && !grabbable.netIsGrabbed.Value)
            {
                GrabObject(grabbable);
                return;
            }
        }
    }

    private void GrabPlayer(PlayerController otherPlayer)
    {
        holdingObject = otherPlayer.gameObject;
        netIsHolding.Value = true;
        netHoldingTargetId.Value = otherPlayer.NetworkObjectId;

        // 상대방 상태 변경
        otherPlayer.netIsGrabbed.Value = true;
        otherPlayer.netGrabberId.Value = this.NetworkObjectId;
        otherPlayer.escapeJumpCount = 0;

        // 상대방 물리 비활성화
        if (otherPlayer.rb != null)
        {
            otherPlayer.rb.isKinematic = true;
        }

        Debug.Log($"[잡기] 플레이어를 잡았습니다: {otherPlayer.gameObject.name}");
    }

    private void GrabObject(GrabbableObject grabbable)
    {
        holdingObject = grabbable.gameObject;
        netIsHolding.Value = true;
        netHoldingTargetId.Value = grabbable.NetworkObjectId;

        // 오브젝트 상태 변경
        grabbable.netIsGrabbed.Value = true;
        grabbable.holder = this;

        // 오브젝트 물리 비활성화
        Rigidbody targetRb = grabbable.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.isKinematic = true;
        }

        Debug.Log($"[잡기] 오브젝트를 잡았습니다: {grabbable.name}");
    }

    private void TryThrow()
    {
        // 잡은게 없으면 입력 무시
        if (holdingObject == null)
        {
            Debug.Log($"[잡기] 잡은 오브젝트가 없는데 잡기 중!!");
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

        // 오브젝트를 던지는 경우
        GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
        if (grabbable != null)
        {
            ThrowObject(grabbable, throwDirection);
        }

        holdingObject = null;
        netIsHolding.Value = false;
        netHoldingTargetId.Value = 0;
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

        SetTriggerClientRpc("Throw");
        Debug.Log("[잡기] 오브젝트를 던졌습니다");
    }

    private void ThrowObject(GrabbableObject target, Vector3 throwDirection)
    {
        target.netIsGrabbed.Value = false;
        target.holder = null;

        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.isKinematic = false;
            targetRb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        }

        SetTriggerClientRpc("Throw");
        Debug.Log("[잡기] 오브젝트를 던졌습니다");
    }

    private void PlayerHeld()
    {
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

    private void EscapeFromGrap()
    {
        if (netGrabberId.Value == 0) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netGrabberId.Value, out NetworkObject grabberObject))
        {
            netIsGrabbed.Value = false;
            netGrabberId.Value = 0;
            rb.isKinematic = false;
            return;
        }

        PlayerController grabbedBy = grabberObject.GetComponent<PlayerController>();

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

            else
            {
                GrabbableObject grabbable = holdingObject.GetComponent<GrabbableObject>();
                if (grabbable != null)
                {
                    grabbable.netIsGrabbed.Value = false;
                    grabbable.holder = null;

                    Rigidbody targetRb = grabbable.GetComponent<Rigidbody>();
                    if (targetRb != null)
                    {
                        targetRb.isKinematic = false;
                    }
                }
            }

            holdingObject = null;
        }

        // 내가 잡혀있었다면
        if (netIsGrabbed.Value)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(netGrabberId.Value, out NetworkObject grabberObject))
            {
                PlayerController grabbedBy = grabberObject.GetComponent<PlayerController>();
                if (grabbedBy != null)
                {
                    grabbedBy.holdingObject = null;
                    grabbedBy.netIsHolding.Value = false;
                    grabbedBy.netHoldingTargetId.Value = 0;
                }
            }
        }

        netIsHolding.Value = false;
        netHoldingTargetId.Value = 0;
        netIsGrabbed.Value = false;
        netGrabberId.Value = 0;
        escapeJumpCount = 0;
    }

    public void PlayerDeath()
    {
        if (netIsDeath.Value) return;

        netIsDeath.Value = true;
        // 인풋벡터 초기화
        netMoveDirection.Value = Vector3.zero;
        ReleaseGrab();

        SetTriggerClientRpc("Death");
    }

    // Death 애니메이션에서 호출됨
    private void DoRespawn()
    {
        if (!IsServer) return;

        if (bodyPrefab != null)
        {
            GameObject bodyInstance = Instantiate(bodyPrefab, transform.position, transform.rotation);
            NetworkObject networkBody = bodyInstance.GetComponent<NetworkObject>();

            if (networkBody != null)
            {
                networkBody.Spawn();
            }
        }

        // 리스폰 리스트 가져오기
        int index = RespawnId.Value;

        var dest = respawnManager.respawnPoints[index];
        if (!dest) { Debug.LogWarning("Respawn Transform null"); return; }

        // 이동/회전 속도 초기화
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 캐릭터 텔레포트
        if (nt != null)
        {
            nt.Teleport(dest.position, dest.rotation, transform.localScale);
        }

        ResetPlayerInput();

        // 애니메이터도 각 클라에서 리셋
        ResetAnimClientRpc();
    }
    public void ResetPlayerInput()
    {
        Debug.Log("입력값 초기화");
        // 이동/점프 관련 상태 최소 초기화
        netMoveDirection.Value = Vector3.zero;
        netCurrentSpeed.Value = 0f;
        isJumpQueued = false;
        netIsGrounded.Value = true;
        netIsDiving.Value = false;
        netIsDiveGrounded.Value = false;
        netIsDeath.Value = false;
        canDive = false;
        isHit = false;
    }

    // 좌표를 이용한 텔레포트
    // 순간이동에도 쓰이므로 public
    public void DoRespawn(Vector3 pos, Quaternion rot)
    {
        if (!IsServer) return;

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
        isJumpQueued = false;
        netIsGrounded.Value = true;
        netIsDiving.Value = false;
        netIsDiveGrounded.Value = false;
        netIsDeath.Value = false;
        canDive = false;
        isHit = false;

        // 애니메이터도 각 클라에서 리셋
        ResetAnimClientRpc();
    }

    // 플레이어 튕겨나가기 함수
    private void BouncePlayer(Vector3 normal, float force)
    {
        // 현재 속도 초기화
        rb.linearVelocity = Vector3.zero;

        // 법선 방향으로 힘 가하기 (위쪽 방향 추가)
        Vector3 bounceDirection = (normal + Vector3.up * 0.3f).normalized;
        rb.AddForce(bounceDirection * force, ForceMode.Impulse);

        Debug.Log($"[튕겨나가기] 방향: {bounceDirection}, 힘: {force}");
    }
    #endregion

    // 충돌관리 로직
    #region Collisions
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
                Debug.Log($"[경고] 매칭되지 않은 Tag: {collision.gameObject.tag}");
                break;
        }
    }
    #endregion

    // 서버에서 클라한테 시킬 Rpc 모음
    // 주로 애니메이션 연동
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
    private void ResetAnimClientRpc()
    {
        if (animator == null) return;

        animator.Rebind();                                  // 바인딩 초기화
    }

    [ClientRpc]
    public void ResetInputClientRpc()
    {
        if (!IsOwner) return;

        // PlayerController의 입력 상태 초기화
        ResetPlayerInput();

        // 입력 핸들러도 초기화
        if (inputHandler != null)
        {
            inputHandler.ResetAllInputs();
        }
    }
    #endregion

    // 애니메이션 로직들
    #region Animation
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
        SetTriggerClientRpc(triggerName);
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
            // 이동 상태를 애니메이터에 전달
            animator.SetBool("IsMoving", netMoveDirection.Value.magnitude > 0.1f);
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

    // 오른쪽 버튼 클릭시 커서 토글
    #region MouseToggle

    // 포인터락 상태 토글
    private void HandlePointerLockToggleRMB()
    {
        if (Input.GetMouseButtonDown(1)) // 우클릭 한번으로 토글
        {
            _pointerLocked = !_pointerLocked;
            ApplyCursorState();
        }
    }

    // 현재 포인터락 상태 강제 적용
    private void EnforcePointerLock()
    {
        // 매 프레임 강제 적용
        ApplyCursorState();
    }

    // 포인터락 여부에 따라 커서 잠금/표시 반영
    private void ApplyCursorState()
    {
        if (_pointerLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // 앱 포커스를 잃으면 잠금 해제해 커서가 갇히는 문제 방지
    //private void OnApplicationFocus(bool hasFocus)
    //{
    //    if (!hasFocus && _pointerLocked)
    //    {
    //        _pointerLocked = false;
    //        ApplyCursorState();
    //    }
    //}
    #endregion
}