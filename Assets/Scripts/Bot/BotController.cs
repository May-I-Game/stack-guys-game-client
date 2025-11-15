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
    [SerializeField] private Color waypointLineColor = Color.blue;      // 웨이포인트 직선 색상
    [SerializeField] private Color goalLineColor = Color.yellow;        // 목표 직선 색상
    [SerializeField] private Color selectedColor = Color.red;           // 봇을 선택했을때 직선 색상
    [SerializeField] private bool requireReachableWaypoint = true;      // 도달 가능한 웨이포인트만 사용하도록 강제

#if UNITY_EDITOR
    [SerializeField] private bool showWaypointInEditor = false;         // 에디터/클라이언트에서 웨이포인트 기즈모 표시 여부
    [SerializeField] private bool showGoalInEditor = false;             // 에디터/클라이언트에서 골 기즈모 표시 여부
#endif

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float debugLogInterval = 3f;               // 로그 출력 주기

    private Vector3 lastDebugPosition;                                  // 이전 디버그 시점에서 캐릭터 포지션
    private float lastDebugLogTime = 0f;                                // 이전 디버그 시간
    private float totalDistanceMoved = 0f;                              // 움직인 거리
    private int consecutiveStuckFrames = 0;                             // 연속적으로 멈춰있는 회수

    private Transform goalTransform;
    private bool isGoingToGoal = false;                                 // Goal로 가는중인가?

    private NavMeshAgent navAgent;
    private NavMeshPath pathBuffer;                                     // 경로 검사용 버퍼

    private Transform[] waypoints;                                      // 자동으로 찾은 웨이포인트들
    private Transform currentWaypoint;                                  // 현재 목표 웨이포인트
    private bool isGoingToWaypoint = false;                             // 웨이포인트로 가는 중인가?
    private float nextWaypointSearchTime;                               // 다음 웨이포인트 재탐색 시간
    private float nextPathUpdateTime;                                   // 다음 업데이트 시간

    // NavMeshLink 점프 관련 변수
    private bool isTraversingLink = false;                              // NavMeshLink 통과 중인가?
    private float linkTraverseTime = 0f;                                // NavMeshLink 통과 경과 시간
    private float linkJumpDuration = 0.5f;                              // NavMeshLink 점프 시간

    // 이 부분을 전처리기로 감싸면 Netcode가 초기화 순서를 체크할 때 문제 발생
    // 서버에서 선택한 웨이포인트 인덱스, 에디터 기즈모는 이 값을 통해 동일한 웨이포인트를 보여줌
    private NetworkVariable<int> currentWaypointIndex = new(
        -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);


    // 전역 우선순위 - 문이 열릴 때 등록되는 웨이포인트들
    private static readonly System.Collections.Generic.List<Transform> openedDoorWaypoints =
        new System.Collections.Generic.List<Transform>();

    // 통과한 문 기록
    private System.Collections.Generic.List<Transform> passedDoorWaypoints = new();

    protected override void Update()
    {
        // 서버는 애니메이션 업데이트할 필요 없음
        if (!IsServer)
        {
            UpdateAnimation();
        }
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

        // 봇 디버깅
        // DebugBotState();

        // 관심 영역 밖 봇들의 Hit 상태 초기화 타이머
        if (isHit)
        {
            hitTime += Time.fixedDeltaTime;
            if (hitTime >= hitDuration)
            {
                isHit = false;
                hitTime = 0f;
            }
        }

        // 웨이포인트 주기적으로 재탐색
        if (Time.time > nextWaypointSearchTime)
        {
            RefreshWaypoints();
            nextWaypointSearchTime = Time.time + waypointSearchInterval;
        }

        // 이동이 활성화 되어 있고 navAgent가 활성화가 되어 있을때 AI 작동
        if (inputEnabled.Value && navAgent != null && navAgent.enabled)
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

            // NavMeshAgent 위치 동기화 (큰 충돌 후 경로 계산이 망가짐)
            if (navAgent != null && navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                navAgent.nextPosition = transform.position;
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
#if UNITY_EDITOR
            currentWaypointIndex.Value = -1;
#endif
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
#if UNITY_EDITOR
            currentWaypointIndex.Value = -1; // NetworkVariable 웨이포인트 인덱스 초기화
#endif
        }
    }

    // 지정 위치로의 경로가 완전 경로인지 검사
    private bool IsReachable(Vector3 targetPos)
    {
        if (navAgent == null || !navAgent.isActiveAndEnabled || !navAgent.isOnNavMesh)
            return false;

        if (!navAgent.CalculatePath(targetPos, pathBuffer))
            return false;

        return pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    // SetDestination을 너무 자주 갱신하지 않도록 간격 제어, 완전 경로일 때만 설정
    private void SetDestinationIfDue(Vector3 targetPos)
    {
        if (Time.time > nextPathUpdateTime)
        {
            if (navAgent.isActiveAndEnabled && navAgent.isOnNavMesh)
            {
                // 부분 경로, 실패 경로는 설정하지 않음
                if (!requireReachableWaypoint || IsReachable(targetPos))
                {
                    navAgent.SetDestination(targetPos);
                }
            }

            nextPathUpdateTime = Time.time + updatePathInterval;
        }
    }

    // Goal로 강제 이동 (경로 체크 무시)
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
#if UNITY_EDITOR
            currentWaypointIndex.Value = -1;
#endif
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
#if UNITY_EDITOR
            currentWaypointIndex.Value = -1;
#endif
            return false;
        }

        // 항상 가장 가까운 N개 중 랜덤 선택
        Vector3 origin = transform.position;

        // forwardIndices를 거리 기준 오름차순 정렬
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
#if UNITY_EDITOR
        currentWaypointIndex.Value = chosen;    // 기즈모 동기화를 위한 인덱스 저장
