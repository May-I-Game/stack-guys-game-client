using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class BotManager : NetworkBehaviour
{
    public static BotManager Singleton;                  // 싱글톤 패턴

    [Header("Bot Prefab")]
    [SerializeField] private GameObject botPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int poolSize = 100;         // 풀 크기

    [Header("Debug Spawn Points")]
    [SerializeField] private Transform[] debugSpawnPoints;

    // 봇 풀링 시스템
    private Queue<GameObject> botPool = new Queue<GameObject>();
    private List<GameObject> activeBots = new List<GameObject>();

    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
        }
        else
        {
            Destroy(gameObject);                // 중복된 Bot 매니저 삭제
            return;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // 디버그 스폰 포인트가 있으면 먼저 생성
            if (debugSpawnPoints != null && debugSpawnPoints.Length > 0)
            {
                StartCoroutine(SpawnDebugBotsDelayed());
            }

            // 서버 시작 시 봇 풀 초기화
            StartCoroutine(InitializePoolDelayed());
        }
    }

    // 디버깅용 봇 스폰
    private IEnumerator SpawnDebugBotsDelayed()
    {
        // 0.5초 대기
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < debugSpawnPoints.Length; i++)
        {
            if (debugSpawnPoints[i] != null)
            {
                CreateDebugBot(debugSpawnPoints[i]);
            }
        }

        //Debug.Log($"[BotManager] {debugSpawnPoints.Length}개의 디버그 봇 생성 완료");
    }

    // 디버그 봇 생성 (풀 사용 안 함)
    private void CreateDebugBot(Transform spawnPoint)
    {
        // 90도 회전
        Quaternion spawnRotation = spawnPoint.rotation * Quaternion.Euler(0, 90, 0);

        // 봇 인스턴스 생성
        GameObject botInstance = Instantiate(botPrefab);

        // 네트워크 오브젝트 스폰
        NetworkObject networkObject = botInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();

            // DoRespawn으로 위치 설정
            BotController botController = botInstance.GetComponent<BotController>();
            if (botController != null)
            {
                botController.DoRespawn(spawnPoint.position, spawnRotation);

                // 봇 이름 설정
                PlayerNameSync nameSync = botInstance.GetComponent<PlayerNameSync>();
                if (nameSync != null)
                {
                    string botName = NetworkBotIdentity.GenerateBotName();
                    nameSync.SetPlayerName(botName);
                }

                // 입력 차단
                botController.SetInputEnabled(false);
            }

            // activeBots에 추가 (디버그 봇도 활성화)
            activeBots.Add(botInstance);
        }
        else
        {
            Debug.LogError("[BotManager] NetworkObject 컴포넌트가 없음");
            Destroy(botInstance);
        }
    }

    // 서버 시작 시 봇 풀 초기화
    private IEnumerator InitializePoolDelayed()
    {
        // 0.5초 딜레이
        yield return new WaitForSeconds(0.5f);

        // 풀 크기만큼 봇 미리 생성
        for (int i = 0; i < poolSize; i++)
        {
            CreateBotInPool();
        }

        //Debug.Log($"[BotManager] {poolSize}개의 봇 풀링 완료");
    }

    // 풀에 봇 생성 및 추가
    private void CreateBotInPool()
    {
        // 봇 인스턴스 생성 (위치는 나중에 설정)
        GameObject botInstance = Instantiate(botPrefab);

        // 네트워크 오브젝트 스폰
        NetworkObject networkObject = botInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // 서버 소유로 스폰
            networkObject.Spawn();

            // BotController로 봇 판별 및 초기화
            BotController botController = botInstance.GetComponent<BotController>();
            if (botController != null)
            {
                // 봇 이름 설정
                PlayerNameSync nameSync = botInstance.GetComponent<PlayerNameSync>();
                if (nameSync != null)
                {
                    // 고유한 봇 이름 생성
                    string botName = NetworkBotIdentity.GenerateBotName();

                    // 모든 클라이언트에 이름 동기화
                    nameSync.SetPlayerName(botName);
                }
                else
                {
                    Debug.LogWarning("[BotManager] PlayerNameSync 컴포넌트가 없음");
                }

                // 생성 직후 입력 차단
                botController.SetInputEnabled(false);
            }

            // 비활성화 상태로 풀에 추가
            botInstance.SetActive(false);
            botPool.Enqueue(botInstance);
        }
        else
        {
            Debug.LogError("[BotManager] NetworkObject 컴포넌트가 없음");
            Destroy(botInstance);
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (Singleton == this)
        {
            Singleton = null;
        }
    }

    // startIndex부터 끝까지 봇 배치
    public void SpawnBotsFromIndex(int startIndex, Transform[] spawnPoints)
    {
        // 서버에서만 봇 생성 가능
        if (!IsServer) return;

        // 봇 프리팹 확인
        if (botPrefab == null)
        {
            Debug.LogError("[BotManager] 봇 프리팹이 설정 안됨");
            return;
        }

        // startIndex부터 끝까지 봇 생성할 개수 계산
        int botsToSpawn = spawnPoints.Length - startIndex;

        // 생성할 봇이 없으면 종료
        if (botsToSpawn <= 0)
        {
            return;
        }

        // 풀에 충분한 봇이 있는지 확인
        if (botPool.Count < botsToSpawn)
        {
            botsToSpawn = botPool.Count; // 가능한 만큼만 스폰
        }

        // startIndex부터 끝까지 반복하면서 각 스폰 포인트에 봇 배치
        for (int i = startIndex; i < startIndex + botsToSpawn; i++)
        {
            // 풀에서 봇 꺼내기
            GameObject bot = botPool.Dequeue();

            // 활성화
            bot.SetActive(true);

            // 게임 스폰 포인트로 텔레포트
            TeleportBotToGamePosition(bot, spawnPoints[i]);

            // 활성화된 봇 리스트에 추가
            activeBots.Add(bot);
        }

        //Debug.Log($"[BotManager] {botsToSpawn}개의 봇을 게임에 배치");
    }

    // 봇을 게임 스폰 포인트로 텔레포트
    private void TeleportBotToGamePosition(GameObject bot, Transform spawnPoint)
    {
        // 90도 회전
        Vector3 targetPosition = spawnPoint.position;
        Quaternion targetRotation = spawnPoint.rotation * Quaternion.Euler(0, 90, 0);

        // NavMeshAgent 비활성화 후 이동 (NavMesh 문제 방지)
        UnityEngine.AI.NavMeshAgent navAgent = bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // BotController의 DoRespawn으로 텔레포트
        BotController botController = bot.GetComponent<BotController>();
        if (botController != null)
        {
            botController.DoRespawn(targetPosition, targetRotation);
        }

        // 새로운 NavMesh에서 NavMeshAgent 재활성화
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.Warp(targetPosition); // NavAgent 위치 이동
        }
    }

    // 생성된 모든 봇의 입력을 활성화
    public void EnableAllBots()
    {
        // 서버에서만 실행
        if (!IsServer) return;

        // 활성화된 모든 봇을 순회하면서 입력 활성화
        foreach (var bot in activeBots)
        {
            if (bot == null) continue;

            // BotController가 있으면 봇임
            BotController botController = bot.GetComponent<BotController>();
            if (botController != null)
            {
                // 봇의 입력 활성화
                botController.SetInputEnabled(true);
            }
        }

        Debug.Log($"[BotManager] {activeBots.Count}개의 봇 입력 활성화");
    }

    // 생성된 모든 봇 정리
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // 풀에 있는 모든 봇 정리
            while (botPool.Count > 0)
            {
                GameObject bot = botPool.Dequeue();
                if (bot != null)
                {
                    NetworkObject netObj = bot.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }

                    Destroy(bot);
                }
            }

            // 활성화된 봇 정리 (디버그 봇 포함)
            foreach (var bot in activeBots)
            {
                if (bot != null)
                {
                    NetworkObject netObj = bot.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }

                    Destroy(bot);
                }
            }

            activeBots.Clear();
        }
    }
}