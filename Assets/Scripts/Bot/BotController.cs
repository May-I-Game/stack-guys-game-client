using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotController : PlayerController
{
    [Header("Bot Settings")]
    [SerializeField] private float updatePathInterval = 0.5f;   // 경로 업데이트 주기

    private NavMeshAgent navAgent;
    private Transform goalTransform;
    private float nextPathUpdateTime;                           // 다음 업데이트 시간

    protected override void Update()
    {
        // 봇은 서버에서만 업데이트
        if (!IsServer) return;

        UpdateAnimation();
    }

    protected override void Start()
    {
        base.Start();

        // 서버에서먄 AI 설정
        if (!IsServer) return;

        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.speed = walkSpeed;                         // 플레이어 컨트롤러의 속도
            navAgent.angularSpeed = rotationSpeed * 50f;        // 최대 회전 속도, updateRotation=true일 때만 트랜스폼 회전에 직접 반영
            navAgent.acceleration = 8f;                         // 가속도
            navAgent.stoppingDistance = 0.5f;                   // 목표 지점에 이 거리만큼 여유를 두고 감속/정지

            // Rigidbody와 충돌하지 않도록 설정
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
        }

        FindGoal();
    }

    protected override void FixedUpdate()
    {
        if (!IsServer) return;
        if (netIsDeath.Value) return;

        // 이동이 활성화 되어 있고 navAgent가 활성화가 되어 있을때 AI 작동
        if (inputEnabled && navAgent != null && navAgent.enabled)
        {
            UpdateBotAI();
        }

        base.FixedUpdate();
    }

    private void FindGoal()
    {
        if (!IsServer) return;

        // Goal 태그를 가진 오브젝트 찾기
        GameObject goal = GameObject.FindGameObjectWithTag("Goal");
        if (goal != null)
        {
            goalTransform = goal.transform;
            Debug.Log($"[Bot] 골 지점 발견: {goal.name}");
        }
        else
        {
            Debug.LogWarning("[Bot] Goal 태그를 가진 오브젝트 없음");
        }
    }

    private void UpdateBotAI()
    {
        // 골이 없으면 중단
        if (goalTransform == null)
        {
            if (Time.time > nextPathUpdateTime)
            {
                FindGoal();
                nextPathUpdateTime = Time.time + updatePathInterval;  // 스팸 호출 방지
            }

            return;
        }

        // 일정 간격으로 경로 업데이트
        if (Time.time > nextPathUpdateTime)
        {
            // NavMeshAgent가 활성이고 실제 NavMesh 위에 있을때
            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(goalTransform.position);
            }

            // 다음 갱신 시간 업데이트
            nextPathUpdateTime = Time.time + updatePathInterval;
        }

        // NavMesh 경로를 따라 이동 입력 생성
        // 유효한 경로가 있고 피격에 따른 이동 잠금 상태, 다이브 착지후 이동 불가 상태, 잡힌 상태가 아닐때
        if (navAgent.hasPath && !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
        {
            // NavMesh가 계산한 방향으로 이동
            Vector3 direction = navAgent.desiredVelocity.normalized;

            // 거의 제로 벡터면 입력을 주지 않는다 (미세한 떨림/노이즈 방지)
            if (direction.magnitude > 0.1f)
            {
                // 캐릭터 이동 입력은 XZ 평면만 사용 (x=좌우, z=전후)
                Vector2 moveInput = new Vector2(direction.x, direction.z);

                // 네트워크로 동기화되는 입력/속도 값 갱신
                netMoveDirection.Value = moveInput; // 이번 프레임 이동 방향

                // NavMeshAgent 내부 위치(nextPosition)를 실제 Transform과 강제로 맞춤
                navAgent.nextPosition = transform.position;
            }
            else
            {
                // 방향이 실질적으로 없으면(거의 0) —> 제자리 유지
                netMoveDirection.Value = Vector2.zero;
            }
        }
        else
        {
            // 경로가 없거나, 이동 금지 상태면 —> 제자리 유지
            netMoveDirection.Value = Vector2.zero;
        }
    }

    public override void OnNetworkSpawn()
    {
        // 봇은 카메라 설정 안함
        // 부모 클래스의 카메라 설정을 무시
    }
}