#endif
        isGoingToWaypoint = true;
        isGoingToGoal = false;
        return true;
    }

    // AI 로직 (우선순위: 열린 문 > 랜덤 웨이포인트 > Goal)
    private void UpdateBotAI()
    {
        // NavMesh 감지 및 처리
        if (navAgent != null && navAgent.isOnOffMeshLink && !isTraversingLink)
        {
            // NavMeshLink를 막 진입한 순간 AI 로직 안돌림 (점프 시작)
            StartJumpLink();
            return;
        }

        // NavMeshLink 통과 중인 경우 - 점프 진행 중
        if (isTraversingLink)
        {
            UpdateJumpLink();
            return;
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
#if UNITY_EDITOR
                currentWaypointIndex.Value = GetWaypointIndex(priorityTarget);
#endif
            }

            float distToDoor = Vector3.Distance(transform.position, priorityTarget.position);

            // 도착 시 통과 완료 기록 + 목표 초기화
            if (distToDoor < waypointReachedDistance)
            {
                passedDoorWaypoints.Add(priorityTarget); // 통과 완료 기록
                isGoingToWaypoint = false;
                isGoingToGoal = false;
                currentWaypoint = null;
#if UNITY_EDITOR
                currentWaypointIndex.Value = -1;
#endif
            }
            else
            {
                SetDestinationIfDue(priorityTarget.position);
            }

            // 이동 입력 (완전 경로일 때만)
            if (navAgent.hasPath && navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
                !isHit && !isDiving && !netIsGrabbed.Value)
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
            if (navAgent.hasPath && !isHit && !isDiving && !netIsGrabbed.Value)
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
                    // 다음 앞쪽 웨이포인트 선택 실패 시 Goal 진행
                    if (!TrySelectForwardWaypoint())
                    {
                        isGoingToGoal = true;
                        isGoingToWaypoint = false;
                        currentWaypoint = null;
#if UNITY_EDITOR
                        currentWaypointIndex.Value = -1;
#endif
                    }
                }
                else
                {
                    // 아직 도달 전이면 현재 웨이포인트로 진행 (완전 경로일 때만)
                    if (!requireReachableWaypoint || IsReachable(currentWaypoint.position))
                        SetDestinationIfDue(currentWaypoint.position);
                    else
                    {
                        // 현재 웨이포인트가 막혀 있으면 다른 앞쪽 웨이포인트 재선택, 실패하면 Goal로 (경로 체크 안함)
                        if (!TrySelectForwardWaypoint())
                        {
                            isGoingToGoal = true;
                            isGoingToWaypoint = false;
                            currentWaypoint = null;
#if UNITY_EDITOR
                            currentWaypointIndex.Value = -1;
#endif

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
        // 유효한 경로가 있고 피격/다이브/잡힘 상태가 아닐 때만 이동
        if (navAgent.hasPath && navAgent.pathStatus == NavMeshPathStatus.PathComplete &&
            !isHit && !isDiving && !netIsGrabbed.Value)
        {
            Vector3 direction = navAgent.desiredVelocity.normalized;

            if (direction.magnitude > 0.1f)
            {
                // 캐릭터 이동 입력
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

    // 점프 구간 시작
    private void StartJumpLink()
    {
        if (!navAgent.isOnOffMeshLink) return;

        // Unity의 OffMeshLinkData 가져오기
        OffMeshLinkData linkData = navAgent.currentOffMeshLinkData;

        // 통과 시작
        isTraversingLink = true;
        linkTraverseTime = 0f;

        // 점프 시작 (PlayerJump 호출)
        isJumpQueued = true;
    }

    // 길 찾기(NavMeshAgent) = 어디로 가야 하는지만 계산
    // 실제 이동/점프 물리 = PlayerController의 Rigidbody가 전부 담당
    // 점프 구간 통과중 (진행도에 따른 포물선 점프)
    private void UpdateJumpLink()
    {
        if (!isTraversingLink) return;

        linkTraverseTime += Time.deltaTime;

        // 0 ~ 1 사이로 정규화된 진행도
        float normalizedTime = linkTraverseTime / linkJumpDuration;

        // 진행도 1 이상
        if (normalizedTime >= 1f)
        {
            // 통과 완료
            CompleteJumpLink();
        }
        // 여기서 직접 물리 처리 X
        //else
        //{
        //    // 시작점에서 끝점까지 직선으로 이동한 위치
        //    Vector3 horizontalPos = Vector3.Lerp(linkStartPos, linkEndPos, normalizedTime);

        //    // 점프 높이 계산 (포물선)
        //    float jumpHeight = 1f; // 점프 최대 높이
        //    float verticalOffset = jumpHeight * 1f * normalizedTime * (1f - normalizedTime);

        //    Vector3 targetPos = horizontalPos + Vector3.up * verticalOffset;
        //    //Vector3 targetPos = horizontalPos;

        //    // NavAgent 위치 업데이트 (수동)
        //    navAgent.nextPosition = targetPos;
        //    transform.position = targetPos;

        //    // 진행 방향을 바라보도록 회전
        //    Vector3 lookDir = (linkEndPos - linkStartPos).normalized;
        //    if (lookDir != Vector3.zero)
        //    {
        //        transform.rotation = Quaternion.LookRotation(lookDir);
        //    }
        //}
    }

    // 점프 구간 통과 완료
    private void CompleteJumpLink()
    {
        if (!isTraversingLink) return;

        // 통과 완료 처리
        navAgent.CompleteOffMeshLink();
        isTraversingLink = false;
        linkTraverseTime = 0f;

        // NavAgent 위치를 끝점으로 설정
        navAgent.nextPosition = transform.position;
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

#if UNITY_EDITOR
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

        if (!showWaypointInEditor && !showGoalInEditor) return;

        // 서버 선택 인덱스 우선, 없으면 앞쪽 웨이포인트 Fallback
        Transform forwardWp = GetSyncedCurrentWaypoint();
        if (forwardWp == null)
            forwardWp = FindForwardWaypointForGizmo();            // 선택 없으면 앞쪽 하나 선택

        // 웨이포인트 선 색상
        if (forwardWp != null && showWaypointInEditor)
        {
            Gizmos.color = waypointLineColor;
            Gizmos.DrawLine(transform.position, forwardWp.position);
        }

        // Goal은 서버가 못 찾으면 클라이언트 태그 재검색
        Transform goal = goalTransform != null ? goalTransform : FindGoalForGizmo();
        if (goal != null && showGoalInEditor)
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

    // 봇이 선택되었을 때만 표시되는 Gizmos
    private void OnDrawGizmosSelected()
    {
        if (!showGoalInEditor && !showWaypointInEditor) return;

        // 동기화된 인덱스를 이용하여 서버와 동일한 웨이포인트를 찾아 선을 그림
        Transform selectedWp = GetSyncedCurrentWaypoint();
        if (selectedWp == null) return;

        Gizmos.color = selectedColor; // 선택 시 색상
        Gizmos.DrawLine(transform.position, selectedWp.position);
    }
#endif

    /////////////////////////////////////////
    // 디버그 관련
    /////////////////////////////////////////

    private void DebugBotState()
    {
        if (!enableDebugLogs || !IsServer) return;

        // 주기적으로만 로그
        if (Time.time < lastDebugLogTime + debugLogInterval) return;
        lastDebugLogTime = Time.time;

        // 실제 이동 거리 계산
        float actualDistance = Vector3.Distance(transform.position, lastDebugPosition);
        totalDistanceMoved += actualDistance;

        // NavMeshAgent 상태 체크
        string navStatus = "Unknown";
        float remainingDistance = 0f;
        bool hasValidPath = false;
        bool isOnNavMesh = false;
        Vector3 desiredVelocity = Vector3.zero;

        if (navAgent != null && navAgent.isActiveAndEnabled)
        {
            isOnNavMesh = navAgent.isOnNavMesh;
            if (navAgent.isOnNavMesh)
            {
                hasValidPath = navAgent.hasPath;
                navStatus = navAgent.pathStatus.ToString();
                remainingDistance = navAgent.remainingDistance;
                desiredVelocity = navAgent.desiredVelocity;
            }
            else
            {
                navStatus = "NOT_ON_NAVMESH";
            }
        }
        else
        {
            navStatus = "AGENT_DISABLED";
        }

        // 움직임 의도 vs 실제 이동 불일치 체크 (봇이 멈춤)
        bool intendToMove = moveDir.magnitude > 0.1f;
        bool actuallyMoved = actualDistance > 0.05f;
        bool isStuck = intendToMove && !actuallyMoved && !isHit && !isDiving && !netIsGrabbed.Value;

        if (isStuck)
        {
            consecutiveStuckFrames++;
        }
        else
        {
            consecutiveStuckFrames = 0;
        }

        // 목적지 상태 체크 (Nav 목적지 없음)
        string destinationInfo = "None";
        float distanceToDestination = 0f;
        bool hasDestination = false;

        if (isGoingToGoal && goalTransform != null)
        {
            destinationInfo = $"Goal";
            distanceToDestination = Vector3.Distance(transform.position, goalTransform.position);
            hasDestination = true;
        }
        else if (isGoingToWaypoint && currentWaypoint != null)
        {
            destinationInfo = $"Waypoint[{currentWaypointIndex.Value}]";
            distanceToDestination = Vector3.Distance(transform.position, currentWaypoint.position);
            hasDestination = true;
        }

        // 문제 상황 감지
        bool hasProblem = false;
        string problemDescription = "";

        // 의도는 있는데 실제로 안 움직임
        if (isStuck && consecutiveStuckFrames >= 2)
        {
            hasProblem = true;
            problemDescription += "[STUCK] 봇이 제자리에 멈춤 \n";
        }

        // 목적지 없음
        //if (!hasDestination && !netIsDeath.Value)
        //{
        //    hasProblem = true;
        //    problemDescription += "[NO_DESTINATION] 목적지 없음 \n";
        //}

        // NavMesh 경로 없음
        //if (!hasValidPath && hasDestination)
        //{
        //    hasProblem = true;
        //    problemDescription += "[NO_PATH] NavMesh 경로 없음 \n";
        //}

        // NavMesh에서 벗어남
        //if (!isOnNavMesh)
        //{
        //    hasProblem = true;
        //    problemDescription += "[OFF_NAVMESH] NavMesh 이탈 \n";
        //}

        // 경로는 있는데 desiredVelocity가 없음 (경로 막힘)
        //if (hasValidPath && hasDestination && desiredVelocity.magnitude < 0.1f && !isHit)
        //{
        //    hasProblem = true;
        //    problemDescription += "[NO_VELOCITY] 경로는 있지만 이동 속도 없음\n";
        //}

        // isHit가 오래 지속됨
        //if (isHit)
        //{
        //    hasProblem = true;
        //    problemDescription += "[HIT_STATE] Hit 상태 지속 중";
        //}

        if (hasProblem)
        {
            Debug.LogWarning($"[Bot {NetworkObjectId}] PROBLEM DETECTED!\n" +
                      $"  {problemDescription}\n" +
                      $"  Position: {transform.position}\n" +
                      $"  Actual Movement: {actualDistance:F3}m (Total: {totalDistanceMoved:F1}m)\n" +
                      $"  moveDir: {moveDir} (magnitude: {moveDir.magnitude:F2})\n" +
                      $"  netIsMove: {netIsMove.Value}\n" +
                      $"  \n" +
                      $"  === NavMesh Status ===\n" +
                      $"  Status: {navStatus}\n" +
                      $"  Has Path: {hasValidPath}\n" +
                      $"  Remaining Distance: {remainingDistance:F2}m\n" +
                      $"  On NavMesh: {isOnNavMesh}\n" +
                      $"  Desired Velocity: {desiredVelocity} (mag: {desiredVelocity.magnitude:F2})\n" +
                      $"  \n" +
                      $"  === Destination ===\n" +
                      $"  Has Destination: {hasDestination}\n" +
                      $"  Target: {destinationInfo}\n" +
                      $"  Distance to Target: {distanceToDestination:F2}m\n" +
                      $"  Is Going To Goal: {isGoingToGoal}\n" +
                      $"  Is Going To Waypoint: {isGoingToWaypoint}\n" +
                      $"  \n" +
                      $"  === State Flags ===\n" +
                      $"  Is Stuck: {isStuck} (Consecutive: {consecutiveStuckFrames})\n" +
                      $"  Is Hit: {isHit}\n" +
                      $"  Is Diving: {isDiving}\n" +
                      $"  Is Grabbed: {netIsGrabbed.Value}\n" +
                      $"  Is Grounded: {netIsGrounded.Value}\n" +
                      $"  Is Death: {netIsDeath.Value}\n" +
                      $"  \n" +
                      $"  === Physics ===\n" +
                      $"  RB Velocity: {(rb != null ? rb.linearVelocity : Vector3.zero)}\n" +
                      $"  RB isKinematic: {(rb != null ? rb.isKinematic : false)}");
        }

        lastDebugPosition = transform.position;
    }

    public override void OnNetworkSpawn()
    {
        // 서버만 물리 활성화
        if (IsServer)
        {
            EnablePhysics(true);
        }
        else
        {
            EnablePhysics(false);
        }

        // 봇은 카메라 설정 안함
    }

    private void OnDestroy()
    {
        if (netIsDeath != null)
        {
            netIsDeath.OnValueChanged -= OnDeathStateChanged;
        }
    }
}

// 1. 열린 문 우선순위 (FIFO + 내 앞 + 도달 가능)
// 2. 랜덤 앞쪽 웨이포인트 (가장 가까운 4개 중 랜덤)
// 3. Goal 직행 (경로 검증 없이 강제)
