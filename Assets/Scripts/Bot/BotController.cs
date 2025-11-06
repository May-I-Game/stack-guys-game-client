using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotController : PlayerController
{
    [Header("Bot Settings")]
    [SerializeField] private float updatePathInterval = 0.5f; // 경로 업데이트 주기
    
    private NavMeshAgent navAgent;
    private Transform goalTransform;
    private float nextPathUpdateTime;
    
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
            navAgent.speed = walkSpeed;
            navAgent.angularSpeed = rotationSpeed * 50f;
            navAgent.acceleration = 8f;
            navAgent.stoppingDistance = 0.5f;
            
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
        
        if (navAgent != null && navAgent.enabled)
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
                nextPathUpdateTime = Time.time + updatePathInterval;
            }
            return;
        }
        
        // 일정 간격으로 경로 업데이트
        if (Time.time > nextPathUpdateTime)
        {
            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(goalTransform.position);
            }
            nextPathUpdateTime = Time.time + updatePathInterval;
        }
        
        // NavMesh 경로를 따라 이동 입력 생성
        if (navAgent.hasPath && !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
        {
            // NavMesh가 계산한 방향으로 이동
            Vector3 direction = navAgent.desiredVelocity.normalized;
            
            if (direction.magnitude > 0.1f)
            {
                Vector2 moveInput = new Vector2(direction.x, direction.z);
                netMoveDirection.Value = moveInput;
                netCurrentSpeed.Value = walkSpeed;
                
                // NavMesh 위치와 실제 위치 동기화
                navAgent.nextPosition = transform.position;
            }
            else
            {
                netMoveDirection.Value = Vector2.zero;
            }
        }
        else
        {
            netMoveDirection.Value = Vector2.zero;
        }
    }

    public override void OnNetworkSpawn()
    {
        // 봇은 카메라 설정 안함
        // 부모 클래스의 카메라 설정을 무시
    }
}