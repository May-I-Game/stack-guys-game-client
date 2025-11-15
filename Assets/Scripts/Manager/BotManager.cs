using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BotManager : NetworkBehaviour
{
    public static BotManager Singleton;                  // 싱글톤 패턴

    [Header("Bot Prefab")]
    [SerializeField] private GameObject botPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private int preBotCount = 100;
    [SerializeField] private PreSpawnManager preSpawnManager;

    [Header("Debug Spawn Points")]
    [SerializeField] private Transform[] debugSpawnPoints;

    private List<GameObject> spawnedBots = new List<GameObject>();
    bool hasPreSpawned = false;

    //[Header("Bot Settings")]
    //[SerializeField] private int numberOfBots = 3;      // 생성할 봇 수
    //[SerializeField] private Transform[] spawnPoints;
    //public override void OnNetworkSpawn()
    //{
    //    if (IsServer)
    //    {
    //        // 게임이 시작될 때 봇 생성
    //        GameManager gameManager = GameManager.instance;
    //        if (gameManager != null)
    //        {
    //            // GameManager가 게임 시작하면 봇 스폰
    //            StartCoroutine(WaitForGameStart());
    //        }
    //    }
    //}

    //private System.Collections.IEnumerator WaitForGameStart()
    //{
    //    // 게임이 Playing 상태가 될 때까지 대기
    //    while (GameManager.instance == null || !GameManager.instance.IsGame)
    //    {
    //        yield return new WaitForSeconds(0.5f);
    //    }

    //    // 게임 시작 후 봇 생성
    //    yield return new WaitForSeconds(1f);
    //    SpawnBots();
    //}

    private void Awake()
    {
        if (Singleton == null)
        {
            Singleton = this;
        }
        else
        {
            Destroy(gameObject);                                // 중복된 Bot 매니저 삭제
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

            // 서버 시작할때 봇 미리 생성
            if (preSpawnManager != null && !hasPreSpawned)
            {
                StartCoroutine(PreSpawnBotsDelayed());
            }
        }
    }

    // PreSpawnPointManager가 준비될 때까지 대기 후 봇 미리 생성
    private IEnumerator PreSpawnBotsDelayed()
    {
        // 이전 스폰했는지 체크
        if (hasPreSpawned)
        {
            yield break;
        }

        // 0.5초 딜레이
        yield return new WaitForSeconds(0.5f);

        if (preSpawnManager == null)
        {
            yield break;
        }

        Transform[] prePoints = preSpawnManager.GetPreSpawnPoints();

        // Pre 리스폰 포인트 체크
        if (prePoints == null || prePoints.Length == 0)
        {
            yield break;
        }

        // 기존 SpawnBotsFromIndex 재활용하여 Pre-spawn 영역에 봇 생성
        SpawnBotsFromIndex(0, prePoints);

        hasPreSpawned = true;
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
                SpawnBot(i, debugSpawnPoints, disableInput: true);
            }
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

    //private void SpawnBots()
    //{
    //    if (!IsServer)
    //    {
    //        Debug.LogWarning("[BotManager] 서버만 봇을 생성할 수 있음");
    //        return;
    //    }

    //    if (botPrefab == null)
    //    {
    //        Debug.LogError("[BotManager] 봇 프리팹이 설정 안됨");
    //        return;
    //    }

    //    if (spawnPoints == null || spawnPoints.Length == 0)
    //    {
    //        Debug.LogError("[BotManager] 스폰 포인트가 설정 안됨");
    //        return;
    //    }

    //    for (int i = 0; i < numberOfBots; i++)
    //    {
    //        SpawnBot(i);
    //    }

    //    Debug.Log($"[BotManager] {numberOfBots}개의 봇을 생성");
    //}

    private void SpawnBot(int botIndex, Transform[] spawnPointsArray = null, bool disableInput = false)
    {
        // 스폰 포인트 배열 (파라미터로 받은거 우선, 없으면 기존 spawnPoints 사용)
        Transform[] pointsToUse = spawnPointsArray;

        // 스폰 포인트 검증
        if (pointsToUse == null || pointsToUse.Length == 0)
        {
            Debug.LogError("[BotManager] 스폰 포인트가 설정 안됨");
            return;
        }

        // 스폰 위치 - botIndex가 배열 크기를 넘어도 안전하게 처리
        Transform spawnPoint = pointsToUse[botIndex % pointsToUse.Length];

        // 오른쪽으로 45도 회전 추가
        Quaternion spawnRotation = spawnPoint.rotation * Quaternion.Euler(0, 90, 0);

        // 봇 인스턴스 생성
        GameObject botInstance = Instantiate(
            botPrefab,
            spawnPoint.position,
            spawnRotation
        );

        // 봇 식별자 설정
        NetworkBotIdentity botIdentity = botInstance.GetComponent<NetworkBotIdentity>();
        if (botIdentity != null)
        {
            // 이 캐릭터가 봇임을 표시
            botIdentity.IsBot = true;
        }

        // 봇 이름 네트워크 오브젝트로 생성 및 설정 (PlayNameSync 사용)
        NetworkObject networkObject = botInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            // 서버 소유로 스폰
            networkObject.Spawn();

            // 스폰 후 회전값 다시 적용 (네트워크 동기화를 위해)
            NetworkTransform nt = botInstance.GetComponent<NetworkTransform>();
            if (nt != null)
            {
                // 텔레포트를 사용하여 위치와 회전을 명확히 설정
                nt.Teleport(spawnPoint.position, spawnRotation, botInstance.transform.localScale);
            }

            PlayerNameSync nameSync = botInstance.GetComponent<PlayerNameSync>();
            if (nameSync != null)
            {
                // 고유한 봇 이름 생성
                string botName = NetworkBotIdentity.GenerateBotName();

                // 모든 클라이언트에 이름 동기화
                nameSync.SetPlayerName(botName);
                Debug.Log($"[BotManager] 봇 생성 완료: {botName}");
            }
            else
            {
                Debug.LogWarning("[BotManager] PlayerNameSync 컴포넌트가 없음");
            }

            // 생성 직후 입력 차단(시네마틱 끝나면 활성화)
            var botController = botInstance.GetComponent<PlayerController>();
            if (botController != null && disableInput)
            {
                botController.SetInputEnabled(false);
            }

            // 생성된 봇을 리스트에 추가
            spawnedBots.Add(botInstance);
        }
        else
        {
            Debug.LogError("[BotManager] NetworkObject 컴포넌트가 없음");
            Destroy(botInstance);
        }
    }

    // 생성된 모든 봇의 입력을 활성화 (시네마틱 끝난 후 호출)
    public void EnableAllBots()
    {
        // 서버에서만 실행
        if (!IsServer) return;

        // 생성된 모든 봇을 순회하면서 봇일때만 입력 활성화
        foreach (var bot in spawnedBots)
        {
            if (bot == null) continue;

            NetworkBotIdentity botIdentity = bot.GetComponent<NetworkBotIdentity>();
            if (botIdentity == null || !botIdentity.IsBot)
            {
                continue;
            }

            PlayerController botController = bot.GetComponent<PlayerController>();
            if (botController != null)
            {
                // 봇의 입력 활성화
                botController.SetInputEnabled(true);
            }
        }

        Debug.Log($"[BotManager] {spawnedBots.Count}개의 봇 입력 활성화");
    }

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
            Debug.Log("[BotManager] 생성할 봇이 없음 (모든 자리가 플레이어로 채워짐)");
            return;
        }

        // 이미 생성한 봇이 있으면 이동만 수행
        if (hasPreSpawned && spawnedBots.Count > 0)
        {
            Debug.Log($"[BotManager] Pre-spawn된 봇 사용 - {botsToSpawn}개 이동");
            MovePreSpawnedBotsToGameStart(startIndex, spawnPoints);
            return; // 새로 생성하지 않고 종료
        }

        // startIndex부터 끝까지 반복하면서 각 스폰 포인트에 봇 생성
        for (int i = startIndex; i < spawnPoints.Length; i++)
        {
            // i번째 스폰 포인트에 봇 생성하고, 시네마틱 동안 입력 차단
            SpawnBot(i, spawnPoints, disableInput: true);
        }
    }

    // 게임 시작할때 미리 생성된 봇들을 텔레포트
    private void MovePreSpawnedBotsToGameStart(int startIndex, Transform[] spawnPoints)
    {
        if (!IsServer) return;

        // 이동할 봇 개수 계산 (스폰 포인트 - 플레이어 수)
        int botsToMove = spawnPoints.Length - startIndex;

        // spawnedBots 개수가 더 적다면 spawnedBots 개수 선택
        botsToMove = Mathf.Min(botsToMove, spawnedBots.Count);

        // 이동할 봇이 없으면 종료
        if (botsToMove <= 0)
        {
            Debug.Log("[BotManager] 이동할 봇 없음");
            return;
        }

        // spawnedBots 봇들을 순서대로 텔레포트
        for (int i = 0; i < botsToMove; i++)
        {
            int spawnIndex = startIndex + i;
            GameObject bot = spawnedBots[i];

            // 스폰 포인트 인덱스 체크
            if (bot != null && spawnIndex < spawnPoints.Length)
            {
                // 90도 회전
                Vector3 targetPosition = spawnPoints[spawnIndex].position;
                Quaternion targetRotation = spawnPoints[spawnIndex].rotation * Quaternion.Euler(0, 90, 0);

                // NavMeshAgent 비활성화 후 이동 (NavMesh 문제 방지)
                UnityEngine.AI.NavMeshAgent navAgent = bot.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (navAgent != null)
                {
                    navAgent.enabled = false;
                }

                // 봇의 트랜스폼으로 위치 이동
                NetworkTransform nt = bot.GetComponent<NetworkTransform>();
                if (nt != null)
                {
                    // 위치 이동 (네트워크 동기화)
                    nt.Teleport(targetPosition, targetRotation, bot.transform.localScale);
                }

                // NavMeshAgent 재활성화 (새 위치의 NavMesh에 배치)
                if (navAgent != null)
                {
                    navAgent.enabled = true;
                    navAgent.Warp(targetPosition); // NavAgent 위치 이동
                }
            }
        }
    }

    // 생성된 모든 봇 정리
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            foreach (var bot in spawnedBots)
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

            spawnedBots.Clear();
        }
    }
}
