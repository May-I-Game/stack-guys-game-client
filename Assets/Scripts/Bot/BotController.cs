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
    [SerializeField] private bool useRandomWaypoint = true;             // 랜덤 웨이포인트 사용
    [SerializeField] private float waypointReachedDistance = 2f;        // 웨이포인트 도달 거리
    private int topClosestCount = 4;                                    // 가장 가까운 N개 웨이포인트

    [Header("Debug Visualization")]
    [SerializeField] private bool showPathInEditor = false;             // 에디터/클라이언트에서 기즈모 표시 여부
    [SerializeField] private Color waypointLineColor = Color.blue;      // 웨이포인트 직선 색상
    [SerializeField] private Color goalLineColor = Color.yellow;        // 목표 직선 색상
    [SerializeField] private Color selectedColor = Color.red;           // 봇을 선택했을때 직선 색상

    [SerializeField] private bool requireReachableWaypoint = true;      // 도달 가능한 웨이포인트만 사용하도록 강제

    private bool isGoingToGoal = false;                                 // Goal로 직행 중인가?

    private NavMeshAgent navAgent;
    private NavMeshPath pathBuffer;                                     // 경로 검사용 버퍼

    private Transform[] waypoints;                                      // 자동으로 찾은 웨이포인트들
    private Transform goalTransform;
    private Transform currentWaypoint;                                  // 현재 목표 웨이포인트
    private bool isGoingToWaypoint = false;                             // 웨이포인트로 가는 중인가?
    private float nextPathUpdateTime;                                   // 다음 업데이트 시간
    private float nextWaypointSearchTime;                               // 다음 웨이포인트 재탐색 시간

    // 서버에서 선택한 웨이포인트 인덱스, 에디터 기즈모는 이 값을 통해 동일한 웨이포인트를 보여줌
    private NetworkVariable<int> currentWaypointIndex = new(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 문 열림 이벤트로 강제 이동할 웨이포인트
    private Transform overrideWaypoint;     // 강제 목표 웨이포인트
    private bool overrideActive = false;    // 강제 모드 여부

    // 전역 우선순위 - 문이 열릴 때 등록되는 웨이포인트들
    private static readonly System.Collections.Generic.List<Transform> openedDoorWaypoints =
        new System.Collections.Generic.List<Transform>();

    // 통과한 문 기록
    private System.Collections.Generic.List<Transform> passedDoorWaypoints = new();

    protected override void Update()
    {
        // 클라이언트 - 애니메이션만 업데이트, AI는 서버에서만 동작
        if (!IsServer)
            UpdateAnimation();
    }

    protected override void Start()
    {
        base.Start();

        // 경로 검증용 버퍼 사전 할당
        navAgent = GetComponent<NavMeshAgent>();
        pathBuffer = new NavMeshPath();

        // 서버에서만 AI 설정
        if (!IsServer)
        {
            // 클라이언트에서는 NavMeshAgent 비활성화 (AI 로직 실행 안 함)
            if (navAgent != null)
                navAgent.enabled = false;

            return;
        }

        // 서버 전용 NavMeshAgent 설정
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.speed = walkSpeed;                         // 플레이어 컨트롤러의 속도
            navAgent.angularSpeed = rotationSpeed * 50f;        // 최대 회전 속도, updateRotation=true일 때만 트랜스폼 회전에 직접 반영
            navAgent.acceleration = 8f;                         // 가속도
            navAgent.stoppingDistance = 0.5f;                   // 목표 지점에 이 거리만큼 여유를 두고 감속
            navAgent.autoRepath = true;                         // NavMesh 환경이 변할 때 자동으로 경로를 재계산

            navAgent.updatePosition = false;                    // Rigidbody와 충돌하지 않도록 설정
            navAgent.updateRotation = false;
        }

        FindGoal();                                             // Goal 태그 오브젝트 찾기
        RefreshWaypoints();                                     // 초기 웨이포인트 탐색

        // 이벤트 구독 (리스폰 감지용)
        netIsDeath.OnValueChanged += OnDeathStateChanged;
    }

    protected override void FixedUpdate()
    {
        // 클라이언트는 물리 체크 안함 & 죽은 상태
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

    // 리스폰 시 NavMeshAgent 재초기화
    private void OnDeathStateChanged(bool previousValue, bool newValue)
    {
        if (!IsServer) return;

        if (previousValue == true && newValue == false)
        {
            // NavMeshAgent 재초기화
            if (navAgent != null)
            {
                // NavMeshAget 완전 리셋 (false, true 해야 내부 상태 리셋됨)
                navAgent.enabled = false;
                navAgent.enabled = true;

                // NavMesh에 강제 배치
                if (!navAgent.isOnNavMesh)
                {
                    navAgent.Warp(transform.position);
                }

                // 경로 및 이동 상태 초기화
                navAgent.ResetPath();
                navAgent.isStopped = false;
            }

            // Rigidbody 속도 초기화
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // AI 상태 초기화
            isGoingToWaypoint = false;
            isGoingToGoal = false;
            currentWaypoint = null;
            currentWaypointIndex.Value = -1;
            overrideActive = false;
            overrideWaypoint = null;

            nextPathUpdateTime = 0f;
            nextWaypointSearchTime = 0f;

            // 목표 재탐색
            FindGoal();
            RefreshWaypoints();
        }
    }

    // Goal 태그를 가진 오브젝트 찾기 (서버 전용)
    private void FindGoal()
    {
        if (!IsServer) return;

        // Goal 태그를 가진 오브젝트 찾기
        goalTransform = WaypointManager.Instance.GetGoal();

    }

    // 웨이포인트 재탐색 및 배열 갱신 (서버 전용)
    private void RefreshWaypoints()
    {
        if (!IsServer) return;

        waypoints = WaypointManager.Instance.GetWaypointArray();

        if (waypoints != null && waypoints.Length > 0)
        {
            // 첫 웨이포인트 선택 (앞쪽 방향 우선)
            if (!isGoingToGoal)
                TrySelectForwardWaypoint();
        }
        else
        {
            waypoints = null;
            isGoingToWaypoint = false;
            currentWaypoint = null;
            currentWaypointIndex.Value = -1; // NetworkVariable 웨이포인트 인덱스 초기화 (없음)
        }
    }

    // 지정 위치로의 경로가 "완전 경로"인지 검사
    private bool IsReachable(Vector3 targetPos)
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return false;

        if (!navAgent.CalculatePath(targetPos, pathBuffer))
            return false;

        return pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    // SetDestination을 너무 자주 갱신하지 않도록 간격 제어 + 완전 경로일 때만 설정
    private void SetDestinationIfDue(Vector3 targetPos)
    {
        if (Time.time > nextPathUpdateTime)
        {
            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                // 부분 경로 또는 실패 경로는 설정하지 않음
                if (!requireReachableWaypoint || IsReachable(targetPos))
                {
                    navAgent.SetDestination(targetPos);
                }
            }

            nextPathUpdateTime = Time.time + updatePathInterval;
        }
    }

    // Goal로 강제 이동 (경로 완전성 체크 무시)
    private void SetDestinationToGoalForced()
    {
        if (goalTransform == null) return;
        if (!navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh) return;

        if (Time.time > nextPathUpdateTime)
        {
            navAgent.SetDestination(goalTransform.position);
            nextPathUpdateTime = Time.time + updatePathInterval;
        }
    }

    // 열린 문 우선순위 - FIFO + 내 앞 + 도달 가능 여부
    private Transform GetNextPriorityWaypointAhead()
    {
        float zRef = transform.position.z + forwardThreshold;

        for (int i = 0; i < openedDoorWaypoints.Count; i++)
        {
            var wp = openedDoorWaypoints[i];
            if (wp == null) continue;

            // 이미 통과한 문은 제외
            if (passedDoorWaypoints.Contains(wp)) continue;

            // 내 앞에 있는 우선순위 웨이포인트만
            if (wp.position.z <= zRef) continue;

            // 경로가 완전해야 채택
            if (requireReachableWaypoint && !IsReachable(wp.position)) continue;

            return wp; // FIFO
        }

        return null;
    }

    // 플레이어 앞(z 기준 forwardThreshold 더한 값) + 가장 가까운 N개 중 랜덤 선택
    // forwardThreshold - 최소한의 범위를 넓힘
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

            // z축 기준으로 앞에 있는 웨이포인트만 후보
            if (wp.position.z > zRef)
            {
                // 도달 가능한 후보만 사용
                if (!requireReachableWaypoint || IsReachable(wp.position))
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
        isGoingToGoal = false;
        return true;
    }

    // AI 로직 - 우선순위 : 강제 웨이포인트 > 열린 문 > 랜덤 웨이포인트 > Goal
    private void UpdateBotAI()
    {
        // 강제 웨이포인트 모드 - 가장 먼저 처리하고 일반 로직은 중단
        if (overrideActive && overrideWaypoint != null)
        {
            float dist = Vector3.Distance(transform.position, overrideWaypoint.position);

            if (dist < waypointReachedDistance)
            {
                // 강제 목표에 도착 -> 강제 모드 해제, 일반 로직 시작
                overrideActive = false;
                overrideWaypoint = null;

                // 다음 프레임에 일반 로직이 새 목표를 잡도록 초기화
                isGoingToWaypoint = false;
                isGoingToGoal = false;
                currentWaypoint = null;
                currentWaypointIndex.Value = -1;
            }
            else
            {
                // 아직 미도달 -> 강제 목표로 계속 진행 (완전 경로만)
                if (!requireReachableWaypoint || IsReachable(overrideWaypoint.position))
                    SetDestinationIfDue(overrideWaypoint.position);
            }

            // 이동 입력 (완전 경로일 때만)
            if (navAgent.hasPath && navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
                !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
            {
                Vector3 direction = navAgent.desiredVelocity.normalized;

                if (direction.magnitude > 0.1f)
                {
                    moveDir = new Vector2(direction.x, direction.z);
                    navAgent.nextPosition = transform.position;
                }
                else
                {
                    moveDir = Vector2.zero;
                }
            }
            else
            {
                moveDir = Vector2.zero;
            }

            return; // 강제 모드에서는 아래 일반 웨이포인트 건너뜀
        }

        // 열린 문 우선순위 처리: 등록 순서대로, 내 앞쪽이며 도달 가능한 문 먼저
        Transform priorityTarget = GetNextPriorityWaypointAhead();
        if (priorityTarget != null)
        {
            // 현재 목표가 아니면 설정
            if (currentWaypoint != priorityTarget)
            {
                currentWaypoint = priorityTarget;
                isGoingToWaypoint = true;
                isGoingToGoal = false;
                currentWaypointIndex.Value = GetWaypointIndex(priorityTarget);
            }

            float distToDoor = Vector3.Distance(transform.position, priorityTarget.position);

            // 도착 시 통과 완료 기록 + 목표 초기화
            if (distToDoor < waypointReachedDistance)
            {
                passedDoorWaypoints.Add(priorityTarget); // 통과 완료 기록
                isGoingToWaypoint = false;
                isGoingToGoal = false;
                currentWaypoint = null;
                currentWaypointIndex.Value = -1;
                // 다음 프레임에 GetNextPriorityWaypointAhead()가 다음 문을 자동으로 선택
                // 또는 모든 문을 통과했으면 null 반환 → 랜덤 웨이포인트/Goal로 진행
            }
            else
            {
                SetDestinationIfDue(priorityTarget.position);
            }

            // 이동 입력 (완전 경로일 때만)
            if (navAgent.hasPath && navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
                !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
            {
                Vector3 direction = navAgent.desiredVelocity.normalized;

                if (direction.magnitude > 0.1f)
                {
                    moveDir = new Vector2(direction.x, direction.z);
                    navAgent.nextPosition = transform.position;
                }
                else
                {
                    moveDir = Vector2.zero;
                }
            }
            else
            {
                moveDir = Vector2.zero;
            }

            return; // 우선순위 문을 먼저 처리하므로, 랜덤 로직은 스킵
        }

        // Goal 직행 모드 처리 (마지막 웨이포인트 도달 후)
        if (isGoingToGoal)
        {
            // 경로 완전성 체크 없이 무조건 Goal로 이동
            SetDestinationToGoalForced();

            // NavMesh 경로를 moveDir으로 변환 (부분 경로여도 허용)
            if (navAgent.hasPath && !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
            {
                Vector3 direction = navAgent.desiredVelocity.normalized;

                if (direction.magnitude > 0.1f)
                {
                    moveDir = new Vector2(direction.x, direction.z);
                    navAgent.nextPosition = transform.position;
                }
                else
                {
                    moveDir = Vector2.zero;
                }
            }
            else
            {
                moveDir = Vector2.zero;
            }

            return; // Goal 직행 모드에서는 아래 로직 건너뜀
        }

        // 일반 웨이포인트 / Goal 경로 로직 (열린 문이 없거나 모두 통과한 경우)
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
                    // 다음 앞쪽 웨이포인트 선택 실패 시 Goal 진행 (경로 체크 판정)
                    if (!TrySelectForwardWaypoint())
                    {
                        isGoingToGoal = true;
                        isGoingToWaypoint = false;
                        currentWaypoint = null;
                        currentWaypointIndex.Value = -1;
                    }
                }
                else
                {
                    // 아직 도달 전이면 현재 웨이포인트로 진행 (완전 경로일 때만)
                    if (!requireReachableWaypoint || IsReachable(currentWaypoint.position))
                        SetDestinationIfDue(currentWaypoint.position);
                    else
                    {
                        // 현재 웨이포인트가 막혀 있으면 다른 앞쪽 웨이포인트 재선택, 실패하면 Goal로 (경로 체크 없이)
                        if (!TrySelectForwardWaypoint())
                        {
                            isGoingToGoal = true;
                            isGoingToWaypoint = false;
                            currentWaypoint = null;
                            currentWaypointIndex.Value = -1;

                        }
                    }
                }
            }
            else
            {
                // 현재 웨이포인트가 없으면 다시 시도, 실패하면 Goal로 (경로 체크 안함)
                if (!TrySelectForwardWaypoint())
                {
                    isGoingToGoal = true;
                }
            }
        }
        else
        {
            // 랜덤 웨이포인트 비활성화 또는 웨이포인트 없음 -> Goal로 직행 (경로 체크 안함)
            isGoingToGoal = true;
        }

        // NavMesh 경로를 moveDir으로 변환하여 PlayerMove()가 실제 이동 처리
        // 유효한 "완전 경로"가 있고 피격/다이브/잡힘 상태가 아닐 때만 이동
        if (navAgent.hasPath && navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
            !isHit && !netIsDiveGrounded.Value && !netIsGrabbed.Value)
        {
            Vector3 direction = navAgent.desiredVelocity.normalized;

            if (direction.magnitude > 0.1f)
            {
                // 캐릭터 이동 입력은 XZ 평면만 사용 (x=좌우, z=전후)
                Vector2 moveInput = new Vector2(direction.x, direction.z);

                moveDir = moveInput; // 이번 프레임 이동 방향
                navAgent.nextPosition = transform.position;
            }
            else
            {
                moveDir = Vector2.zero;
            }
        }
        else
        {
            moveDir = Vector2.zero;
        }
    }

    // 봇의 웨이포인트를 강제로 전환
    public void ForceWaypoint(Transform wp)
    {
        if (!IsServer || wp == null) return;

        // 리스트가 비어있다면 최신화 (인데스 동기화용)
        if (waypoints == null || waypoints.Length == 0)
            RefreshWaypoints();

        overrideWaypoint = wp;
        overrideActive = true;

        // 현재 목표/상태 갱신
        currentWaypoint = wp;
        isGoingToWaypoint = true;
        isGoingToGoal = false;

        // 기즈모 동기화를 위해 인덱스 설정 (안전 가드)
        int forcedIndex = -1;
        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == wp)
                {
                    forcedIndex = i;
                    break;
                }
            }
        }

        currentWaypointIndex.Value = forcedIndex; // NetworkVariable 웨이포인트 인덱스

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.ResetPath();
            if (!requireReachableWaypoint || IsReachable(wp.position))
                navAgent.SetDestination(wp.position);
            nextPathUpdateTime = Time.time + updatePathInterval;
        }
    }

    // 문 열림 시 호출되어 웨이포인트를 전역 우선순위 목록에 추가
    public static void RegisterOpenedDoorWaypoint(Transform wp)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (wp == null) return;
        if (!openedDoorWaypoints.Contains(wp))
            openedDoorWaypoints.Add(wp);
    }

    /////////////////////////////////////////
    // Gizmos 관련
    /////////////////////////////////////////

    // Goal 태그 재검색 (서버 참조 없을 때)

    // 웨이포인트 배열에서 인덱스 찾기 (기즈모 동기화용)
    private int GetWaypointIndex(Transform wp)
    {
        if (wp == null || waypoints == null) return -1;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == wp) return i;
        }

        return -1;
    }

    // Gizmos를 이용한 에디터 경로 시각화 (웨이포인트 없으면 태그 재검색)
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!showPathInEditor) return;

        // 선택한 봇의 선 색상
        if (overrideActive && overrideWaypoint != null)
        {
            Gizmos.color = waypointLineColor;
            Gizmos.DrawLine(transform.position, overrideWaypoint.position);
        }
        else
        {
            // 서버 선택 인덱스 우선, 없으면 앞쪽 웨이포인트 Fallback
            Transform forwardWp = GetSyncedCurrentWaypoint();
            if (forwardWp == null)
                forwardWp = FindForwardWaypointForGizmo();            // 선택 없으면 앞쪽 하나 선택

            // 웨이포인트 선 색상
            if (forwardWp != null)
            {
                Gizmos.color = waypointLineColor;
                Gizmos.DrawLine(transform.position, forwardWp.position);
            }
        }

        // Goal은 서버가 못 찾으면 클라이언트 태그 재검색
        Transform goal = goalTransform != null ? goalTransform : FindGoalForGizmo();
        if (goal != null)
        {
            Gizmos.color = goalLineColor;
            Gizmos.DrawLine(transform.position, goal.position);
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
            waypoints = WaypointManager.Instance.GetWaypointArray();
        }

        if (waypoints != null && idx < waypoints.Length)
            return waypoints[idx];

        return null;
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
            source = WaypointManager.Instance.GetWaypointArray();
            if (source == null || source.Length == 0) return null;
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

            // 앞쪽이 아니거나 threshold 이내면 제외
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

    private Transform FindGoalForGizmo()
    {
        // Goal 가져오기
        return WaypointManager.Instance.GetGoal();
    }

    // 봇이 선택되었을 때만 표시되는 Gizmos (상세 정보)
    private void OnDrawGizmosSelected()
    {
        if (!showPathInEditor) return;

        // 강제 웨이포인트가 있으면 그것만 강조
        if (overrideActive && overrideWaypoint != null)
        {
            Gizmos.color = selectedColor; // 선택 시 색상
            Gizmos.DrawLine(transform.position, overrideWaypoint.position);
            return;
        }

        // 동기화된 인덱스를 이용하여 서버와 동일한 웨이포인트를 찾아 선을 그림
        Transform selectedWp = GetSyncedCurrentWaypoint();
        if (selectedWp == null) return;

        Gizmos.color = selectedColor; // 선택 시 색상
        Gizmos.DrawLine(transform.position, selectedWp.position);
    }

    public override void OnNetworkSpawn()
    {
        // 서버만 물리 활성화 (PlayerController와 동일)
        if (IsServer)
        {
            EnablePhysics(true);
        }
        else
        {
            EnablePhysics(false);
        }

        // 봇은 카메라 설정 안함 (플레이어와 다른 점)
    }

    private void OnDestroy()
    {
        if (netIsDeath != null)
        {
            netIsDeath.OnValueChanged -= OnDeathStateChanged;
        }
    }
}

// 열린 문 우선 = 등록된 순서대로 내 앞 + 도달 가능한 문을 우선 방문
// 리스폰 후에도 동일한 순서로 다시 방문
// 열린 문이 없을 때만 랜덤 앞쪽 웨이포인트 진행
// 이동은 PathComplete일 때만 수행 (골 제외)