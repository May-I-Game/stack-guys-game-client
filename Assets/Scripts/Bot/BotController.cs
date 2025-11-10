using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BotController : PlayerController
{
    [Header("Bot Settings")]
    [SerializeField] private float updatePathInterval = 2;              // 경로 업데이트 주기
    [SerializeField] private float waypointSearchInterval = 2f;         // 웨이포인트 재탐색 주기
    [SerializeField] private float forwardThreshold = 1f;               // 전진 판정 거리

    [Header("Random Path Settings")]
    [SerializeField] private string waypointTag = "Waypoint";           // 웨이포인트 태그
    [SerializeField] private bool useRandomWaypoint = true;             // 랜덤 웨이포인트 사용
    [SerializeField] private float waypointReachedDistance = 3f;        // 웨이포인트 도달 거리

    [Header("Debug Visualization")]
    [SerializeField] private bool showPathInEditor = true;              // 에디터/클라이언트에서 기즈모 표시 여부
    [SerializeField] private Color pathColor = Color.cyan;              // 경로 선 색상
    [SerializeField] private Color waypointLineColor = Color.yellow;    // 웨이포인트 직선 생상
    [SerializeField] private Color goalLineColor = Color.green;         // 목표 직선 색상

    private Transform[] waypoints;                                      // 자동으로 찾은 웨이포인트들
    private NavMeshAgent navAgent;
    private Transform goalTransform;
    private Transform currentWaypoint;                                  // 현재 목표 웨이포인트
    private bool isGoingToWaypoint = false;                             // 웨이포인트로 가는 중인가?
    private float nextPathUpdateTime;                                   // 다음 업데이트 시간
    private float nextWaypointSearchTime;                               // 다음 웨이포인트 재탐색 시간

    protected override void Update()
    {
        UpdateAnimation();
    }

    protected override void Start()
    {
        base.Start();

        navAgent = GetComponent<NavMeshAgent>();

        // 서버에서먄 AI 설정
        if (!IsServer)
        {
            // 클라이언트에서는 NavMeshAgent 비활성화 (AI 로직 실행 안 함)
            if (navAgent != null)
            {
                navAgent.enabled = false;
            }

            return;
        }

        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.speed = walkSpeed;                         // 플레이어 컨트롤러의 속도
            navAgent.angularSpeed = rotationSpeed * 50f;        // 최대 회전 속도, updateRotation=true일 때만 트랜스폼 회전에 직접 반영
            navAgent.acceleration = 8f;                         // 가속도
            navAgent.stoppingDistance = 0.5f;                   // 목표 지점에 이 거리만큼 여유를 두고 감속/

            navAgent.autoRepath = true;                         // NavMesh 환경이 변할 때 자동으로 경로를 재계산
            
            // Rigidbody와 충돌하지 않도록 설정
            navAgent.updatePosition = false;
            navAgent.updateRotation = false; 
        }

        FindGoal();
        RefreshWaypoints();                                     // 초기 웨이포인트 탐색
    }

    protected override void FixedUpdate()
    {
        if (!IsServer) return;
        if (netIsDeath.Value) return;

        ServerPerformanceProfiler.Start("BotController.FixedUpdate");
        // 웨이포인트 주기적으로 재탐색
        if (Time.time > nextWaypointSearchTime)
        {
            RefreshWaypoints();
            nextWaypointSearchTime = Time.time + waypointSearchInterval;
        }

        // 이동이 활성화 되어 있고 navAgent가 활성화가 되어 있을때 AI 작동
        if (inputEnabled && navAgent != null && navAgent.enabled)
        {
            // 목표 지점이 없으면 목표 찾기
            if (goalTransform == null && Time.time > nextPathUpdateTime)
            {
                FindGoal();
                nextPathUpdateTime = Time.time + updatePathInterval;  // 스팸 호출 방지
            }

            // 목표 지점이 있으면 길찾기 로직
            if (goalTransform != null)
            {
                ServerPerformanceProfiler.Start("BotController.BotUpdate");
                UpdateBotAI();
            }

            ServerPerformanceProfiler.End("BotController.BotUpdate");
        }

        // 땅 체크
        GroundCheck();

        // 이동 처리
        PlayerMove();

        // 점프 요청이 있으면 점프
        if (isJumpQueued)
        {
            PlayerJump();
        }

        // 잡기 요청이 있으면 잡기 처리
        if (isGrabQueued)
        {
            PlayerGrab();
        }

        // 잡고 있으면
        if (isHolding && holdingObject != null)
        {
            PlayerHeld();
        }

        ServerPerformanceProfiler.End("BotController.FixedUpdate");
    }

    private void FindGoal()
    {
        if (!IsServer) return;

        // Goal 태그를 가진 오브젝트 찾기
        GameObject goal = GameObject.FindGameObjectWithTag("Goal");
        if (goal != null)
        {
            goalTransform = goal.transform;
        }
    }

    // 웨이포인트 재탐색, 전진 방향 웨이포인트 선택
    private void RefreshWaypoints()
    {
        if (!IsServer) return;

        GameObject[] waypointObjects = GameObject.FindGameObjectsWithTag(waypointTag);

        // 웨이포인터들이 있을때
        if (waypointObjects.Length > 0)
        {
            waypoints = new Transform[waypointObjects.Length];
            for (int i = 0; i < waypointObjects.Length; i++)
            {
                waypoints[i] = waypointObjects[i].transform;
            }

            if (useRandomWaypoint && waypoints.Length > 0)
            {
                SelectForwardWaypoint();                     // 앞쪽 웨이포인트만 선택
            }
        }
        else
        {
            waypoints = null;
        }
    }

    // 전진 방향 웨이포인트만 선택 (일직선 맵 최적화)
    private void SelectForwardWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        System.Collections.Generic.List<Transform> forwardWaypoints = new System.Collections.Generic.List<Transform>();

        foreach (Transform wp in waypoints)
        {
            // Z 축 기준으로 앞에 있는 웨이포인트만 선택
            if (wp.position.z > transform.position.z + forwardThreshold)
            {
                forwardWaypoints.Add(wp);
            }
        }

        // 앞에 웨이포인트가 없으면 골로 직행
        if (forwardWaypoints.Count == 0)
        {
            isGoingToWaypoint = false;
            return;
        }

        int randomIndex = Random.Range(0, forwardWaypoints.Count);
        currentWaypoint = forwardWaypoints[randomIndex];
        isGoingToWaypoint = true;
    }

    private void UpdateBotAI()
    {
        // 웨이포인트 시스템 사용
        if (useRandomWaypoint && waypoints != null && waypoints.Length > 0)
        {
            // 웨이포인트로 가는 중
            if (isGoingToWaypoint && currentWaypoint != null)
            {
                // 웨이포인트에 도착했는지 체크
                float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint.position);

                // 정해둔 도달 거리보다 짧으면 도착
                if (distanceToWaypoint < waypointReachedDistance)
                {
                    // 즉시 다음 목표 선택
                    SelectForwardWaypoint();
                }
                else
                {
                    // 웨이포인트로 경로 설정
                    if (Time.time > nextPathUpdateTime)
                    {
                        // NavMeshAgent가 활성이고 실제 NavMesh 위에 있을때
                        if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                        {
                            navAgent.SetDestination(currentWaypoint.position);
                        }

                        // 다음 갱신 시간 업데이트
                        nextPathUpdateTime = Time.time + updatePathInterval;
                    }
                }
            }
            // 웨이포인트가 없으면 다시 선택
            else if (!isGoingToWaypoint)
            {
                SelectForwardWaypoint();
            }
        }
        // 웨이포인트 통과 후 골로 직진
        else
        {
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
                moveDir = moveInput; // 이번 프레임 이동 방향

                // NavMeshAgent 내부 위치(nextPosition)를 실제 Transform과 강제로 맞춤
                navAgent.nextPosition = transform.position;
            }
            else
            {
                // 방향이 실질적으로 없으면(거의 0) —> 제자리 유지
                moveDir = Vector2.zero;
            }
        }
        else
        {
            // 경로가 없거나, 이동 금지 상태면 —> 제자리 유지
            moveDir = Vector2.zero;
        }
    }

    // Gizmos를 이용한 에디터 경로 시각화

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!showPathInEditor) return;

        Transform target = null;

        if (isGoingToWaypoint && currentWaypoint != null)
        {
            target = currentWaypoint;
        }
        else if (goalTransform != null)
        {
            target = goalTransform;
        }
        else
        {
            // 목표가 없다면 태그로 찾아보기 (에디터에서도 동작)
            var goal = GameObject.FindGameObjectWithTag("Goal");
            if (goal != null) target = goal.transform;
        }

        if (target == null) return;

        DrawCalculatedPath(transform.position, target.position, pathColor);

        // 목표 직선도 함께 표시
        Gizmos.color = isGoingToWaypoint ? waypointLineColor : goalLineColor;
        Gizmos.DrawLine(transform.position, target.position);

    }

    // 봇이 선택되었을 때만 표시되는 Gizmos (상세 정보)
    private void OnDrawGizmosSelected()
    {
        // 반투명 선
        DrawForwardWaypoints();
    }

    // NavMesh.CalculatePath를 사용하여 경로를 즉석 계산해 Gizmos로 표시
    private void DrawCalculatedPath(Vector3 from, Vector3 to, Color color)
    {
        var calcPath = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, calcPath)) return;
        if (calcPath.corners == null || calcPath.corners.Length < 2) return;

        Gizmos.color = color;
        Gizmos.DrawLine(from, calcPath.corners[0]);
        for (int i = 0; i < calcPath.corners.Length - 1; i++)
        {
            Gizmos.DrawLine(calcPath.corners[i], calcPath.corners[i + 1]);
        }
    }

    // 봇 선택 시 앞쪽에 있는 모든 웨이포인트까지 선 표시
    private void DrawForwardWaypoints()
    {
        // 웨이포인트 배열이 없으면 무시
        if (waypoints == null) return;

        // 반투명 노란색
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

        // 모든 웨이포인트 순회
        foreach (Transform wp in waypoints)
        {
            if (wp == null) continue;

            // 앞쪽에 있는 웨이포인트만 선으로 연결
            if (wp.position.z > transform.position.z + forwardThreshold)
            {
                Gizmos.DrawLine(transform.position, wp.position);
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // 봇은 카메라 설정 안함
        // 부모 클래스의 카메라 설정을 무시
    }
}

// 1. 웨이포인트 시스템 활성 → 전진 방향 랜덤 선택
// 2. 앞쪽 웨이포인트 없음 → Goal로 직진
// 3. 웨이포인트 미사용 → Goal로 직진