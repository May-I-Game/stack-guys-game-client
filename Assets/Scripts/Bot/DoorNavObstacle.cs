using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

// 문의 NavMeshObstacle을 관리함, 문이 열릴 때 봇들이 자동으로 경로 변경
public class DoorNavObstacle : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public bool autoDetectSize = true;

    [Header("Auto Find Settings")]
    public Vector3 manualObstacleSize = new Vector3(2f, 2f, 0.2f);

    [Header("NavMesh Settings")]
    public float sizePadding = 1.0f;

    [Header("Near Waypoint")]
    public Transform nearWaypoint;

    [SerializeField] private bool isOpen = false;

    private NavMeshObstacle doorNavObstacle;

    private void Start()
    { 
        SetupNavMeshObstacles();

        // 웨이포인트 자동 탐색
        nearWaypoint = FindNearestWaypointTo(transform.position);
    }

    // 부모 오브젝트에 하나의 Obstacle 생성
    private void SetupNavMeshObstacles()
    {
        doorNavObstacle = GetComponent<NavMeshObstacle>();
        if (doorNavObstacle == null)
        {
            doorNavObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }
        else
        {
            doorNavObstacle.enabled = false;    // 기존 Obstacle 재설정
        }

        // 크기 자동 감지 or 수동 설정
        Vector3 size = autoDetectSize ? GetDoorSize() * sizePadding : manualObstacleSize;

        ConfigureObstacle(doorNavObstacle, size);

    }

    // 문 전체 크기를 자동으로 계산
    private Vector3 GetDoorSize()
    {
        // 본인의 Renderer로 크기 계산
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Vector3 size = renderer.bounds.size;
            return size;
        }

        // 본인의 Collider로 크기 계산
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            Vector3 size = collider.bounds.size;
            return size;
        }
        
        // 모두 실패하면 기본 값
        return manualObstacleSize;
    }

    // NavMeshObstacle 세부 설정
    private void ConfigureObstacle(NavMeshObstacle obstacle, Vector3 size)
    {
        obstacle.carving = true;                        // NavMesh에 구멍 생성
        obstacle.carveOnlyStationary = false;           // 움직이는 문도 지원
        obstacle.shape = NavMeshObstacleShape.Box;      // 박스 형태
        obstacle.center = Vector3.zero;                 // 중심점
        obstacle.size = size;                           // 크기
        obstacle.enabled = true;                        // 초기 상태: 닫힘
    }

    // 문 열기 - 외부에서 호출
    public void OpenDoor()
    {
        if (isOpen) return;

        isOpen = true;

        if (doorNavObstacle != null)
        {
            doorNavObstacle.enabled = false;
        }

        // 서버에서만 처리
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (nearWaypoint == null) return; // 없으면 패스

        // 가장 가까운 웨이포인트로 모든 봇 강제 전환
        BotController.ForceAllBotsToWaypoint(nearWaypoint);
    }

    // 문 위치 기준 가장 가까운 Waypoint 태그 오브젝트 찾기
    private Transform FindNearestWaypointTo(Vector3 origin)
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Waypoint");
        if (objs == null || objs.Length == 0) return null;

        Transform best = null;
        float bestSqr = float.MaxValue;

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

    //// 문 열기 ServerRpc
    //[ServerRpc(RequireOwnership = false)]
    //private void OpenDoorServerRpc()
    //{
    //    if (isOpen) return;

    //    isOpen = true;

    //    // Obstacle 비활성화 -> NavMesh 구멍 제거 -> 봇 통과 가능
    //    if (leftObstacle != null)
    //    {
    //        leftObstacle.enabled = false;
    //    }

    //    if (rightObstacle != null)
    //    {
    //        rightObstacle.enabled = false;
    //    }
    //}   

    public bool IsOpen()
    {
        return isOpen;
    }
}
