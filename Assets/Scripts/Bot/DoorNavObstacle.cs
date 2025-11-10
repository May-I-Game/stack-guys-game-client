using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

// 문의 NavMeshObstacle을 관리함, 문이 열릴 때 봇들이 자동으로 경로 변경
public class DoorNavObstacle : MonoBehaviour
{

    [Header("References")]
    [SerializeField] private NavMeshObstacle doorNavObstacle;

    [Header("Near Waypoint")]
    public Transform nearWaypoint;

    [SerializeField] private bool isOpen = false;
   
    private void Start()
    {
        if (doorNavObstacle == null)
            doorNavObstacle = GetComponent<NavMeshObstacle>();

        if (doorNavObstacle != null)
            doorNavObstacle.enabled = !isOpen;

        // 웨이포인트 자동 탐색
        if (nearWaypoint == null)
            nearWaypoint = FindNearestWaypointTo(transform.position);
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

    public bool IsOpen()
    {
        return isOpen;
    }
}
