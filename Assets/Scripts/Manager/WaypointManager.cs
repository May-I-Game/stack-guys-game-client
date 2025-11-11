using System.Collections.Generic;
using UnityEngine;

// 웨이포인트 및 골 지점을 관리하는 싱글톤 매니저
public class WaypointManager : MonoBehaviour
{
    private static WaypointManager _instance;
    public static WaypointManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<WaypointManager>();

                if (_instance == null)
                {
                    GameObject go = new GameObject("WaypointManager");
                    _instance = go.AddComponent<WaypointManager>();
                }
            }

            return _instance;
        }
    }

    // 캐싱된 정적 오브젝트들
    private List<Transform> waypointList = new List<Transform>();
    private Transform goalTransform;

    // 초기화 완료 플래그
    private bool isInitialized = false;

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // 게임 씬에서만 초기화
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().ToString();
        if (sceneName == "GameScene")
        {
            InitializeCache();
        }
    }

    // 게임 시작 시 정적 오브젝트를 한 번에 캐싱
    private void InitializeCache()
    {
        if (isInitialized) return;

        // Waypoint 검색
        GameObject[] waypointObjects = GameObject.FindGameObjectsWithTag("Waypoint");
        waypointList.Clear();

        foreach (var obj in waypointObjects)
        {
            if (obj != null)
                waypointList.Add(obj.transform);
        }

        // Goal 검색
        GameObject goalObject = GameObject.FindGameObjectWithTag("Goal");
        goalTransform = goalObject != null ? goalObject.transform : null;

        isInitialized = true;
    }

    // 캐싱된 웨이포인트 배열 반환
    public Transform[] GetWaypoints()
    {
        if (!isInitialized)
            InitializeCache();

        return waypointList.ToArray();
    }

    // 캐싱된 Goal Transform 반환
    public Transform GetGoal()
    {
        if (!isInitialized)
            InitializeCache();

        return goalTransform;
    }

    public List<Transform> GetWaypointList()
    {
        if (!isInitialized)
            InitializeCache();

        return waypointList;
    }

    // 캐시 강제 갱신
    public void RefreshCache()
    {
        isInitialized = false;
        InitializeCache();
    }
}
