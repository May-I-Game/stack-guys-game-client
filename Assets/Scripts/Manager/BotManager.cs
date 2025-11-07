using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BotManager : NetworkBehaviour
{
    public static BotManager instance;                  // 싱글톤 패턴

    [Header("Bot Prefab")]
    [SerializeField] private GameObject botPrefab;

    [Header("Bot Settings")]
    [SerializeField] private int numberOfBots = 3;      // 생성할 봇 수
    [SerializeField] private Transform[] spawnPoints;

    private List<GameObject> spawnedBots = new List<GameObject>();

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
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);                                // 중복된 Bot 매니저 삭제
            return;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void SpawnBots()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[BotManager] 서버만 봇을 생성할 수 있음");
            return;
        }

        if (botPrefab == null)
        {
            Debug.LogError("[BotManager] 봇 프리팹이 설정 안됨");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[BotManager] 스폰 포인트가 설정 안됨");
            return;
        }

        for (int i = 0; i < numberOfBots; i++)
        {
            SpawnBot(i);
        }

        Debug.Log($"[BotManager] {numberOfBots}개의 봇을 생성");
    }

    private void SpawnBot(int botIndex, Transform[] spawnPointsArray = null, bool disableInput = false)
    {
        // 스폰 포인트 배열 (파라미터로 받은거 우선, 없으면 기존 spawnPoints 사용)
        Transform[] pointsToUse = spawnPointsArray ?? spawnPoints;

        // 스폰 포인트 검증
        if (pointsToUse == null || pointsToUse.Length == 0)
        {
            Debug.LogError("[BotManager] 스폰 포인트가 설정 안됨");
            return;
        }

        // 스폰 위치 - botIndex가 배열 크기를 넘어도 안전하게 처리
        Transform spawnPoint = pointsToUse[botIndex % pointsToUse.Length];

        // 봇 인스턴스 생성
        GameObject botInstance = Instantiate(
            botPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // 봇 식별자 설정
        NetworkBotIdentity botIdentity = botInstance.GetComponent<NetworkBotIdentity>();
        if (botIdentity != null)
        {
            // 이 캐릭터가 봇임을 표시
            botIdentity.IsBot = true;
        }

        // 생성 직후 입력 차단 (시네마틱 끝나면 활성화)
        if (disableInput)
        {
            PlayerController botController = botInstance.GetComponent<PlayerController>();
            if (botController != null)
            {
                botController.inputEnabled = false;
            }
        }

        // 네트워크 오브젝트로 스폰 (서버 + 모든 클라이언트에 동기화)
        NetworkObject networkObject = botInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedBots.Add(botInstance);
            Debug.Log($"[BotManager] 봇 생성 완료: {botIdentity?.BotName ?? "Bot"} at {spawnPoint.name}");
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

        // 생성된 모든 봇을 순회하면서 입력 활성화
        foreach (var bot in spawnedBots)
        {
            if (bot == null) continue;

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

        // startIndex부터 끝까지 반복하면서 각 스폰 포인트에 봇 생성
        for (int i = startIndex; i < spawnPoints.Length; i++)
        {
            // i번째 스폰 포인트에 봇 생성하고, 시네마틱 동안 입력 차단
            SpawnBot(i, spawnPoints, disableInput: true);
        }

        // 생성 완료 로그
        Debug.Log($"[BotManager] {botsToSpawn}개의 봇을 생성");
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
