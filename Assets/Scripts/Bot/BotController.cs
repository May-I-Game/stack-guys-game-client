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
    private int topClosestCount = 3;                                    // 가장 가까운 N개 웨이포인트

    [Header("Debug Visualization")]
    [SerializeField] private bool showPathInEditor = false;             // 에디터/클라이언트에서 기즈모 표시 여부
    [SerializeField] private Color waypointLineColor = Color.blue;      // 웨이포인트 직선 색상
    [SerializeField] private Color goalLineColor = Color.yellow;        // 목표 직선 색상
    [SerializeField] private Color selectedColor = Color.red;           // 봇을 선택했을때 직선 색상
    
    private NavMeshAgent navAgent;

    private Transform[] waypoints;                                      // 자동으로 찾은 웨이포인트들
    private Transform goalTransform;
    private Transform currentWaypoint;                                  // 현재 목표 웨이포인트
    private bool isGoingToWaypoint = false;                             // 웨이포인트로 가는 중인가?
    private float nextPathUpdateTime;                                   // 다음 업데이트 시간
    private float nextWaypointSearchTime;                               // 다음 웨이포인트 재탐색 시간

    // 서버에서 선택한 웨이포인트 인덱스, 에디터 기즈모는 이 값을 통해 동일한 웨이포인트를 보여줌
    private NetworkVariable<int> currentWaypointIndex = new(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    protected override void Update()
    {
        // 클라이언트: 애니메이션만 업데이트, AI는 서버에서만 동작
        if (!IsServer)
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
                navAgent.enabled = false;

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

            navAgent.updatePosition = false;                    // Rigidbody와 충돌하지 않도록 설정
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
            // 목표 지점이 없으면 일정 주기로 찾기
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
                ServerPerformanceProfiler.End("BotController.BotUpdate");
            }
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

    // 웨이포인트 재탐색 후 서버가 배열을 관리하고 랜덤 선택
    private void RefreshWaypoints()
    {
        if (!IsServer) return;

        GameObject[] waypointObjects = GameObject.FindGameObjectsWithTag(waypointTag);

        if (waypointObjects.Length > 0)
        {
            waypoints = new Transform[waypointObjects.Length];
            for (int i = 0; i < waypointObjects.Length; i++)
                waypoints[i] = waypointObjects[i].transform;

            // 처음 진입 시 앞쪽 웨이포인터를 하나 잡아서 시작
            TrySelectForwardWaypoint();
        }
        else
        {
            waypoints = null;
            isGoingToWaypoint = false;
            currentWaypoint = null;
            currentWaypointIndex.Value = -1; // 선택 인덱스 초기화 (없음)
        }
    }


    // 플레이어 앞(z 기준 forwardThreshold 더한 값) + 가장 가까운 N개 중 랜덤 선택
    // forwardThreshold 역할: 최소한의 범위를 넓힘
    private bool TrySelectForwardWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            isGoingToWaypoint = false;
            currentWaypoint = null;
            currentWaypointIndex.Value = -1;
            return false;
        }

        System.Collections.Generic.List<int> forwardIndices = new System.Collections.Generic.List<int>();
        float zRef = transform.position.z + forwardThreshold;

        for (int i = 0; i < waypoints.Length; i++)
        {
            var wp = waypoints[i];
            if (wp == null) continue;

            // z축 기준으로 앞에 있는 웨이포인트만 선택
            if (wp.position.z > zRef)
            {
                forwardIndices.Add(i);
            }
        }

        if (forwardIndices.Count == 0)
        {
            isGoingToWaypoint = false;
            currentWaypoint = null;
            currentWaypointIndex.Value = -1;
            return false;
        }

        // 항상 가장 가까운 N개 중 랜덤 선택
        Vector3 origin = transform.position;

        // forwardIndices를 거리 기준 오름차순 정렬 (가까운 순)
        forwardIndices.Sort((a, b) =>
        {
            float distA = (waypoints[a].position - origin).sqrMagnitude;
            float distB = (waypoints[b].position - origin).sqrMagnitude;
            return distA.CompareTo(distB);
        });

        // 가장 가까운 topCloesestCount개 중에서 랜덤 선택
        int topCount = Mathf.Min(topClosestCount, forwardIndices.Count);
        int randomPick = Random.Range(0, topCount);
        int chosen = forwardIndices[randomPick];

        currentWaypoint = waypoints[chosen];
        currentWaypointIndex.Value = chosen;    // 기즈모 동기화를 위한 인덱스 저장
        isGoingToWaypoint = true;
        return true;
    }

    // SetDestination을 너무 자주 갱신하지 않도록 간격 제어
    private void SetDestinationIfDue(Vector3 targetPos)
    {
        if (Time.time > nextPathUpdateTime)
        {
            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
                navAgent.SetDestination(targetPos);

            nextPathUpdateTime = Time.time + updatePathInterval;
        }
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

                // 도달 -> 다음 앞쪽 웨이포인트 시도, 실패하면 Goal로 폴백
                if (distanceToWaypoint < waypointReachedDistance)
                {
                    // 다음 앞쪽 웨이포인트 선택 실패 시 Goal 진행
                    if (!TrySelectForwardWaypoint() && goalTransform != null)
                        SetDestinationIfDue(goalTransform.position);
                }
                else
                {
                    // 아직 도달 전이면 현재 웨이포인트로 진행
                    SetDestinationIfDue(currentWaypoint.position);
                }
            }
            else
            {
                // 현재 웨이포인트가 없으면 다시 시도, 실패하면 Goal로  폴백
                if (!TrySelectForwardWaypoint() && goalTransform != null)
                    SetDestinationIfDue(goalTransform.position);
            }
        }
        else
        {
            if (goalTransform != null)
                SetDestinationIfDue(goalTransform.position);
        }

        // NavMesh 경로를 moveDir으로 변환하여 PlayerMove()가 실제 이동 처리
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

    // 에디터에서 서버가 선택한 웨이포인트 트랜스폼 복원
    private Transform GetSyncedCurrentWaypoint()
    {
        int idx = currentWaypointIndex.Value;
        if (idx < 0) return null;

        // 클라이언트는 서버처럼 배열을 유지하지 않으므로 필요 시 태그 재검색
        if (!IsServer && (waypoints == null || waypoints.Length == 0))
        {
            var objs = GameObject.FindGameObjectsWithTag(waypointTag);
            
            if (objs == null || objs.Length == 0) return null;

            waypoints = new Transform[objs.Length];
            for (int i = 0; i < objs.Length; i++)
                waypoints[i] = objs[i].transform;
        }

        if (waypoints != null && idx < waypoints.Length)
            return waypoints[idx];

        return null;
    }


    // Gizmos를 이용한 에디터 경로 시각화 (웨이포인트 없으면 태그 재검색)
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!showPathInEditor) return;

        // 서버 선택 인덱스 우선, 없으면 앞쪽 웨이포인트 Fallback
        Transform forwardWp = GetSyncedCurrentWaypoint();
        if (forwardWp == null)
            forwardWp = FindForwardWaypointForGizmo();            // 선택 없으면 앞쪽 하나 선택

        // 웨이포인트 선
        if (forwardWp != null)
        {
            Gizmos.color = waypointLineColor;
            Gizmos.DrawLine(transform.position, forwardWp.position);
        }

        // Goal은 서버가 못 찾으면 클라이언트 태그 재검색
        Transform goal = goalTransform != null ? goalTransform : FindGoalForGizmo();
        if (goal != null)
        {
            Gizmos.color = goalLineColor;
            Gizmos.DrawLine(transform.position, goal.position);
        }
    }

    // 에디터용 Fallback: 가장 가까운 앞쪽 웨이포인트 찾기
    // 두 값 A, B 에 대해 A < B 와 sqrt(A) < sqrt(B) 는 결과가 동일
    private Transform FindForwardWaypointForGizmo()
    {
        // 웨이포인트 사용 체크
        if (!useRandomWaypoint) return null;

        // 서버에서 관리하는 배열 사용
        Transform[] source = waypoints;

        // 배열이 없으면 태그로 재검색
        if (source == null || source.Length == 0)
        {
            var objs = GameObject.FindGameObjectsWithTag(waypointTag);
            if (objs == null || objs.Length == 0) return null;

            source = new Transform[objs.Length];
            for (int i = 0; i < objs.Length; i++)
                source[i] = objs[i].transform;
        }

        // z 앞쪽 기준이 되는 값
        float zRef = transform.position.z + forwardThreshold;

        // 최종 선택될 웨이포인트
        Transform best = null;

        // 현재까지의 최소 제곱거리(루트 연산 생략하기 위해서 사용)
        float bestSqr = float.MaxValue;

        // 모든 후보 순위
        for (int i = 0; i < source.Length; i++)
        {
            var t = source[i];
            if (!t) continue;   // Null 가드

            // 앞쪽이 아니거나 threshould 이내면 제외
            if (t.position.z <= zRef) continue; // 뒤/가까운 웨이포인트 제외

            // 거리 제곱로 최소 거리 후보 갱신
            float sqr = (t.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    //Goal 태그 재검색 (서버 참조 없을 때)
    private Transform FindGoalForGizmo()
    {
        var goalObj = GameObject.FindGameObjectWithTag("Goal");
        return goalObj ? goalObj.transform : null;
    }

    // 봇이 선택되었을 때만 표시되는 Gizmos (상세 정보)
    private void OnDrawGizmosSelected()
    {
        if (!showPathInEditor) return;

        // 동기화된 인덱스를 이용하여 서버와 동일한 웨이포인트를 찾아 선을 그림
        Transform selectedWp = GetSyncedCurrentWaypoint();
        if (selectedWp == null) return;

        Gizmos.color = selectedColor; // 선택 시 색상
        Gizmos.DrawLine(transform.position, selectedWp.position);
    }

    public override void OnNetworkSpawn()
    {
        // 봇은 카메라 설정 안함
    }
}

// 앞쪽 웨이포인트 존재 -> 가장 가까운 3개 중 랜덤 → 웨이포인트 경유
// 앞쪽 웨이포인트 없음 -> 최종 Goal로 직행
// 각 웨이포인트 도착 시마다 다음 경로 재탐색