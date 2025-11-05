using Unity.Netcode;
using UnityEngine;

// 봇을 식별하기 위한 네트워크 컴포넌트
// PlayerController와 다른 시스템에서 봇 여부를 확인할 때 사용
public class NetworkBotIdentity : NetworkBehaviour
{
    // 캐릭터가 봇인지 여부
    [Header("Bot Settings")]
    public bool IsBot = false;

    // 봇의 이름
    public string BotName = "";

    private static int botCounter = 0;

    private void Awake()
    {
        if (IsBot && string.IsNullOrEmpty(BotName))
        {
            BotName = GenerateBotName();
        }
    }

    // 봇 이름 생성
    private string GenerateBotName()
    {
        string[] botNames = new string[]
        {
            "Bot_A", "Bot_A", "Bot_C"
        };

        int nameIndex = botCounter % botNames.Length;
        botCounter++;

        return $"{botNames[nameIndex]}_{botCounter:D2}";
    }

    // 다른 스크립트에서 봇 여부를 확인하는 헬퍼 함수
    public static bool IsPlayerBot(GameObject player)
    {
        if (player == null) return false;

        var botIdentity = player.GetComponent<NetworkBotIdentity>();
        return botIdentity != null && botIdentity.IsBot;
    }

    // 캐릭터 이름 가져오기
    public string GetDisplayName()
    {
        if (IsBot)
        {
            return BotName;
        }
        else
        {
            // 실제 캐릭터 이름
            return $"Player_{OwnerClientId}";
        }
    }
}