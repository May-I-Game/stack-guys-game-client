using Unity.Netcode;
using UnityEngine;

// 봇을 식별하기 위한 네트워크 컴포넌트
// PlayerController와 다른 시스템에서 봇 여부를 확인할 때 사용
public class NetworkBotIdentity : NetworkBehaviour
{
    // 캐릭터가 봇인지 여부
    //[Header("Bot Settings")]
    //public bool IsBot = false;

    // 모든 인스턴스가 공유하는 카운터
    private static int botCounter = 0;

    // 봇 이름 생성
    public static string GenerateBotName()
    {
        string[] botNames = new string[]
        {
            "Bot"
        };

        int nameIndex = botCounter % botNames.Length;
        botCounter++;

        return $"{botNames[nameIndex]}_{botCounter:D2}";
    }

    // 다른 스크립트에서 봇 여부를 확인하는 헬퍼 함수
    //public static bool IsPlayerBot(GameObject player)
    //{
    //    if (player == null) return false;

    //    var botIdentity = player.GetComponent<NetworkBotIdentity>();
    //    return botIdentity != null && botIdentity.IsBot;
    //}
}