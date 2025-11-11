using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

// 문의 NavMeshObstacle을 관리함, 문이 열릴 때 봇들이 자동으로 경로 변경
public class DoorNavObstacle : NetworkBehaviour
{
    [Header("NavMeshObstacle")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    [Header("Near Waypoint")]
    public Transform nearWaypoint;

    [SerializeField] private bool isOpen = false;

    // 네트워크로 동기화되는 문 열림 상태
    private readonly NetworkVariable<bool> netIsOpen = new NetworkVariable<bool>(false);

    private void Awake()
    {
        if (navMeshObstacle == null)
            navMeshObstacle = GetComponent<NavMeshObstacle>();
    }

    private void Start()
    {
        if (navMeshObstacle != null)
        {
            // 카빙 활성화 - Obstacle이 NavMesh를 즉시 파내도록 설정
            navMeshObstacle.carving = true;
            navMeshObstacle.carveOnlyStationary = false;
        }

        // 웨이포인트 자동 탐색
        if (nearWaypoint == null)
            nearWaypoint = FindNearestWaypointTo(transform.position);

        // 서버는 인스펙터 초기값을 네트워크 변수로 반영
        if (IsServer)
        {
            netIsOpen.Value = isOpen;
        }
    }

    public override void OnNetworkSpawn()
    {
        // 값 변경을 모든 클라이언트에서 반영
        netIsOpen.OnValueChanged += OnOpenStateChanged;

        // 현재 상태 즉시 적용 (늦게 접속한 클라이언트 대응)
        if (navMeshObstacle != null)
        {
            navMeshObstacle.enabled = !netIsOpen.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        // 콜백 등록 해제 (메모리 누수 방지)
        netIsOpen.OnValueChanged -= OnOpenStateChanged;
    }

    // NetworkVariable 값 변경 시 호출 (모든 클라이언트)
    private void OnOpenStateChanged(bool previous, bool current)
    {
        // 문 열림 상태에 따라 Obstacle 활성화/비활성화
        if (navMeshObstacle != null)
        {
            navMeshObstacle.enabled = !current;
        }
    }

    // 문 열기 - 외부에서 호출 (서버 전용)
    public void OpenDoor()
    {
        if (!IsServer) return; // 서버만 상태 변경

        if (netIsOpen.Value) return; // 이미 열린 문은 무시

        // 네트워크 변수 업데이트
        netIsOpen.Value = true;
        isOpen = true;

        if (nearWaypoint != null)
        {
            // 열린 문의 웨이포인트를 우선 방문 목록에 추가 (큐에 추가만)
            BotController.RegisterOpenedDoorWaypoint(nearWaypoint);
        }
    }

    // 문 위치 기준 가장 가까운 Waypoint 태그 오브젝트 찾기
    private Transform FindNearestWaypointTo(Vector3 origin)
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Waypoint");
        if (objs == null || objs.Length == 0) return null;

        Transform best = null;
        float bestSqr = float.MaxValue; // 최소 거리 제곱 값

        // 가장 가까운 웨이포인트 탐색
        for (int i = 0; i < objs.Length; i++)
        {
            Transform t = objs[i].transform;
            if (t == null) continue;

            float sqr = (t.position - origin).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }

        return best;
    }

    public bool IsOpen()
    {
        // 네트워크 스폰 전에는 인스펙터 값, 이후에는 네트워크 값
        return IsSpawned ? netIsOpen.Value : isOpen;
    }
}
