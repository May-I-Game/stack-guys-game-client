using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class BotManager : NetworkBehaviour
{
    [Header("Bot Prefab")]
    [SerializeField] private GameObject botPrefab;

    [Header("Bot Settings")]
    [SerializeField] private int numberOfBots = 3;      // 생성할 봇 수
    [SerializeField] private Transform[] spawnPoints;

    private List<GameObject> spawnedBots = new List<GameObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 게임이 시작될 때 봇 생성
            GameManager gameManager = GameManager.instance;
            if (gameManager != null)
            {
                // GameManager가 게임 시작하면 봇 스폰
                StartCoroutine(WaitForGameStart());
            }
        }
    }

    private System.Collections.IEnumerator WaitForGameStart()
    {
        // 게임이 Playing 상태가 될 때까지 대기
        while (GameManager.instance == null || !GameManager.instance.IsGame)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // 게임 시작 후 봇 생성
        yield return new WaitForSeconds(1f);
        SpawnBots();
    }

    [ContextMenu("Spawn Bots")]
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

    private void SpawnBot(int botIndex)
    {
        // 스폰 위치 선택 (순환)
        Transform spawnPoint = spawnPoints[botIndex % spawnPoints.Length];

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
            botIdentity.IsBot = true;
        }

        // 네트워크 오브젝트로 스폰 (서버 소유)
        NetworkObject networkObject = botInstance.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn();
            spawnedBots.Add(botInstance);
            Debug.Log($"[BotManager] 봇 생성 완료: {botIdentity?.BotName ?? "Bot"}");
        }
        else
        {
            Debug.LogError("[BotManager] NetworkObject 컴포넌트가 없습니다!");
            Destroy(botInstance);
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
